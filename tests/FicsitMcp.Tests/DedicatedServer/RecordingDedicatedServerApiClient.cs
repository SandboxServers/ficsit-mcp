using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// A scriptable, call-recording fake <see cref="IDedicatedServerApiClient"/> for the save-management
/// backing-service tests. It records every method the service may call, in call order, so a test can
/// assert ordering (checkpoint before load), short-circuiting (no destructive call after a validation
/// miss), and argument values — all without HTTP or a live server.
/// </summary>
/// <remarks>
/// Methods the service never touches throw <see cref="NotSupportedException"/> so an accidental call
/// fails loudly. Each scripted method runs an optional hook (to simulate a server-side failure) and
/// records its invocation.
/// </remarks>
internal sealed class RecordingDedicatedServerApiClient : IDedicatedServerApiClient
{
    /// <summary>An ordered, human-readable log of the calls the service made, e.g. "EnumerateSessions", "SaveGame:pre-rollback-...".</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The response EnumerateSessions returns; defaults to an empty server.</summary>
    public EnumerateSessionsResponse SessionsResponse { get; set; } =
        new(Sessions: [], CurrentSessionIndex: -1);

    /// <summary>Optional hook run when SaveGame is called (throw to simulate a checkpoint/save failure).</summary>
    public Action<SaveGameRequest>? OnSaveGame { get; set; }

    /// <summary>Optional hook run when LoadGame is called.</summary>
    public Action<LoadGameRequest>? OnLoadGame { get; set; }

    /// <summary>Optional hook run when EnumerateSessions is called (throw to simulate an enumerate failure).</summary>
    public Action? OnEnumerateSessions { get; set; }

    public Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("EnumerateSessions");
        OnEnumerateSessions?.Invoke();
        return Task.FromResult(SessionsResponse);
    }

    public Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"SaveGame:{request.SaveName}");
        OnSaveGame?.Invoke(request);
        return Task.CompletedTask;
    }

    public Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"LoadGame:{request.SaveName}");
        OnLoadGame?.Invoke(request);
        return Task.CompletedTask;
    }

    public Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"DeleteSaveFile:{request.SaveName}");
        return Task.CompletedTask;
    }

    public Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"DeleteSaveSession:{request.SessionName}");
        return Task.CompletedTask;
    }

    public Task SetAutoLoadSessionNameAsync(
        SetAutoLoadSessionNameRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"SetAutoLoadSessionName:{request.SessionName}");
        return Task.CompletedTask;
    }

    public Task DownloadSaveGameAsync(
        DownloadSaveGameRequest request,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"DownloadSaveGame:{request.SaveName}");
        return Task.CompletedTask;
    }

    public Task UploadSaveGameAsync(
        UploadSaveGameRequest request,
        Stream saveGameContent,
        CancellationToken cancellationToken = default)
    {
        Calls.Add($"UploadSaveGame:{request.SaveName}:load={request.LoadSaveGame}");
        // Mirror the real client's ownership contract: the upload owns and disposes the stream.
        saveGameContent.Dispose();
        return Task.CompletedTask;
    }

    // --- Functions the save-management service never calls: fail loudly if one is reached. ---------

    public Task<AuthenticationTokenResponse> PasswordlessLoginAsync(
        PasswordlessLoginRequest request, CancellationToken cancellationToken = default) => NotUsed<AuthenticationTokenResponse>();

    public Task<AuthenticationTokenResponse> PasswordLoginAsync(
        PasswordLoginRequest request, CancellationToken cancellationToken = default) => NotUsed<AuthenticationTokenResponse>();

    public Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default) => NotUsed();

    public Task<HealthCheckResponse> HealthCheckAsync(
        HealthCheckRequest? request = null, CancellationToken cancellationToken = default) => NotUsed<HealthCheckResponse>();

    public Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default) => NotUsed<QueryServerStateResponse>();

    public Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default) => NotUsed<GetServerOptionsResponse>();

    public Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default) => NotUsed<GetAdvancedGameSettingsResponse>();

    public Task<AuthenticationTokenResponse> ClaimServerAsync(
        ClaimServerRequest request, CancellationToken cancellationToken = default) => NotUsed<AuthenticationTokenResponse>();

    public Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default) => NotUsed();

    public Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default) => NotUsed();

    public Task<AuthenticationTokenResponse> SetAdminPasswordAsync(
        SetAdminPasswordRequest request, CancellationToken cancellationToken = default) => NotUsed<AuthenticationTokenResponse>();

    public Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default) => NotUsed();

    public Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default) => NotUsed();

    public Task<RunCommandResponse?> RunCommandAsync(RunCommandRequest request, CancellationToken cancellationToken = default) => NotUsed<RunCommandResponse?>();

    public Task ShutdownAsync(CancellationToken cancellationToken = default) => NotUsed();

    public Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default) => NotUsed();

    public Task<JsonElement?> InvokeRawAsync(
        string function, JsonElement? data, bool allowRetry = false, CancellationToken cancellationToken = default) => NotUsed<JsonElement?>();

    private static Task<T> NotUsed<T>([System.Runtime.CompilerServices.CallerMemberName] string member = "") =>
        throw new NotSupportedException($"{member} must not be called by the save-management service.");

    private static Task NotUsed([System.Runtime.CompilerServices.CallerMemberName] string member = "") =>
        throw new NotSupportedException($"{member} must not be called by the save-management service.");
}
