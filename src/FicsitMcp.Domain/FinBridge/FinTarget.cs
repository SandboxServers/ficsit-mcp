using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.FinBridge;

/// <summary>
/// How a command names the component(s) it acts on, mirroring the <c>target</c> oneOf in
/// <c>common.schema.json</c>: exactly one addressing mode is set. Use the factory helpers so the
/// "exactly one" invariant holds by construction.
/// </summary>
/// <remarks>
/// A <see cref="ByNick"/> is player-assigned and not unique; a single-target write against an
/// ambiguous nick yields <see cref="FinErrorCode.AmbiguousTarget"/>, never a silent first-match.
/// <see cref="ByClass"/> is for reads and broadcasts that intentionally fan out over a type.
/// </remarks>
public sealed record FinTarget
{
    /// <summary>Player-assigned nick/group string, resolved agent-side via findComponent.</summary>
    [JsonPropertyName("byNick")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ByNick { get; init; }

    /// <summary>Stable FIN component UUID, resolved via component.proxy. Preferred once discovered.</summary>
    [JsonPropertyName("byId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ById { get; init; }

    /// <summary>FIN class query (e.g. Build_SmelterMk1_C) for reads and broadcasts.</summary>
    [JsonPropertyName("byClass")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ByClass { get; init; }

    /// <summary>Addresses a single component by its player-assigned nick.</summary>
    public static FinTarget Nick(string nick) => new() { ByNick = nick };

    /// <summary>Addresses a single component by its stable UUID.</summary>
    public static FinTarget Id(string id) => new() { ById = id };

    /// <summary>Addresses all components of a class (reads/broadcasts).</summary>
    public static FinTarget Class(string @class) => new() { ByClass = @class };
}
