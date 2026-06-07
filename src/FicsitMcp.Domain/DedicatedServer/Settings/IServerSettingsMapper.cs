using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Domain.DedicatedServer.Settings;

/// <summary>
/// Maps between the typed settings domain (<see cref="ServerOptions"/>,
/// <see cref="AdvancedGameSettings"/>, <see cref="NewGameSettings"/>) and the client's wire records
/// (string→string maps under <c>FG.*</c> keys). This is where the typed-property↔wire-dictionary
/// translation and the applied-vs-pending shaping live, so both the MCP tools and the typed client stay
/// free of stringly-typed parsing. Pure, deterministic, no I/O — trivially unit-testable.
/// </summary>
public interface IServerSettingsMapper
{
    /// <summary>
    /// Projects a <c>GetServerOptions</c> response into the applied/pending typed view. Keys this layer
    /// models become typed properties; every other key is preserved verbatim in the relevant view's
    /// <see cref="ServerOptions.Passthrough"/>.
    /// </summary>
    ServerOptionsView ToServerOptionsView(GetServerOptionsResponse response);

    /// <summary>
    /// Flattens typed <see cref="ServerOptions"/> into the wire request for <c>ApplyServerOptions</c>.
    /// Only non-null typed properties and every passthrough entry are emitted, so a partial patch stays
    /// partial. Modeled keys take precedence over a passthrough entry of the same key.
    /// </summary>
    ApplyServerOptionsRequest ToApplyServerOptionsRequest(ServerOptions options);

    /// <summary>
    /// Projects a <c>GetAdvancedGameSettings</c> response into the typed view (settings + the
    /// creative-mode flag), with unmodeled keys preserved in
    /// <see cref="AdvancedGameSettings.Passthrough"/>.
    /// </summary>
    AdvancedGameSettingsView ToAdvancedGameSettingsView(GetAdvancedGameSettingsResponse response);

    /// <summary>
    /// Flattens typed <see cref="AdvancedGameSettings"/> into the wire request for
    /// <c>ApplyAdvancedGameSettings</c>. Same non-null/passthrough/precedence rules as
    /// <see cref="ToApplyServerOptionsRequest"/>.
    /// </summary>
    ApplyAdvancedGameSettingsRequest ToApplyAdvancedGameSettingsRequest(AdvancedGameSettings settings);

    /// <summary>
    /// Maps typed <see cref="NewGameSettings"/> into the client's <c>CreateNewGame</c> request,
    /// flattening any advanced game settings to the stringified map and honoring the server's
    /// <c>bSkipOnboarding</c> field. Throws <see cref="System.ArgumentException"/> if the session name is
    /// blank.
    /// </summary>
    CreateNewGameRequest ToCreateNewGameRequest(NewGameSettings settings);
}
