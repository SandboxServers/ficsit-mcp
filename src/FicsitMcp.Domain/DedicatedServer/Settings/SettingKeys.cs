namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// The canonical wire keys for the dedicated server's server-options and advanced-game-settings
/// string→string maps. These are the <c>FG.*</c> identifiers the server itself uses; they are the
/// single source of truth for the typed↔wire mapping in <see cref="ServerSettingsMapper"/> so a key
/// is spelled in exactly one place.
/// </summary>
/// <remarks>
/// Verified against the community OpenAPI spec (satisfactory-oas.github.io/spec) and independent
/// client SDKs on 2026-06-06. The base-game key set is intentionally NOT exhaustive of every future
/// game version — unknown keys round-trip untouched through the <c>Passthrough</c> maps on
/// <see cref="ServerOptions"/> / <see cref="AdvancedGameSettings"/>, so a new game version that adds a
/// key needs no release here.
/// </remarks>
internal static class SettingKeys
{
    /// <summary>Server-option keys (the <c>FG.*</c> identifiers in the server-options map).</summary>
    internal static class ServerOptions
    {
        internal const string AutoPause = "FG.DSAutoPause";
        internal const string AutoSaveOnDisconnect = "FG.DSAutoSaveOnDisconnect";
        internal const string AutosaveInterval = "FG.AutosaveInterval";
        internal const string DisableSeasonalEvents = "FG.DisableSeasonalEvents";
        internal const string ServerRestartTimeSlot = "FG.ServerRestartTimeSlot";
        internal const string SendGameplayData = "FG.SendGameplayData";
        internal const string NetworkQuality = "FG.NetworkQuality";

        /// <summary>All keys this layer maps to a typed property (used to split out passthrough).</summary>
        internal static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            AutoPause,
            AutoSaveOnDisconnect,
            AutosaveInterval,
            DisableSeasonalEvents,
            ServerRestartTimeSlot,
            SendGameplayData,
            NetworkQuality,
        };
    }

    /// <summary>Advanced-game-settings keys (the <c>FG.GameRules.*</c> / <c>FG.PlayerRules.*</c> identifiers).</summary>
    internal static class AdvancedGameSettings
    {
        internal const string NoPower = "FG.GameRules.NoPower";
        internal const string StartingTier = "FG.GameRules.StartingTier";
        internal const string DisableArachnidCreatures = "FG.GameRules.DisableArachnidCreatures";
        internal const string NoUnlockCost = "FG.GameRules.NoUnlockCost";
        internal const string SetGamePhase = "FG.GameRules.SetGamePhase";
        internal const string UnlockAllResearchSchematics = "FG.GameRules.UnlockAllResearchSchematics";
        internal const string UnlockInstantAltRecipes = "FG.GameRules.UnlockInstantAltRecipes";
        internal const string UnlockAllResourceSinkSchematics = "FG.GameRules.UnlockAllResourceSinkSchematics";
        internal const string NoBuildCost = "FG.PlayerRules.NoBuildCost";
        internal const string GodMode = "FG.PlayerRules.GodMode";
        internal const string FlightMode = "FG.PlayerRules.FlightMode";

        /// <summary>All keys this layer maps to a typed property (used to split out passthrough).</summary>
        internal static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            NoPower,
            StartingTier,
            DisableArachnidCreatures,
            NoUnlockCost,
            SetGamePhase,
            UnlockAllResearchSchematics,
            UnlockInstantAltRecipes,
            UnlockAllResourceSinkSchematics,
            NoBuildCost,
            GodMode,
            FlightMode,
        };
    }
}
