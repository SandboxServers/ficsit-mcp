using System.Collections.Concurrent;
using System.Threading.Channels;

using FicsitMcp.Domain.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Default <see cref="IFinBridge"/>: a long-poll command channel with an at-most-once execution
/// model. Owns, per agent, a bounded command queue, liveness, and a bounded drop-oldest event ring;
/// globally, the result-correlation registry (TCS keyed by command id), a tombstone set, and the
/// channel fan-out of events to live subscribers. Time is read through an injected
/// <see cref="TimeProvider"/> so deadlines and liveness are testable at ~zero wall-clock.
/// </summary>
/// <remarks>
/// <para>
/// No HTTP or MCP dependency: the host's endpoint layer drives the transport-facing methods and the
/// machine-control tools drive the tool-facing ones.
/// </para>
/// <para>
/// <b>Thread-safety.</b> All members are safe for concurrent use by many agents (poll threads) and
/// many callers (tool threads) at once. Per-command lifecycle transitions (admit-to-batch, deadline,
/// cancel, result) are serialized by a per-<see cref="PendingCommand"/> lock so the at-most-once
/// reconciliation is atomic; the tombstone set and each agent's event ring have their own locks.
/// </para>
/// </remarks>
public sealed class FinBridge : IFinBridge, IDisposable
{
    private readonly FinBridgeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FinBridge> _logger;

    private readonly ConcurrentDictionary<string, AgentState> _agents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingCommand> _pending = new(StringComparer.Ordinal);

    // Tombstones: ids whose deadline already fired, or whose caller cancelled. A result that arrives
    // for one of these is discarded, never re-applied to a caller who gave up; a command re-read from
    // the channel for one of these is dropped, never delivered. Bounded by oldest-first eviction so
    // the set cannot grow without limit on a long-running server.
    private readonly object _tombstoneLock = new();
    private readonly LinkedList<string> _tombstoneOrder = [];
    private readonly HashSet<string> _tombstones = new(StringComparer.Ordinal);

    // Subscriber fan-out is global (a subscriber sees every agent's events); the event *ring* is
    // per-agent (see AgentState) so one chatty agent cannot evict another's recent events. One lock
    // guards the subscriber list so a subscriber added mid-burst sees a consistent view.
    private readonly object _subscriberLock = new();
    private readonly List<Channel<FinEvent>> _subscribers = [];

    private bool _disposed;

