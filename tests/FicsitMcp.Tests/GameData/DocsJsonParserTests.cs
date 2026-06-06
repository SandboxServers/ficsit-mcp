using System.Text;

using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

namespace FicsitMcp.Tests.GameData;

/// <summary>
/// Loader/parser tests against the checked-in UTF-16 Docs.json slice: prove the encoding
/// is handled, the per-native-class structure is parsed, and unit/field mapping is correct.
/// </summary>
public sealed class DocsJsonParserTests
{
    [Fact]
    public void ParsesUtf16DocsSlice_WithoutGarbledText()
    {
        GameDataSnapshot snapshot = Fixtures.ParseSlice();

        // If the UTF-16 BOM file were read as UTF-8, display names would be mojibake.
        GameItem plate = snapshot.Items.Single(i => i.ClassName == "Desc_IronPlate_C");
        Assert.Equal("Iron Plate", plate.DisplayName);
        Assert.Equal(ItemForm.Solid, plate.Form);
    }

    [Fact]
    public void DecodingUtf16BytesAsUtf8_ProducesGarbage_DocumentingTheEncodingGotcha()
    {
        // This is the failure mode the loader guards against: prove that interpreting the
        // UTF-16 bytes as UTF-8 really does corrupt the data, so the UTF-16 read in
        // DocsJsonParser is load-bearing. (Bytes are decoded directly to bypass
        // StreamReader's BOM auto-detection, which would otherwise quietly self-correct.)
        byte[] bytes = File.ReadAllBytes(Fixtures.DocsSlicePath);
        string wrong = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("Iron Plate", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsesAllThreeEntityKinds_FromTheSlice()
    {
        GameDataSnapshot snapshot = Fixtures.ParseSlice();

        Assert.Contains(snapshot.Items, i => i.ClassName == "Desc_IronIngot_C");
        Assert.Contains(snapshot.Recipes, r => r.ClassName == "Recipe_IronPlate_C");
        Assert.Contains(snapshot.Buildings, b => b.ClassName == "Build_ConstructorMk1_C");
    }

    [Fact]
    public void FiltersHandCraftSourcesFromProducedIn_KeepingOnlyBuildMachines()
    {
        GameRecipe plate = Fixtures.ParseSlice().Recipes.Single(r => r.ClassName == "Recipe_IronPlate_C");

        // The raw mProducedIn also lists workbench/automated-workbench tokens; only the
        // real automation building should survive.
        Assert.Equal("Build_ConstructorMk1_C", Assert.Single(plate.ProducedInBuildingClassNames));
    }

    [Fact]
    public void FlagsAlternateRecipes_ByClassNamePrefix()
    {
        GameDataSnapshot snapshot = Fixtures.ParseSlice();

        Assert.True(snapshot.Recipes.Single(r => r.ClassName == "Recipe_Alternate_CoatedIronPlate_C").IsAlternate);
        Assert.False(snapshot.Recipes.Single(r => r.ClassName == "Recipe_IronPlate_C").IsAlternate);
    }

    [Fact]
    public void ParsesBuildingPowerAndClearance()
    {
        GameBuilding constructor = Fixtures.ParseSlice().Buildings.Single(b => b.ClassName == "Build_ConstructorMk1_C");

        Assert.Equal(4.0, constructor.PowerConsumptionMw, precision: 3);
        Assert.NotNull(constructor.Clearance);
        Assert.Equal(800.0, constructor.Clearance!.Width, precision: 3);
    }

    [Fact]
    public void ParseFile_OnMissingPath_ThrowsNamingThePath()
    {
        string bogus = Path.Combine(AppContext.BaseDirectory, "does-not-exist.json");

        GameDataLoadException ex = Assert.Throws<GameDataLoadException>(
            () => DocsJsonParser.ParseFile(bogus, new GameDataMetadata("t", "t", "t")));

        Assert.Contains(bogus, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OnNonArrayRoot_ThrowsActionableError()
    {
        GameDataLoadException ex = Assert.Throws<GameDataLoadException>(
            () => DocsJsonParser.Parse("{\"not\":\"an array\"}", new GameDataMetadata("t", "t", "t")));

        Assert.Contains("array", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
