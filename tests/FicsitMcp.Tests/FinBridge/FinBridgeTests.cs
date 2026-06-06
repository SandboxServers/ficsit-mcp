using System.Threading.Channels;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.FinBridge;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FicsitMcp.Tests.FinBridge;

/// <summary>
/// Unit tests for the in-process <see cref="Domain.FinBridge.FinBridge"/> command/result/event core,
/// driven entirely through its public surface with a <see cref="FakeTimeProvider"/> so deadlines and
/// liveness are exercised deterministically at ~zero wall-clock. These cover the failure modes the
/// ADR and issue call out: dead-agent fast-fail, the two distinct timeouts, queue-cap rejection,
/// event-ring eviction, tombstoned-result discard, liveness transitions, and version skew.
/// </summary>
public sealed class FinBridgeTests
{
    private const string AgentId = "factory-floor-1";
    private const string ScriptVersion = "0.3.1";

    private static FinBridgeOptions CreateOptions(Action<FinBridgeOptions>? configure = null)
    {
        var options = new FinBridgeOptions
        {
            ListenUrl = "http://127.0.0.1:8421",
            SharedSecret = "test-secret",
        };
        configure?.Invoke(options);
        return options;
    }

    private static Domain.FinBridge.FinBridge CreateBridge(FakeTimeProvider time, FinBridgeOptions options)
        => new(Options.Create(options), time, NullLogger<Domain.FinBridge.FinBridge>.Instance);

    private static Domain.FinBridge.FinBridge CreateBridge(
        FakeTimeProvider time,
        Action<FinBridgeOptions>? configure = null)
        => CreateBridge(time, CreateOptions(configure));

    private static HelloRequest Hello() => new()
    {
        ProtocolVersion = FinProtocol.Version,
        AgentId = AgentId,
        AgentScriptVersion = ScriptVersion,
    };

    private static FinCommand Command(string id, int? deadlineMs = null) => new()
    {
        Id = id,
        Target = FinTarget.Nick("iron-smelters"),
        Operation = "setStandby",
        DeadlineMs = deadlineMs,
    };

    private static PollRequest Poll(params FinResult[] results) => new()
    {
        ProtocolVersion = FinProtocol.Version,
        AgentId = AgentId,
        AgentScriptVersion = ScriptVersion,
        Results = results,
        Events = [],
        DroppedEvents = 0,
    };

    // A poll ALWAYS holds for commands up to ServerHoldMs after ingesting results/events. With a
    // FakeTimeProvider the hold only releases when time is advanced, so this helper performs the poll
    // and, when no command is queued to satisfy it immediately, expires the hold so the call returns.
    // Ingestion (results/events -> waiters, ring, subscribers) happens before the hold, so it has
    // already taken effect by the time this returns.
    private static async Task<PollResponse> PollAndReleaseAsync(
        Domain.FinBridge.FinBridge bridge, FakeTimeProvider time, FinBridgeOptions options, params FinResult[] results)
    {
        Task<PollResponse> poll = bridge.PollAsync(Poll(results));
        if (!poll.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(options.ServerHoldMs + 1));
        }

