namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// Typed input for starting a new game (<c>CreateNewGame</c>): session identity, optional starting
/// location, onboarding skip, and the initial advanced game settings to seed the new save with.
/// </summary>
/// <remarks>
/// Domain type, no MCP/HTTP dependencies; <see cref="ServerSettingsMapper"/> maps it into the client's
/// <c>NewGameData</c> wire record (including flattening <paramref name="AdvancedGameSettings"/> to the
/// stringified map and honoring the server's <c>bSkipOnboarding</c> field quirk).
/// </remarks>
/// <param name="SessionName">
/// The name of the session/save to create. Required; must not be blank.
/// </param>
/// <param name="StartingLocation">
/// Optional starting location id (e.g. a starting-area key the server recognizes). Null lets the server
/// choose its default.
/// </param>
/// <param name="SkipOnboarding">
/// Whether to skip the intro/onboarding sequence. Maps to the server's <c>bSkipOnboarding</c> field.
/// </param>
/// <param name="AdvancedGameSettings">
/// Optional advanced game settings to enable on the new save. <b>Providing any non-empty AGS here
/// creates the session with advanced game settings active, which permanently flags it and disables
/// achievements</b> — same consequence as <c>set_advanced_game_settings</c>. Null/empty starts a normal,
/// achievement-eligible game.
/// </param>
public sealed record NewGameSettings(
    string SessionName,
    string? StartingLocation = null,
    bool SkipOnboarding = false,
    AdvancedGameSettings? AdvancedGameSettings = null);
