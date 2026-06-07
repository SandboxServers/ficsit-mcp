using System.Globalization;

using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// Default <see cref="IServerSettingsMapper"/>. Translates between typed settings and the wire
/// string→string maps, parsing/formatting values with the invariant culture and the server's
/// boolean convention. Stateless and thread-safe — safe to register as a singleton.
/// </summary>
/// <remarks>
/// <para>
/// <b>Parsing is lenient; formatting is canonical.</b> Reads accept the server's <c>"True"/"False"</c>
/// as well as <c>"true"/"false"/"1"/"0"</c> for booleans (a value that does not parse is left in the
/// passthrough map, never silently dropped). Writes emit booleans as <c>"True"/"False"</c> (the
/// server's own convention) and numbers via the invariant culture, so a value never round-trips through
/// a locale-specific decimal separator.
/// </para>
/// <para>
/// <b>Passthrough split.</b> On a read, any key not in the modeled set
/// (<see cref="SettingKeys.ServerOptions.Known"/> / <see cref="SettingKeys.AdvancedGameSettings.Known"/>)
/// — and any modeled key whose value fails to parse — is preserved verbatim in the result's passthrough
/// map. On a write, modeled non-null properties are emitted first, then passthrough entries are added
/// only for keys a modeled property did not already write (modeled wins).
/// </para>
/// </remarks>
public sealed class ServerSettingsMapper : IServerSettingsMapper
{
    /// <inheritdoc/>
    public ServerOptionsView ToServerOptionsView(GetServerOptionsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        ServerOptions applied = ReadServerOptions(response.ServerOptions);
        ServerOptions pending = ReadServerOptions(response.PendingServerOptions);
        bool hasPending = response.PendingServerOptions.Count > 0;

        return new ServerOptionsView(applied, pending, hasPending);
    }

    /// <inheritdoc/>
    public ApplyServerOptionsRequest ToApplyServerOptionsRequest(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        PutBool(map, SettingKeys.ServerOptions.AutoPause, options.AutoPause);
        PutBool(map, SettingKeys.ServerOptions.AutoSaveOnDisconnect, options.AutoSaveOnDisconnect);
        PutInt(map, SettingKeys.ServerOptions.AutosaveInterval, options.AutosaveIntervalSeconds);
        PutBool(map, SettingKeys.ServerOptions.DisableSeasonalEvents, options.DisableSeasonalEvents);
        PutInt(map, SettingKeys.ServerOptions.ServerRestartTimeSlot, options.ServerRestartTimeSlot);
        PutBool(map, SettingKeys.ServerOptions.SendGameplayData, options.SendGameplayData);
        PutInt(map, SettingKeys.ServerOptions.NetworkQuality, options.NetworkQuality);
        MergePassthrough(map, options.Passthrough);

        return new ApplyServerOptionsRequest(map);
    }

    /// <inheritdoc/>
    public AdvancedGameSettingsView ToAdvancedGameSettingsView(GetAdvancedGameSettingsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        AdvancedGameSettings settings = ReadAdvancedGameSettings(response.AdvancedGameSettings);
        return new AdvancedGameSettingsView(response.CreativeModeEnabled, settings);
    }

    /// <inheritdoc/>
    public ApplyAdvancedGameSettingsRequest ToApplyAdvancedGameSettingsRequest(AdvancedGameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Dictionary<string, string> map = BuildAdvancedGameSettingsMap(settings);
        return new ApplyAdvancedGameSettingsRequest(map);
    }

    /// <inheritdoc/>
    public CreateNewGameRequest ToCreateNewGameRequest(NewGameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.SessionName);

        // Only attach an AGS map if the caller actually specified settings; an all-null AGS with no
        // passthrough flattens to an empty map, and sending that would needlessly flag the new save's
        // creative state. Null keeps the new game achievement-eligible.
        IReadOnlyDictionary<string, string>? agsMap = null;
        if (settings.AdvancedGameSettings is { } ags)
        {
            Dictionary<string, string> built = BuildAdvancedGameSettingsMap(ags);
            if (built.Count > 0)
            {
                agsMap = built;
            }
        }