        return await poll;
    }

    // As above, but for a poll that carries events (drained into the ring / fanned to subscribers
    // before the hold).
    private static async Task<PollResponse> PollWithEventsAsync(
        Domain.FinBridge.FinBridge bridge, FakeTimeProvider time, FinBridgeOptions options, params FinEvent[] events)
    {
        Task<PollResponse> poll = bridge.PollAsync(new PollRequest
        {
            ProtocolVersion = FinProtocol.Version,
            AgentId = AgentId,
            AgentScriptVersion = ScriptVersion,
            Results = [],
            Events = events,
            DroppedEvents = 0,
        });
        if (!poll.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(options.ServerHoldMs + 1));
        }

        return await poll;
    }

    [Fact]
    public async Task SendAsync_RoundTrips_WhenAgentPollsAndPostsResult()
    {
        var time = new FakeTimeProvider();
        FinBridgeOptions options = CreateOptions(o => o.AgentLivenessMs = 100_000);
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, options);
        bridge.HelloAsync(Hello());

        // The tool enqueues a command and awaits its result.
        Task<FinResult> send = bridge.SendAsync(AgentId, Command("cmd-1", deadlineMs: 60_000));

        // The agent long-polls and receives the command (returns immediately — already queued).
        PollResponse delivered = await bridge.PollAsync(Poll());
        Assert.Single(delivered.Commands);
        Assert.Equal("cmd-1", delivered.Commands[0].Id);

        // The agent executes and reports the result on the next poll. That poll ingests the result
        // (completing the waiter) before holding for more commands; release the hold so it returns.
        await PollAndReleaseAsync(bridge, time, options, new FinResult { Id = "cmd-1", Ok = true });

        FinResult roundTripped = await send;
        Assert.True(roundTripped.Ok);
        Assert.Equal("cmd-1", roundTripped.Id);
    }

    [Fact]
    public async Task SendAsync_FailsFast_AgentOffline_WhenNeverSeen()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time);

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(
            () => bridge.SendAsync(AgentId, Command("cmd-1")));

        Assert.Equal(FinErrorCode.AgentOffline, ex.Code);
        Assert.Contains("powered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_FailsFast_AgentOffline_AfterMissedHeartbeats()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o => o.AgentLivenessMs = 40_000);
        bridge.HelloAsync(Hello());

        // Push past the liveness window with no further hello/poll.
        time.Advance(TimeSpan.FromMilliseconds(40_001));

        Assert.False(bridge.GetLiveness(AgentId).IsAlive);

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(
            () => bridge.SendAsync(AgentId, Command("cmd-1")));
        Assert.Equal(FinErrorCode.AgentOffline, ex.Code);
    }

    [Fact]
    public void Liveness_TransitionsToAlive_OnHello_AndBackToOffline_OnTimeout()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o =>
        {
            o.ServerHoldMs = 25_000;
            o.AgentLivenessMs = 40_000;
        });

        Assert.False(bridge.GetLiveness(AgentId).IsAlive);

        bridge.HelloAsync(Hello());
        Assert.True(bridge.GetLiveness(AgentId).IsAlive);

        // Just inside the window: still alive.
        time.Advance(TimeSpan.FromMilliseconds(39_999));
        Assert.True(bridge.GetLiveness(AgentId).IsAlive);

        // Past the window: offline.
        time.Advance(TimeSpan.FromMilliseconds(2));
        Assert.False(bridge.GetLiveness(AgentId).IsAlive);
    }

    [Fact]
    public async Task SendAsync_TimesOut_QueuedNotPickedUp_WhenAgentNeverPolls()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o => o.AgentLivenessMs = 100_000);
        bridge.HelloAsync(Hello());

        Task<FinResult> send = bridge.SendAsync(AgentId, Command("cmd-1", deadlineMs: 5_000));

        // No poll ever delivers it; advance past the deadline.
        time.Advance(TimeSpan.FromMilliseconds(5_001));

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(() => send);
        Assert.Equal(FinErrorCode.QueuedNotPickedUp, ex.Code);
    }

    [Fact]
    public async Task SendAsync_TimesOut_DeliveredNoResult_WhenPolledButNoResult()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o => o.AgentLivenessMs = 100_000);
        bridge.HelloAsync(Hello());

        Task<FinResult> send = bridge.SendAsync(AgentId, Command("cmd-1", deadlineMs: 5_000));

        // The agent polls and receives the command (now DELIVERED) but never reports a result.
        PollResponse delivered = await bridge.PollAsync(Poll());
        Assert.Single(delivered.Commands);

        time.Advance(TimeSpan.FromMilliseconds(5_001));

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(() => send);
        Assert.Equal(FinErrorCode.DeliveredNoResult, ex.Code);
    }

    [Fact]
    public async Task LateResult_ForTombstonedCommand_IsDiscarded_NotReApplied()
    {
        var time = new FakeTimeProvider();
        FinBridgeOptions options = CreateOptions(o => o.AgentLivenessMs = 100_000);
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, options);
        bridge.HelloAsync(Hello());

        Task<FinResult> send = bridge.SendAsync(AgentId, Command("cmd-1", deadlineMs: 5_000));
        await bridge.PollAsync(Poll()); // deliver (returns immediately — command already queued)

        time.Advance(TimeSpan.FromMilliseconds(5_001)); // tombstone

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(() => send);
        Assert.Equal(FinErrorCode.DeliveredNoResult, ex.Code);

        // A straggling result arrives after the caller gave up; must be silently discarded and must
        // not throw or resurface to anyone.
        PollResponse after = await PollAndReleaseAsync(bridge, time, options, new FinResult { Id = "cmd-1", Ok = true });
        Assert.Empty(after.Commands);
    }

    [Fact]
    public async Task SendAsync_RejectsWithQueueFull_WhenQueueAtCap()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o =>
        {
            o.MaxQueuedCommands = 2;
            o.AgentLivenessMs = 100_000;
        });
        bridge.HelloAsync(Hello());

        // Fill the queue (no agent drains it). Long deadlines so they stay enqueued.
        Task<FinResult> a = bridge.SendAsync(AgentId, Command("a", deadlineMs: 60_000));
        Task<FinResult> b = bridge.SendAsync(AgentId, Command("b", deadlineMs: 60_000));

        FinBridgeException ex = await Assert.ThrowsAsync<FinBridgeException>(
            () => bridge.SendAsync(AgentId, Command("c", deadlineMs: 60_000)));
        Assert.Equal(FinErrorCode.QueueFull, ex.Code);

        // The two accepted commands are still in flight (not faulted by the rejection of the third).
        Assert.False(a.IsCompleted);
        Assert.False(b.IsCompleted);
    }

    [Fact]
    public async Task EventRing_EvictsOldest_WhenOverflowing_AndExposesRecent()
    {
        var time = new FakeTimeProvider();
        FinBridgeOptions options = CreateOptions(o => o.MaxBufferedEvents = 3);
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, options);
        bridge.HelloAsync(Hello());

        FinEvent[] five = [.. Enumerable.Range(1, 5).Select(i => new FinEvent
        {
            Seq = i,
            Signal = "ItemTransfer",
            Source = new ComponentRef { Id = $"belt-{i}" },
        })];

        await PollWithEventsAsync(bridge, time, options, five);

        IReadOnlyList<FinEvent> recent = bridge.RecentEvents();
        Assert.Equal(3, recent.Count);
        // Oldest two (seq 1, 2) evicted; newest three retained in order.
        Assert.Equal([3L, 4L, 5L], recent.Select(e => e.Seq));
        // The server stamped receivedAt and agentId on ingest.
        Assert.All(recent, e =>
        {
            Assert.NotNull(e.ReceivedAt);
            Assert.Equal(AgentId, e.AgentId);
        });
    }

    [Fact]
    public async Task Subscribe_ReceivesLiveEvents_AndUnsubscribeStops()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time);
        bridge.HelloAsync(Hello());

        IDisposable subscription = bridge.Subscribe(out ChannelReader<FinEvent> reader);

        await PollWithEventsAsync(bridge, time, CreateOptions(),
            new FinEvent { Seq = 1, Signal = "PowerFuseChanged", Source = new ComponentRef { Id = "fuse-1" } });

        FinEvent received = await reader.ReadAsync();
        Assert.Equal("PowerFuseChanged", received.Signal);

        subscription.Dispose();
        Assert.False(await reader.WaitToReadAsync()); // channel completed
    }

    [Fact]
    public void HelloAsync_Throws_OnUnsupportedProtocolVersion()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time);

        ProtocolVersionMismatchException ex = Assert.Throws<ProtocolVersionMismatchException>(
            () => bridge.HelloAsync(Hello() with { ProtocolVersion = 999 }));

        Assert.Equal(999, ex.AgentVersion);
        Assert.Equal(FinProtocol.MinSupportedVersion, ex.ServerSupportedMin);
        Assert.Equal(FinProtocol.MaxSupportedVersion, ex.ServerSupportedMax);
    }

    [Fact]
    public async Task PollAsync_HoldExpires_ReturnsEmptyCommands_WhenNothingQueued()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o =>
        {
            o.ServerHoldMs = 25_000;
            o.AgentLivenessMs = 40_000;
        });
        bridge.HelloAsync(Hello());

        Task<PollResponse> poll = bridge.PollAsync(Poll());
        Assert.False(poll.IsCompleted); // held open

        time.Advance(TimeSpan.FromMilliseconds(25_001)); // hold expires

        PollResponse response = await poll;
        Assert.Empty(response.Commands);
    }

    [Fact]
    public async Task PollAsync_ReturnsImmediately_WhenCommandAlreadyQueued()
    {
        var time = new FakeTimeProvider();
        using Domain.FinBridge.FinBridge bridge = CreateBridge(time, o => o.AgentLivenessMs = 100_000);
        bridge.HelloAsync(Hello());

        _ = bridge.SendAsync(AgentId, Command("cmd-1", deadlineMs: 60_000));

        // Without advancing time, the held poll returns the already-queued command at once.
        PollResponse response = await bridge.PollAsync(Poll());
        Assert.Single(response.Commands);
        Assert.Equal("cmd-1", response.Commands[0].Id);
    }
}
