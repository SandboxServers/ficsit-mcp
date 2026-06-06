using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>PowerInfo</c> sub-object (getPowerConsumptionJSON), embedded on factory buildings,
/// generators, trains, etc. Only the fields the normalizer needs (draw + fuse) are declared.
/// </summary>
public sealed record RawPowerInfo
{
    [JsonPropertyName("PowerConsumed")]
    public double PowerConsumed { get; init; }

    [JsonPropertyName("FuseTriggered")]
    public bool FuseTriggered { get; init; }
}
