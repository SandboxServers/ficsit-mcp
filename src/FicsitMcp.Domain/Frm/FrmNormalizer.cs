using System.Collections.Immutable;

using FicsitMcp.Domain.Frm.Model;
using FicsitMcp.Domain.Frm.Model.Raw;

namespace FicsitMcp.Domain.Frm;

/// <summary>
/// Maps verbose raw FRM DTOs to the compact normalized records the model consumes, and derives the
/// mobile-entity anomaly flags. Pure (no I/O), so it is unit-testable in isolation from transport.
/// </summary>
/// <remarks>
/// Two jobs: (1) trim precision the model cannot use — world coordinates are rounded to whole
/// centimetres and rotation to one decimal; verbose sub-objects (bounding boxes, GeoJSON features,
/// raw inventories) are dropped upstream by not being declared on the raw DTOs. (2) Derive
/// <see cref="FrmMobileAnomaly"/> flags from FRM's status fields so a summary call alone reveals
/// trouble. The "healthy" sentinel for FRM's diagnostic enums is the string <c>"NoError"</c>.
/// </remarks>
public static class FrmNormalizer
{
    // FRM's diagnostic enums (ESelfDrivingLocomotiveError, EPathDiagnosticsError) serialize their
    // healthy state as this name. Anything else is a real fault.
    private const string NoError = "NoError";

    // A self-driving entity reporting a speed at or below this (km/h) is treated as not moving, for
    // the stuck/idle heuristic. Small non-zero floor absorbs FRM's float jitter around a standstill.
    private const double StationarySpeedKmh = 0.1;

    /// <summary>Rounds a verbose FRM <c>location</c> to a compact <see cref="FrmLocation"/>.</summary>
    public static FrmLocation ToLocation(RawLocation? raw)
    {
        if (raw is null)
        {
            return new FrmLocation(0, 0, 0, 0);
        }

        return new FrmLocation(
            (long)Math.Round(raw.X),
            (long)Math.Round(raw.Y),
            (long)Math.Round(raw.Z),
            Math.Round(raw.Rotation, 1));
    }

    /// <summary>Normalizes a <c>/getProdStats</c> row.</summary>
    public static FrmProdStatsItem ToProdStatsItem(RawProdStatsItem raw) => new(
        ClassName: raw.ClassName ?? string.Empty,
        DisplayName: raw.Name ?? string.Empty,
        CurrentProduced: raw.CurrentProd,
        MaxProduced: raw.MaxProd,
        CurrentConsumed: raw.CurrentConsumed,
        MaxConsumed: raw.MaxConsumed,
        ProducedPercent: raw.ProdPercent,
        ConsumedPercent: raw.ConsPercent,
        Form: raw.Type ?? string.Empty);

    /// <summary>Normalizes a <c>/getPower</c> circuit row.</summary>
    public static FrmPowerCircuit ToPowerCircuit(RawPowerCircuit raw) => new(
        CircuitGroupId: raw.CircuitGroupId,
        ProductionMw: raw.PowerProduction,
        ConsumedMw: raw.PowerConsumed,
        CapacityMw: raw.PowerCapacity,
        MaxConsumedMw: raw.PowerMaxConsumed,
        BatteryPercent: raw.BatteryPercent,
        BatteryCapacityMwh: raw.BatteryCapacity,
        BatteryDifferentialMw: raw.BatteryDifferential,
        FuseTriggered: raw.FuseTriggered,
        AssociatedCircuitIds: raw.AssociatedCircuits.IsDefault ? [] : raw.AssociatedCircuits);

    /// <summary>Normalizes a <c>/getFactory</c> building.</summary>
    public static FrmFactoryBuilding ToFactoryBuilding(RawFactoryBuilding raw) => new(
        Id: raw.Id ?? string.Empty,
        Name: raw.Name ?? string.Empty,
        ClassName: raw.ClassName ?? string.Empty,
        Location: ToLocation(raw.Location),
        Recipe: raw.Recipe ?? string.Empty,
        RecipeClassName: raw.RecipeClassName ?? string.Empty,
        Productivity: raw.Productivity,
        ClockPercent: raw.ManuSpeed,
        Somersloops: raw.Somersloops,
        PowerShards: raw.PowerShards,
        IsConfigured: raw.IsConfigured,
        IsProducing: raw.IsProducing,
        IsPaused: raw.IsPaused,
        PowerConsumedMw: raw.PowerInfo?.PowerConsumed ?? 0,
        FuseTriggered: raw.PowerInfo?.FuseTriggered ?? false,
        Production: MapProduction(raw.Production),
        Ingredients: MapIngredients(raw.Ingredients));

