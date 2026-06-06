using System.Collections.Concurrent;
using System.Threading.Channels;

using FicsitMcp.Domain.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Default <see cref="IFinBridge"/>: a long-poll command channel with an at-most-once execution
/// model. Owns, per agent, a bounded command queue and liveness; globally, the result-correlation
/// registry (TCS keyed by command id), a tombstone set, and a bounded drop-oldest event ring with
/// channel fan-out. Time is read through an injected <see cref="TimeProvider"/> so deadlines and
/// liveness are testable at ~zero wall-clock.
/// </summary>
/// <remarks>
/// No HTTP or MCP dependency: the host's endpoint layer drives the transport-facing methods and the
/// machine-control tools drive the tool-facing ones. Thread-safe for concurrent agents and callers.
/// </remarks>
public sealed class FinBridge : IFinBridge, IDisposable
{
    private readonly FinBridgeOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FinBridge> _logger;

    private readonly ConcurrentDictionary<string, AgentState> _agents = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingCommand> _pending = new(StringComparer.Ordinal);

    // Tombstones: ids whose deadline already fired. A result that arrives for one of these is
    // discarded, never re-applied to a caller who gave up. Bounded by eviction (oldest-first) so the
    // set cannot grow without limit on a long-running server.
    private readonly object _tombstoneLock = new();
    private readonly LinkedList<string> _tombstoneOrder = [];
    private readonly HashSet<string> _tombstones = new(StringComparer.Ordinal);
    private const int MaxTombstones = 4096;

    // Event ring + subscriber fan-out. One lock guards both so a subscriber added mid-burst sees a
    // consistent view.
    private readonly object _eventLock = new();
    private readonly Queue<FinEvent> _eventRing = new();
    private readonly List<Channel<FinEvent>> _subscribers = [];
    private long _droppedEvents;

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

        // Arm the server-side deadline. On fire, complete the waiter with the outcome that reflects
        // whether the command was ever delivered, and tombstone the id so a late result is dropped.
        ITimer timer = _timeProvider.CreateTimer(
            static state => ((PendingCommand)state!).FireDeadline(),
            pending,
            TimeSpan.FromMilliseconds(deadlineMs),
            Timeout.InfiniteTimeSpan);
        pending.AttachTimer(timer);
        pending.OnTimeout += () => OnCommandDeadline(pending);

        await using CancellationTokenRegistration registration = cancellationToken.Register(
            static state => ((PendingCommand)state!).Cancel(), pending);

