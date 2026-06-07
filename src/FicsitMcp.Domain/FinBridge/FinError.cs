using System.Text.Json;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// Uniform error shape across result envelopes and HTTP error bodies, mirroring
/// <c>errorObject</c> in <c>common.schema.json</c>.
/// </summary>
public sealed record FinError
{
    /// <summary>Typed error code so callers can branch programmatically.</summary>
    [JsonPropertyName("code")]
    public required FinErrorCode Code { get; init; }

    /// <summary>
    /// Human-actionable text. For operator-facing failures it names the real-world remedy
    /// (e.g. "Is the FIN computer powered and running the agent script?").
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Free-form structured context, e.g. the candidate list for an ambiguous target or version
    /// bounds for a protocol mismatch.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Details { get; init; }
}
