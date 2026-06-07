using System.ComponentModel;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.DedicatedServer.Settings;

using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// MCP tools over the dedicated server's <b>server options</b> and <b>advanced game settings</b>
/// (the creative-mode levers), plus starting a new game. Thin: each method validates its input,
/// delegates to <see cref="IDedicatedServerApiClient"/> for the wire call and to
/// <see cref="IServerSettingsMapper"/> for the typed↔wire-map translation, and returns the typed
/// result. No HTTP, envelope, or stringly-typed key handling lives here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Typed properties + passthrough.</b> Options and settings are exposed as documented, typed
/// properties (e.g. <c>autoPause</c>, <c>noBuildCost</c>). Anything a newer game version adds that this
/// release does not model can still be read and written via the <c>passthrough</c> map (full wire key →
/// stringified value), so a new key never requires a client release.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class ServerSettingsTool
{
    /// <summary>Reads server options, surfacing applied and pending values separately.</summary>
    /// <remarks>Pure read: no privilege required by the server, no state change.</remarks>
    [McpServerTool(Name = "get_server_options", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Reads the dedicated server's options as typed values, separated into APPLIED (live now) and " +
        "PENDING (accepted but not yet in effect — typically taking effect after a server restart). " +
        "'hasPendingChanges' is true when the server reports any queued option, i.e. a restart is needed " +
        "to apply them. Typed options include: autoPause (pause sim when empty), autoSaveOnDisconnect, " +
        "autosaveIntervalSeconds, disableSeasonalEvents, serverRestartTimeSlot (minutes past midnight; " +
        "-1 = none), sendGameplayData, networkQuality (0=Low..3=Ultra). Any option this release does not " +
        "model appears under each view's 'passthrough' map (full wire key -> stringified value). " +
        "Read-only.")]
    public static async Task<ServerOptionsView> GetServerOptions(
        IDedicatedServerApiClient client,
        IServerSettingsMapper mapper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mapper);

        GetServerOptionsResponse response = await client
            .GetServerOptionsAsync(cancellationToken)
            .ConfigureAwait(false);

        return mapper.ToServerOptionsView(response);
    }

    /// <summary>Applies server options. Only the fields you set are changed.</summary>
    /// <remarks>
    /// Write (not read-only) and admin-only on the server. Idempotent: applying the same options again
    /// converges to the same state. Not flagged Destructive — option changes alter configuration, are
    /// reversible by reapplying, and do not delete data or disconnect players; some take effect only
    /// after a restart (see <c>get_server_options</c> pending values). OpenWorld: it mutates live
    /// external server state.
    /// </remarks>
    [McpServerTool(Name = "set_server_options", ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Applies dedicated-server options (admin only). Provide only the fields you want to change; " +
        "omitted/null fields are left unchanged, so this is a partial patch, not a full replace. Some " +
        "options take effect immediately; others are queued until the next server restart and will show " +
        "up as PENDING in get_server_options. Settable: autoPause, autoSaveOnDisconnect, " +
        "autosaveIntervalSeconds, disableSeasonalEvents, serverRestartTimeSlot (minutes past midnight; " +
        "-1 disables scheduled restarts), sendGameplayData, networkQuality (0=Low,1=Medium,2=High," +
        "3=Ultra). To set an option this release does not model, put its full wire key and stringified " +
        "value in the 'passthrough' map (a modeled field of the same key takes precedence). This is " +
        "reversible by reapplying and does not delete saves or disconnect players.")]
    public static async Task<ServerOptionsView> SetServerOptions(
        IDedicatedServerApiClient client,
        IServerSettingsMapper mapper,
        [Description(
            "The options to change. Only non-null fields are sent; everything else is left as-is. Use " +
            "'passthrough' (wire key -> stringified value) for options not modeled as typed fields.")]
        ServerOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(options);

        ApplyServerOptionsRequest request = mapper.ToApplyServerOptionsRequest(options);
        await client.ApplyServerOptionsAsync(request, cancellationToken).ConfigureAwait(false);

        // Re-read so the caller sees the authoritative post-apply state (including anything the server
        // moved to PENDING). The acceptance criterion for this issue is "set then get reflects the
        // change"; returning a fresh read makes that observable in one call.
        GetServerOptionsResponse refreshed = await client
            .GetServerOptionsAsync(cancellationToken)
            .ConfigureAwait(false);

        return mapper.ToServerOptionsView(refreshed);
    }

    /// <summary>Reads advanced game settings (the creative levers) plus the creative-mode flag.</summary>
    /// <remarks>Pure read; no privilege required by the server.</remarks>
    [McpServerTool(Name = "get_advanced_game_settings", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Reads the dedicated server's advanced game settings (the 'creative mode' levers) as typed " +
        "values, plus 'creativeModeEnabled' (whether the session is already flagged for advanced " +
        "settings, which disables achievements). Typed settings include: noPower, startingTier, " +
        "disableArachnidCreatures (arachnophobia mode), noUnlockCost, setGamePhase, " +
        "unlockAllResearchSchematics, unlockInstantAltRecipes, unlockAllResourceSinkSchematics, " +
        "noBuildCost, godMode, flightMode. Any setting this release does not model appears under " +
        "'settings.passthrough' (full wire key -> stringified value). Read-only.")]
    public static async Task<AdvancedGameSettingsView> GetAdvancedGameSettings(
        IDedicatedServerApiClient client,
        IServerSettingsMapper mapper,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mapper);

        GetAdvancedGameSettingsResponse response = await client
            .GetAdvancedGameSettingsAsync(cancellationToken)
            .ConfigureAwait(false);

        return mapper.ToAdvancedGameSettingsView(response);
    }

    /// <summary>Applies advanced game settings. Permanently flags the session and disables achievements.</summary>
    /// <remarks>
    /// Write and admin-only. Idempotent (reapplying the same settings converges). Not flagged Destructive
    /// — it does not delete saves or disconnect players — but it has a genuinely permanent side effect
    /// (the session's advanced-settings flag and the resulting loss of achievement eligibility cannot be
    /// undone), stated up front in the description so a well-behaved model treats it with care. OpenWorld:
    /// mutates live server state.
    /// </remarks>
    [McpServerTool(Name = "set_advanced_game_settings", ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "WARNING: applying advanced game settings PERMANENTLY flags the session and DISABLES " +
        "achievements for that save — this cannot be undone. Applies the dedicated server's advanced " +
        "game settings (the 'creative' levers; admin only). Provide only the fields you want to change; " +
        "omitted/null fields are left unchanged (partial patch). Settable: noPower, startingTier, " +
        "disableArachnidCreatures, noUnlockCost, setGamePhase (jumps progression to a game phase; not " +
        "reversible by lowering it), unlockAllResearchSchematics, unlockInstantAltRecipes, " +
        "unlockAllResourceSinkSchematics, noBuildCost, godMode, flightMode. To set a setting this " +
        "release does not model, put its full wire key and stringified value in the 'passthrough' map " +
        "(a modeled field of the same key takes precedence).")]
    public static async Task<AdvancedGameSettingsView> SetAdvancedGameSettings(
        IDedicatedServerApiClient client,
        IServerSettingsMapper mapper,
        [Description(
            "The advanced settings to change. Only non-null fields are sent. Use 'passthrough' (wire key " +
            "-> stringified value) for settings not modeled as typed fields. Applying any of these " +
            "disables achievements for the session permanently.")]
        AdvancedGameSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(settings);

        ApplyAdvancedGameSettingsRequest request = mapper.ToApplyAdvancedGameSettingsRequest(settings);
        await client.ApplyAdvancedGameSettingsAsync(request, cancellationToken).ConfigureAwait(false);

        // Re-read so the caller observes the post-apply state, including creativeModeEnabled flipping
        // true. Satisfies the issue's "set then get reflects the change" acceptance criterion.
        GetAdvancedGameSettingsResponse refreshed = await client
            .GetAdvancedGameSettingsAsync(cancellationToken)
            .ConfigureAwait(false);

        return mapper.ToAdvancedGameSettingsView(refreshed);
    }

    /// <summary>Starts a brand-new game, replacing the running session. Destructive.</summary>
    /// <remarks>
    /// Destructive and non-idempotent: it ends the current session (every connected player is
    /// disconnected as the new game loads) and each call creates another new game. OpenWorld: it mutates
    /// live external server state.
    /// </remarks>
    [McpServerTool(Name = "create_new_game", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description(
        "Starts a brand-new game on the dedicated server, REPLACING the currently running session: the " +
        "active game ends and EVERY connected player is disconnected while the new game loads (admin " +
        "only). The previous session's existing saves on disk are not deleted, but the live session is " +
        "torn down. Requires a sessionName; optional startingLocation and skipOnboarding. You may seed " +
        "advanced game settings via 'advancedGameSettings' — doing so creates the save with advanced " +
        "settings active, which PERMANENTLY disables achievements for it; leave it null/empty for a " +
        "normal, achievement-eligible game. Unmodeled advanced settings go in " +
        "'advancedGameSettings.passthrough'. Confirm with the user before calling — this interrupts " +
        "everyone currently playing.")]
    public static async Task CreateNewGame(
        IDedicatedServerApiClient client,
        IServerSettingsMapper mapper,
        [Description(
            "The new game to start. sessionName is required (must not be blank). Providing " +
            "advancedGameSettings disables achievements for the new save permanently.")]
        NewGameSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(settings);

        // Validate up front (mapper also guards) so a blank session name fails as an ArgumentException
        // before any wire call, never as an opaque server error.
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.SessionName);

        CreateNewGameRequest request = mapper.ToCreateNewGameRequest(settings);
        await client.CreateNewGameAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
