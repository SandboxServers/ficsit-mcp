using System.Threading.Channels;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The in-process bridge between MCP tools and the in-world FIN Lua agent. Has two faces:
/// a <b>tool-facing</b> side (<see cref="SendAsync"/>, <see cref="GetLiveness"/>,
/// <see cref="RecentEvents"/>, <see cref="Subscribe"/>) the machine-control tools call, and a
/// <b>transport-facing</b> side (<see cref="HelloAsync"/>, <see cref="PollAsync"/>) the HTTP endpoint
/// layer drives. The implementation owns the per-agent command queue, the result-correlation
/// registry, liveness, and the bounded event buffer. It has no HTTP or MCP dependency, so it is
/// unit-testable in isolation.
/// </summary>
/// <remarks>
/// At-most-once is structural here: command ids are the sole correlation key, a deadline tombstones
/// an id so a late result is discarded, and nothing is ever auto-retried. The two distinct timeout
/// outcomes (<see cref="FinErrorCode.QueuedNotPickedUp"/> vs <see cref="FinErrorCode.DeliveredNoResult"/>)
/// are surfaced via <see cref="FinBridgeException"/> so callers can reason about whether a mutation
/// may have applied.
/// </remarks>
public interface IFinBridge
{
    // ---- Tool-facing ---------------------------------------------------------------------------

    /// <summary>
    /// Enqueues <paramref name="command"/> for <paramref name="agentId"/> and awaits its result.
    /// Fails fast with <see cref="FinErrorCode.AgentOffline"/> if the agent is not currently alive,
    /// and with <see cref="FinErrorCode.QueueFull"/> if its queue is at the cap — neither hangs to
    /// the deadline. On a result-wait timeout it throws <see cref="FinBridgeException"/> with either
    /// <see cref="FinErrorCode.QueuedNotPickedUp"/> (agent never pulled it; almost certainly did not
    /// execute) or <see cref="FinErrorCode.DeliveredNoResult"/> (delivered, no result; may have
    /// executed). The command is never auto-retried.
    /// </summary>
    /// <param name="agentId">The agent to run the command on.</param>
    /// <param name="command">The command to enqueue. Its id must be unique and is the correlation key.</param>
    /// <param name="cancellationToken">Cancels the wait (the enqueue is not unwound; the result is still discarded on arrival).</param>
    /// <returns>The agent's successful or failed <see cref="FinResult"/> for this command.</returns>
    /// <exception cref="FinBridgeException">Delivery failed or no result arrived before the deadline.</exception>
    Task<FinResult> SendAsync(string agentId, FinCommand command, CancellationToken cancellationToken = default);

    /// <summary>Returns the current liveness snapshot for an agent, or a not-alive snapshot if never seen.</summary>
    AgentLiveness GetLiveness(string agentId);

    /// <summary>Lists the agents the server has ever registered, with their current liveness.</summary>
    IReadOnlyList<AgentLiveness> GetAgents();

    /// <summary>
    /// A snapshot of the most recent events in the server-side ring (oldest-first), across all
    /// agents. Bounded by <c>MaxBufferedEvents</c>; the oldest are evicted under pressure.
    /// </summary>
    IReadOnlyList<FinEvent> RecentEvents();

    /// <summary>
    /// Subscribes to the live event stream. Each subscriber gets its own bounded channel; a slow
    /// subscriber drops oldest rather than back-pressuring ingestion or the host. Issue #21 consumes
    /// this to fan events out as MCP notifications. Dispose the returned subscription to unsubscribe.
    /// </summary>
    /// <param name="reader">Receives the subscriber's channel reader.</param>
    /// <returns>A disposable that unsubscribes and completes the channel.</returns>
    IDisposable Subscribe(out ChannelReader<FinEvent> reader);

    // ---- Transport-facing (driven by the HTTP endpoint layer) ----------------------------------

    /// <summary>
    /// Handles a boot <see cref="HelloRequest"/>: validates the protocol version, registers the
    /// agent, marks it alive, and returns the timing contract. Throws
    /// <see cref="ProtocolVersionMismatchException"/> on unsupported skew (host → HTTP 426).
    /// </summary>
    HelloResponse HelloAsync(HelloRequest hello);

    /// <summary>
    /// Handles one long-poll: ingests the request's results (completing matching waiters, discarding
    /// tombstoned ones) and events (server-stamped, ring-buffered, fanned out), refreshes liveness,
    /// then holds up to <c>ServerHoldMs</c> for queued commands and returns them. Commands returned
    /// here are marked delivered so a subsequent no-result timeout reports
    /// <see cref="FinErrorCode.DeliveredNoResult"/>. Throws
    /// <see cref="ProtocolVersionMismatchException"/> on unsupported skew.
    /// </summary>
    /// <param name="poll">The agent's poll body (results + events + dropped count).</param>
    /// <param name="cancellationToken">Cancels the hold (e.g. the agent disconnected or the host is stopping).</param>
    Task<PollResponse> PollAsync(PollRequest poll, CancellationToken cancellationToken = default);
}
