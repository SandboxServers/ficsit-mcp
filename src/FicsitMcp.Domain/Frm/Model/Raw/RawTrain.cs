using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getTrains</c> array element. The timetable, railcar list, GeoJSON features, and
/// PowerInfo are not declared (and thus ignored): the normalizer needs only status/motion fields
/// to derive anomalies. <c>ForwardSpeed</c> is already km/h (FRM applies the cm/s conversion).
/// </summary>
public sealed record RawTrain
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ClassName")]
    public string? ClassName { get; init; }

    [JsonPropertyName("location")]
    public RawLocation? Location { get; init; }

    [JsonPropertyName("ForwardSpeed")]
    public double ForwardSpeed { get; init; }

    [JsonPropertyName("ThrottlePercent")]
    public double ThrottlePercent { get; init; }

    [JsonPropertyName("TrainStation")]
    public string? TrainStation { get; init; }

    [JsonPropertyName("TimeTableIndex")]
    public int TimeTableIndex { get; init; }

    [JsonPropertyName("Derailed")]
    public bool Derailed { get; init; }

    [JsonPropertyName("PendingDerail")]
    public bool PendingDerail { get; init; }

    [JsonPropertyName("Status")]
    public string? Status { get; init; }

    [JsonPropertyName("SelfDriving")]
    public string? SelfDriving { get; init; }

    [JsonPropertyName("Docking")]
    public string? Docking { get; init; }

    [JsonPropertyName("Path")]
    public string? Path { get; init; }
}
