namespace FicsitMcp.Domain.Frm.Model;

/// <summary>
/// A train from FRM <c>/getTrains</c>, normalized with derived <see cref="Anomalies"/> so a summary
/// call alone reveals a derailed, pathless, or stuck train without inspecting the raw timetable.
/// </summary>
/// <param name="Name">Train name.</param>
/// <param name="ClassName">Native train class.</param>
/// <param name="Location">Compact world position, or <c>null</c> when FRM reported no location.</param>
/// <param name="SpeedKmh">Current forward speed in km/h (FRM already converts from cm/s).</param>
/// <param name="ThrottlePercent">Throttle input, percentage.</param>
/// <param name="Station">
/// Current or target station name FRM reports (<c>TrainStation</c>); empty when between stops.
/// </param>
/// <param name="TimetableIndex">Index of the current timetable stop.</param>
/// <param name="Derailed">Raw derail flag from FRM.</param>
/// <param name="PendingDerail">Raw pending-collision flag from FRM.</param>
/// <param name="Status">FRM status/form string (verbatim, for context).</param>
/// <param name="SelfDriving">FRM <c>ESelfDrivingLocomotiveError</c> name (<c>"NoError"</c> when healthy).</param>
/// <param name="Docking">FRM <c>ETrainDockingState</c> name.</param>
/// <param name="Path">FRM <c>EPathDiagnosticsError</c> name (<c>"NoError"</c>/empty when a path exists).</param>
/// <param name="Anomalies">Derived problem flags (see <see cref="FrmMobileAnomaly"/>).</param>
public sealed record FrmTrain(
    string Name,
    string ClassName,
    FrmLocation? Location,
    double SpeedKmh,
    double ThrottlePercent,
    string Station,
    int TimetableIndex,
    bool Derailed,
    bool PendingDerail,
    string Status,
    string SelfDriving,
    string Docking,
    string Path,
    FrmMobileAnomaly Anomalies);
