namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A wheeled vehicle (truck, tractor, explorer, factory cart) from FRM <c>/getVehicles</c>,
/// normalized with derived <see cref="Anomalies"/> — the richest anomaly source, since FRM reports
/// fuel, autopilot, and path-following booleans directly.
/// </summary>
/// <param name="Name">Vehicle name.</param>
/// <param name="ClassName">Native vehicle class.</param>
/// <param name="Location">Compact world position.</param>
/// <param name="PathName">Assigned automation path name; empty when none.</param>
/// <param name="Status">FRM status/form string (verbatim).</param>
/// <param name="Driver">Driving player's name, or empty when nobody is driving.</param>
/// <param name="SpeedKmh">Current forward speed in km/h.</param>
/// <param name="ThrottlePercent">Throttle input.</param>
/// <param name="Autopilot">Whether the vehicle is self-driving.</param>
/// <param name="FollowingPath">Whether it is currently following its path.</param>
/// <param name="HasFuel">Whether it has any fuel.</param>
/// <param name="HasFuelForRoundtrip">Whether it has enough fuel for a full round trip.</param>
/// <param name="FuelEnergy">Current stored fuel energy.</param>
/// <param name="MaxFuelEnergy">Maximum fuel energy capacity.</param>
/// <param name="Anomalies">Derived problem flags.</param>
public sealed record FrmVehicle(
    string Name,
    string ClassName,
    FrmLocation Location,
    string PathName,
    string Status,
    string Driver,
    double SpeedKmh,
    double ThrottlePercent,
    bool Autopilot,
    bool FollowingPath,
    bool HasFuel,
    bool HasFuelForRoundtrip,
    double FuelEnergy,
    double MaxFuelEnergy,
    FrmMobileAnomaly Anomalies);
