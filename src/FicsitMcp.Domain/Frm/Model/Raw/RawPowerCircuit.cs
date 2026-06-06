using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Frm.Model.Raw;

/// <summary>
/// Raw FRM <c>/getPower</c> array element. The top-level power rows key on <c>CircuitGroupID</c>
/// (the per-building <c>PowerInfo</c> object carries both <c>CircuitGroupID</c> and
/// <c>CircuitID</c>; this endpoint does not emit a bare <c>CircuitID</c>). Battery-time fields are
/// display strings and are not consumed.
/// </summary>
public sealed record RawPowerCircuit
{
    [JsonPropertyName("CircuitGroupID")]
    public int CircuitGroupId { get; init; }

    [JsonPropertyName("PowerProduction")]
    public double PowerProduction { get; init; }

    [JsonPropertyName("PowerConsumed")]
    public double PowerConsumed { get; init; }

    [JsonPropertyName("PowerCapacity")]
    public double PowerCapacity { get; init; }

    [JsonPropertyName("PowerMaxConsumed")]
    public double PowerMaxConsumed { get; init; }

    [JsonPropertyName("BatteryPercent")]
    public double BatteryPercent { get; init; }

    [JsonPropertyName("BatteryCapacity")]
    public double BatteryCapacity { get; init; }

    [JsonPropertyName("BatteryDifferential")]
    public double BatteryDifferential { get; init; }

    [JsonPropertyName("FuseTriggered")]
    public bool FuseTriggered { get; init; }

    [JsonPropertyName("AssociatedCircuits")]
    public ImmutableArray<int> AssociatedCircuits { get; init; } = [];
}
