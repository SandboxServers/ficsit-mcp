namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// A point-in-time view of one agent's liveness, derived from the last hello/poll the server saw.
/// </summary>
/// <param name="AgentId">The agent this snapshot describes.</param>
/// <param name="IsAlive">
/// True when the last contact was within the liveness window. A command enqueued against a non-alive
/// agent fails fast with <see cref="FinErrorCode.AgentOffline"/> rather than hanging to its deadline.
/// </param>
/// <param name="LastSeen">When the server last received a hello or poll from this agent.</param>
/// <param name="AgentScriptVersion">The script semver the agent last reported. Advisory.</param>
/// <param name="DroppedEvents">
/// Count of events the server's per-agent drop-oldest ring has evicted for this agent under buffer
/// pressure. Lets a stats/observability surface (issue #21) report telemetry loss without trusting
/// the agent's own self-reported count.
/// </param>
public sealed record AgentLiveness(
    string AgentId,
    bool IsAlive,
    DateTimeOffset LastSeen,
    string? AgentScriptVersion,
    long DroppedEvents);
