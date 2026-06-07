namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// The result of reading advanced game settings: the typed settings plus the server's
/// <c>creativeModeEnabled</c> flag. (Unlike server options, the AGS API does not expose a separate
/// pending map — applied settings take effect immediately, so there is one settings view, not two.)
/// </summary>
/// <param name="CreativeModeEnabled">
/// Whether advanced (creative) game settings are active on the session. Once true the session is
/// permanently flagged and achievements are disabled.
/// </param>
/// <param name="Settings">
/// The applied advanced game settings, typed, with any unmodeled keys in
/// <see cref="AdvancedGameSettings.Passthrough"/>.
/// </param>
public sealed record AdvancedGameSettingsView(
    bool CreativeModeEnabled,
    AdvancedGameSettings Settings);
