using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.DedicatedServer.Settings;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Unit tests for <see cref="ServerSettingsMapper"/>: the typed-property↔wire-dictionary translation
/// both ways, applied-vs-pending shaping, lenient value parsing, unknown-key passthrough (including
/// untouched round-tripping), and new-game mapping/validation. Pure, no HTTP.
/// </summary>
public sealed class ServerSettingsMapperTests
{
    private static readonly IServerSettingsMapper Mapper = new ServerSettingsMapper();

    // ----- server options: wire -> typed --------------------------------------------------------

    [Fact]
    public void ToServerOptionsView_MapsKnownKeys_AndSplitsAppliedVsPending()
    {
        var applied = new Dictionary<string, string>
        {
            ["FG.DSAutoPause"] = "True",
            ["FG.AutosaveInterval"] = "300",
            ["FG.NetworkQuality"] = "3",
        };
        var pending = new Dictionary<string, string>
        {
            ["FG.AutosaveInterval"] = "600",
        };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(applied, pending));

        Assert.True(view.Applied.AutoPause);
        Assert.Equal(300, view.Applied.AutosaveIntervalSeconds);
        Assert.Equal(3, view.Applied.NetworkQuality);
        Assert.Equal(600, view.Pending.AutosaveIntervalSeconds);
        Assert.True(view.HasPendingChanges);
    }

    [Fact]
    public void ToServerOptionsView_NoPending_ReportsNoPendingChanges_NotNull()
    {
        var applied = new Dictionary<string, string> { ["FG.DSAutoPause"] = "False" };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(applied, new Dictionary<string, string>()));

        Assert.False(view.HasPendingChanges);
        Assert.NotNull(view.Pending);
        Assert.Null(view.Pending.AutoPause);
        Assert.Empty(view.Pending.Passthrough!);
    }

    [Fact]
    public void ToServerOptionsView_UnknownKey_LandsInPassthrough_Verbatim()
    {
        var applied = new Dictionary<string, string>
        {
            ["FG.DSAutoPause"] = "True",
            ["FG.SomeFutureOption"] = "hello",
        };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(applied, new Dictionary<string, string>()));

        Assert.True(view.Applied.AutoPause);
        Assert.Equal("hello", view.Applied.Passthrough!["FG.SomeFutureOption"]);
        // A modeled key must NOT leak into passthrough.
        Assert.False(view.Applied.Passthrough!.ContainsKey("FG.DSAutoPause"));
    }

    [Theory]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void ToServerOptionsView_ParsesBoolLeniently(string raw, bool expected)
    {
        var applied = new Dictionary<string, string> { ["FG.DSAutoPause"] = raw };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(applied, new Dictionary<string, string>()));

        Assert.Equal(expected, view.Applied.AutoPause);
    }

    [Fact]
    public void ToServerOptionsView_UnparseableModeledValue_PreservedInPassthrough_NotDropped()
    {
        var applied = new Dictionary<string, string> { ["FG.AutosaveInterval"] = "not-a-number" };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(applied, new Dictionary<string, string>()));

        Assert.Null(view.Applied.AutosaveIntervalSeconds);
        Assert.Equal("not-a-number", view.Applied.Passthrough!["FG.AutosaveInterval"]);
    }

    // ----- server options: typed -> wire --------------------------------------------------------

    [Fact]
    public void ToApplyServerOptionsRequest_EmitsOnlyNonNullFields()
    {
        var options = new ServerOptions(AutoPause: true, AutosaveIntervalSeconds: 120);

        ApplyServerOptionsRequest request = Mapper.ToApplyServerOptionsRequest(options);

        Assert.Equal("True", request.UpdatedServerOptions["FG.DSAutoPause"]);
        Assert.Equal("120", request.UpdatedServerOptions["FG.AutosaveInterval"]);
        Assert.False(request.UpdatedServerOptions.ContainsKey("FG.NetworkQuality"));
        Assert.Equal(2, request.UpdatedServerOptions.Count);
    }

    [Fact]
    public void ToApplyServerOptionsRequest_IncludesPassthrough_ButModeledKeyWins()
    {
        var options = new ServerOptions(
            AutoPause: true,
            Passthrough: new Dictionary<string, string>
            {
                ["FG.DSAutoPause"] = "False",       // collides with the modeled property
                ["FG.BrandNewOption"] = "xyz",      // genuinely unmodeled
            });

        ApplyServerOptionsRequest request = Mapper.ToApplyServerOptionsRequest(options);

        Assert.Equal("True", request.UpdatedServerOptions["FG.DSAutoPause"]); // modeled wins
        Assert.Equal("xyz", request.UpdatedServerOptions["FG.BrandNewOption"]);
    }

    [Fact]
    public void ServerOptions_RoundTrip_PreservesUnknownKeysUntouched()
    {
        // Read a map containing an unmodeled key, then write the typed result straight back.
        var original = new Dictionary<string, string>
        {
            ["FG.DSAutoPause"] = "True",
            ["FG.AutosaveInterval"] = "300",
            ["FG.ExperimentalToggle"] = "weird-value",
        };

        ServerOptionsView view = Mapper.ToServerOptionsView(
            new GetServerOptionsResponse(original, new Dictionary<string, string>()));
        ApplyServerOptionsRequest back = Mapper.ToApplyServerOptionsRequest(view.Applied);

        Assert.Equal("True", back.UpdatedServerOptions["FG.DSAutoPause"]);
        Assert.Equal("300", back.UpdatedServerOptions["FG.AutosaveInterval"]);
        Assert.Equal("weird-value", back.UpdatedServerOptions["FG.ExperimentalToggle"]);
    }

    // ----- advanced game settings ---------------------------------------------------------------

    [Fact]
    public void ToAdvancedGameSettingsView_MapsKnownKeys_AndCreativeFlag()
    {
        var ags = new Dictionary<string, string>
        {
            ["FG.PlayerRules.NoBuildCost"] = "True",
            ["FG.PlayerRules.GodMode"] = "True",
            ["FG.GameRules.SetGamePhase"] = "2",
            ["FG.GameRules.NoPower"] = "False",
        };

        AdvancedGameSettingsView view = Mapper.ToAdvancedGameSettingsView(
            new GetAdvancedGameSettingsResponse(true, ags));

        Assert.True(view.CreativeModeEnabled);
        Assert.True(view.Settings.NoBuildCost);
        Assert.True(view.Settings.GodMode);
        Assert.Equal(2, view.Settings.SetGamePhase);
        Assert.False(view.Settings.NoPower);
    }

    [Fact]
    public void ToAdvancedGameSettingsView_UnknownKey_LandsInPassthrough()
    {
        var ags = new Dictionary<string, string>
        {
            ["FG.PlayerRules.GodMode"] = "True",
            ["FG.GameRules.FutureCreativeLever"] = "9000",
        };

        AdvancedGameSettingsView view = Mapper.ToAdvancedGameSettingsView(
            new GetAdvancedGameSettingsResponse(true, ags));

        Assert.True(view.Settings.GodMode);
        Assert.Equal("9000", view.Settings.Passthrough!["FG.GameRules.FutureCreativeLever"]);
    }

    [Fact]
    public void ToApplyAdvancedGameSettingsRequest_EmitsOnlyNonNull_AndPassthrough()
    {
        var settings = new AdvancedGameSettings(
            NoBuildCost: true,
            FlightMode: true,
            Passthrough: new Dictionary<string, string> { ["FG.GameRules.FutureLever"] = "7" });

        ApplyAdvancedGameSettingsRequest request = Mapper.ToApplyAdvancedGameSettingsRequest(settings);

        Assert.Equal("True", request.AppliedAdvancedGameSettings["FG.PlayerRules.NoBuildCost"]);
        Assert.Equal("True", request.AppliedAdvancedGameSettings["FG.PlayerRules.FlightMode"]);
        Assert.Equal("7", request.AppliedAdvancedGameSettings["FG.GameRules.FutureLever"]);
        Assert.False(request.AppliedAdvancedGameSettings.ContainsKey("FG.PlayerRules.GodMode"));
    }

    // ----- create new game ----------------------------------------------------------------------

    [Fact]
    public void ToCreateNewGameRequest_MapsFields_AndFlattensAgs()
    {
        var settings = new NewGameSettings(
            SessionName: "Fresh Start",
            StartingLocation: "GrassFields",
            SkipOnboarding: true,
            AdvancedGameSettings: new AdvancedGameSettings(GodMode: true));

        CreateNewGameRequest request = Mapper.ToCreateNewGameRequest(settings);

        Assert.Equal("Fresh Start", request.NewGameData.SessionName);
        Assert.Equal("GrassFields", request.NewGameData.StartingLocation);
        Assert.True(request.NewGameData.SkipOnboarding);
        Assert.NotNull(request.NewGameData.AdvancedGameSettings);
        Assert.Equal("True", request.NewGameData.AdvancedGameSettings!["FG.PlayerRules.GodMode"]);
    }

    [Fact]
    public void ToCreateNewGameRequest_NoAgs_LeavesAgsNull_ForAchievementEligibility()
    {
        var settings = new NewGameSettings(SessionName: "Vanilla Run");

        CreateNewGameRequest request = Mapper.ToCreateNewGameRequest(settings);

        Assert.Null(request.NewGameData.AdvancedGameSettings);
    }

    [Fact]
    public void ToCreateNewGameRequest_EmptyAgs_FlattensToNull_NotEmptyMap()
    {
        // An all-null AGS with no passthrough must not send an empty creative map (which would needlessly
        // flag the save); it should be omitted entirely.
        var settings = new NewGameSettings(
            SessionName: "Still Vanilla",
            AdvancedGameSettings: new AdvancedGameSettings());

        CreateNewGameRequest request = Mapper.ToCreateNewGameRequest(settings);

        Assert.Null(request.NewGameData.AdvancedGameSettings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToCreateNewGameRequest_BlankSessionName_Throws(string blank)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Mapper.ToCreateNewGameRequest(new NewGameSettings(SessionName: blank)));
    }
}
