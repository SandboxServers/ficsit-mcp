using System.Net;
using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Protocol-level tests for <see cref="DedicatedServerApiClient"/>: envelope shape per function,
/// bearer-auth attach, the per-request AllowRetry opt-in (set ONLY on idempotent functions),
/// error-envelope mapping, and 401 re-auth/replay semantics including the non-idempotent ambiguity.
/// </summary>
public sealed class DedicatedServerApiClientTests
{
    // ----- Envelope shape ------------------------------------------------------------------------

    [Fact]
    public async Task QueryServerState_PostsFunctionEnvelope_ToApiV1_AndParsesNestedState()
    {
        // Arrange
        const string data =
            "{\"serverGameState\":{\"activeSessionName\":\"My Session\",\"numConnectedPlayers\":2," +
            "\"playerLimit\":4,\"techTier\":5,\"activeSchematic\":\"\",\"gamePhase\":\"\"," +
            "\"isGameRunning\":true,\"totalGameDuration\":1000,\"isGamePaused\":false," +
            "\"averageTickRate\":30.0,\"autoLoadSessionName\":\"My Session\"}}";
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope(data));

        // Act
        QueryServerStateResponse response = await client.QueryServerStateAsync();

        // Assert: one POST to /api/v1 carrying { "function": "QueryServerState" }.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1", request.RequestUri!.AbsolutePath);
        Assert.Equal("QueryServerState", request.Function);
        Assert.Equal("application/json", request.ContentType);
        // The nested serverGameState sub-envelope is unwrapped to the typed record.
        Assert.Equal(2, response.ServerGameState.NumConnectedPlayers);
        Assert.True(response.ServerGameState.IsGameRunning);
        Assert.Equal("My Session", response.ServerGameState.ActiveSessionName);
    }

    [Fact]
    public async Task SaveGame_PostsFunctionAndDataFields_WithCamelCaseNames()
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.NoContent());

        // Act
        await client.SaveGameAsync(new SaveGameRequest("AutoSave_0"));

        // Assert: data carries saveName (camelCase) with the value.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("SaveGame", request.Function);
        JsonElement data = request.Data!.Value;
        Assert.Equal("AutoSave_0", data.GetProperty("saveName").GetString());
    }

    [Fact]
    public async Task CreateNewGame_UsesBSkipOnboardingFieldName()
    {
        // Arrange: the server quirk requires the Unreal-prefixed bSkipOnboarding, not skipOnboarding.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.NoContent());

        // Act
        await client.CreateNewGameAsync(new CreateNewGameRequest(
            new NewGameData("Fresh", SkipOnboarding: true)));

        // Assert
        CapturedRequest request = Assert.Single(handler.Requests);
        JsonElement newGameData = request.Data!.Value.GetProperty("newGameData");
        Assert.True(newGameData.GetProperty("bSkipOnboarding").GetBoolean());
        Assert.False(newGameData.TryGetProperty("skipOnboarding", out _));
    }

    [Fact]
    public async Task PasswordLogin_SerializesPrivilegeLevelByName()
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithoutToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope("{\"authenticationToken\":\"tok-123\"}"));

        // Act
        AuthenticationTokenResponse response = await client.PasswordLoginAsync(
            new PasswordLoginRequest(ApiPrivilegeLevel.Administrator, "hunter2"));

        // Assert: enum serialized as the string name; token returned.
        CapturedRequest request = Assert.Single(handler.Requests);
        JsonElement data = request.Data!.Value;
        Assert.Equal("Administrator", data.GetProperty("minimumPrivilegeLevel").GetString());
        Assert.Equal("tok-123", response.AuthenticationToken);
    }

    // ----- Secret handling -----------------------------------------------------------------------

    [Fact]
    public async Task PasswordLogin_NeverLeaksPasswordInException()
    {
        // Arrange: the server rejects the password.
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithoutToken((_, _) =>
                DedicatedServerTestHarness.ErrorEnvelope(
                    HttpStatusCode.Unauthorized, "wrong_password", "Incorrect password"));

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAnyAsync<DedicatedServerApiException>(
            () => client.PasswordLoginAsync(new PasswordLoginRequest(ApiPrivilegeLevel.Administrator, "s3cr3t")));

        // Assert: the password text never appears anywhere in the surfaced error.
        Assert.DoesNotContain("s3cr3t", ex.ToString(), StringComparison.Ordinal);
        Assert.Equal("wrong_password", ex.ErrorCode);
    }

    // ----- Auth header attach --------------------------------------------------------------------

    [Fact]
    public async Task AuthenticatedCall_AttachesConfigToken_AsBearer()
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"serverOptions\":{},\"pendingServerOptions\":{}}"));

        // Act
        await client.GetServerOptionsAsync();

        // Assert
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Equal(DedicatedServerTestHarness.ConfigToken, request.AuthorizationToken);
    }

    [Fact]
    public async Task UnauthenticatedFunction_DoesNotAttachBearer()
    {
        // Arrange: HealthCheck requires no auth.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"health\":\"healthy\",\"serverCustomData\":\"\"}"));

        // Act
        HealthCheckResponse response = await client.HealthCheckAsync();

        // Assert
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Null(request.AuthorizationScheme);
        Assert.Equal("healthy", response.Health);
    }

    [Fact]
    public async Task AuthenticatedCall_WithNoToken_ThrowsAuthException_WithoutSending()
    {
        // Arrange: no config token, no prior login.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithoutToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope("{}"));

        // Act + Assert: fails fast with an actionable auth error and never hits the wire.
        DedicatedServerAuthException ex = await Assert.ThrowsAsync<DedicatedServerAuthException>(
            () => client.QueryServerStateAsync());
        Assert.Equal("no_credentials", ex.ErrorCode);
        Assert.Equal(0, handler.AttemptCount);
    }

    // ----- AllowRetry opt-in (idempotent functions ONLY) -----------------------------------------

    [Theory]
    [InlineData("QueryServerState")]
    [InlineData("HealthCheck")]
    [InlineData("VerifyAuthenticationToken")]
    [InlineData("EnumerateSessions")]
    public async Task IdempotentFunctions_SetAllowRetryOption(string function)
    {
        // Arrange: respond appropriately per function (204 for verify, envelope otherwise).
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((req, _) => req.Function switch
            {
                "VerifyAuthenticationToken" => DedicatedServerTestHarness.NoContent(),
                "HealthCheck" => DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"health\":\"healthy\",\"serverCustomData\":\"\"}"),
                "EnumerateSessions" => DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"sessions\":[],\"currentSessionIndex\":-1}"),
                _ => DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"serverGameState\":{\"activeSessionName\":\"\",\"numConnectedPlayers\":0," +
                    "\"playerLimit\":4,\"techTier\":0,\"activeSchematic\":\"\",\"gamePhase\":\"\"," +
                    "\"isGameRunning\":false,\"totalGameDuration\":0,\"isGamePaused\":false," +
                    "\"averageTickRate\":0.0,\"autoLoadSessionName\":\"\"}}"),
            });

        // Act
        await InvokeByName(client, function);

        // Assert: the idempotent function opted into retries.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.True(request.AllowRetryOptionSet, $"{function} should set AllowRetry");
    }

    [Theory]
    [InlineData("SaveGame")]
    [InlineData("LoadGame")]
    [InlineData("RunCommand")]
    [InlineData("Shutdown")]
    [InlineData("DeleteSaveFile")]
    [InlineData("ApplyServerOptions")]
    [InlineData("ClaimServer")]
    public async Task NonIdempotentFunctions_NeverSetAllowRetryOption(string function)
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((req, _) => req.Function switch
            {
                "RunCommand" => DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"commandResult\":\"ok\",\"returnValue\":true}"),
                "ClaimServer" => DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"authenticationToken\":\"new-tok\"}"),
                _ => DedicatedServerTestHarness.NoContent(),
            });

        // Act
        await InvokeByName(client, function);

        // Assert: a state-changing function must NOT opt into retries.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.False(request.AllowRetryOptionSet, $"{function} must NOT set AllowRetry");
    }

    // ----- Error envelope mapping ----------------------------------------------------------------

    [Fact]
    public async Task ErrorEnvelope_MapsToTypedException_CarryingCodeAndMessage()
    {
        // Arrange
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.ErrorEnvelope(
                    HttpStatusCode.InternalServerError, "save_game_load_failed", "Could not load"));

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAsync<DedicatedServerApiException>(
            () => client.LoadGameAsync(new LoadGameRequest("Missing")));

        // Assert
        Assert.Equal("save_game_load_failed", ex.ErrorCode);
        Assert.Equal("Could not load", ex.ServerMessage);
        Assert.Equal(500, ex.HttpStatusCode);
    }

    [Fact]
    public async Task ClaimServer_OnAlreadyClaimed_ThrowsServerClaimed()
    {
        // Arrange: claiming is one-shot; a claimed server rejects it.
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.ErrorEnvelope(
                    HttpStatusCode.Forbidden, "server_claimed", "Already claimed"));

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAsync<DedicatedServerApiException>(
            () => client.ClaimServerAsync(new ClaimServerRequest("Name", "pw")));

        // Assert
        Assert.Equal("server_claimed", ex.ErrorCode);
    }

    // ----- 401 re-auth / replay semantics --------------------------------------------------------

    [Fact]
    public async Task Idempotent401_WithConfigToken_ReauthsAndReplaysOnce_ThenSucceeds()
    {
        // Arrange: first attempt 401, second (replay) succeeds. Config token is re-presentable.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, attempt) => attempt == 1
                ? DedicatedServerTestHarness.Unauthorized()
                : DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"sessions\":[],\"currentSessionIndex\":-1}"));

        // Act
        EnumerateSessionsResponse response = await client.EnumerateSessionsAsync();

        // Assert: exactly two attempts (original + one replay), both bearing the token.
        Assert.Equal(2, handler.AttemptCount);
        Assert.All(handler.Requests, r => Assert.Equal(DedicatedServerTestHarness.ConfigToken, r.AuthorizationToken));
        Assert.Empty(response.Sessions);
    }

    [Fact]
    public async Task Idempotent401_ReauthFailsAgain_SurfacesAuthException()
    {
        // Arrange: every attempt 401, even after re-auth.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.Unauthorized());

        // Act
        await Assert.ThrowsAsync<DedicatedServerAuthException>(() => client.EnumerateSessionsAsync());

        // Assert: it tried once, re-authed, replayed once — then gave up (no infinite loop).
        Assert.Equal(2, handler.AttemptCount);
    }

    [Fact]
    public async Task NonIdempotent401_DoesNotReplay_SurfacesAmbiguousResult()
    {
        // Arrange: a 401 on RunCommand. The command may already have executed before the token was
        // rejected, so the client must NOT replay — it surfaces the ambiguity instead.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.Unauthorized());

        // Act
        DedicatedServerAmbiguousResultException ex =
            await Assert.ThrowsAsync<DedicatedServerAmbiguousResultException>(
                () => client.RunCommandAsync(new RunCommandRequest("Stat FPS")));

        // Assert: only ONE attempt was made (no replay), and the function is named.
        Assert.Equal(1, handler.AttemptCount);
        Assert.Equal("RunCommand", ex.FunctionName);
    }

    [Fact]
    public async Task Idempotent401_WithoutConfigToken_CannotReauth_SurfacesAuth()
    {
        // Arrange: login first to get a session token (no config token), then the server 401s. The
        // client cannot silently re-derive a session token (no retained password), so it surfaces 401.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithoutToken((req, _) => req.Function == "PasswordLogin"
                ? DedicatedServerTestHarness.SuccessEnvelope("{\"authenticationToken\":\"session-tok\"}")
                : DedicatedServerTestHarness.Unauthorized());

        await client.PasswordLoginAsync(new PasswordLoginRequest(ApiPrivilegeLevel.Administrator, "pw"));

        // Act
        await Assert.ThrowsAsync<DedicatedServerAuthException>(() => client.QueryServerStateAsync());

        // Assert: login (1) + the single QueryServerState attempt (1) = 2; the query did NOT replay.
        Assert.Equal(2, handler.AttemptCount);
        Assert.Equal("session-tok", handler.Requests[1].AuthorizationToken);
    }

    [Fact]
    public async Task PasswordLogin_CachesToken_ForSubsequentAuthenticatedCalls()
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithoutToken((req, _) => req.Function == "PasswordLogin"
                ? DedicatedServerTestHarness.SuccessEnvelope("{\"authenticationToken\":\"logged-in\"}")
                : DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"serverGameState\":{\"activeSessionName\":\"\",\"numConnectedPlayers\":0," +
                    "\"playerLimit\":4,\"techTier\":0,\"activeSchematic\":\"\",\"gamePhase\":\"\"," +
                    "\"isGameRunning\":false,\"totalGameDuration\":0,\"isGamePaused\":false," +
                    "\"averageTickRate\":0.0,\"autoLoadSessionName\":\"\"}}"));

        // Act
        await client.PasswordLoginAsync(new PasswordLoginRequest(ApiPrivilegeLevel.Administrator, "pw"));
        await client.QueryServerStateAsync();

        // Assert: the second call used the freshly-cached login token.
        Assert.Equal("logged-in", handler.Requests[1].AuthorizationToken);
    }

    // ----- Multipart upload ----------------------------------------------------------------------

    [Fact]
    public async Task UploadSaveGame_SendsMultipart_WithJsonDataAndBinaryParts()
    {
        // Arrange
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.NoContent());
        using var save = new MemoryStream("SAVEBYTES"u8.ToArray());

        // Act
        await client.UploadSaveGameAsync(new UploadSaveGameRequest("Uploaded", LoadSaveGame: true), save);

        // Assert: multipart content type and the three named parts present in the raw body.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.StartsWith("multipart/form-data", request.ContentType, StringComparison.Ordinal);
        Assert.Equal("Bearer", request.AuthorizationScheme);
        Assert.Contains("name=data", request.RawBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=saveGameFile", request.RawBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=_charset_", request.RawBody!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SAVEBYTES", request.RawBody!, StringComparison.Ordinal);
    }

    // ----- Streaming download --------------------------------------------------------------------

    [Fact]
    public async Task DownloadSaveGame_StreamsBinaryBody_ToDestination()
    {
        // Arrange: a direct binary response (not a JSON envelope).
        byte[] payload = "BINARY-SAVE-CONTENT"u8.ToArray();
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.Binary(payload));
        using var destination = new MemoryStream();

        // Act
        await client.DownloadSaveGameAsync(new DownloadSaveGameRequest("ToDownload"), destination);

        // Assert
        Assert.Equal(payload, destination.ToArray());
    }

    [Fact]
    public async Task DownloadSaveGame_OnErrorEnvelope_Throws_NotWriteGarbage()
    {
        // Arrange: the server returns a JSON error envelope instead of a file.
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.ErrorEnvelope(
                    HttpStatusCode.NotFound, "file_not_found", "No such save"));
        using var destination = new MemoryStream();

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAsync<DedicatedServerApiException>(
            () => client.DownloadSaveGameAsync(new DownloadSaveGameRequest("Nope"), destination));

        // Assert: the error mapped and nothing was written to the destination.
        Assert.Equal("file_not_found", ex.ErrorCode);
        Assert.Equal(0, destination.Length);
    }

    // ----- Open-set escape hatch (for #11 FRM passthrough) ---------------------------------------

    [Fact]
    public async Task InvokeRaw_AllowsArbitraryFunctionName_AndReturnsRawData()
    {
        // Arrange: a mod-registered function (FRM's "frm") the typed methods do not cover.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope("{\"factories\":42}"));

        using var requestData = JsonDocument.Parse("{\"endpoint\":\"getFactory\"}");

        // Act
        JsonElement? result = await client.InvokeRawAsync(
            "frm", requestData.RootElement, allowRetry: true);

        // Assert: the arbitrary function name went out verbatim, opted into retry, and the raw data
        // payload (under "data") came back.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal("frm", request.Function);
        Assert.True(request.AllowRetryOptionSet);
        Assert.Equal(42, result!.Value.GetProperty("factories").GetInt32());
    }

    [Fact]
    public async Task InvokeRaw_DefaultsToNoRetry_ForUnknownFunction()
    {
        // Arrange: an unknown mod function with no idempotency guarantee defaults to no-retry.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.NoContent());

        // Act
        JsonElement? result = await client.InvokeRawAsync("someModWrite", data: null);

        // Assert
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.False(request.AllowRetryOptionSet);
        Assert.Null(result);
    }

    // ----- G2/G9: double-401 surfaces the config-token-rejected error ----------------------------

    [Fact]
    public async Task Idempotent401_ConfigTokenAlsoRejected_SurfacesConfigTokenRejected()
    {
        // Arrange: every attempt 401, so the re-presented CONFIG token is rejected too (rotated/
        // revoked server-side). The error must say so specifically, not a generic 401.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.Unauthorized());

        // Act
        DedicatedServerAuthException ex = await Assert.ThrowsAsync<DedicatedServerAuthException>(
            () => client.EnumerateSessionsAsync());

        // Assert: original + one replay = 2 attempts; the code names the config-token rejection and
        // the remedy env var, never leaking the token value itself.
        Assert.Equal(2, handler.AttemptCount);
        Assert.Equal("config_token_rejected", ex.ErrorCode);
        Assert.Contains("FICSITMCP_DedicatedServer__AdminToken", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(DedicatedServerTestHarness.ConfigToken, ex.ToString(), StringComparison.Ordinal);
    }

    // ----- G4/G10: download fail-fast on unexpected content-types --------------------------------

    [Fact]
    public async Task DownloadSaveGame_OnTextPlainBody_FailsFast_NamingContentType()
    {
        // Arrange: a reverse proxy / gateway returns a text/plain error instead of a binary save or a
        // JSON envelope. The old code would have streamed it as the "save". It must fail fast.
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.Text(HttpStatusCode.BadGateway, "upstream timed out"));
        using var destination = new MemoryStream();

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAsync<DedicatedServerApiException>(
            () => client.DownloadSaveGameAsync(new DownloadSaveGameRequest("Whatever"), destination));

        // Assert: the failure names the content type and nothing was written.
        Assert.Contains("text/plain", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task DownloadSaveGame_OnUnexpectedJsonSuccess_FailsFast_NotEmptyDownload()
    {
        // Arrange: server returns a JSON success-shaped body (no errorCode) with application/json on a
        // function that should produce a BINARY save. That is an unexpected shape — fail fast rather
        // than silently returning having written nothing (C4).
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.JsonOk("{\"data\":{\"unexpected\":true}}"));
        using var destination = new MemoryStream();

        // Act
        DedicatedServerApiException ex = await Assert.ThrowsAsync<DedicatedServerApiException>(
            () => client.DownloadSaveGameAsync(new DownloadSaveGameRequest("Whatever"), destination));

        // Assert
        Assert.Equal("unexpected_response_shape", ex.ErrorCode);
        Assert.Equal(0, destination.Length);
    }

    // ----- G11: SetAdminPassword rejection does NOT adopt the new token --------------------------

    [Fact]
    public async Task SetAdminPassword_WhenServerRejects_ThrowsAndDoesNotAdoptNewToken()
    {
        // Arrange: the change is attempted with the OLD config token; the server rejects it (e.g.
        // wrong old password / insufficient privilege). The client must NOT adopt the would-be new
        // token, so a subsequent authenticated call still presents the ORIGINAL config token.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((req, _) => req.Function == "SetAdminPassword"
                ? DedicatedServerTestHarness.ErrorEnvelope(
                    HttpStatusCode.Forbidden, "insufficient_privilege", "Not allowed")
                : DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"sessions\":[],\"currentSessionIndex\":-1}"));

        // Act: the set fails...
        await Assert.ThrowsAnyAsync<DedicatedServerApiException>(() =>
            client.SetAdminPasswordAsync(
                new SetAdminPasswordRequest("newpw", AuthenticationToken: "rejected-new-token")));

        // ...and a follow-up authenticated call still uses the original token (no adoption happened).
        await client.EnumerateSessionsAsync();

        // Assert
        CapturedRequest followUp = handler.Requests[^1];
        Assert.Equal("EnumerateSessions", followUp.Function);
        Assert.Equal(DedicatedServerTestHarness.ConfigToken, followUp.AuthorizationToken);
    }

    [Fact]
    public async Task SetAdminPassword_OnSuccess_Adopts204Contract_NewTokenUsedNext()
    {
        // Arrange: SetAdminPassword returns 204 (the current OAS contract — no body; the new token is
        // the one the caller supplied). G3: this asserts that contract so a future server body change
        // that breaks the synthetic-response assumption is caught.
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((req, _) => req.Function == "SetAdminPassword"
                ? DedicatedServerTestHarness.NoContent()
                : DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"sessions\":[],\"currentSessionIndex\":-1}"));

        // Act
        AuthenticationTokenResponse response = await client.SetAdminPasswordAsync(
            new SetAdminPasswordRequest("newpw", AuthenticationToken: "brand-new-token"));
        await client.EnumerateSessionsAsync();

        // Assert: the request carried the OLD token; the response echoes the new token; the follow-up
        // call adopts the new token.
        Assert.Equal(DedicatedServerTestHarness.ConfigToken, handler.Requests[0].AuthorizationToken);
        Assert.Equal("brand-new-token", response.AuthenticationToken);
        Assert.Equal("brand-new-token", handler.Requests[^1].AuthorizationToken);
    }

    // ----- G12: concurrent token adoption stays consistent (no torn state) -----------------------

    [Fact]
    public async Task ConcurrentLogins_AdoptTokenConsistently_NoTornState()
    {
        // Arrange: many parallel PasswordLogins race on the token lock. Each returns the same token;
        // whatever the interleaving, the cache must end on a valid token and a later authenticated
        // call must present it (no torn/empty state from the race).
        (DedicatedServerApiClient client, _) =
            DedicatedServerTestHarness.CreateWithoutToken((req, _) => req.Function == "PasswordLogin"
                ? DedicatedServerTestHarness.SuccessEnvelope("{\"authenticationToken\":\"raced-token\"}")
                : DedicatedServerTestHarness.SuccessEnvelope(
                    "{\"sessions\":[],\"currentSessionIndex\":-1}"));

        // Act: 32 concurrent logins.
        Task[] logins = Enumerable.Range(0, 32)
            .Select(_ => client.PasswordLoginAsync(
                new PasswordLoginRequest(ApiPrivilegeLevel.Administrator, "pw")))
            .ToArray();
        await Task.WhenAll(logins);

        // Assert: the cache is consistent — the next authenticated call succeeds with the adopted
        // token (a torn/empty cache would throw no_credentials before sending).
        EnumerateSessionsResponse sessions = await client.EnumerateSessionsAsync();
        Assert.Empty(sessions.Sessions);
    }

    // ----- G13: InvokeRaw JSON-encodes a special-character function name -------------------------

    [Fact]
    public async Task InvokeRaw_FunctionNameWithSpecialCharacters_IsCorrectlyJsonEncoded()
    {
        // Arrange: a (pathological) mod function name containing quotes, a backslash, and unicode.
        // It must be JSON-encoded in the envelope's "function" field and round-trip verbatim, never
        // corrupting the envelope.
        const string weirdFunction = "mod\"weird\\nameé☃";
        (DedicatedServerApiClient client, RecordingHandler handler) =
            DedicatedServerTestHarness.CreateWithConfigToken((_, _) =>
                DedicatedServerTestHarness.SuccessEnvelope("{\"ok\":true}"));

        // Act
        await client.InvokeRawAsync(weirdFunction, data: null);

        // Assert: the parsed envelope's function field equals the original string exactly.
        CapturedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(weirdFunction, request.Function);
    }

    // ----- helpers -------------------------------------------------------------------------------

    private static Task InvokeByName(DedicatedServerApiClient client, string function) => function switch
    {
        "QueryServerState" => client.QueryServerStateAsync(),
        "HealthCheck" => client.HealthCheckAsync(),
        "VerifyAuthenticationToken" => client.VerifyAuthenticationTokenAsync(),
        "EnumerateSessions" => client.EnumerateSessionsAsync(),
        "SaveGame" => client.SaveGameAsync(new SaveGameRequest("S")),
        "LoadGame" => client.LoadGameAsync(new LoadGameRequest("S")),
        "RunCommand" => client.RunCommandAsync(new RunCommandRequest("cmd")),
        "Shutdown" => client.ShutdownAsync(),
        "DeleteSaveFile" => client.DeleteSaveFileAsync(new DeleteSaveFileRequest("S")),
        "ApplyServerOptions" => client.ApplyServerOptionsAsync(
            new ApplyServerOptionsRequest(new Dictionary<string, string>())),
        "ClaimServer" => client.ClaimServerAsync(new ClaimServerRequest("N", "pw")),
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, "Unmapped test function."),
    };
}
