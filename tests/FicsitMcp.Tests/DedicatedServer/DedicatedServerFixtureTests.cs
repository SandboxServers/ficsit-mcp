using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Round-trips the captured wire fixtures under <c>Fixtures/DedicatedServer/</c> through the
/// source-generated <see cref="DedicatedServerJsonContext"/> against the DTO each one represents.
/// Without these, the fixtures were copied to output but read by ZERO tests — so a server-side wire
/// change (a renamed/removed field) drifted away from the DTOs undetected. These tests pin the
/// success envelopes, the error envelopes (including <c>errorData</c>), and the request envelopes
/// (including the <c>bSkipOnboarding</c> Unreal-prefix quirk) to the shapes the client deserializes.
/// </summary>
public sealed class DedicatedServerFixtureTests
{
    private static readonly string FixtureDir =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "DedicatedServer");

    private static byte[] Load(string fileName)
    {
        string path = Path.Combine(FixtureDir, fileName);
        Assert.True(File.Exists(path), $"Fixture '{fileName}' not found at '{path}'. Is it copied to output?");
        return File.ReadAllBytes(path);
    }

    // ----- Success response envelopes ------------------------------------------------------------

    [Fact]
    public void QueryServerStateSuccess_BindsAllServerGameStateFields()
    {
        DedicatedServerSuccessEnvelope<QueryServerStateResponse>? envelope = JsonSerializer.Deserialize(
            Load("response_queryserverstate_success.json"),
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeQueryServerStateResponse);

        ServerGameState state = envelope!.Data!.ServerGameState;
        Assert.Equal("My Session", state.ActiveSessionName);
        Assert.Equal(2, state.NumConnectedPlayers);
        Assert.Equal(4, state.PlayerLimit);
        Assert.Equal(5, state.TechTier);
        Assert.Equal("Schematic_Tier5_C", state.ActiveSchematic);
        Assert.Equal("GP_Project_Assembly_Phase_2", state.GamePhase);
        Assert.True(state.IsGameRunning);
        Assert.Equal(123456, state.TotalGameDuration);
        Assert.False(state.IsGamePaused);
        Assert.Equal(29.97, state.AverageTickRate, precision: 2);
        Assert.Equal("My Session", state.AutoLoadSessionName);
    }

    [Fact]
    public void EnumerateSessionsSuccess_BindsSessionsAndSaveHeaders()
    {
        DedicatedServerSuccessEnvelope<EnumerateSessionsResponse>? envelope = JsonSerializer.Deserialize(
            Load("response_enumeratesessions_success.json"),
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeEnumerateSessionsResponse);

        EnumerateSessionsResponse data = envelope!.Data!;
        Assert.Equal(0, data.CurrentSessionIndex);
        SessionSaveStruct session = Assert.Single(data.Sessions);
        Assert.Equal("My Session", session.SessionName);
        SaveHeader header = Assert.Single(session.SaveHeaders);
        Assert.Equal("AutoSave_0", header.SaveName);
        Assert.Equal(46, header.SaveVersion);
        Assert.Equal(368883, header.BuildVersion);
        Assert.Equal("Persistent_Level", header.MapName);
        Assert.Equal("My Session", header.SessionName);
        Assert.Equal(123456, header.PlayDurationSeconds);
        Assert.False(header.IsModdedSave);
        Assert.False(header.IsCreativeModeEnabled);
    }

    [Fact]
    public void HealthCheckSuccess_BindsHealth()
    {
        DedicatedServerSuccessEnvelope<HealthCheckResponse>? envelope = JsonSerializer.Deserialize(
            Load("response_healthcheck_success.json"),
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeHealthCheckResponse);

        Assert.Equal("healthy", envelope!.Data!.Health);
        Assert.Equal(string.Empty, envelope.Data.ServerCustomData);
    }

    // ----- Error envelopes -----------------------------------------------------------------------

    [Theory]
    [InlineData("response_error_wrong_password.json", "wrong_password")]
    [InlineData("response_error_server_claimed.json", "server_claimed")]
    [InlineData("response_error_save_game_load_failed.json", "save_game_load_failed")]
    public void ErrorFixtures_BindToErrorEnvelope_WithCodeAndMessage(string fileName, string expectedCode)
    {
        DedicatedServerErrorEnvelope? error = JsonSerializer.Deserialize(
            Load(fileName), DedicatedServerJsonContext.Default.DedicatedServerErrorEnvelope);

        Assert.NotNull(error);
        Assert.Equal(expectedCode, error.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
    }

    [Fact]
    public void SaveGameLoadFailedError_CarriesErrorData()
    {
        DedicatedServerErrorEnvelope? error = JsonSerializer.Deserialize(
            Load("response_error_save_game_load_failed.json"),
            DedicatedServerJsonContext.Default.DedicatedServerErrorEnvelope);

        Assert.NotNull(error!.ErrorData);
        Assert.Equal(JsonValueKind.Object, error.ErrorData!.Value.ValueKind);
        Assert.Equal("Missing", error.ErrorData.Value.GetProperty("saveName").GetString());
    }

    [Fact]
    public void ServerClaimedError_HasNoErrorData()
    {
        DedicatedServerErrorEnvelope? error = JsonSerializer.Deserialize(
            Load("response_error_server_claimed.json"),
            DedicatedServerJsonContext.Default.DedicatedServerErrorEnvelope);

        // This fixture omits errorData entirely (it is optional/free-form), so it must bind to null.
        Assert.Null(error!.ErrorData);
    }

    // ----- Request envelopes ---------------------------------------------------------------------

    [Fact]
    public void QueryServerStateRequest_HasFunction_AndNoData()
    {
        DedicatedServerRequestEnvelope? envelope = JsonSerializer.Deserialize(
            Load("request_queryserverstate.json"),
            DedicatedServerJsonContext.Default.DedicatedServerRequestEnvelope);

        Assert.Equal("QueryServerState", envelope!.Function);
        Assert.Null(envelope.Data);
    }

    [Fact]
    public void SaveGameRequest_CarriesTypedDataPayload()
    {
        DedicatedServerRequestEnvelope? envelope = JsonSerializer.Deserialize(
            Load("request_savegame.json"),
            DedicatedServerJsonContext.Default.DedicatedServerRequestEnvelope);

        Assert.Equal("SaveGame", envelope!.Function);
        Assert.Equal("AutoSave_0", envelope.Data!.Value.GetProperty("saveName").GetString());
    }

    [Fact]
    public void PasswordLoginRequest_SerializesPrivilegeLevelByName()
    {
        DedicatedServerRequestEnvelope? envelope = JsonSerializer.Deserialize(
            Load("request_passwordlogin.json"),
            DedicatedServerJsonContext.Default.DedicatedServerRequestEnvelope);

        Assert.Equal("PasswordLogin", envelope!.Function);
        Assert.Equal("Administrator", envelope.Data!.Value.GetProperty("minimumPrivilegeLevel").GetString());
        // The fixture's password is redacted, but the field must be present.
        Assert.True(envelope.Data.Value.TryGetProperty("password", out _));
    }

    [Fact]
    public void CreateNewGameRequest_UsesBSkipOnboardingQuirk_NotSkipOnboarding()
    {
        // The Unreal boolean prefix is a server quirk the wire must match exactly; the fixture pins it.
        DedicatedServerRequestEnvelope? envelope = JsonSerializer.Deserialize(
            Load("request_createnewgame.json"),
            DedicatedServerJsonContext.Default.DedicatedServerRequestEnvelope);

        Assert.Equal("CreateNewGame", envelope!.Function);
        JsonElement newGameData = envelope.Data!.Value.GetProperty("newGameData");
        Assert.True(newGameData.GetProperty("bSkipOnboarding").GetBoolean());
        Assert.False(newGameData.TryGetProperty("skipOnboarding", out _));
    }

    [Fact]
    public void CreateNewGameRequest_DataRoundTripsToTypedNewGameData()
    {
        // Beyond envelope shape, the data payload must bind to the typed NewGameData record (with the
        // bSkipOnboarding -> SkipOnboarding mapping intact).
        DedicatedServerRequestEnvelope? envelope = JsonSerializer.Deserialize(
            Load("request_createnewgame.json"),
            DedicatedServerJsonContext.Default.DedicatedServerRequestEnvelope);

        CreateNewGameRequest? typed = JsonSerializer.Deserialize(
            envelope!.Data!.Value.GetRawText(),
            DedicatedServerJsonContext.Default.CreateNewGameRequest);

        Assert.Equal("New World", typed!.NewGameData.SessionName);
        Assert.True(typed.NewGameData.SkipOnboarding);
    }
}
