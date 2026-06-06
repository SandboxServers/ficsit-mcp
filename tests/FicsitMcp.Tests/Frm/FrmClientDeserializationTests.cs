using System.Collections.Immutable;

using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Frm.Model;

namespace FicsitMcp.Tests.Frm;

/// <summary>
/// One fixture-deserialization test per consumed FRM endpoint, asserting the normalized record's
/// fields against the doc/source-derived fixture (FRM v1.4.10-437). Tight equality assertions.
/// </summary>
public sealed class FrmClientDeserializationTests
{
    [Fact]
    public async Task GetProdStats_Fixture_NormalizesRatesAndDropsDisplayString()
    {
        FrmClient client = FrmFixtures.ClientServing("getProdStats.json");

        ImmutableArray<FrmProdStatsItem> stats = await client.GetProdStatsAsync(CancellationToken.None);

        Assert.Equal(2, stats.Length);
        FrmProdStatsItem iron = stats[0];
        Assert.Equal("Desc_IronPlate_C", iron.ClassName);
        Assert.Equal("Iron Plate", iron.DisplayName);
        Assert.Equal(60.0, iron.CurrentProduced);
        Assert.Equal(80.0, iron.MaxProduced);
        Assert.Equal(45.0, iron.CurrentConsumed);
        Assert.Equal(90.0, iron.MaxConsumed);
        Assert.Equal(75.0, iron.ProducedPercent);
        Assert.Equal(50.0, iron.ConsumedPercent);
        Assert.Equal("Solid", iron.Form);
    }

    [Fact]
    public async Task GetFactory_Fixture_MapsRecipeRatesAndPowerFromNestedInfo()
    {
        FrmClient client = FrmFixtures.ClientServing("getFactory.json");

        ImmutableArray<FrmFactoryBuilding> buildings = await client.GetFactoryAsync(CancellationToken.None);

        Assert.Equal(2, buildings.Length);

        FrmFactoryBuilding constructor = buildings[0];
        Assert.Equal("Build_ConstructorMk1_C_2147480001", constructor.Id);
        Assert.Equal("Build_ConstructorMk1_C", constructor.ClassName);
        Assert.Equal("Recipe_IronPlate_C", constructor.RecipeClassName);
        Assert.True(constructor.IsProducing);
        Assert.False(constructor.IsPaused);
        Assert.Equal(4.0, constructor.PowerConsumedMw);
        Assert.False(constructor.FuseTriggered);

        FrmItemRate output = Assert.Single(constructor.Production);
        Assert.Equal("Desc_IronPlate_C", output.ClassName);
        Assert.Equal(20.0, output.CurrentRate);
        Assert.Equal(20.0, output.MaxRate);

        FrmItemRate input = Assert.Single(constructor.Ingredients);
        Assert.Equal("Desc_IronIngot_C", input.ClassName);
        Assert.Equal(30.0, input.CurrentRate);

        FrmFactoryBuilding assembler = buildings[1];
        Assert.False(assembler.IsConfigured);
        Assert.True(assembler.IsPaused);
        Assert.Equal(2, assembler.Somersloops);
        Assert.Equal(1, assembler.PowerShards);
    }

    [Fact]
    public async Task GetPower_Fixture_MapsCircuitGroupIdAndAssociatedCircuits()
    {
        FrmClient client = FrmFixtures.ClientServing("getPower.json");

        ImmutableArray<FrmPowerCircuit> circuits = await client.GetPowerAsync(CancellationToken.None);

        Assert.Equal(2, circuits.Length);

        FrmPowerCircuit healthy = circuits[0];
        Assert.Equal(0, healthy.CircuitGroupId);
        Assert.Equal(750.0, healthy.ProductionMw);
        Assert.Equal(420.5, healthy.ConsumedMw);
        Assert.Equal(900.0, healthy.CapacityMw);
        Assert.False(healthy.FuseTriggered);
        Assert.Equal([0, 5, 6], healthy.AssociatedCircuitIds.ToArray());

        FrmPowerCircuit tripped = circuits[1];
        Assert.Equal(7, tripped.CircuitGroupId);
        Assert.True(tripped.FuseTriggered);
    }

    [Fact]
    public async Task GetPlayers_Fixture_NormalizesPositionAndHealth()
    {
        FrmClient client = FrmFixtures.ClientServing("getPlayer.json");

        FrmPlayer player = Assert.Single(await client.GetPlayersAsync(CancellationToken.None));

        Assert.Equal("Pioneer", player.Name);
        Assert.True(player.Online);
        Assert.False(player.Dead);
        Assert.Equal(100.0, player.Health);
        Assert.Equal(18.5, player.SpeedKmh);
        // Coordinates rounded to whole centimetres; rotation kept to one decimal.
        FrmLocation location = Assert.IsType<FrmLocation>(player.Location);
        Assert.Equal(12346, location.X);
        Assert.Equal(-98765, location.Y);
        Assert.Equal(359.9, location.RotationDegrees);
    }

    [Fact]
    public async Task GetFactory_BuildingWithNoLocation_HasNullLocation()
    {
        FrmClient client = FrmFixtures.ClientServing("getFactory.json");

        ImmutableArray<FrmFactoryBuilding> buildings = await client.GetFactoryAsync(CancellationToken.None);

        // The constructor has a location; the assembler fixture row omits the `location` object.
        // Null must mean "FRM gave no location", distinct from a building at world origin.
        Assert.NotNull(buildings[0].Location);
        Assert.Null(buildings[1].Location);
    }

    [Fact]
    public async Task GetFactory_EmptyArray_ReturnsEmptyWithoutThrowing()
    {
        FrmClient client = FrmFixtures.ClientServing("empty.json");

        ImmutableArray<FrmFactoryBuilding> buildings = await client.GetFactoryAsync(CancellationToken.None);

        Assert.True(buildings.IsEmpty);
    }

    [Fact]
    public async Task GetResourceNodes_Fixture_MapsPurityAndExploitedFlag()
    {
        FrmClient client = FrmFixtures.ClientServing("getResourceNode.json");

        ImmutableArray<FrmResourceNode> nodes = await client.GetResourceNodesAsync(CancellationToken.None);

        Assert.Equal(2, nodes.Length);
        Assert.Equal("Desc_OreIron_C", nodes[0].ClassName);
        Assert.Equal("Pure", nodes[0].Purity);
        Assert.True(nodes[0].Exploited);
        Assert.Equal("Normal", nodes[1].Purity);
        Assert.False(nodes[1].Exploited);
    }
}