    /// <summary>Constructs the bridge from validated options, a time source, and a logger.</summary>
    public FinBridge(
        IOptions<FinBridgeOptions> options,
        TimeProvider timeProvider,
        ILogger<FinBridge> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---- Tool-facing ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<FinResult> SendAsync(string agentId, FinCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrEmpty(command.Id);

        // Fast-fail against a dead agent with the operator remedy, rather than queueing a command no
        // one will ever pull and hanging to the deadline.
        if (!GetLiveness(agentId).IsAlive)
        {
            throw new FinBridgeException(new FinError
            {
                Code = FinErrorCode.AgentOffline,
                Message =
                    $"FIN agent '{agentId}' not responding. Is the FIN computer powered and running " +
                    "the agent script?",
            });
        }

        AgentState agent = GetOrAddAgent(agentId);

        int deadlineMs = command.DeadlineMs ?? _options.DefaultCommandDeadlineMs;
        FinCommand toEnqueue = command with
        {
            IssuedAt = command.IssuedAt ?? _timeProvider.GetUtcNow(),
            DeadlineMs = deadlineMs,
        };

        var pending = new PendingCommand(toEnqueue.Id);
        if (!_pending.TryAdd(toEnqueue.Id, pending))
        {
            // A duplicate id is a caller bug: the id is the sole correlation key and must be unique.
            throw new FinBridgeException(new FinError
            {
                Code = FinErrorCode.InvalidArgs,
                Message = $"Command id '{toEnqueue.Id}' is already in flight; ids must be unique.",
            });
        }

        // Reject (never drop) on a full queue: silently discarding a mutation is unsafe.
        if (!agent.CommandQueue.Writer.TryWrite(toEnqueue))
        {
            _pending.TryRemove(toEnqueue.Id, out _);
            throw new FinBridgeException(new FinError
            {
                Code = FinErrorCode.QueueFull,
                Message =
                    $"FIN agent '{agentId}' command queue is full ({_options.MaxQueuedCommands}). " +
                    "Wait for in-flight commands to drain before issuing more.",
            });
        }

        // Wire the deadline handler BEFORE arming the timer: a very small deadline (or a fake clock
        // already advanced) could otherwise fire the timer before OnTimeout is attached, making the
        // timeout a silent no-op and leaving the waiter to hang forever.
        pending.OnTimeout += () => OnCommandDeadline(pending);
        pending.ArmDeadline(_timeProvider, TimeSpan.FromMilliseconds(deadlineMs));

        // Caller cancellation tombstones the id and completes the waiter as cancelled, so a command
        // that is later re-read from the channel is dropped (not delivered) and a straggling result
        // is discarded — cancellation is as binding as a timeout for at-most-once.
        await using CancellationTokenRegistration registration = cancellationToken.Register(
            static state =>
            {
                var ctx = (CancelContext)state!;
                ctx.Bridge.OnCommandCancelled(ctx.Pending);
            },
            new CancelContext(this, pending));

        try
        {
            return await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            // The waiter is done one way or another; stop tracking it as pending. (If it timed out or
            // was cancelled it is already tombstoned, so a straggling result or a re-read command is
            // still recognised and discarded.)
            _pending.TryRemove(toEnqueue.Id, out _);
            pending.Dispose();
        }
    }

    // State captured for the cancellation callback (avoids a closure allocation per registration).
    private sealed record CancelContext(FinBridge Bridge, PendingCommand Pending);

    private void OnCommandDeadline(PendingCommand pending)
    {
        // Resolve the terminal transition under the pending's lock so the delivered-vs-not decision
        // cannot race the drain loop's admit step. Returns null if the waiter already completed (a
        // result or cancellation won); in that case the deadline is a no-op and nothing is tombstoned,
        // so a command that genuinely succeeded is never tombstoned.
        FinErrorCode? code = pending.TryTimeout();
        if (code is null)
        {
            return;
        }

        string message = code == FinErrorCode.DeliveredNoResult
            ? $"Command '{pending.Id}' was delivered to the agent but no result arrived in time; " +
              "it may have executed. Do not blindly reissue."
            : $"Command '{pending.Id}' was never picked up by the agent before its deadline; " +
              "it almost certainly did not execute.";

        // Tombstone so a result racing in right now, or a re-read of the command from the channel, is
        // recognised and discarded.
        Tombstone(pending.Id);

        pending.Completion.TrySetException(new FinBridgeException(new FinError
        {
            Code = code.Value,
            Message = message,
        }));
    }

    private void OnCommandCancelled(PendingCommand pending)
    {
        // Mark the command terminal under its lock; if the deadline or a result already won, this is a
        // no-op. Tombstone first so a command later re-read from the channel is dropped (not delivered)
        // and a straggling result is discarded.
        if (!pending.TryCancel())
        {
            return;
        }

        Tombstone(pending.Id);
        pending.Completion.TrySetCanceled();
    }

    /// <inheritdoc />
    public AgentLiveness GetLiveness(string agentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        if (!_agents.TryGetValue(agentId, out AgentState? agent))
        {
            return new AgentLiveness(agentId, IsAlive: false, LastSeen: default, AgentScriptVersion: null, DroppedEvents: 0);
        }

        return agent.Snapshot(_timeProvider.GetUtcNow(), _options.AgentLivenessMs);
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentLiveness> GetAgents()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        return [.. _agents.Values.Select(a => a.Snapshot(now, _options.AgentLivenessMs))];
    }

    /// <inheritdoc />
    public IReadOnlyList<FinEvent> RecentEvents()
    {
        // Merge each agent's per-agent ring, ordered by server-stamped receipt time (the authoritative
        // order; the agent clock is never trusted). Per-agent rings mean a chatty agent cannot evict
        // another agent's recent events.
        return
        [
            .. _agents.Values
                .SelectMany(a => a.SnapshotEvents())
                .OrderBy(e => e.ReceivedAt)
        ];
    }

    /// <inheritdoc />
    public IDisposable Subscribe(out ChannelReader<FinEvent> reader)
    {
        // Per-subscriber bounded channel, drop-oldest: a slow consumer loses old events rather than
        // back-pressuring ingestion or stalling the host.
        Channel<FinEvent> channel = Channel.CreateBounded<FinEvent>(new BoundedChannelOptions(_options.MaxBufferedEvents)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_subscriberLock)
        {
            _subscribers.Add(channel);
        }

        reader = channel.Reader;
        return new Subscription(this, channel);
    }

    private void Unsubscribe(Channel<FinEvent> channel)
    {
        lock (_subscriberLock)
        {
            _subscribers.Remove(channel);
        }

        channel.Writer.TryComplete();
    }

    // ---- Transport-facing ----------------------------------------------------------------------

    /// <inheritdoc />
    public HelloResponse HelloAsync(HelloRequest hello)
    {
        ArgumentNullException.ThrowIfNull(hello);
        RequireSupportedVersion(hello.ProtocolVersion);

        AgentState agent = GetOrAddAgent(hello.AgentId);
        agent.MarkSeen(_timeProvider.GetUtcNow(), hello.AgentScriptVersion);

        _logger.LogInformation(
            "FIN agent {AgentId} said hello (script {ScriptVersion}, protocol {ProtocolVersion})",
            hello.AgentId, hello.AgentScriptVersion, hello.ProtocolVersion);

        return new HelloResponse
        {
            ProtocolVersion = FinProtocol.Version,
            SessionAccepted = true,
            ServerHoldMs = _options.ServerHoldMs,
            AgentLivenessMs = _options.AgentLivenessMs,
        };
    }

    /// <inheritdoc />
    public async Task<PollResponse> PollAsync(PollRequest poll, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);
        RequireSupportedVersion(poll.ProtocolVersion);

        AgentState agent = GetOrAddAgent(poll.AgentId);
        agent.MarkSeen(_timeProvider.GetUtcNow(), poll.AgentScriptVersion);

        IngestResults(poll.Results);
        IngestEvents(agent, poll.Events);
        agent.ReportedDroppedEvents = poll.DroppedEvents;

        IReadOnlyList<FinCommand> commands = await DrainCommandsAsync(agent, cancellationToken).ConfigureAwait(false);

        return new PollResponse
        {
            ProtocolVersion = FinProtocol.Version,
            Commands = commands,
        };
    }

