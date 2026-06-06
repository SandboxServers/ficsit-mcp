using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;
using FicsitMcp.Tools;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// The MCP tool layer is intentionally thin: validate input, delegate to
/// <see cref="IGameDataService"/>, return the result. These tests confirm the delegation
/// and the input guards, using the real service over the embedded snapshot.
/// </summary>
public sealed class GameDataToolTests
{
    private static IGameDataService Service() => Fixtures.EmbeddedService();

    [Fact]
    public void LookupRecipe_ReturnsProducingAndConsumingRecipes()
    {
        ItemRecipesResult result = GameDataTool.LookupRecipe(Service(), "Iron Plate");

        Assert.Equal("Desc_IronPlate_C", result.Item.ClassName);
        Assert.Contains(result.ProducedBy, r => r.ClassName == "Recipe_IronPlate_C");
        Assert.NotEmpty(result.ConsumedBy);
    }

    [Fact]
    public void LookupItem_ReturnsResolvedItem()
    {
        GameItem item = GameDataTool.LookupItem(Service(), "Desc_IronPlate_C");

        Assert.Equal("Iron Plate", item.DisplayName);
        Assert.Equal(ItemForm.Solid, item.Form);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Tools_RejectBlankInput(string blank)
    {
        IGameDataService svc = Service();
        Assert.ThrowsAny<ArgumentException>(() => GameDataTool.LookupRecipe(svc, blank));
        Assert.ThrowsAny<ArgumentException>(() => GameDataTool.LookupItem(svc, blank));
    }

    [Fact]
    public void LookupItem_UnknownName_Throws_ForTheModelToSeeNearMatches()
    {
        UnknownGameDataNameException ex = Assert.Throws<UnknownGameDataNameException>(
            () => GameDataTool.LookupItem(Service(), "Iron Platee"));
        Assert.NotEmpty(ex.NearMatches);
    }
}
