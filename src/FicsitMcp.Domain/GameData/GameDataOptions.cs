namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Configuration for the game-data layer. Bound from the host's configuration under the
/// <c>GameData</c> section (e.g. environment variable
/// <c>FICSITMCP_GameData__DocsPath</c>).
/// </summary>
public sealed class GameDataOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "GameData";

    /// <summary>
    /// Optional absolute path to a local Satisfactory <c>Docs.json</c> (UTF-16). When set,
    /// it overrides the shipped snapshot — the escape hatch for players on a newer game
    /// version than the checked-in snapshot. When unset/empty, the embedded snapshot is used.
    /// </summary>
    /// <remarks>
    /// A configured-but-broken path is a hard, named failure at startup (see
    /// <see cref="GameDataLoadException"/>), never a silent fallback to the snapshot: a
    /// silent fallback would mask the operator's misconfiguration and serve stale data.
    /// </remarks>
    public string? DocsPath { get; set; }
}