        try
        {
            return await pending.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            // The waiter is done one way or another; stop tracking it as pending. (If it timed out it
            // is already tombstoned so a straggling result is still recognised and discarded.)
            _pending.TryRemove(toEnqueue.Id, out _);
            pending.Dispose();
        }
    }

    private void OnCommandDeadline(PendingCommand pending)
    {
        // If the result (or a cancellation) already completed the waiter, the deadline lost the race:
        // do nothing, so a command that genuinely succeeded is not tombstoned.
        if (pending.Completion.Task.IsCompleted)
        {
            return;
        }

        FinErrorCode code = pending.Delivered
            ? FinErrorCode.DeliveredNoResult
            : FinErrorCode.QueuedNotPickedUp;

        string message = pending.Delivered
            ? $"Command '{pending.Id}' was delivered to the agent but no result arrived in time; " +
              "it may have executed. Do not blindly reissue."
            : $"Command '{pending.Id}' was never picked up by the agent before its deadline; " +
              "it almost certainly did not execute.";

        // Tombstone before completing so a result racing in right now is recognised and discarded.
        Tombstone(pending.Id);

        pending.Completion.TrySetException(new FinBridgeException(new FinError
        {
            Code = code,
            Message = message,
        }));
    }

    /// <inheritdoc />
    public AgentLiveness GetLiveness(string agentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        if (!_agents.TryGetValue(agentId, out AgentState? agent))
        {
            return new AgentLiveness(agentId, IsAlive: false, LastSeen: default, AgentScriptVersion: null);
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
        lock (_eventLock)
        {
            return [.. _eventRing];
        }
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

        lock (_eventLock)
        {
            _subscribers.Add(channel);
        }

        reader = channel.Reader;
        return new Subscription(this, channel);
    }

    private void Unsubscribe(Channel<FinEvent> channel)
    {
        lock (_eventLock)
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
        IngestEvents(poll.AgentId, poll.Events);
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

            // A result for a tombstoned id is a straggler that arrived after the caller gave up:
            // recognise and discard it, never re-apply.
            if (IsTombstoned(result.Id))
            {
                _logger.LogDebug("Discarding result for tombstoned command {CommandId}", result.Id);
                continue;
            }

            if (_pending.TryGetValue(result.Id, out PendingCommand? pending))
            {
                pending.Completion.TrySetResult(result);
            }
            else
            {
                // No waiter and not tombstoned: a result for a command we never tracked (e.g. agent
                // restart replaying state). Nothing safe to do but log it.
                _logger.LogDebug("Discarding result for unknown command {CommandId}", result.Id);
            }
        }
    }

    private void IngestEvents(string agentId, IReadOnlyList<FinEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        lock (_eventLock)
        {
            foreach (FinEvent raw in events)
            {
                // The server stamps authoritative receivedAt + agentId; the agent clock is advisory.
                FinEvent stamped = raw with { ReceivedAt = now, AgentId = agentId };

                _eventRing.Enqueue(stamped);
                while (_eventRing.Count > _options.MaxBufferedEvents)
                {
                    _eventRing.Dequeue();
                    _droppedEvents++;
                }

                foreach (Channel<FinEvent> subscriber in _subscribers)
                {
                    // Drop-oldest channels never block; ignore the (always-true unless completed) result.
                    subscriber.Writer.TryWrite(stamped);
                }
            }
        }
    }

    private async Task<IReadOnlyList<FinCommand>> DrainCommandsAsync(AgentState agent, CancellationToken cancellationToken)
    {
        ChannelReader<FinCommand> reader = agent.CommandQueue.Reader;

        // Hold the poll open up to ServerHoldMs waiting for the first command, so a healthy idle
        // agent makes near-zero request volume. The hold is bounded by the timer source so it is
        // testable at ~zero wall-clock.
        using var holdCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        FinCommand? first;
        try
        {
            using ITimer holdTimer = _timeProvider.CreateTimer(
                static state => ((CancellationTokenSource)state!).Cancel(),
                holdCts,
                TimeSpan.FromMilliseconds(_options.ServerHoldMs),
                Timeout.InfiniteTimeSpan);

            first = await reader.ReadAsync(holdCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Hold expired with nothing queued: return an empty command list (a normal long-poll).
            return [];
        }

        var batch = new List<FinCommand> { first };

        // Drain any further commands already queued, but never more than one wake's worth, so a flood
        // cannot starve the agent's game-thread tick budget.
        while (batch.Count < MaxCommandsPerWake && reader.TryRead(out FinCommand? next))
        {
            batch.Add(next);
        }

        // Mark each delivered command so a later no-result timeout reports DELIVERED_NO_RESULT
        // (may-have-executed) rather than QUEUED_NOT_PICKED_UP (did-not-execute).
        foreach (FinCommand command in batch)
        {
            if (_pending.TryGetValue(command.Id, out PendingCommand? pending))
            {
                pending.MarkDelivered();
            }
        }

        return batch;
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
            while (_tombstoneOrder.Count > MaxTombstones)
            {
                string oldest = _tombstoneOrder.First!.Value;
                _tombstoneOrder.RemoveFirst();
                _tombstones.Remove(oldest);
            }
        }
    }

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

        foreach (PendingCommand pending in _pending.Values)
        {
            pending.Dispose();
        }

        lock (_eventLock)
        {
            foreach (Channel<FinEvent> subscriber in _subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }

    /// <summary>Per-agent state: a bounded reject-on-full command queue plus liveness.</summary>
    private sealed class AgentState
    {
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

        public AgentLiveness Snapshot(DateTimeOffset now, int livenessMs)
        {
            long lastSeenTicks = Interlocked.Read(ref _lastSeenUtcTicks);
            var lastSeen = new DateTimeOffset(lastSeenTicks, TimeSpan.Zero);
            bool alive = lastSeenTicks != 0 && (now - lastSeen) <= TimeSpan.FromMilliseconds(livenessMs);
            return new AgentLiveness(AgentId, alive, lastSeen, Volatile.Read(ref _agentScriptVersion));
        }
    }

    /// <summary>
    /// One in-flight command's correlation state: the waiter TCS, a delivered flag, and the deadline
    /// timer. The TCS uses run-continuations-asynchronously so completing it from a timer callback or
    /// an ingest path never inlines arbitrary continuation work on that thread.
    /// </summary>
    private sealed class PendingCommand : IDisposable
    {
        private int _delivered;
        private ITimer? _timer;

        public PendingCommand(string id) => Id = id;

        public string Id { get; }

        public TaskCompletionSource<FinResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Delivered => Volatile.Read(ref _delivered) == 1;

        /// <summary>Raised when the deadline timer fires, on the timer thread.</summary>
        public event Action? OnTimeout;

        public void AttachTimer(ITimer timer) => _timer = timer;

        public void MarkDelivered() => Interlocked.Exchange(ref _delivered, 1);

        public void FireDeadline() => OnTimeout?.Invoke();

        public void Cancel() => Completion.TrySetCanceled();

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
