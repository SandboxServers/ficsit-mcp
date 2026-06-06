using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Golden vanilla-rate assertions. These are not tolerances or judgement calls: a stock
/// smelter/constructor line makes exactly these rates, and any deviation is a bug. Rates
/// are asserted against BOTH the fixture slice and the shipped embedded snapshot, so the
/// acceptance criterion ("lookup_recipe(\"Iron Plate\") returns correct vanilla rates")
/// is checked on the data the server actually ships.
/// </summary>
public sealed class GoldenRateTests
{
    public static TheoryData<bool> Sources => new() { true, false };

    private static GameDataService Service(bool embedded) =>
        embedded ? Fixtures.EmbeddedService() : Fixtures.SliceService();

    [Theory]
    [MemberData(nameof(Sources))]
    public void IronPlate_Is30IngotInTo20PlateOut_Over6Seconds(bool embedded)
    {
        GameRecipe recipe = Service(embedded).GetRecipe("Recipe_IronPlate_C");

        Assert.Equal(6.0, recipe.DurationSeconds, precision: 6);

        RecipeItemAmount ingot = recipe.Ingredients.Single(i => i.ItemClassName == "Desc_IronIngot_C");
        Assert.Equal(3.0, ingot.AmountPerCraft, precision: 6);
        Assert.Equal(30.0, ingot.AmountPerMinute, precision: 6); // 3 * 60 / 6

        RecipeItemAmount plate = recipe.Products.Single(p => p.ItemClassName == "Desc_IronPlate_C");
        Assert.Equal(2.0, plate.AmountPerCraft, precision: 6);
        Assert.Equal(20.0, plate.AmountPerMinute, precision: 6); // 2 * 60 / 6 — THE golden rate
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void IronPlate_ProducedInConstructor(bool embedded)
    {
        GameRecipe recipe = Service(embedded).GetRecipe("Recipe_IronPlate_C");
        Assert.Equal("Build_ConstructorMk1_C", Assert.Single(recipe.ProducedInBuildingClassNames));
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void Plastic_FluidIngredientScaledToCubicMetres(bool embedded)
    {
        // Raw docs: 3000 ml crude oil -> 2 plastic + 1000 ml heavy oil residue over 6s.
        // After ml->m3 scaling: 30 m3/min crude oil -> 20 plastic/min + 10 m3/min residue.
        GameRecipe recipe = Service(embedded).GetRecipe("Recipe_Plastic_C");

        RecipeItemAmount oil = recipe.Ingredients.Single(i => i.ItemClassName == "Desc_LiquidOil_C");
        Assert.Equal(ItemForm.Liquid, Service(embedded).GetItem("Desc_LiquidOil_C").Form);
        Assert.Equal(3.0, oil.AmountPerCraft, precision: 6); // 3000 ml / 1000
        Assert.Equal(30.0, oil.AmountPerMinute, precision: 6);

        RecipeItemAmount plastic = recipe.Products.Single(p => p.ItemClassName == "Desc_Plastic_C");
        Assert.Equal(20.0, plastic.AmountPerMinute, precision: 6);

        RecipeItemAmount residue = recipe.Products.Single(p => p.ItemClassName == "Desc_HeavyOilResidue_C");
        Assert.Equal(1.0, residue.AmountPerCraft, precision: 6); // 1000 ml / 1000
        Assert.Equal(10.0, residue.AmountPerMinute, precision: 6);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void LookupRecipeForItem_ByDisplayName_ReturnsGoldenProducingRate(bool embedded)
    {
        // Mirrors the lookup_recipe tool surface: query by display name, get producing rate.
        ItemRecipesResult result = Service(embedded).GetRecipesForItem("Iron Plate");

        RecipeView byBaseRecipe = result.ProducedBy.Single(r => r.ClassName == "Recipe_IronPlate_C");
        RecipeRate plate = byBaseRecipe.Products.Single(p => p.ItemClassName == "Desc_IronPlate_C");
        Assert.Equal(20.0, plate.AmountPerMinute, precision: 6);
        Assert.Equal("Iron Plate", plate.ItemDisplayName);
        Assert.Equal("Constructor", byBaseRecipe.ProducedIn.Single().DisplayName);
    }
}
