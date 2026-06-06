using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getVehicles</c> array element. <c>ForwardSpeed</c> is already km/h. Inventories and
/// GeoJSON features are ignored. The fuel/autopilot/path booleans drive anomaly derivation.
/// </summary>
public sealed record RawVehicle
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }

    [JsonPropertyName("PathName")]
    public string? PathName { get; init; }

    [JsonPropertyName("Status")]
    public string? Status { get; init; }

    [JsonPropertyName("Driver")]
    public string? Driver { get; init; }

    [JsonPropertyName("ForwardSpeed")]
    public double ForwardSpeed { get; init; }

    [JsonPropertyName("ThrottlePercent")]
    public double ThrottlePercent { get; init; }

    [JsonPropertyName("Autopilot")]
    public bool Autopilot { get; init; }

    [JsonPropertyName("FollowingPath")]
    public bool FollowingPath { get; init; }

    [JsonPropertyName("HasFuel")]
    public bool HasFuel { get; init; }

    [JsonPropertyName("HasFuelForRoundtrip")]
    public bool HasFuelForRoundtrip { get; init; }

    [JsonPropertyName("TotalFuelEnergy")]
    public double TotalFuelEnergy { get; init; }

    [JsonPropertyName("MaxFuelEnergy")]
    public double MaxFuelEnergy { get; init; }
}
