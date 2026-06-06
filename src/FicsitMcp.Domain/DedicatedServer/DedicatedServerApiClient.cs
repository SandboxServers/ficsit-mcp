using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.Http;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// Default <see cref="IDedicatedServerApiClient"/>. Owns ALL protocol knowledge for the surface:
/// the single <c>POST {BaseUrl}/api/v1</c> endpoint, the function envelope, bearer-token attach,
/// transparent 401 re-auth-and-replay, error-envelope→typed-exception mapping, multipart upload,
/// and streaming download. Built on the infrastructure <see cref="SurfaceHttpClient"/> shell (which
/// owns TLS/TOFU and transport-fault mapping) — it NEVER constructs an <see cref="HttpClient"/> or
/// adds resilience.
/// </summary>
/// <remarks>
/// <para>
/// Auth precedence: a pre-provisioned API token from config (<see cref="DedicatedServerOptions.AdminToken"/>,
/// the <c>server.GenerateAPIToken</c> token) is preferred and used directly — password login is
/// skipped entirely. With no config token, the client has no implicit password to log in with, so
/// authenticated calls fail with an actionable <see cref="DedicatedServerAuthException"/> until a
/// caller establishes a token via <see cref="PasswordLoginAsync"/> / <see cref="ClaimServerAsync"/>.
/// </para>
/// <para>
/// 401 handling: on a rejected token the client re-authenticates ONCE and replays — but only for
/// idempotent functions. For a non-idempotent function whose delivery is ambiguous, it surfaces
/// <see cref="DedicatedServerAmbiguousResultException"/> instead of blind-replaying a side effect.
/// Re-auth is only possible against a config token (re-presenting it); a session token obtained via
/// password login cannot be silently re-derived (no password is retained), so that case surfaces as
/// an auth error for the caller to re-login.
/// </para>
/// </remarks>
public sealed class DedicatedServerApiClient : IDedicatedServerApiClient
{
    private const string ApiPath = "/api/v1";
    private const string MultipartCharset = "utf-8";

    private readonly SurfaceHttpClient _http;
    private readonly DedicatedServerOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // The currently-cached bearer token (config token at construction, or a login/claim token
    // adopted at runtime). Guarded by _tokenLock for writes. Never logged.
    private string? _cachedToken;

    /// <summary>
    /// Creates the client over the configured surface client and options. Seeds the token cache with
    /// the config API token when present (the preferred credential).
    /// </summary>
    public DedicatedServerApiClient(SurfaceHttpClient http, DedicatedServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options;
        _cachedToken = options.AdminToken.Reveal();
    }