        var newGameData = new NewGameData(
            SessionName: settings.SessionName,
            MapName: null,
            StartingLocation: settings.StartingLocation,
            SkipOnboarding: settings.SkipOnboarding,
            AdvancedGameSettings: agsMap,
            CustomOptionsOnlyForModding: null);

        return new CreateNewGameRequest(newGameData);
    }

    // --- reads (wire map -> typed) ------------------------------------------------------------

    private static ServerOptions ReadServerOptions(IReadOnlyDictionary<string, string> map)
    {
        var passthrough = new Dictionary<string, string>(StringComparer.Ordinal);

        bool? autoPause = TakeBool(map, SettingKeys.ServerOptions.AutoPause, passthrough);
        bool? autoSaveOnDisconnect = TakeBool(map, SettingKeys.ServerOptions.AutoSaveOnDisconnect, passthrough);
        int? autosaveInterval = TakeInt(map, SettingKeys.ServerOptions.AutosaveInterval, passthrough);
        bool? disableSeasonal = TakeBool(map, SettingKeys.ServerOptions.DisableSeasonalEvents, passthrough);
        int? restartSlot = TakeInt(map, SettingKeys.ServerOptions.ServerRestartTimeSlot, passthrough);
        bool? sendGameplayData = TakeBool(map, SettingKeys.ServerOptions.SendGameplayData, passthrough);
        int? networkQuality = TakeInt(map, SettingKeys.ServerOptions.NetworkQuality, passthrough);

        AddUnmodeled(map, SettingKeys.ServerOptions.Known, passthrough);

        return new ServerOptions(
            AutoPause: autoPause,
            AutoSaveOnDisconnect: autoSaveOnDisconnect,
            AutosaveIntervalSeconds: autosaveInterval,
            DisableSeasonalEvents: disableSeasonal,
            ServerRestartTimeSlot: restartSlot,
            SendGameplayData: sendGameplayData,
            NetworkQuality: networkQuality,
            Passthrough: passthrough);
    }

    private static AdvancedGameSettings ReadAdvancedGameSettings(IReadOnlyDictionary<string, string> map)
    {
        var passthrough = new Dictionary<string, string>(StringComparer.Ordinal);

        bool? noPower = TakeBool(map, SettingKeys.AdvancedGameSettings.NoPower, passthrough);
        int? startingTier = TakeInt(map, SettingKeys.AdvancedGameSettings.StartingTier, passthrough);
        bool? disableArachnids = TakeBool(map, SettingKeys.AdvancedGameSettings.DisableArachnidCreatures, passthrough);
        bool? noUnlockCost = TakeBool(map, SettingKeys.AdvancedGameSettings.NoUnlockCost, passthrough);
        int? setGamePhase = TakeInt(map, SettingKeys.AdvancedGameSettings.SetGamePhase, passthrough);
        bool? unlockResearch = TakeBool(map, SettingKeys.AdvancedGameSettings.UnlockAllResearchSchematics, passthrough);
        bool? instantAlt = TakeBool(map, SettingKeys.AdvancedGameSettings.UnlockInstantAltRecipes, passthrough);
        bool? unlockSink = TakeBool(map, SettingKeys.AdvancedGameSettings.UnlockAllResourceSinkSchematics, passthrough);
        bool? noBuildCost = TakeBool(map, SettingKeys.AdvancedGameSettings.NoBuildCost, passthrough);
        bool? godMode = TakeBool(map, SettingKeys.AdvancedGameSettings.GodMode, passthrough);
        bool? flightMode = TakeBool(map, SettingKeys.AdvancedGameSettings.FlightMode, passthrough);

        AddUnmodeled(map, SettingKeys.AdvancedGameSettings.Known, passthrough);

        return new AdvancedGameSettings(
            NoPower: noPower,
            StartingTier: startingTier,
            DisableArachnidCreatures: disableArachnids,
            NoUnlockCost: noUnlockCost,
            SetGamePhase: setGamePhase,
            UnlockAllResearchSchematics: unlockResearch,
            UnlockInstantAltRecipes: instantAlt,
            UnlockAllResourceSinkSchematics: unlockSink,
            NoBuildCost: noBuildCost,
            GodMode: godMode,
            FlightMode: flightMode,
            Passthrough: passthrough);
    }

    // --- writes (typed -> wire map) -----------------------------------------------------------

    private static Dictionary<string, string> BuildAdvancedGameSettingsMap(AdvancedGameSettings settings)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        PutBool(map, SettingKeys.AdvancedGameSettings.NoPower, settings.NoPower);
        PutInt(map, SettingKeys.AdvancedGameSettings.StartingTier, settings.StartingTier);
        PutBool(map, SettingKeys.AdvancedGameSettings.DisableArachnidCreatures, settings.DisableArachnidCreatures);
        PutBool(map, SettingKeys.AdvancedGameSettings.NoUnlockCost, settings.NoUnlockCost);
        PutInt(map, SettingKeys.AdvancedGameSettings.SetGamePhase, settings.SetGamePhase);
        PutBool(map, SettingKeys.AdvancedGameSettings.UnlockAllResearchSchematics, settings.UnlockAllResearchSchematics);
        PutBool(map, SettingKeys.AdvancedGameSettings.UnlockInstantAltRecipes, settings.UnlockInstantAltRecipes);
        PutBool(map, SettingKeys.AdvancedGameSettings.UnlockAllResourceSinkSchematics, settings.UnlockAllResourceSinkSchematics);
        PutBool(map, SettingKeys.AdvancedGameSettings.NoBuildCost, settings.NoBuildCost);
        PutBool(map, SettingKeys.AdvancedGameSettings.GodMode, settings.GodMode);
        PutBool(map, SettingKeys.AdvancedGameSettings.FlightMode, settings.FlightMode);
        MergePassthrough(map, settings.Passthrough);
        return map;
    }

    // --- value helpers ------------------------------------------------------------------------

    /// <summary>
    /// Reads a modeled boolean key: parses it to a typed value, or — if present but unparseable —
    /// preserves the raw value in <paramref name="passthrough"/> so it is never silently lost.
    /// </summary>
    private static bool? TakeBool(
        IReadOnlyDictionary<string, string> map,
        string key,
        IDictionary<string, string> passthrough)
    {
        if (!map.TryGetValue(key, out string? raw))
        {
            return null;
        }

        if (TryParseBool(raw, out bool value))
        {
            return value;
        }

        passthrough[key] = raw;
        return null;
    }

    private static int? TakeInt(
        IReadOnlyDictionary<string, string> map,
        string key,
        IDictionary<string, string> passthrough)
    {
        if (!map.TryGetValue(key, out string? raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return value;
        }

        passthrough[key] = raw;
        return null;
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        switch (raw.Trim())
        {
            case "1":
                value = true;
                return true;
            case "0":
                value = false;
                return true;
            default:
                return bool.TryParse(raw, out value);
        }
    }

    /// <summary>Copies every key not in <paramref name="known"/> into the passthrough map verbatim.</summary>
    private static void AddUnmodeled(
        IReadOnlyDictionary<string, string> map,
        IReadOnlySet<string> known,
        IDictionary<string, string> passthrough)
    {
        foreach (KeyValuePair<string, string> entry in map)
        {
            if (!known.Contains(entry.Key))
            {
                passthrough[entry.Key] = entry.Value;
            }
        }
    }

    private static void PutBool(IDictionary<string, string> map, string key, bool? value)
    {
        if (value is { } v)
        {
            // Match the server's own convention ("True"/"False"), seen in the API examples.
            map[key] = v ? "True" : "False";
        }
    }

    private static void PutInt(IDictionary<string, string> map, string key, int? value)
    {
        if (value is { } v)
        {
            map[key] = v.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Adds passthrough entries for any key a modeled property did not already write.</summary>
    private static void MergePassthrough(
        IDictionary<string, string> map,
        IReadOnlyDictionary<string, string>? passthrough)
    {
        if (passthrough is null)
        {
            return;
        }

        foreach (KeyValuePair<string, string> entry in passthrough)
        {
            // Modeled (typed) values win over a passthrough entry that names the same key.
            map.TryAdd(entry.Key, entry.Value);
        }
    }
}
