using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The agent's answer to one command, sent up inside a poll-request (agent → server), mirroring
/// <c>result.schema.json</c>. Correlated only by <see cref="Id"/> (it rides a different HTTP request
/// than the command delivery). A result whose id the server has already tombstoned is discarded,
/// never re-applied.
/// </summary>
/// <remarks>
/// The schema (<c>result.schema.json</c> <c>allOf</c>) enforces <c>ok ⇒ payload present, error
/// absent</c> and <c>!ok ⇒ error present, payload absent</c>. <see cref="FinBridge"/> validates this
/// invariant on ingest (<c>IngestResults</c>): a result that violates it is logged and dropped — never
/// used to complete a waiter — so a malformed body cannot masquerade as a definitive answer and the
/// command's deadline still fires with the correct at-most-once outcome. This DTO keeps both nullable
/// so such a body can be inspected and rejected rather than failing to deserialize opaquely.
/// </remarks>
public sealed record FinResult
{
    /// <summary>Echoes the id of the command this result answers. The only correlation.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>True for success (payload present, error absent); false for failure (error present).</summary>
    [JsonPropertyName("ok")]
    public required bool Ok { get; init; }

    /// <summary>
    /// Operation-specific success data. Write operations include both before and after state so the
    /// effect of a mutation is visible. Shape per-operation is owned by the tool layer (issue #20).
    /// </summary>
    [JsonPropertyName("payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Payload { get; init; }

    /// <summary>The failure detail when <see cref="Ok"/> is false.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FinError? Error { get; init; }
}