    private void IngestResults(IReadOnlyList<FinResult> results)
    {
        foreach (FinResult result in results)
        {
            if (string.IsNullOrEmpty(result.Id))
            {
                _logger.LogWarning("Discarding FIN result with no command id");
                continue;
            }

            // Enforce the result invariant the schema's allOf defines (ok => payload, no error;
            // !ok => error, no payload). A malformed result is rejected outright — never used to
            // complete a waiter — so the deadline still fires with the correct at-most-once outcome
            // (DELIVERED_NO_RESULT for a delivered command) rather than a caller acting on a body the
            // contract forbids. Dropping (vs. failing the waiter with a typed error) is the safer
            // choice: a malformed body is indistinguishable from a buggy/compromised agent, and we
            // must not let it masquerade as a definitive "did/did not happen" answer.
            if (!IsResultInvariantValid(result))
            {
                _logger.LogWarning(
                    "Discarding malformed FIN result for command {CommandId}: ok={Ok} but payload/error " +
                    "do not satisfy the result invariant", result.Id, result.Ok);
                continue;
            }

            // Complete the waiter FIRST: TaskCompletionSource completion is atomic and first-writer-
            // wins, so an in-time result that races the deadline timer cannot be lost. The tombstone
            // check below is only for classifying/logging a *late* straggler, never a gate before
            // completion.
            if (_pending.TryGetValue(result.Id, out PendingCommand? pending) && pending.TryComplete(result))
            {
                continue;
            }

            // We did not complete a waiter. Either it was already terminal (timed out/cancelled — the
            // result lost the race and the id is tombstoned) or there was never a waiter (e.g. an agent
            // restart replaying state). Distinguish for logging only.
            if (IsTombstoned(result.Id))
            {
                _logger.LogDebug("Discarding result for tombstoned command {CommandId} (deadline/cancel won the race)", result.Id);
            }
            else
            {
                _logger.LogDebug("Discarding result for unknown command {CommandId}", result.Id);
            }
        }
    }

    // The result invariant from result.schema.json: ok=true requires a payload and forbids an error;
    // ok=false requires an error and forbids a payload.
    private static bool IsResultInvariantValid(FinResult result)
        => result.Ok
            ? result.Payload is not null && result.Error is null
            : result.Error is not null && result.Payload is null;

