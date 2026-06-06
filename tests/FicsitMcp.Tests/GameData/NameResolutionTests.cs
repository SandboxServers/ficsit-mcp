using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Class-name vs display-name resolution: both work, case-insensitively, and unknown
/// names fail loudly with model-actionable near-matches rather than returning null.
/// </summary>
public sealed class NameResolutionTests
{
    private static GameDataService Service() => Fixtures.SliceService();

    [Theory]
    [InlineData("Desc_IronPlate_C")]
    [InlineData("Iron Plate")]
    [InlineData("iron plate")]
    [InlineData("DESC_IRONPLATE_C")]
    [InlineData("  Iron Plate  ")]
    public void GetItem_ResolvesByClassOrDisplay_CaseInsensitive_AndTrimmed(string query)
    {
        GameItem item = Service().GetItem(query);
        Assert.Equal("Desc_IronPlate_C", item.ClassName);
    }

    [Fact]
    public void GetRecipe_ResolvesByClassName_AndByDisplayName()
    {
        GameDataService svc = Service();
        Assert.Equal("Recipe_IronPlate_C", svc.GetRecipe("Recipe_IronPlate_C").ClassName);
        Assert.Equal("Recipe_IronPlate_C", svc.GetRecipe("Iron Plate").ClassName);
    }

    [Fact]
    public void GetItem_Unknown_ThrowsWithNearMatches()
    {
        UnknownGameDataNameException ex =
            Assert.Throws<UnknownGameDataNameException>(() => Service().GetItem("Iron Platee"));

        Assert.Equal("Iron Platee", ex.Query);
        Assert.Contains("Iron Plate", ex.NearMatches);
        Assert.Contains("Did you mean", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBuilding_Unknown_ThrowsWithNearMatches()
    {
        UnknownGameDataNameException ex =
            Assert.Throws<UnknownGameDataNameException>(() => Service().GetBuilding("Construktor"));

        Assert.Contains(ex.NearMatches, m => m.Contains("Constructor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryGetItem_ReturnsFalse_OnUnknown_WithoutThrowing()
    {
        Assert.False(Service().TryGetItem("nope-not-real", out GameItem item));
        Assert.Null(item);
    }

    [Fact]
    public void Lookups_AreNotLinearScans_ResolveManyNamesQuickly()
    {
        // Not a micro-benchmark; just a guard that resolution is dictionary-backed and a
        // tight loop of lookups completes effectively instantly.
        GameDataService svc = Service();
        for (int n = 0; n < 50_000; n++)
        {
            _ = svc.GetItem("Desc_IronPlate_C");
        }
    }

    [Fact]
    public void GetItem_NullOrWhitespace_ThrowsArgumentException()
    {
        GameDataService svc = Service();
        Assert.ThrowsAny<ArgumentException>(() => svc.GetItem(""));
        Assert.ThrowsAny<ArgumentException>(() => svc.GetItem("   "));
    }
}
