using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getResourceNode</c> array element. The raw <c>EnumPurity</c> and GeoJSON features
/// are ignored; the human <c>Purity</c> string is kept.
/// </summary>
public sealed record RawResourceNode
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("Purity")]
    public string? Purity { get; init; }

    [JsonPropertyName("ResourceForm")]
    public string? ResourceForm { get; init; }

    [JsonPropertyName("NodeType")]
    public string? NodeType { get; init; }

    [JsonPropertyName("Exploited")]
    public bool Exploited { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }
}
