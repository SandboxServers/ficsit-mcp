namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// A typed, documented projection of the dedicated server's option map (wire keys <c>FG.*</c>,
/// stringified values). Every well-known option is a strongly-typed, nullable property; anything the
/// game adds that this layer does not (yet) model lands in <see cref="Passthrough"/>, so a new game
/// version never requires a release of this client to set or read an unmodeled option.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nullability = "unspecified".</b> A null typed property means the option was not present (on a
/// read) or should be left unchanged (on a write). On a write, only non-null properties — plus every
/// <see cref="Passthrough"/> entry — are sent, so callers can patch a single option without restating
/// the rest.
/// </para>
/// <para>
/// This is a domain type with no MCP or HTTP dependencies; <see cref="ServerSettingsMapper"/> converts
/// it to and from the wire string→string map.
/// </para>
/// </remarks>
/// <param name="AutoPause">
/// <c>FG.DSAutoPause</c> — pause the simulation when no players are connected (saves CPU on an empty
/// server).
/// </param>
/// <param name="AutoSaveOnDisconnect">
/// <c>FG.DSAutoSaveOnDisconnect</c> — auto-save whenever a player disconnects.
/// </param>
/// <param name="AutosaveIntervalSeconds">
/// <c>FG.AutosaveInterval</c> — seconds between automatic saves.
/// </param>
/// <param name="DisableSeasonalEvents">
/// <c>FG.DisableSeasonalEvents</c> — disable in-game seasonal/holiday events (e.g. FICSMAS).
/// </param>
/// <param name="ServerRestartTimeSlot">
/// <c>FG.ServerRestartTimeSlot</c> — the daily time slot (minutes past midnight, server-local) at
/// which the server schedules an automatic restart; -1 disables scheduled restarts.
/// </param>
/// <param name="SendGameplayData">
/// <c>FG.SendGameplayData</c> — opt into sending anonymous gameplay telemetry to Coffee Stain.
/// </param>
/// <param name="NetworkQuality">
/// <c>FG.NetworkQuality</c> — network-quality preset (0=Low, 1=Medium, 2=High, 3=Ultra); higher values
/// raise bandwidth/update fidelity.
/// </param>
/// <param name="Passthrough">
/// Any option keys this layer does not model, carried verbatim (wire key → stringified value). Lets new
/// or modded options be read and written without a client release. Keys here must be full wire keys
/// (e.g. <c>"FG.SomeNewOption"</c>); a modeled key placed here is ignored in favor of its typed property.
/// </param>
public sealed record ServerOptions(
    bool? AutoPause = null,
    bool? AutoSaveOnDisconnect = null,
    int? AutosaveIntervalSeconds = null,
    bool? DisableSeasonalEvents = null,
    int? ServerRestartTimeSlot = null,
    bool? SendGameplayData = null,
    int? NetworkQuality = null,
    IReadOnlyDictionary<string, string>? Passthrough = null);
