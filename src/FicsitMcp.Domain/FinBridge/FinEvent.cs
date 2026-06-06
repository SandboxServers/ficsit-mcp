using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// One FIN signal forwarded up inside a poll-request (agent → server), mirroring
/// <c>event.schema.json</c>. Observational, not a mutation: under buffer pressure the oldest events
/// are dropped and a running count reported. Tool-layer throttling/burst-collapse (issue #21) sits
/// above this raw envelope.
/// </summary>
public sealed record FinEvent
{
    /// <summary>
    /// Monotonically increasing per-agent sequence number. With the poll-request dropped count it
    /// lets the server detect gaps and order events without trusting the agent clock.
    /// </summary>
    [JsonPropertyName("seq")]
    public required long Seq { get; init; }

    /// <summary>FIN signal name (e.g. ItemTransfer) — the first element of event.pull's tuple.</summary>
    [JsonPropertyName("signal")]
    public required string Signal { get; init; }

    /// <summary>The component that emitted the signal.</summary>
    [JsonPropertyName("source")]
    public required ComponentRef Source { get; init; }

    /// <summary>Signal-specific payload (the trailing event.pull args). May be empty.</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; init; }

    /// <summary>
    /// The agent's computer.millis()-style clock at emission. Advisory telemetry only; the server
    /// stamps authoritative <see cref="ReceivedAt"/> on ingest and orders by seq + receivedAt.
    /// </summary>
    [JsonPropertyName("agentTimestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? AgentTimestamp { get; init; }

    /// <summary>
    /// Server-stamped receipt time, authoritative for ordering. Not part of the wire envelope the
    /// agent sends; the server adds it on ingest (ADR-001 Decision 6) and it is carried to
    /// subscribers. Null until the server has stamped it.
    /// </summary>
    [JsonPropertyName("receivedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ReceivedAt { get; init; }

    /// <summary>The agent that reported this event. Server-side context, not part of the agent's envelope.</summary>
    [JsonPropertyName("agentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentId { get; init; }
}
