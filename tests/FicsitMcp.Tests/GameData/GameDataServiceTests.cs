using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Service-surface behaviour: the synthesized produces/consumes answer, recipe validation
/// for the FIN bridge, and resolved presentation views.
/// </summary>
public sealed class GameDataServiceTests
{
    private static GameDataService Service() => Fixtures.SliceService();

    [Fact]
    public void GetRecipesForItem_SplitsProducingFromConsuming()
    {
        ItemRecipesResult result = Service().GetRecipesForItem("Iron Plate");

        // Iron Plate is produced by its base recipe; a recipe that produces an item must
        // never also be reported as consuming it.
        Assert.Contains(result.ProducedBy, r => r.ClassName == "Recipe_IronPlate_C");
        Assert.DoesNotContain(result.ConsumedBy, r => r.ClassName == "Recipe_IronPlate_C");
        Assert.Equal("Desc_IronPlate_C", result.Item.ClassName);
    }

    [Fact]
    public void GetRecipesForItem_IronIngot_IsConsumedByIronPlateRecipe()
    {
        ItemRecipesResult result = Service().GetRecipesForItem("Iron Ingot");

        Assert.Contains(result.ConsumedBy, r => r.ClassName == "Recipe_IronPlate_C");
    }

    [Fact]
    public void GetRecipeView_ResolvesDisplayNamesAndBuildings()
    {
        RecipeView view = Service().GetRecipeView("Recipe_IronPlate_C");

        Assert.Equal("Iron Plate", view.DisplayName);
        Assert.Equal("Iron Ingot", view.Ingredients.Single().ItemDisplayName);
        Assert.Equal("Constructor", view.ProducedIn.Single().DisplayName);
        Assert.Equal(4.0, view.ProducedIn.Single().PowerConsumptionMw);
    }

    [Fact]
    public void ValidateRecipeForBuilding_ValidPairing_IsValid()
    {
        RecipeValidationResult result =
            Service().ValidateRecipeForBuilding("Recipe_IronPlate_C", "Build_ConstructorMk1_C");

        Assert.True(result.IsValid);
        Assert.Equal(string.Empty, result.Reason);
    }

    [Fact]
    public void ValidateRecipeForBuilding_WrongBuilding_IsInvalid_WithCorrectableBuildings()
    {
        RecipeValidationResult result =
            Service().ValidateRecipeForBuilding("Recipe_IronPlate_C", "Build_AssemblerMk1_C");

        Assert.False(result.IsValid);
        Assert.Contains("Build_ConstructorMk1_C", result.ValidBuildingClassNames);
        Assert.Contains("cannot be run in", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRecipeForBuilding_UnknownRecipe_IsInvalid_NotThrown()
    {
        RecipeValidationResult result =
            Service().ValidateRecipeForBuilding("Recipe_DoesNotExist_C", "Build_ConstructorMk1_C");

        Assert.False(result.IsValid);
        Assert.Null(result.RecipeClassName);
        Assert.Contains("Unknown recipe", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRecipeForBuilding_UnknownBuilding_IsInvalid_NotThrown()
    {
        RecipeValidationResult result =
            Service().ValidateRecipeForBuilding("Recipe_IronPlate_C", "Build_DoesNotExist_C");

        Assert.False(result.IsValid);
        Assert.Equal("Recipe_IronPlate_C", result.RecipeClassName);
        Assert.Contains("Unknown building", result.Reason, StringComparison.Ordinal);
    }
}
