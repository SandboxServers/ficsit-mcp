using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Shared helpers for loading the checked-in UTF-16 Docs.json slice and building a service
/// over it, so every test runs against real game-data structure rather than a hand-rolled
/// in-memory stub.
/// </summary>
internal static class Fixtures
{
    /// <summary>Absolute path to the captured UTF-16 LE Docs.json slice fixture.</summary>
    public static string DocsSlicePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "docs-slice.utf16.json");

    private static readonly GameDataMetadata Metadata =
        new("test", "fixture", "docs-slice.utf16.json");

    /// <summary>Parses the fixture slice into a snapshot (exercises the UTF-16 read path).</summary>
    public static GameDataSnapshot ParseSlice() =>
        DocsJsonParser.ParseFile(DocsSlicePath, Metadata);

    /// <summary>Builds a service backed by the fixture slice.</summary>
    public static GameDataService SliceService() => new(ParseSlice());

    /// <summary>Builds a service backed by the shipped embedded snapshot.</summary>
    public static GameDataService EmbeddedService() => new(GameDataSnapshotLoader.LoadEmbedded());
}
