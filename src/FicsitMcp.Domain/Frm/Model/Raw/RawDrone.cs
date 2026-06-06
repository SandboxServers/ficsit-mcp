using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getDrone</c> array element. Speeds are already km/h. GeoJSON features are ignored.
/// </summary>
public sealed record RawDrone
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }

    [JsonPropertyName("HomeStation")]
    public string? HomeStation { get; init; }

    [JsonPropertyName("PairedStation")]
    public string? PairedStation { get; init; }

    [JsonPropertyName("HasPairedStation")]
    public bool HasPairedStation { get; init; }

    [JsonPropertyName("CurrentDestination")]
    public string? CurrentDestination { get; init; }

    [JsonPropertyName("FlyingSpeed")]
    public double FlyingSpeed { get; init; }

    [JsonPropertyName("MaxSpeed")]
    public double MaxSpeed { get; init; }

    [JsonPropertyName("CurrentFlyingMode")]
    public string? CurrentFlyingMode { get; init; }
}
