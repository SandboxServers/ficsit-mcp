using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Config-override and loader behaviour: unset -> embedded snapshot; set -> parse that
/// file; broken path -> loud, named error (never a silent fallback).
/// </summary>
public sealed class SnapshotLoaderTests
{
    [Fact]
    public void Load_WithNoDocsPath_UsesEmbeddedSnapshot()
    {
        GameDataSnapshot snapshot = GameDataSnapshotLoader.Load(new GameDataOptions());

        Assert.Equal(GameDataSnapshotLoader.SnapshotVersion, snapshot.Metadata.SnapshotVersion);
        Assert.Equal(GameDataSnapshotLoader.SnapshotGameBuildId, snapshot.Metadata.GameBuildId);
        Assert.NotEmpty(snapshot.Recipes);
    }

    [Fact]
    public void Load_WithDocsPathOverride_ParsesThatFile_NotTheEmbeddedSnapshot()
    {
        var options = new GameDataOptions { DocsPath = Fixtures.DocsSlicePath };

        GameDataSnapshot snapshot = GameDataSnapshotLoader.Load(options);

        Assert.Equal("override", snapshot.Metadata.SnapshotVersion);
        Assert.Equal(Fixtures.DocsSlicePath, snapshot.Metadata.Source);
        // The slice is far smaller than the full embedded snapshot, proving the override won.
        Assert.True(snapshot.Recipes.Length < 20);
    }

    [Fact]
    public void Load_WithMissingDocsPath_FailsLoudly_NamingThePath_NoSilentFallback()
    {
        string missing = Path.Combine(AppContext.BaseDirectory, "nope", "Docs.json");
        var options = new GameDataOptions { DocsPath = missing };

        GameDataLoadException ex =
            Assert.Throws<GameDataLoadException>(() => GameDataSnapshotLoader.Load(options));

        Assert.Contains(missing, ex.Message, StringComparison.Ordinal);
        Assert.Contains("DocsPath", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithWhitespaceDocsPath_FallsBackToEmbedded()
    {
        // An empty/whitespace setting is "not configured", not a broken path.
        GameDataSnapshot snapshot = GameDataSnapshotLoader.Load(new GameDataOptions { DocsPath = "   " });
        Assert.Equal(GameDataSnapshotLoader.SnapshotVersion, snapshot.Metadata.SnapshotVersion);
    }

    [Fact]
    public void EmbeddedResource_IsPresent_AndDeserializes()
    {
        GameDataSnapshot snapshot = GameDataSnapshotLoader.LoadEmbedded();
        Assert.NotEmpty(snapshot.Items);
        Assert.NotEmpty(snapshot.Buildings);
    }

    [Fact]
    public void SnapshotSerializer_RoundTrips_PreservingRates()
    {
        GameDataSnapshot original = Fixtures.ParseSlice();

        string json = GameDataSnapshotSerializer.Serialize(original);
        GameDataSnapshot reloaded = GameDataSnapshotSerializer.Deserialize(json, "test");

        GameRecipe before = original.Recipes.Single(r => r.ClassName == "Recipe_IronPlate_C");
        GameRecipe after = reloaded.Recipes.Single(r => r.ClassName == "Recipe_IronPlate_C");
        Assert.Equal(
            before.Products.Single().AmountPerMinute,
            after.Products.Single().AmountPerMinute,
            precision: 6);
    }

    [Fact]
    public void SnapshotSerializer_OnGarbage_ThrowsNamingTheSource()
    {
        GameDataLoadException ex = Assert.Throws<GameDataLoadException>(
            () => GameDataSnapshotSerializer.Deserialize("{ not json", "my-source"));

        Assert.Contains("my-source", ex.Message, StringComparison.Ordinal);
    }
}
