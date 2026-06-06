using System.Collections.Immutable;

using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Frm.Model;

namespace FicsitMcp.Tests.Frm;

/// <summary>
/// Verifies the derived <see cref="FrmMobileAnomaly"/> flags for trains, drones, and vehicles, and
/// the false positives deliberately tuned out (a manually-driven idle vehicle is not "stuck").
/// </summary>
public sealed class FrmAnomalyTests
{
    [Fact]
    public async Task GetTrains_HealthySelfDriver_HasNoAnomalies()
    {
        ImmutableArray<FrmTrain> trains =
            await FrmFixtures.ClientServing("getTrains.json").GetTrainsAsync(CancellationToken.None);

        FrmTrain healthy = trains.Single(t => t.Name == "Iron Express");
        Assert.Equal(FrmMobileAnomaly.None, healthy.Anomalies);
    }

    [Fact]
    public async Task GetTrains_DerailedTrain_FlagsDerailedAndNoPath()
    {
        ImmutableArray<FrmTrain> trains =
            await FrmFixtures.ClientServing("getTrains.json").GetTrainsAsync(CancellationToken.None);

        FrmTrain derailed = trains.Single(t => t.Name == "Derailed Hauler");
        Assert.True(derailed.Derailed);
        Assert.Equal(
            FrmMobileAnomaly.Derailed | FrmMobileAnomaly.NoPath,
            derailed.Anomalies);
    }

    [Fact]
    public async Task GetTrains_StationarySelfDriverNotDocking_FlagsStuck()
    {
        ImmutableArray<FrmTrain> trains =
            await FrmFixtures.ClientServing("getTrains.json").GetTrainsAsync(CancellationToken.None);

        FrmTrain stuck = trains.Single(t => t.Name == "Stuck Shuttle");
        Assert.Equal(FrmMobileAnomaly.Stuck, stuck.Anomalies);
    }

    [Fact]
    public async Task GetTrains_DerailedTrain_SetsBothDerailedAndNoPathFlags()
    {
        // Combination anomaly: a derailed train that also has no path must carry BOTH flags, so a
        // summary call sees the full picture rather than the first problem only.
        ImmutableArray<FrmTrain> trains =
            await FrmFixtures.ClientServing("getTrains.json").GetTrainsAsync(CancellationToken.None);

        FrmTrain train = trains.Single(t => t.Name == "Derailed Hauler");
        Assert.True(train.Anomalies.HasFlag(FrmMobileAnomaly.Derailed));
        Assert.True(train.Anomalies.HasFlag(FrmMobileAnomaly.NoPath));
    }

    [Fact]
    public async Task GetTrains_StationarySelfDriverWithNoneSuffixDockingState_IsNotStuck()
    {
        // The docking state "ETrainDockingState::TDS_RampNone" merely ENDS with "None" — it is not the
        // not-docking sentinel "TDS_None". Exact-match docking logic must treat it as mid-dock, so this
        // stationary self-driver must NOT be flagged Stuck (regression guard for the EndsWith bug).
        ImmutableArray<FrmTrain> trains =
            await FrmFixtures.ClientServing("getTrains.json").GetTrainsAsync(CancellationToken.None);

        FrmTrain train = trains.Single(t => t.Name == "Docking None-Suffix Train");
        Assert.Equal(FrmMobileAnomaly.None, train.Anomalies);
    }

    [Fact]
    public async Task GetDrones_UnpairedDrone_FlagsNoStation()
    {
        ImmutableArray<FrmDrone> drones =
            await FrmFixtures.ClientServing("getDrone.json").GetDronesAsync(CancellationToken.None);

        Assert.Equal(FrmMobileAnomaly.None, drones.Single(d => d.Name == "Drone").Anomalies);
        Assert.Equal(FrmMobileAnomaly.NoStation, drones.Single(d => d.Name == "Idle Drone").Anomalies);
    }

    [Fact]
    public async Task GetVehicles_AutopilotWithFuel_HasNoAnomalies()
    {
        ImmutableArray<FrmVehicle> vehicles =
            await FrmFixtures.ClientServing("getVehicles.json").GetVehiclesAsync(CancellationToken.None);

        Assert.Equal(FrmMobileAnomaly.None, vehicles.Single(v => v.Name == "Truck").Anomalies);
    }

    [Fact]
    public async Task GetVehicles_OutOfFuelAutopilot_FlagsNoFuelAndNoPath()
    {
        ImmutableArray<FrmVehicle> vehicles =
            await FrmFixtures.ClientServing("getVehicles.json").GetVehiclesAsync(CancellationToken.None);

        FrmVehicle stranded = vehicles.Single(v => v.Name == "Stranded Truck");
        // No fuel AND autopilot-on-but-not-following-path => both fuel and path anomalies.
        Assert.Equal(
            FrmMobileAnomaly.NoFuel | FrmMobileAnomaly.NoPath,
            stranded.Anomalies);
    }

    [Fact]
    public async Task GetVehicles_ManuallyDrivenIdleVehicle_IsNotStuck()
    {
        ImmutableArray<FrmVehicle> vehicles =
            await FrmFixtures.ClientServing("getVehicles.json").GetVehiclesAsync(CancellationToken.None);

        // Idle, speed 0, but a player is driving (Autopilot=false): must NOT be flagged Stuck.
        FrmVehicle manual = vehicles.Single(v => v.Name == "Manual Explorer");
        Assert.Equal("Pioneer", manual.Driver);
        Assert.Equal(FrmMobileAnomaly.None, manual.Anomalies);
    }
}
