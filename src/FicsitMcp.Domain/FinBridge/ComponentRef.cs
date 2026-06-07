using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// The resolved form a component takes in discovery results and event sources, mirroring
/// <c>componentRef</c> in <c>common.schema.json</c>. Always carries the authoritative UUID;
/// nick/class/displayName are advisory and may be absent.
/// </summary>
public sealed record ComponentRef
{
    /// <summary>Authoritative FIN component UUID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Player-assigned nick, if any. Advisory and not unique.</summary>
    [JsonPropertyName("nick")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nick { get; init; }

    /// <summary>FIN component class name, if known.</summary>
    [JsonPropertyName("class")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Class { get; init; }

    /// <summary>Human-friendly name from the game, if available.</summary>
    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }
}