    /// <summary>Normalizes a <c>/getTrains</c> train and derives its anomaly flags.</summary>
    public static FrmTrain ToTrain(RawTrain raw)
    {
        FrmMobileAnomaly anomalies = FrmMobileAnomaly.None;

        if (raw.Derailed || raw.PendingDerail)
        {
            anomalies |= FrmMobileAnomaly.Derailed;
        }

        // Self-driving error reported by the locomotive (e.g. blocked, no power). "NoError" / empty
        // are healthy; an empty SelfDriving means the train is manually driven, also not an anomaly.
        if (HasError(raw.SelfDriving))
        {
            anomalies |= FrmMobileAnomaly.SelfDrivingError;
        }

        // Path diagnostics non-clear => the auto-train cannot route to its next stop.
        if (HasError(raw.Path))
        {
            anomalies |= FrmMobileAnomaly.NoPath;
        }

        // Stuck: the train believes it can self-drive (no self-driving error) yet is not moving and
        // not in a docking sequence. A train legitimately parked at a station mid-timetable IS
        // docking, so Docking != "None"/empty suppresses the false positive.
        bool selfDriveHealthy = !HasError(raw.SelfDriving) && !string.IsNullOrEmpty(raw.SelfDriving);
        if (selfDriveHealthy
            && !raw.Derailed
            && Math.Abs(raw.ForwardSpeed) <= StationarySpeedKmh
            && IsIdleDocking(raw.Docking))
        {
            anomalies |= FrmMobileAnomaly.Stuck;
        }

        return new FrmTrain(
            Name: raw.Name ?? string.Empty,
            ClassName: raw.ClassName ?? string.Empty,
            Location: ToLocation(raw.Location),
            SpeedKmh: raw.ForwardSpeed,
            ThrottlePercent: raw.ThrottlePercent,
            Station: raw.TrainStation ?? string.Empty,
            TimetableIndex: raw.TimeTableIndex,
            Derailed: raw.Derailed,
            PendingDerail: raw.PendingDerail,
            Status: raw.Status ?? string.Empty,
            SelfDriving: raw.SelfDriving ?? string.Empty,
            Docking: raw.Docking ?? string.Empty,
            Path: raw.Path ?? string.Empty,
            Anomalies: anomalies);
    }

    /// <summary>Normalizes a <c>/getDrone</c> drone and derives its anomaly flags.</summary>
    public static FrmDrone ToDrone(RawDrone raw)
    {
        // Drones have no fuel/path diagnostics in FRM; the only derivable problem is having no
        // paired station, which leaves the drone with no route to fly.
        FrmMobileAnomaly anomalies = raw.HasPairedStation
            ? FrmMobileAnomaly.None
            : FrmMobileAnomaly.NoStation;

        return new FrmDrone(
            Name: raw.Name ?? string.Empty,
            ClassName: raw.ClassName ?? string.Empty,
            Location: ToLocation(raw.Location),
            HomeStation: raw.HomeStation ?? string.Empty,
            PairedStation: raw.PairedStation ?? string.Empty,
            HasPairedStation: raw.HasPairedStation,
            CurrentDestination: raw.CurrentDestination ?? string.Empty,
            SpeedKmh: raw.FlyingSpeed,
            MaxSpeedKmh: raw.MaxSpeed,
            FlyingMode: raw.CurrentFlyingMode ?? string.Empty,
            Anomalies: anomalies);
    }

