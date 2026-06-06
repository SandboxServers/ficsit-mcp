namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A delivery drone from FRM <c>/getDrone</c>, normalized with derived <see cref="Anomalies"/>.
/// </summary>
/// <remarks>
/// Drones carry no fuel concept in FRM (their stations hold the batteries), so fuel anomalies
/// never apply here; the meaningful derived flag is <see cref="FrmMobileAnomaly.NoStation"/> when
/// no paired station is set, leaving the drone with nowhere to fly.
/// </remarks>
/// <param name="Name">Drone name.</param>
/// <param name="ClassName">Native drone class.</param>
/// <param name="Location">Compact world position, or <c>null</c> when FRM reported no location.</param>
/// <param name="HomeStation">Home drone-port name.</param>
/// <param name="PairedStation">Paired (destination) drone-port name; empty when unpaired.</param>
/// <param name="HasPairedStation">Whether a paired station exists.</param>
/// <param name="CurrentDestination">Current destination FRM reports.</param>
/// <param name="SpeedKmh">Current flying speed in km/h.</param>
/// <param name="MaxSpeedKmh">Maximum speed in km/h.</param>
/// <param name="FlyingMode">FRM flying-mode/form string (verbatim).</param>
/// <param name="Anomalies">Derived problem flags.</param>
public sealed record FrmDrone(
    string Name,
    string ClassName,
    FrmLocation? Location,
    string HomeStation,
    string PairedStation,
    bool HasPairedStation,
    string CurrentDestination,
    double SpeedKmh,
    double MaxSpeedKmh,
    string FlyingMode,
    FrmMobileAnomaly Anomalies);
