using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.GameData.Model;

/// <summary>
/// Axis-aligned clearance box for a building, in centimetres (UE units), parsed from
/// the primary <c>ClearanceBox</c> in Docs.json <c>mClearanceData</c>.
/// </summary>
/// <param name="MinX">Minimum X corner.</param>
/// <param name="MinY">Minimum Y corner.</param>
/// <param name="MinZ">Minimum Z corner.</param>
/// <param name="MaxX">Maximum X corner.</param>
/// <param name="MaxY">Maximum Y corner.</param>
/// <param name="MaxZ">Maximum Z corner.</param>
public sealed record ClearanceBox(
    [property: JsonPropertyName("nx")] double MinX,
    [property: JsonPropertyName("ny")] double MinY,
    [property: JsonPropertyName("nz")] double MinZ,
    [property: JsonPropertyName("xx")] double MaxX,
    [property: JsonPropertyName("xy")] double MaxY,
    [property: JsonPropertyName("xz")] double MaxZ)
{
    /// <summary>Footprint width along X (cm). Derived; not serialized.</summary>
    [JsonIgnore]
    public double Width => MaxX - MinX;

    /// <summary>Footprint depth along Y (cm). Derived; not serialized.</summary>
    [JsonIgnore]
    public double Depth => MaxY - MinY;

    /// <summary>Footprint height along Z (cm). Derived; not serialized.</summary>
    [JsonIgnore]
    public double Height => MaxZ - MinZ;
}
