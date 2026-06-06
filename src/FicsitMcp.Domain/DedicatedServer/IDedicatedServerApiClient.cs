using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// Typed client for the official Satisfactory Dedicated Server HTTPS API
/// (<c>POST {BaseUrl}/api/v1</c>, one endpoint, a function-envelope body). This is the single place
/// the wire protocol lives: envelope shape, bearer auth, 401 re-auth/replay, error-code mapping,
/// and multipart/streaming framing are all hidden behind these typed methods. Callers (MCP tools)
/// only ever see records and typed exceptions — never an <c>HttpRequestMessage</c>, header, or
/// <c>errorCode</c> integer.
/// </summary>
/// <remarks>
/// <para>
/// Every method may throw <see cref="DedicatedServerApiException"/> (server error envelope),
/// <see cref="DedicatedServerAuthException"/> (auth failed even after transparent re-auth),
/// <see cref="DedicatedServerAmbiguousResultException"/> (a non-idempotent call whose delivery is
/// unconfirmed after a 401), or <see cref="FicsitMcp.Domain.Http.SurfaceUnreachableException"/> /
/// <see cref="FicsitMcp.Domain.Http.CertificatePinMismatchException"/> from the transport shell.
/// </para>
/// <para>
/// Function names are an OPEN SET: mods can register additional functions on this same endpoint
/// (e.g. FRM registers <c>frm</c>). The typed methods cover the base game; arbitrary functions go
/// through <see cref="InvokeRawAsync"/> so #11's FRM-passthrough has a TLS+authenticated path
/// without bypassing this layer.
/// </para>
/// </remarks>
public interface IDedicatedServerApiClient
{
    // --- Authentication lifecycle -------------------------------------------------------------

