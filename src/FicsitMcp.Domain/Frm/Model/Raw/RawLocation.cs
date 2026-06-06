using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// The raw FRM <c>location</c> object exactly as the mod emits it (lowercase keys). Verbose by
/// design — see <see cref="FrmLocation"/> for the compact form the model actually consumes.
/// </summary>
/// <remarks>
/// Deserialization is unknown-field-tolerant: System.Text.Json ignores JSON members with no
/// matching property, so a mod update that adds a sibling key here cannot break parsing.
/// </remarks>
public sealed record RawLocation
{
    /// <summary>World X in centimetres.</summary>
    [JsonPropertyName("x")]
    public double X { get; init; }

    /// <summary>World Y in centimetres.</summary>
    [JsonPropertyName("y")]
    public double Y { get; init; }

    /// <summary>World Z in centimetres.</summary>
    [JsonPropertyName("z")]
    public double Z { get; init; }

    /// <summary>Heading in degrees (0&#8211;360, 0 = due north). Absent on some location objects.</summary>
    [JsonPropertyName("rotation")]
    public double Rotation { get; init; }
}