    // ----- Authentication ------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> PasswordlessLoginAsync(
        PasswordlessLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthenticationTokenResponse response = await InvokeAsync(
            ApiFunctions.PasswordlessLogin,
            request,
            DedicatedServerJsonContext.Default.PasswordlessLoginRequest,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeAuthenticationTokenResponse,
            authenticated: false,
            idempotent: true,
            cancellationToken).ConfigureAwait(false);
        await AdoptTokenAsync(response.AuthenticationToken, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> PasswordLoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthenticationTokenResponse response = await InvokeAsync(
            ApiFunctions.PasswordLogin,
            request,
            DedicatedServerJsonContext.Default.PasswordLoginRequest,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeAuthenticationTokenResponse,
            authenticated: false,
            // PasswordLogin is effectively idempotent (same password → same outcome) and has no side
            // effect to double-fire, so it may be retried on transient transport faults.
            idempotent: true,
            cancellationToken).ConfigureAwait(false);
        await AdoptTokenAsync(response.AuthenticationToken, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default) =>
        InvokeNoContentAsync(
            ApiFunctions.VerifyAuthenticationToken,
            data: (object?)null,
            requestTypeInfo: null,
            authenticated: true,
            idempotent: true,
            cancellationToken);

    // ----- Server state (read) -------------------------------------------------------------------

    /// <inheritdoc />
    public Task<HealthCheckResponse> HealthCheckAsync(
        HealthCheckRequest? request = null,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(
            ApiFunctions.HealthCheck,
            request ?? new HealthCheckRequest(),
            DedicatedServerJsonContext.Default.HealthCheckRequest,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeHealthCheckResponse,
            authenticated: false,
            idempotent: true,
            cancellationToken);

    /// <inheritdoc />
    public Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync<object?, QueryServerStateResponse>(
            ApiFunctions.QueryServerState,
            data: null,
            requestTypeInfo: null,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeQueryServerStateResponse,
            authenticated: true,
            idempotent: true,
            cancellationToken);

    /// <inheritdoc />
    public Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync<object?, GetServerOptionsResponse>(
            ApiFunctions.GetServerOptions,
            data: null,
            requestTypeInfo: null,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeGetServerOptionsResponse,
            authenticated: true,
            idempotent: true,
            cancellationToken);

    /// <inheritdoc />
    public Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync<object?, GetAdvancedGameSettingsResponse>(
            ApiFunctions.GetAdvancedGameSettings,
            data: null,
            requestTypeInfo: null,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeGetAdvancedGameSettingsResponse,
            authenticated: true,
            idempotent: true,
            cancellationToken);

    /// <inheritdoc />
    public Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default) =>
        InvokeAsync<object?, EnumerateSessionsResponse>(
            ApiFunctions.EnumerateSessions,
            data: null,
            requestTypeInfo: null,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeEnumerateSessionsResponse,
            // EnumerateSessions is a pure read; idempotent so a transient blip may be retried.
            authenticated: true,
            idempotent: true,
            cancellationToken);

    // ----- Server management ---------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> ClaimServerAsync(
        ClaimServerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AuthenticationTokenResponse response = await InvokeAsync(
            ApiFunctions.ClaimServer,
            request,
            DedicatedServerJsonContext.Default.ClaimServerRequest,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeAuthenticationTokenResponse,
            // Claim grants InitialAdmin implicitly on a never-claimed server; no bearer is attached.
            authenticated: false,
            // One-shot side effect: never retry.
            idempotent: false,
            cancellationToken).ConfigureAwait(false);
        await AdoptTokenAsync(response.AuthenticationToken, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.RenameServer,
            request,
            DedicatedServerJsonContext.Default.RenameServerRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.SetClientPassword,
            request,
            DedicatedServerJsonContext.Default.SetClientPasswordRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> SetAdminPasswordAsync(
        SetAdminPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Changing the admin password invalidates the OLD token used to make the change. The request
        // carries the NEW token; the server begins honoring it on success. We must NOT re-auth/replay
        // on a 401 here (that would race the very invalidation we're causing), so this goes through
        // the single-attempt path. On success we adopt the new token the caller supplied.
        var response = new AuthenticationTokenResponse(request.AuthenticationToken);
        await InvokeNoContentAsync(
            ApiFunctions.SetAdminPassword,
            request,
            DedicatedServerJsonContext.Default.SetAdminPasswordRequest,
            authenticated: true,
            idempotent: false,
            allowReauth: false,
            cancellationToken).ConfigureAwait(false);
        await AdoptTokenAsync(request.AuthenticationToken, cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <inheritdoc />
    public Task SetAutoLoadSessionNameAsync(SetAutoLoadSessionNameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.SetAutoLoadSessionName,
            request,
            DedicatedServerJsonContext.Default.SetAutoLoadSessionNameRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.ApplyServerOptions,
            request,
            DedicatedServerJsonContext.Default.ApplyServerOptionsRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.ApplyAdvancedGameSettings,
            request,
            DedicatedServerJsonContext.Default.ApplyAdvancedGameSettingsRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    // ----- Console -------------------------------------------------------------------------------

    /// <inheritdoc />
    public Task<RunCommandResponse?> RunCommandAsync(RunCommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeOptionalAsync(
            ApiFunctions.RunCommand,
            request,
            DedicatedServerJsonContext.Default.RunCommandRequest,
            DedicatedServerJsonContext.Default.DedicatedServerSuccessEnvelopeRunCommandResponse,
            authenticated: true,
            // RunCommand is the sharp knife: never retried, and a 401 mid-flight is ambiguous.
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken = default) =>
        InvokeNoContentAsync(
            ApiFunctions.Shutdown,
            data: (object?)null,
            requestTypeInfo: null,
            authenticated: true,
            idempotent: false,
            cancellationToken);

    // ----- Save game -----------------------------------------------------------------------------

    /// <inheritdoc />
    public Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.CreateNewGame,
            request,
            DedicatedServerJsonContext.Default.CreateNewGameRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.SaveGame,
            request,
            DedicatedServerJsonContext.Default.SaveGameRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.LoadGame,
            request,
            DedicatedServerJsonContext.Default.LoadGameRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.DeleteSaveFile,
            request,
            DedicatedServerJsonContext.Default.DeleteSaveFileRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return InvokeNoContentAsync(
            ApiFunctions.DeleteSaveSession,
            request,
            DedicatedServerJsonContext.Default.DeleteSaveSessionRequest,
            authenticated: true,
            idempotent: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task UploadSaveGameAsync(
        UploadSaveGameRequest request,
        Stream saveGameContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(saveGameContent);

        // Multipart upload is non-idempotent (writes a file, may load it) and not retried. A 401 here
        // would be ambiguous, but a multipart body backed by a forward-only stream cannot be replayed
        // anyway, so we always single-attempt and surface auth failures as-is.
        using HttpRequestMessage message = BuildMultipartUpload(request, saveGameContent);
        ApplyAuth(message, RequireToken());

        using HttpResponseMessage response =
            await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        byte[] body = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(ApiFunctions.UploadSaveGame, response, body, idempotent: false);
    }

    /// <inheritdoc />
    public async Task DownloadSaveGameAsync(
        DownloadSaveGameRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);

        JsonElement dataElement = SerializeToElement(
            request, DedicatedServerJsonContext.Default.DownloadSaveGameRequest);

        // The download returns a DIRECT binary body (not a JSON envelope) on success, but a JSON
        // error envelope on failure. We branch on Content-Type, then stream the success body to the
        // destination in chunks via ReadAsStreamAsync -> CopyToAsync, so we never materialize a second
        // in-memory copy of the save. (A true header-only completion would require a streaming overload
        // on the infra SurfaceHttpClient shell, which is dotnet-infra-engineer's to add; today the shell
        // sends with the default completion, so very large saves are bounded by HttpClient's buffering.)
        using HttpResponseMessage response = await SendWithReauthAsync(
            ApiFunctions.DownloadSaveGame,
            () => BuildEnvelopeRequest(ApiFunctions.DownloadSaveGame, dataElement, allowRetry: false),
            authenticated: true,
            idempotent: false,
            allowReauth: true,
            cancellationToken).ConfigureAwait(false);

        if (IsJsonResponse(response))
        {
            // A JSON body here is an error envelope (or, defensively, a non-file JSON) — buffer and
            // map it. Buffering JSON is safe; only the binary success path must stay streamed.
            byte[] jsonBody = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
            ThrowIfErrorEnvelope(ApiFunctions.DownloadSaveGame, response, jsonBody);

            // A success-shaped JSON with no binary content means there is nothing to stream.
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw MapStatusToException(
                ApiFunctions.DownloadSaveGame, response.StatusCode, idempotent: false, serverMessage: null);
        }

        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await body.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    // ----- Open-set escape hatch -----------------------------------------------------------------

    /// <inheritdoc />
    public async Task<JsonElement?> InvokeRawAsync(
        string function,
        JsonElement? data,
        bool allowRetry = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(function);

        using HttpResponseMessage response = await SendWithReauthAsync(
            function,
            () => BuildEnvelopeRequest(function, data, allowRetry),
            authenticated: true,
            idempotent: allowRetry,
            allowReauth: true,
            cancellationToken).ConfigureAwait(false);

        byte[] body = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
        ThrowIfErrorEnvelope(function, response, body);

        if (response.StatusCode == HttpStatusCode.NoContent || body.Length == 0)
        {
            return null;
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        // The success envelope wraps the payload under "data"; return that sub-element if present.
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("data", out JsonElement dataEl))
        {
            return dataEl.Clone();
        }

        return doc.RootElement.Clone();
    }

    // ====== Core send pipeline ===================================================================

    private async Task<TResponse> InvokeAsync<TRequest, TResponse>(
        string function,
        TRequest data,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest>? requestTypeInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<DedicatedServerSuccessEnvelope<TResponse>> responseTypeInfo,
        bool authenticated,
        bool idempotent,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        JsonElement? dataElement = data is null
            ? null
            : SerializeToElement(data, requestTypeInfo!);

        using HttpResponseMessage response = await SendWithReauthAsync(
            function,
            () => BuildEnvelopeRequest(function, dataElement, allowRetry: idempotent),
            authenticated,
            idempotent,
            allowReauth: true,
            cancellationToken).ConfigureAwait(false);

        byte[] body = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
        ThrowIfErrorEnvelope(function, response, body);

        DedicatedServerSuccessEnvelope<TResponse>? envelope = body.Length == 0
            ? null
            : JsonSerializer.Deserialize(body, responseTypeInfo);

        return envelope?.Data
            ?? throw new DedicatedServerApiException(
                "empty_response",
                $"'{function}' returned no data payload.",
                httpStatusCode: (int)response.StatusCode);
    }

    private async Task<TResponse?> InvokeOptionalAsync<TRequest, TResponse>(
        string function,
        TRequest data,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestTypeInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<DedicatedServerSuccessEnvelope<TResponse>> responseTypeInfo,
        bool authenticated,
        bool idempotent,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        JsonElement dataElement = SerializeToElement(data, requestTypeInfo);

        using HttpResponseMessage response = await SendWithReauthAsync(
            function,
            () => BuildEnvelopeRequest(function, dataElement, allowRetry: idempotent),
            authenticated,
            idempotent,
            allowReauth: true,
            cancellationToken).ConfigureAwait(false);

        byte[] body = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
        ThrowIfErrorEnvelope(function, response, body);

        if (response.StatusCode == HttpStatusCode.NoContent || body.Length == 0)
        {
            return null;
        }

        DedicatedServerSuccessEnvelope<TResponse>? envelope =
            JsonSerializer.Deserialize(body, responseTypeInfo);
        return envelope?.Data;
    }

    private Task InvokeNoContentAsync<TRequest>(
        string function,
        TRequest? data,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest>? requestTypeInfo,
        bool authenticated,
        bool idempotent,
        CancellationToken cancellationToken)
        => InvokeNoContentAsync(function, data, requestTypeInfo, authenticated, idempotent, allowReauth: true, cancellationToken);

    private async Task InvokeNoContentAsync<TRequest>(
        string function,
        TRequest? data,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest>? requestTypeInfo,
        bool authenticated,
        bool idempotent,
        bool allowReauth,
        CancellationToken cancellationToken)
    {
        JsonElement? dataElement = data is null
            ? null
            : SerializeToElement(data, requestTypeInfo!);

        using HttpResponseMessage response = await SendWithReauthAsync(
            function,
            () => BuildEnvelopeRequest(function, dataElement, allowRetry: idempotent),
            authenticated,
            idempotent,
            allowReauth,
            cancellationToken).ConfigureAwait(false);

        byte[] body = await ReadBodyBytesAsync(response, cancellationToken).ConfigureAwait(false);
        EnsureSuccessOrThrow(function, response, body, idempotent);
    }

    /// <summary>
    /// Sends a freshly-built request and, on a 401, re-authenticates ONCE and replays — but only
    /// when replay is safe. A factory (not a single message) is taken so the replay uses a brand-new
    /// <see cref="HttpRequestMessage"/> (a sent message cannot be reused).
    /// </summary>
    private async Task<HttpResponseMessage> SendWithReauthAsync(
        string function,
        Func<HttpRequestMessage> requestFactory,
        bool authenticated,
        bool idempotent,
        bool allowReauth,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage first = requestFactory();
        if (authenticated)
        {
            ApplyAuth(first, RequireToken());
        }

        HttpResponseMessage response =
            await _http.SendAsync(first, cancellationToken).ConfigureAwait(false);
        first.Dispose();

        if (response.StatusCode != HttpStatusCode.Unauthorized || !authenticated || !allowReauth)
        {
            return response;
        }

        // A 401 on a non-idempotent function is ambiguous: the call may already have executed before
        // the token was rejected. Replaying could double-fire the side effect, so refuse and surface
        // the ambiguity rather than risk it.
        if (!idempotent)
        {
            response.Dispose();
            throw new DedicatedServerAmbiguousResultException(
                function, "unauthorized", "the authentication token was rejected");
        }

        // Idempotent: try to obtain a fresh token and replay exactly once.
        string? refreshed = await TryReauthenticateAsync(cancellationToken).ConfigureAwait(false);
        if (refreshed is null)
        {
            // No way to silently re-auth (no config token / no retained password). Surface the 401.
            return response;
        }

        response.Dispose();
        HttpRequestMessage replay = requestFactory();
        ApplyAuth(replay, refreshed);
        HttpResponseMessage replayResponse =
            await _http.SendAsync(replay, cancellationToken).ConfigureAwait(false);
        replay.Dispose();
        return replayResponse;
    }

    /// <summary>
    /// Attempts to re-derive a bearer token without caller involvement. Only possible when a config
    /// API token exists (we re-present it); a session token from a password login cannot be silently
    /// renewed because no password is retained. Returns the token to replay with, or null.
    /// </summary>
    private async Task<string?> TryReauthenticateAsync(CancellationToken cancellationToken)
    {
        string? configToken = _options.AdminToken.Reveal();
        if (configToken is null)
        {
            return null;
        }

        // Re-present the config token (it may have been rotated server-side independently, but the
        // config value is the source of truth for an API-token deployment). Refresh the cache so
        // subsequent calls use it too.
        await AdoptTokenAsync(configToken, cancellationToken).ConfigureAwait(false);
        return configToken;
    }

    // ====== Envelope + transport helpers =========================================================

    private HttpRequestMessage BuildEnvelopeRequest(string function, JsonElement? data, bool allowRetry)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, ApiPath)
        {
            Content = JsonContent.Create(
                BuildEnvelopePayload(function, data),
                DedicatedServerJsonContext.Default.JsonElement),
        };

        // Per-FUNCTION retry opt-in: only idempotent functions set AllowRetry so the host resilience
        // pipeline may replay them on a transient transport fault. Non-idempotent functions never do.
        if (allowRetry)
        {
            message.Options.Set(SurfaceHttpRequestOptions.AllowRetry, true);
        }

        return message;
    }

    private static JsonElement BuildEnvelopePayload(string function, JsonElement? data)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("function", function);
            if (data is { } d)
            {
                writer.WritePropertyName("data");
                d.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        buffer.Position = 0;
        using JsonDocument doc = JsonDocument.Parse(buffer);
        return doc.RootElement.Clone();
    }

    private HttpRequestMessage BuildMultipartUpload(UploadSaveGameRequest request, Stream saveGameContent)
    {
        var content = new MultipartFormDataContent();

        // The JSON metadata part is named "data"; the server reads the upload params from it.
        JsonElement dataElement = SerializeToElement(
            request, DedicatedServerJsonContext.Default.UploadSaveGameRequest);
        var jsonPart = new StringContent(dataElement.GetRawText(), System.Text.Encoding.UTF8, "application/json");
        content.Add(jsonPart, "data");

        // Explicit charset part the server expects alongside the multipart body.
        content.Add(new StringContent(MultipartCharset), "_charset_");

        // The binary save streams straight from the caller's stream — never buffered whole in memory.
        var filePart = new StreamContent(saveGameContent);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(filePart, "saveGameFile", "save.sav");

        return new HttpRequestMessage(HttpMethod.Post, ApiPath) { Content = content };
        // NB: not retried — a forward-only upload stream cannot be replayed, and the write is a side effect.
    }

    private static void ApplyAuth(HttpRequestMessage message, string token) =>
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private string RequireToken() =>
        _cachedToken
        ?? throw new DedicatedServerAuthException(
            "no_credentials",
            "No authentication token is available. Set FICSITMCP_DedicatedServer__AdminToken or "
            + "authenticate first (PasswordLogin / ClaimServer).");

    private async Task AdoptTokenAsync(string token, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedToken = token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static JsonElement SerializeToElement<T>(
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        using JsonDocument doc = JsonDocument.Parse(utf8);
        return doc.RootElement.Clone();
    }

    private static bool IsJsonResponse(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType is "application/json" or "text/json";

    /// <summary>
    /// Buffers the response body once so it can be inspected for an error envelope AND deserialized
    /// as the success payload without re-reading a consumed stream.
    /// </summary>
    private static async Task<byte[]> ReadBodyBytesAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// For functions that return 204 (or an error envelope): throw on an error envelope, otherwise
    /// succeed. A non-2xx without a JSON error body maps to a generic protocol error.
    /// </summary>
    private void EnsureSuccessOrThrow(
        string function,
        HttpResponseMessage response,
        byte[] body,
        bool idempotent)
    {
        ThrowIfErrorEnvelope(function, response, body);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Non-success with no parseable error envelope.
        throw MapStatusToException(function, response.StatusCode, idempotent, serverMessage: null);
    }

    /// <summary>
    /// Inspects the (already-buffered) body as an error envelope and throws the mapped exception if
    /// it is one. A 401 maps to <see cref="DedicatedServerAuthException"/>. Returns normally if the
    /// body is not an error, so the caller can deserialize it as the success shape.
    /// </summary>
    private void ThrowIfErrorEnvelope(string function, HttpResponseMessage response, byte[] body)
    {
        // 204 / empty bodies are never error envelopes — but a bare 401 is still an auth failure.
        if (body.Length == 0)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DedicatedServerAuthException(
                    "unauthorized", "Authentication token was rejected.", httpStatusCode: 401);
            }

            return;
        }

        if (!IsJsonResponse(response))
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new DedicatedServerAuthException(
                    "unauthorized", "Authentication token was rejected.", httpStatusCode: 401);
            }

            return;
        }

        DedicatedServerErrorEnvelope? error;
        try
        {
            error = JsonSerializer.Deserialize(
                body, DedicatedServerJsonContext.Default.DedicatedServerErrorEnvelope);
        }
        catch (JsonException)
        {
            // Body wasn't actually an error envelope (likely a valid success payload) — let the
            // caller deserialize it as the success shape.
            return;
        }

        if (error is null || string.IsNullOrEmpty(error.ErrorCode))
        {
            // Not an error envelope (e.g. a success payload that happens to be JSON). If the status
            // itself is a failure, map it; otherwise return and let the caller read the success body.
            if (!response.IsSuccessStatusCode)
            {
                throw MapStatusToException(function, response.StatusCode, idempotent: false, serverMessage: null);
            }

            return;
        }

        string? errorData = error.ErrorData is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null }
            ? error.ErrorData.Value.GetRawText()
            : null;

        bool isAuth = response.StatusCode == HttpStatusCode.Unauthorized
            || IsAuthErrorCode(error.ErrorCode);

        throw isAuth
            ? new DedicatedServerAuthException(error.ErrorCode, error.ErrorMessage, errorData, (int)response.StatusCode)
            : new DedicatedServerApiException(error.ErrorCode, error.ErrorMessage, errorData, (int)response.StatusCode);
    }

    private static DedicatedServerApiException MapStatusToException(
        string function,
        HttpStatusCode status,
        bool idempotent,
        string? serverMessage)
    {
        if (status == HttpStatusCode.Unauthorized)
        {
            return new DedicatedServerAuthException("unauthorized", "Authentication token was rejected.", httpStatusCode: 401);
        }

        return new DedicatedServerApiException(
            $"http_{(int)status}",
            serverMessage ?? $"'{function}' failed with HTTP {(int)status}.",
            httpStatusCode: (int)status);
    }

    private static bool IsAuthErrorCode(string errorCode) =>
        errorCode is "wrong_password"
            or "unauthorized"
            or "invalid_token"
            or "token_expired"
            or "insufficient_privilege"
            or "passwordless_login_not_possible";
}