    private void IngestEvents(AgentState agent, IReadOnlyList<FinEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Stamp authoritative receivedAt + agentId (the agent clock is advisory), append to this
        // agent's own ring (drop-oldest, per-agent cap), then fan out to global subscribers.
        FinEvent[] stamped = new FinEvent[events.Count];
        for (int i = 0; i < events.Count; i++)
        {
            stamped[i] = events[i] with { ReceivedAt = now, AgentId = agent.AgentId };
        }

        agent.AppendEvents(stamped, _options.MaxBufferedEvents);

        lock (_subscriberLock)
        {
            foreach (FinEvent ev in stamped)
            {
                foreach (Channel<FinEvent> subscriber in _subscribers)
                {
                    // Drop-oldest channels never block; ignore the (always-true unless completed) result.
                    subscriber.Writer.TryWrite(ev);
                }
            }
        }
    }

    private async Task<IReadOnlyList<FinCommand>> DrainCommandsAsync(AgentState agent, CancellationToken cancellationToken)
    {
        ChannelReader<FinCommand> reader = agent.CommandQueue.Reader;

        // Hold the poll open up to ServerHoldMs waiting for the first deliverable command, so a healthy
        // idle agent makes near-zero request volume. The hold uses a TimeProvider-backed
        // CancellationTokenSource whose internal timer disposes safely (no manual ITimer whose callback
        // could race its own Dispose, the old hold-timer CTS dispose race), linked with the caller's
        // token. Reading time through the injected TimeProvider keeps the hold testable at ~zero
        // wall-clock.
        using var holdTimerCts = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(_options.ServerHoldMs), _timeProvider);
        using var holdCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, holdTimerCts.Token);

        var batch = new List<FinCommand>();

        // Read commands until we have a deliverable one (or the hold expires). A command whose id is
        // tombstoned or no longer pending (timed out, cancelled, or already completed) is skipped and
        // logged — never delivered — closing the stale-delivery hole. We keep reading past skipped
        // commands within the same hold so a single dead command does not waste the whole long-poll.
        try
        {
            while (batch.Count < MaxCommandsPerWake)
            {
                FinCommand command;
                if (batch.Count == 0)
                {
                    // Block (up to the hold) for the first deliverable command.
                    command = await reader.ReadAsync(holdCts.Token).ConfigureAwait(false);
                }
                else if (!reader.TryRead(out command!))
                {
                    // No more already-queued commands; ship what we have rather than re-blocking.
                    break;
                }

                if (TryAdmit(command))
                {
                    batch.Add(command);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Hold expired with nothing deliverable queued: return whatever (possibly empty) batch we
            // accumulated. A normal long-poll.
        }

        return batch;
    }

    /// <summary>
    /// Decides whether a command pulled from the queue may be delivered, and atomically marks it
    /// delivered if so. A command whose waiter is already terminal (timed out, cancelled, or
    /// completed) or whose id is tombstoned is NOT delivered — this is the guard the deadline path
    /// consults, so a command admitted here can only ever time out as DELIVERED_NO_RESULT, never
    /// QUEUED_NOT_PICKED_UP.
    /// </summary>
    private bool TryAdmit(FinCommand command)
    {
        if (IsTombstoned(command.Id))
        {
            _logger.LogDebug("Skipping delivery of tombstoned command {CommandId}", command.Id);
            return false;
        }

        if (!_pending.TryGetValue(command.Id, out PendingCommand? pending))
        {
            // No waiter: the caller already gave up (removed in SendAsync's finally) before the agent
            // pulled it. Nothing to deliver to.
            _logger.LogDebug("Skipping delivery of command {CommandId} with no live waiter", command.Id);
            return false;
        }

        if (!pending.TryMarkDelivered())
        {
            _logger.LogDebug("Skipping delivery of already-terminal command {CommandId}", command.Id);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Cap on commands handed to the agent per poll wake (ADR-001 <c>maxCommandsPerWake</c>). Fixed
    /// here because it is a protocol-cadence fact shared with the agent loop, not an operator knob.
    /// </summary>
    private const int MaxCommandsPerWake = 8;

    private AgentState GetOrAddAgent(string agentId)
        => _agents.GetOrAdd(
            agentId,
            static (id, max) => new AgentState(id, max),
            _options.MaxQueuedCommands);

    private static void RequireSupportedVersion(int agentVersion)
    {
        if (!FinProtocol.IsSupportedVersion(agentVersion))
        {
            throw new ProtocolVersionMismatchException(
                agentVersion, FinProtocol.MinSupportedVersion, FinProtocol.MaxSupportedVersion);
        }
    }

    // ---- Tombstones ----------------------------------------------------------------------------

    private void Tombstone(string id)
    {
        lock (_tombstoneLock)
        {
            if (!_tombstones.Add(id))
            {
                return;
            }

            _tombstoneOrder.AddLast(id);

            // Oldest-first eviction caps the set. MaxTombstones is fixed (not an operator knob)
            // because it is a memory/safety trade-off, not a behavioural tuning point: at 4096 ids a
            // ~26-byte ULID costs ~100 KB worst case, far more than the in-flight window of even a
            // busy operator, while still bounding a multi-day uptime. A late result for an id evicted
            // this long after its deadline is treated as "unknown command" and dropped anyway, so the
            // only risk of too-small a cap is a noisier log line, never a re-applied mutation.
            while (_tombstoneOrder.Count > MaxTombstones)
            {
                string oldest = _tombstoneOrder.First!.Value;
                _tombstoneOrder.RemoveFirst();
                _tombstones.Remove(oldest);
            }
        }
    }

    private const int MaxTombstones = 4096;

    private bool IsTombstoned(string id)
    {
        lock (_tombstoneLock)
        {
            return _tombstones.Contains(id);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Fail every outstanding waiter with a typed shutdown error so a caller parked on SendAsync
        // does not hang past host shutdown. Tombstone each id so a result that arrives during the
        // shutdown window is still recognised and discarded (at-most-once survives shutdown).
        foreach (PendingCommand pending in _pending.Values)
        {
            if (pending.TryShutdown())
            {
                Tombstone(pending.Id);
                pending.Completion.TrySetException(new FinBridgeException(new FinError
                {
                    Code = FinErrorCode.OperationFailed,
                    Message = "FIN bridge is shutting down; the command's result will not be awaited.",
                }));
            }

            pending.Dispose();
        }

        lock (_subscriberLock)
        {
            foreach (Channel<FinEvent> subscriber in _subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }

    /// <summary>Per-agent state: a bounded reject-on-full command queue, liveness, and an event ring.</summary>
    private sealed class AgentState
    {
        private readonly object _ringLock = new();
        private readonly Queue<FinEvent> _eventRing = new();
        private long _droppedEvents;

        public AgentState(string agentId, int maxQueuedCommands = 64)
        {
            AgentId = agentId;
            CommandQueue = Channel.CreateBounded<FinCommand>(new BoundedChannelOptions(maxQueuedCommands)
            {
                // Reject on full (TryWrite returns false): a mutation is never silently dropped.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            });
        }

        public string AgentId { get; }

        public Channel<FinCommand> CommandQueue { get; }

        /// <summary>The agent's self-reported running dropped count (its own ring), advisory.</summary>
        public long ReportedDroppedEvents { get; set; }

        private long _lastSeenUtcTicks;
        private string? _agentScriptVersion;

        public void MarkSeen(DateTimeOffset now, string? agentScriptVersion)
        {
            Interlocked.Exchange(ref _lastSeenUtcTicks, now.UtcTicks);
            if (agentScriptVersion is not null)
            {
                Volatile.Write(ref _agentScriptVersion, agentScriptVersion);
            }
        }

        /// <summary>Appends stamped events to this agent's ring, evicting oldest past the cap.</summary>
        public void AppendEvents(IReadOnlyList<FinEvent> stamped, int maxBufferedEvents)
        {
            lock (_ringLock)
            {
                foreach (FinEvent ev in stamped)
                {
                    _eventRing.Enqueue(ev);
                    while (_eventRing.Count > maxBufferedEvents)
                    {
                        _eventRing.Dequeue();
                        _droppedEvents++;
                    }
                }
            }
        }

        /// <summary>A point-in-time copy of this agent's ring (oldest-first).</summary>
        public IReadOnlyList<FinEvent> SnapshotEvents()
        {
            lock (_ringLock)
            {
                return [.. _eventRing];
            }
        }

        public AgentLiveness Snapshot(DateTimeOffset now, int livenessMs)
        {
            long lastSeenTicks = Interlocked.Read(ref _lastSeenUtcTicks);
            var lastSeen = new DateTimeOffset(lastSeenTicks, TimeSpan.Zero);
            bool alive = lastSeenTicks != 0 && (now - lastSeen) <= TimeSpan.FromMilliseconds(livenessMs);
            long dropped = Interlocked.Read(ref _droppedEvents);
            return new AgentLiveness(AgentId, alive, lastSeen, Volatile.Read(ref _agentScriptVersion), dropped);
        }
    }

    /// <summary>
    /// One in-flight command's correlation state: the waiter TCS, a small lifecycle state machine, and
    /// the deadline timer. All transitions go through <see cref="_lock"/> so admit-to-batch, deadline,
    /// cancel, and result completion are mutually exclusive — the heart of at-most-once reconciliation.
    /// The TCS uses run-continuations-asynchronously so completing it from a timer callback or an
    /// ingest path never inlines arbitrary continuation work on that thread.
    /// </summary>
    private sealed class PendingCommand : IDisposable
    {
        private enum LifeState
        {
            Pending,
            Delivered,
            Completed,
            TimedOut,
            Cancelled,
            ShutDown,
        }

        private readonly object _lock = new();
        private LifeState _state = LifeState.Pending;
        private ITimer? _timer;

        public PendingCommand(string id) => Id = id;

        public string Id { get; }

        public TaskCompletionSource<FinResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Raised when the deadline timer fires, on the timer thread.</summary>
        public event Action? OnTimeout;

        /// <summary>Arms the deadline timer. Call only AFTER <see cref="OnTimeout"/> is wired.</summary>
        public void ArmDeadline(TimeProvider timeProvider, TimeSpan deadline)
            => _timer = timeProvider.CreateTimer(
                static state => ((PendingCommand)state!).OnTimeout?.Invoke(),
                this,
                deadline,
                Timeout.InfiniteTimeSpan);

        private bool IsTerminal => _state is LifeState.Completed or LifeState.TimedOut or LifeState.Cancelled or LifeState.ShutDown;

        /// <summary>
        /// Marks the command delivered to the agent if it is still admissible (Pending or already
        /// Delivered). Returns false if it has reached a terminal state, so a tombstoned/timed-out/
        /// cancelled command is never delivered.
        /// </summary>
        public bool TryMarkDelivered()
        {
            lock (_lock)
            {
                if (IsTerminal)
                {
                    return false;
                }

                _state = LifeState.Delivered;
                return true;
            }
        }

        /// <summary>
        /// Completes the waiter with a result if it is not already terminal. Returns true if this call
        /// completed it. TrySetResult itself is atomic; the lock keeps <see cref="_state"/> consistent
        /// with the other transitions.
        /// </summary>
        public bool TryComplete(FinResult result)
        {
            lock (_lock)
            {
                if (IsTerminal)
                {
                    return false;
                }

                if (!Completion.TrySetResult(result))
                {
                    return false;
                }

                _state = LifeState.Completed;
                return true;
            }
        }

        /// <summary>
        /// Transitions to TimedOut if not already terminal, returning the at-most-once outcome code
        /// (DELIVERED_NO_RESULT if it had been delivered, else QUEUED_NOT_PICKED_UP). Returns null if
        /// the waiter already completed (a result or cancellation won the race) — the caller then does
        /// nothing, so a succeeded command is never tombstoned.
        /// </summary>
        public FinErrorCode? TryTimeout()
        {
            lock (_lock)
            {
                if (IsTerminal)
                {
                    return null;
                }

                FinErrorCode code = _state == LifeState.Delivered
                    ? FinErrorCode.DeliveredNoResult
                    : FinErrorCode.QueuedNotPickedUp;
                _state = LifeState.TimedOut;
                return code;
            }
        }

        /// <summary>Transitions to Cancelled if not already terminal; returns whether this won.</summary>
        public bool TryCancel()
        {
            lock (_lock)
            {
                if (IsTerminal)
                {
                    return false;
                }

                _state = LifeState.Cancelled;
                return true;
            }
        }

        /// <summary>Transitions to ShutDown if not already terminal; returns whether this won.</summary>
        public bool TryShutdown()
        {
            lock (_lock)
            {
                if (IsTerminal)
                {
                    return false;
                }

                _state = LifeState.ShutDown;
                return true;
            }
        }

        public void Dispose() => _timer?.Dispose();
    }

    /// <summary>An event subscription handle; disposing it unsubscribes and completes the channel.</summary>
    private sealed class Subscription : IDisposable
    {
        private readonly FinBridge _bridge;
        private readonly Channel<FinEvent> _channel;
        private bool _disposed;

        public Subscription(FinBridge bridge, Channel<FinEvent> channel)
        {
            _bridge = bridge;
            _channel = channel;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _bridge.Unsubscribe(_channel);
        }
    }
}
