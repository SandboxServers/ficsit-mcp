using System.Reflection;
using System.Text;

using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Domain.GameData;

/// <summary>
/// Resolves the <see cref="GameDataSnapshot"/> to load at startup: either a user-supplied
/// local <c>Docs.json</c> override (UTF-16, parsed live) or the versioned snapshot
/// embedded in this assembly (UTF-8). This is the single decision point for "where does
/// game data come from", kept out of the service so the service only ever sees a snapshot.
/// </summary>
public static class GameDataSnapshotLoader
{
    /// <summary>Our snapshot asset version. Bumped when the format or tracked release changes.</summary>
    public const string SnapshotVersion = "v1";

    /// <summary>The Satisfactory Steam build id the shipped v1 snapshot was generated from.</summary>
    public const string SnapshotGameBuildId = "23300430";

    /// <summary>
    /// Logical name of the embedded snapshot resource. Matches the file path under the
    /// Domain project; the assembly's default namespace is <c>FicsitMcp.Domain</c>.
    /// </summary>
    public const string EmbeddedResourceName = "FicsitMcp.Domain.GameData.game-data.v1.json";

    /// <summary>
    /// Loads the active snapshot. When <see cref="GameDataOptions.DocsPath"/> is set, the
    /// file is parsed live (and a broken path is a named, fatal error — never a silent
    /// fallback). Otherwise the embedded snapshot is deserialized.
    /// </summary>
    public static GameDataSnapshot Load(GameDataOptions options)
    {
        string? docsPath = options.DocsPath?.Trim();
        return string.IsNullOrEmpty(docsPath)
            ? LoadEmbedded()
            : LoadFromDocs(docsPath);
    }

    /// <summary>Deserializes the snapshot embedded in this assembly.</summary>
    public static GameDataSnapshot LoadEmbedded()
    {
        Assembly assembly = typeof(GameDataSnapshotLoader).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
        if (stream is null)
        {
            throw new GameDataLoadException(
                $"The embedded game-data snapshot '{EmbeddedResourceName}' was not found in " +
                $"assembly '{assembly.GetName().Name}'. The build is missing the snapshot asset.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string json = reader.ReadToEnd();
        return GameDataSnapshotSerializer.Deserialize(json, $"embedded:{EmbeddedResourceName}");
    }

    /// <summary>Parses a user-supplied Docs.json override (UTF-16) into a snapshot.</summary>
    public static GameDataSnapshot LoadFromDocs(string docsPath)
    {
        if (!File.Exists(docsPath))
        {
            throw new GameDataLoadException(
                $"GameData:DocsPath points at '{docsPath}', but no file exists there. " +
                $"Set it to a valid Satisfactory Docs.json (e.g. CommunityResources/Docs/en-US.json), " +
                $"or unset it to use the shipped snapshot.");
        }

        var metadata = new GameDataMetadata(
            SnapshotVersion: "override",
            GameBuildId: "user-supplied",
            Source: docsPath);

        return DocsJsonParser.ParseFile(docsPath, metadata);
    }
}
