using FicsitMcp.Domain.DedicatedServer.Settings;
using FicsitMcp.Tools;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Tests the thin settings tools against a fake <see cref="FakeDedicatedServerApiClient"/> behind the
/// interface: delegation, the set-then-get round-trip the issue's acceptance criteria require, the
/// pending-vs-applied surfacing, and create_new_game validation/delegation.
/// </summary>
public sealed class ServerSettingsToolTests
{
    private static readonly IServerSettingsMapper Mapper = new ServerSettingsMapper();

    // ----- get_server_options -------------------------------------------------------------------

    [Fact]
    public async Task GetServerOptions_SurfacesAppliedAndPendingSeparately()
    {
        var client = new FakeDedicatedServerApiClient();
        client.SeedAppliedOptions(new Dictionary<string, string>
        {
            ["FG.DSAutoPause"] = "True",
            ["FG.AutosaveInterval"] = "300",
        });
        client.SeedPendingOptions(new Dictionary<string, string>
        {
            ["FG.AutosaveInterval"] = "600",
        });

        ServerOptionsView view = await ServerSettingsTool.GetServerOptions(client, Mapper, default);

        Assert.True(view.Applied.AutoPause);
        Assert.Equal(300, view.Applied.AutosaveIntervalSeconds);
        Assert.Equal(600, view.Pending.AutosaveIntervalSeconds);
        Assert.True(view.HasPendingChanges);
    }

    // ----- set_server_options (round-trip) ------------------------------------------------------

    [Fact]
    public async Task SetServerOptions_AppliesPatch_AndReturnedViewReflectsTheChange()
    {
        var client = new FakeDedicatedServerApiClient();
        client.SeedAppliedOptions(new Dictionary<string, string>
        {
            ["FG.DSAutoPause"] = "False",
            ["FG.AutosaveInterval"] = "300",
        });

        ServerOptionsView after = await ServerSettingsTool.SetServerOptions(
            client,
            Mapper,
            new ServerOptions(AutoPause: true),
            default);

        // The apply request carried only the patched field.
        Assert.NotNull(client.LastApplyServerOptions);
        Assert.Single(client.LastApplyServerOptions!.UpdatedServerOptions);
        Assert.Equal("True", client.LastApplyServerOptions!.UpdatedServerOptions["FG.DSAutoPause"]);

        // The returned (re-read) view reflects the change AND leaves the untouched option alone.
        Assert.True(after.Applied.AutoPause);
        Assert.Equal(300, after.Applied.AutosaveIntervalSeconds);
    }

    [Fact]
    public async Task SetServerOptions_Passthrough_RoundTripsUnknownKey()
    {
        var client = new FakeDedicatedServerApiClient();

        ServerOptionsView after = await ServerSettingsTool.SetServerOptions(
            client,
            Mapper,
            new ServerOptions(Passthrough: new Dictionary<string, string> { ["FG.FutureKnob"] = "42" }),
            default);

        Assert.Equal("42", client.LastApplyServerOptions!.UpdatedServerOptions["FG.FutureKnob"]);
        Assert.Equal("42", after.Applied.Passthrough!["FG.FutureKnob"]);
    }

    // ----- advanced game settings ---------------------------------------------------------------

    [Fact]
    public async Task GetAdvancedGameSettings_SurfacesSettingsAndCreativeFlag()
    {
        var client = new FakeDedicatedServerApiClient();
        client.SeedAdvancedGameSettings(
            new Dictionary<string, string> { ["FG.PlayerRules.GodMode"] = "True" },
            creativeModeEnabled: true);

        AdvancedGameSettingsView view = await ServerSettingsTool.GetAdvancedGameSettings(client, Mapper, default);

        Assert.True(view.CreativeModeEnabled);
        Assert.True(view.Settings.GodMode);
    }

    [Fact]
    public async Task SetAdvancedGameSettings_AppliesPatch_FlipsCreativeFlag_AndReReadReflectsIt()
    {
        var client = new FakeDedicatedServerApiClient();
        client.SeedAdvancedGameSettings(new Dictionary<string, string>(), creativeModeEnabled: false);

        AdvancedGameSettingsView after = await ServerSettingsTool.SetAdvancedGameSettings(
            client,
            Mapper,
            new AdvancedGameSettings(NoBuildCost: true),
            default);

        Assert.Equal("True", client.LastApplyAdvancedGameSettings!.AppliedAdvancedGameSettings["FG.PlayerRules.NoBuildCost"]);
        // Applying AGS permanently flags the session — the re-read view shows it.
        Assert.True(after.CreativeModeEnabled);
        Assert.True(after.Settings.NoBuildCost);
    }

    // ----- create_new_game ----------------------------------------------------------------------

    [Fact]
    public async Task CreateNewGame_DelegatesWithMappedRequest()
    {
        var client = new FakeDedicatedServerApiClient();

        await ServerSettingsTool.CreateNewGame(
            client,
            Mapper,
            new NewGameSettings(SessionName: "New World", SkipOnboarding: true),
            default);

        Assert.Equal(1, client.CreateNewGameCallCount);
        Assert.Equal("New World", client.LastCreateNewGame!.NewGameData.SessionName);
        Assert.True(client.LastCreateNewGame!.NewGameData.SkipOnboarding);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateNewGame_BlankSessionName_Throws_BeforeAnyWireCall(string blank)
    {
        var client = new FakeDedicatedServerApiClient();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => ServerSettingsTool.CreateNewGame(client, Mapper, new NewGameSettings(SessionName: blank), default));

        Assert.Equal(0, client.CreateNewGameCallCount);
    }
}
