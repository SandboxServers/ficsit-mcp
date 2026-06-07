namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// A typed, documented projection of the dedicated server's advanced-game-settings map (the
/// "creative" levers: wire keys <c>FG.GameRules.*</c> / <c>FG.PlayerRules.*</c>, stringified values).
/// Every well-known setting is a strongly-typed, nullable property; anything the game adds that this
/// layer does not (yet) model lands in <see cref="Passthrough"/>, so a new game version never requires
/// a release of this client to set or read an unmodeled setting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Applying advanced game settings flags the session permanently and disables achievements.</b>
/// That is a property of the server/save, not of this client — see the <c>set_advanced_game_settings</c>
/// tool description.
/// </para>
/// <para>
/// <b>Nullability = "unspecified".</b> A null typed property means the setting was not present (on a
/// read) or should be left unchanged (on a write). On a write only non-null properties — plus every
/// <see cref="Passthrough"/> entry — are sent.
/// </para>
/// <para>
/// Domain type, no MCP/HTTP dependencies; <see cref="ServerSettingsMapper"/> converts it to/from the
/// wire string→string map.
/// </para>
/// </remarks>
/// <param name="NoPower">
/// <c>FG.GameRules.NoPower</c> — machines run without power (you still wire them, but they need no
/// generation).
/// </param>
/// <param name="StartingTier">
/// <c>FG.GameRules.StartingTier</c> — the milestone tier the game starts unlocked at.
/// </param>
/// <param name="DisableArachnidCreatures">
/// <c>FG.GameRules.DisableArachnidCreatures</c> — arachnophobia mode: replaces spiders with a harmless
/// marker.
/// </param>
/// <param name="NoUnlockCost">
/// <c>FG.GameRules.NoUnlockCost</c> — milestones/research/MAM unlocks cost no items.
/// </param>
/// <param name="SetGamePhase">
/// <c>FG.GameRules.SetGamePhase</c> — jumps the save to the given game phase (0-based). A blunt
/// progression lever; advancing the phase is not reversible by lowering this.
/// </param>
/// <param name="UnlockAllResearchSchematics">
/// <c>FG.GameRules.UnlockAllResearchSchematics</c> — unlock all milestone/research schematics.
/// </param>
/// <param name="UnlockInstantAltRecipes">
/// <c>FG.GameRules.UnlockInstantAltRecipes</c> — alternate recipes from hard-drive research complete
/// instantly.
/// </param>
/// <param name="UnlockAllResourceSinkSchematics">
/// <c>FG.GameRules.UnlockAllResourceSinkSchematics</c> — unlock all AWESOME Sink shop schematics.
/// </param>
/// <param name="NoBuildCost">
/// <c>FG.PlayerRules.NoBuildCost</c> — the build gun consumes no items.
/// </param>
/// <param name="GodMode">
/// <c>FG.PlayerRules.GodMode</c> — the player takes no damage.
/// </param>
/// <param name="FlightMode">
/// <c>FG.PlayerRules.FlightMode</c> — the player can fly (no-clip-style free movement).
/// </param>
/// <param name="Passthrough">
/// Any setting keys this layer does not model, carried verbatim (wire key → stringified value). Lets
/// new or modded settings be read and written without a client release. Keys here must be full wire
/// keys (e.g. <c>"FG.GameRules.SomeNewRule"</c>); a modeled key placed here is ignored in favor of its
/// typed property.
/// </param>
public sealed record AdvancedGameSettings(
    bool? NoPower = null,
    int? StartingTier = null,
    bool? DisableArachnidCreatures = null,
    bool? NoUnlockCost = null,
    int? SetGamePhase = null,
    bool? UnlockAllResearchSchematics = null,
    bool? UnlockInstantAltRecipes = null,
    bool? UnlockAllResourceSinkSchematics = null,
    bool? NoBuildCost = null,
    bool? GodMode = null,
    bool? FlightMode = null,
    IReadOnlyDictionary<string, string>? Passthrough = null);
