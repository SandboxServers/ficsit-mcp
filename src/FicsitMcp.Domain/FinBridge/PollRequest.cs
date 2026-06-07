using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The only body the agent POSTs to <c>/fin/v1/poll</c> (agent → server), mirroring
/// <c>poll-request.schema.json</c>. Wraps results of commands executed since the last poll and any
/// buffered events, plus version, identity, and the running dropped-event count. Its arrival
/// refreshes the agent's liveness.
/// </summary>
public sealed record PollRequest
{
    /// <summary>Integer wire-contract version, gated by the server on every poll.</summary>
    [JsonPropertyName("protocolVersion")]
    public required int ProtocolVersion { get; init; }

    /// <summary>The agent reporting in; scopes queue/liveness/events server-side.</summary>
    [JsonPropertyName("agentId")]
    public required string AgentId { get; init; }

    /// <summary>Semver of the in-world Lua agent script. Advisory.</summary>
    [JsonPropertyName("agentScriptVersion")]
    public required string AgentScriptVersion { get; init; }

    /// <summary>Result envelopes for commands executed since the last poll. Possibly empty.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<FinResult> Results { get; init; } = [];

    /// <summary>Buffered event envelopes drained from the agent's bounded ring. Possibly empty.</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<FinEvent> Events { get; init; } = [];

    /// <summary>
    /// Running count of events the agent's drop-oldest ring has discarded since boot. Tells the
    /// server telemetry gaps exist even when the events array looks healthy.
    /// </summary>
    [JsonPropertyName("droppedEvents")]
    public required long DroppedEvents { get; init; }
}
