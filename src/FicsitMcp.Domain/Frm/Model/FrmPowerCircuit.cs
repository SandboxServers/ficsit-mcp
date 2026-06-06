using System.Collections.Immutable;

namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// One power circuit's live state from FRM <c>/getPower</c>: production vs consumption,
/// battery buffer, and whether the fuse has blown. This is the circuit-level rollup the model
/// needs to spot a browning-out or fully-tripped grid without enumerating every machine.
/// </summary>
/// <param name="CircuitGroupId">
/// FRM circuit group id (the merged-network id; FRM keys <c>/getPower</c> rows on the GROUP id,
/// not the per-building <c>CircuitID</c>).
/// </param>
/// <param name="ProductionMw">Current production into the circuit, MW.</param>
/// <param name="ConsumedMw">Current draw from the circuit, MW.</param>
/// <param name="CapacityMw">Maximum production capacity available, MW.</param>
/// <param name="MaxConsumedMw">Maximum potential consumption if everything ran, MW.</param>
/// <param name="BatteryPercent">Battery charge across the circuit, 0&#8211;100.</param>
/// <param name="BatteryCapacityMwh">Total battery storage capacity, MWh.</param>
/// <param name="BatteryDifferentialMw">Net battery flow (input − output), MW; negative = discharging.</param>
/// <param name="FuseTriggered">
/// True when the circuit's fuse has blown (consumption exceeded capacity) — the single most
/// important power anomaly: everything on this circuit is unpowered.
/// </param>
/// <param name="AssociatedCircuitIds">The individual circuit ids merged into this group.</param>
public sealed record FrmPowerCircuit(
    int CircuitGroupId,
    double ProductionMw,
    double ConsumedMw,
    double CapacityMw,
    double MaxConsumedMw,
    double BatteryPercent,
    double BatteryCapacityMwh,
    double BatteryDifferentialMw,
    bool FuseTriggered,
    ImmutableArray<int> AssociatedCircuitIds);