    /// <summary>
    /// <c>PasswordlessLogin</c> — obtains a bearer token at the requested privilege without a
    /// password (only possible when the server permits it). Idempotent.
    /// </summary>
    Task<AuthenticationTokenResponse> PasswordlessLoginAsync(
        PasswordlessLoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>PasswordLogin</c> — exchanges the admin (or client) password for a bearer token at the
    /// requested privilege. The password is never logged or echoed.
    /// </summary>
    Task<AuthenticationTokenResponse> PasswordLoginAsync(
        PasswordLoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>VerifyAuthenticationToken</c> — confirms the currently-held token is still valid (the
    /// server replies 204). Idempotent. Throws <see cref="DedicatedServerAuthException"/> if invalid.
    /// </summary>
    Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default);

    // --- Server state (read) ------------------------------------------------------------------

    /// <summary>
    /// <c>HealthCheck</c> — lightweight liveness/responsiveness probe. Idempotent; no auth required.
    /// </summary>
    Task<HealthCheckResponse> HealthCheckAsync(
        HealthCheckRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>QueryServerState</c> — current game state (players, tier, phase, tick rate, etc.).
    /// Idempotent. Returns the nested <c>serverGameState</c> shape.
    /// </summary>
    Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GetServerOptions</c> — active and pending server options. Read-only.</summary>
    Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>GetAdvancedGameSettings</c> — creative-mode flag and advanced settings. Read-only.</summary>
    Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary><c>EnumerateSessions</c> — all sessions and their save headers. Read-only.</summary>
    Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default);

    // --- Server management (state-changing) ---------------------------------------------------

    /// <summary>
    /// <c>ClaimServer</c> — sets the first admin password on a NEVER-CLAIMED server and returns the
    /// admin token. One-shot: a claimed server rejects this with <c>server_claimed</c>; the only way
    /// to re-run is a full server wipe. The admin password is never logged or echoed.
    /// </summary>
    Task<AuthenticationTokenResponse> ClaimServerAsync(
        ClaimServerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary><c>RenameServer</c> — sets the server's display name. Requires admin.</summary>
    Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>SetClientPassword</c> — sets/clears the client password. Requires admin.</summary>
    Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>SetAdminPassword</c> — changes the admin password and the bearer token together. Changing
    /// the password invalidates the OLD token, so the request supplies the new token; this client
    /// adopts that new token for subsequent calls. The password is never logged or echoed.
    /// </summary>
    Task<AuthenticationTokenResponse> SetAdminPasswordAsync(
        SetAdminPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary><c>SetAutoLoadSessionName</c> — sets the session to auto-load on start. Requires admin.</summary>
    Task SetAutoLoadSessionNameAsync(SetAutoLoadSessionNameRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>ApplyServerOptions</c> — applies server options. Requires admin.</summary>
    Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>ApplyAdvancedGameSettings</c> — applies advanced settings. Requires admin.</summary>
    Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default);

    // --- Console -------------------------------------------------------------------------------

    /// <summary>
    /// <c>RunCommand</c> — executes an arbitrary console command. NON-IDEMPOTENT and powerful: never
    /// retried on transient faults, and a 401 mid-flight surfaces as
    /// <see cref="DedicatedServerAmbiguousResultException"/> rather than risking double execution.
    /// </summary>
    Task<RunCommandResponse?> RunCommandAsync(RunCommandRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>Shutdown</c> — shuts the server down. NON-IDEMPOTENT; never retried, and a 401 mid-flight
    /// surfaces as <see cref="DedicatedServerAmbiguousResultException"/>.
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken = default);

    // --- Save game (state-changing) -----------------------------------------------------------

    /// <summary><c>CreateNewGame</c> — starts a new game. NON-IDEMPOTENT; the API is briefly unavailable while loading.</summary>
    Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>SaveGame</c> — saves under the given name. NON-IDEMPOTENT; never retried.</summary>
    Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>LoadGame</c> — loads a save (disconnects players; API briefly unavailable). NON-IDEMPOTENT.</summary>
    Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DeleteSaveFile</c> — deletes one save file. NON-IDEMPOTENT.</summary>
    Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>DeleteSaveSession</c> — deletes a whole session's saves. NON-IDEMPOTENT.</summary>
    Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>UploadSaveGame</c> — streams a save file to the server via multipart/form-data (JSON
    /// metadata part + binary <c>saveGameFile</c> part). The save stream is copied in chunks and is
    /// never buffered whole in memory. NON-IDEMPOTENT.
    /// </summary>
    /// <param name="request">Upload metadata (target name, whether to load).</param>
    /// <param name="saveGameContent">The save file bytes; read (not owned) by this call.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task UploadSaveGameAsync(
        UploadSaveGameRequest request,
        Stream saveGameContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>DownloadSaveGame</c> — downloads a save as a direct binary response (not a JSON envelope),
    /// copied to <paramref name="destination"/> in chunks so the whole save is never buffered in
    /// memory. Idempotent in effect, but POST-only and not retried by default (a partial copy on a
    /// transient fault is the caller's to re-request). An error is still returned as a JSON envelope.
    /// </summary>
    /// <param name="request">Which save to download.</param>
    /// <param name="destination">Where to stream the bytes; written (not owned) by this call.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task DownloadSaveGameAsync(
        DownloadSaveGameRequest request,
        Stream destination,
        CancellationToken cancellationToken = default);

    // --- Open-set escape hatch -----------------------------------------------------------------

    /// <summary>
    /// Invokes an ARBITRARY function on the same endpoint with a raw JSON <c>data</c> payload and
    /// returns the raw <c>data</c> payload — the seam for mod-registered functions (e.g. FRM's
    /// <c>frm</c>) that the typed methods do not cover. Goes through the same envelope build, bearer
    /// auth, and error mapping as the typed methods; only the payload shape is untyped.
    /// </summary>
    /// <param name="function">The function name (not validated against a closed enum).</param>
    /// <param name="data">The request <c>data</c> payload, or null for none.</param>
    /// <param name="allowRetry">
    /// Whether the call is idempotent and may be retried on transient faults. Defaults to false —
    /// callers must opt in only for functions they know are safe to replay.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The response <c>data</c> as a <see cref="JsonElement"/>, or null for a 204.</returns>
    Task<JsonElement?> InvokeRawAsync(
        string function,
        JsonElement? data,
        bool allowRetry = false,
        CancellationToken cancellationToken = default);
}
