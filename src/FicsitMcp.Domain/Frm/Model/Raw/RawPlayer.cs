using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getPlayer</c> array element. <c>Speed</c> is already km/h. Inventory and GeoJSON
/// features are ignored.
/// </summary>
public sealed record RawPlayer
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }

    [JsonPropertyName("Speed")]
    public double Speed { get; init; }

    [JsonPropertyName("Online")]
    public bool Online { get; init; }

    [JsonPropertyName("PlayerHP")]
    public double PlayerHp { get; init; }

    [JsonPropertyName("Dead")]
    public bool Dead { get; init; }
}
