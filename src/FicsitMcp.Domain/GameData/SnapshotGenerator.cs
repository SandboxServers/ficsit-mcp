using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Repeatable, documented procedure for regenerating the shipped game-data snapshot from
/// a fresh Satisfactory <c>Docs.json</c> after a game release. Pure: reads the docs file,
/// parses it, and writes the compact UTF-8 snapshot asset. Invoked from a normally-skipped
/// test (<c>SnapshotGeneratorTests</c>); see <c>GameData/README.md</c> for the full workflow.
/// </summary>
public static class SnapshotGenerator
{
    /// <summary>
    /// Parses <paramref name="docsPath"/> (UTF-16 Docs.json) and writes the compact UTF-8
    /// snapshot to <paramref name="outputPath"/>, stamped with the current shipped version
    /// and game build id.
    /// </summary>
    /// <returns>The parsed snapshot that was written (for caller-side assertions).</returns>
    public static GameDataSnapshot Regenerate(string docsPath, string outputPath)
    {
        var metadata = new GameDataMetadata(
            GameDataSnapshotLoader.SnapshotVersion,
            GameDataSnapshotLoader.SnapshotGameBuildId,
            "shipped-snapshot");

        GameDataSnapshot snapshot = DocsJsonParser.ParseFile(docsPath, metadata);
        string json = GameDataSnapshotSerializer.Serialize(snapshot);
        File.WriteAllText(outputPath, json);
        return snapshot;
    }
}