    /// <summary>Normalizes a <c>/getVehicles</c> vehicle and derives its anomaly flags.</summary>
    public static FrmVehicle ToVehicle(RawVehicle raw)
    {
        FrmMobileAnomaly anomalies = FrmMobileAnomaly.None;

        // Fuel: out of fuel is the hard failure; having fuel but not enough for a round trip is the
        // softer "will strand" warning. Only meaningful for fuelled, self-driving vehicles.
        if (!raw.HasFuel)
        {
            anomalies |= FrmMobileAnomaly.NoFuel;
        }
        else if (raw.Autopilot && !raw.HasFuelForRoundtrip)
        {
            anomalies |= FrmMobileAnomaly.LowFuel;
        }

        // Path: autopilot on but not following the path => it cannot route.
        if (raw.Autopilot && !raw.FollowingPath)
        {
            anomalies |= FrmMobileAnomaly.NoPath;
        }

        // Stuck: autopilot on, following a path, has fuel, yet not moving. A vehicle a player is
        // manually driving (Driver != "") and idling is NOT stuck — only autopilot idling counts,
        // which the Autopilot gate already enforces.
        if (raw.Autopilot
            && raw.FollowingPath
            && raw.HasFuel
            && Math.Abs(raw.ForwardSpeed) <= StationarySpeedKmh)
        {
            anomalies |= FrmMobileAnomaly.Stuck;
        }

        return new FrmVehicle(
            Name: raw.Name ?? string.Empty,
            ClassName: raw.ClassName ?? string.Empty,
            Location: ToLocation(raw.Location),
            PathName: raw.PathName ?? string.Empty,
            Status: raw.Status ?? string.Empty,
            Driver: raw.Driver ?? string.Empty,
            SpeedKmh: raw.ForwardSpeed,
            ThrottlePercent: raw.ThrottlePercent,
            Autopilot: raw.Autopilot,
            FollowingPath: raw.FollowingPath,
            HasFuel: raw.HasFuel,
            HasFuelForRoundtrip: raw.HasFuelForRoundtrip,
            FuelEnergy: raw.TotalFuelEnergy,
            MaxFuelEnergy: raw.MaxFuelEnergy,
            Anomalies: anomalies);
    }

    /// <summary>Normalizes a <c>/getPlayer</c> player.</summary>
    public static FrmPlayer ToPlayer(RawPlayer raw) => new(
        Name: raw.Name ?? string.Empty,
        ClassName: raw.ClassName ?? string.Empty,
        Location: ToLocation(raw.Location),
        SpeedKmh: raw.Speed,
        Online: raw.Online,
        Health: raw.PlayerHp,
        Dead: raw.Dead);

    /// <summary>Normalizes a <c>/getResourceNode</c> node.</summary>
    public static FrmResourceNode ToResourceNode(RawResourceNode raw) => new(
        Name: raw.Name ?? string.Empty,
        ClassName: raw.ClassName ?? string.Empty,
        Purity: raw.Purity ?? string.Empty,
        ResourceForm: raw.ResourceForm ?? string.Empty,
        NodeType: raw.NodeType ?? string.Empty,
        Exploited: raw.Exploited,
        Location: ToLocation(raw.Location));

    private static ImmutableArray<FrmItemRate> MapProduction(ImmutableArray<RawProductionLine> lines)
    {
        if (lines.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<FrmItemRate>(lines.Length);
        foreach (RawProductionLine line in lines)
        {
            builder.Add(new FrmItemRate(
                line.ClassName ?? string.Empty,
                line.Name ?? string.Empty,
                line.CurrentProd,
                line.MaxProd,
                line.ProdPercent));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<FrmItemRate> MapIngredients(ImmutableArray<RawIngredientLine> lines)
    {
        if (lines.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<FrmItemRate>(lines.Length);
        foreach (RawIngredientLine line in lines)
        {
            builder.Add(new FrmItemRate(
                line.ClassName ?? string.Empty,
                line.Name ?? string.Empty,
                line.CurrentConsumed,
                line.MaxConsumed,
                line.ConsPercent));
        }

        return builder.ToImmutable();
    }

    // A diagnostic-enum string indicates a fault when it is present AND not the healthy "NoError"
    // sentinel. An absent/empty value means "not applicable" (e.g. manual driving), not a fault.
    private static bool HasError(string? diagnostic) =>
        !string.IsNullOrEmpty(diagnostic)
        && !string.Equals(diagnostic, NoError, StringComparison.OrdinalIgnoreCase);

    // The train is NOT mid-docking when its docking state is absent or the explicit idle/none state.
    // FRM serializes ETrainDockingState; "None" is the not-docking value. Treat empty as not-docking.
    private static bool IsIdleDocking(string? docking) =>
        string.IsNullOrEmpty(docking)
        || docking.EndsWith("None", StringComparison.OrdinalIgnoreCase);
}
