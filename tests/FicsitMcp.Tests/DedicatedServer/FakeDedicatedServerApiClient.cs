using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// An in-memory fake <see cref="IDedicatedServerApiClient"/> for testing the settings tools at the
/// interface seam (no HTTP). It maintains a mutable applied/pending server-options store and an
/// advanced-game-settings store so a set-then-get round-trip is observable, records the last
/// create-new-game request, and records every apply request for assertions. Methods the settings
/// tools never call throw <see cref="NotSupportedException"/> so an accidental dependency is loud.
/// </summary>
internal sealed class FakeDedicatedServerApiClient : IDedicatedServerApiClient
{
    private Dictionary<string, string> _appliedOptions = new(StringComparer.Ordinal);
    private Dictionary<string, string> _pendingOptions = new(StringComparer.Ordinal);
    private Dictionary<string, string> _advancedGameSettings = new(StringComparer.Ordinal);
    private bool _creativeModeEnabled;

    /// <summary>Seed the applied server options the next get returns.</summary>
    public void SeedAppliedOptions(IDictionary<string, string> options) =>
        _appliedOptions = new Dictionary<string, string>(options, StringComparer.Ordinal);

    /// <summary>Seed the pending server options the next get returns.</summary>
    public void SeedPendingOptions(IDictionary<string, string> options) =>
        _pendingOptions = new Dictionary<string, string>(options, StringComparer.Ordinal);

    /// <summary>Seed the advanced game settings + creative flag the next get returns.</summary>
    public void SeedAdvancedGameSettings(IDictionary<string, string> settings, bool creativeModeEnabled)
    {
        _advancedGameSettings = new Dictionary<string, string>(settings, StringComparer.Ordinal);
        _creativeModeEnabled = creativeModeEnabled;
    }

    /// <summary>The request captured by the last <see cref="ApplyServerOptionsAsync"/> call.</summary>
    public ApplyServerOptionsRequest? LastApplyServerOptions { get; private set; }

    /// <summary>The request captured by the last <see cref="ApplyAdvancedGameSettingsAsync"/> call.</summary>
    public ApplyAdvancedGameSettingsRequest? LastApplyAdvancedGameSettings { get; private set; }

    /// <summary>The request captured by the last <see cref="CreateNewGameAsync"/> call.</summary>
    public CreateNewGameRequest? LastCreateNewGame { get; private set; }

    public int CreateNewGameCallCount { get; private set; }

    public Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new GetServerOptionsResponse(
            new Dictionary<string, string>(_appliedOptions, StringComparer.Ordinal),
            new Dictionary<string, string>(_pendingOptions, StringComparer.Ordinal)));

    public Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default)
    {
        LastApplyServerOptions = request;
        // Model the server: applying merges the supplied options into the live applied set so a
        // subsequent get reflects the change (round-trip).
        foreach (KeyValuePair<string, string> entry in request.UpdatedServerOptions)
        {
            _appliedOptions[entry.Key] = entry.Value;
        }

        return Task.CompletedTask;
    }

    public Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new GetAdvancedGameSettingsResponse(
            _creativeModeEnabled,
            new Dictionary<string, string>(_advancedGameSettings, StringComparer.Ordinal)));

    public Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default)
    {
        LastApplyAdvancedGameSettings = request;
        foreach (KeyValuePair<string, string> entry in request.AppliedAdvancedGameSettings)
        {
            _advancedGameSettings[entry.Key] = entry.Value;
        }

        // Applying any advanced game setting flags the session permanently.
        if (request.AppliedAdvancedGameSettings.Count > 0)
        {
            _creativeModeEnabled = true;
        }

        return Task.CompletedTask;
    }

    public Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateNewGame = request;
        CreateNewGameCallCount++;
        return Task.CompletedTask;
    }

    // --- members the settings tools never use ------------------------------------------------

    public Task<AuthenticationTokenResponse> PasswordlessLoginAsync(PasswordlessLoginRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuthenticationTokenResponse> PasswordLoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<HealthCheckResponse> HealthCheckAsync(HealthCheckRequest? request = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuthenticationTokenResponse> ClaimServerAsync(ClaimServerRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<AuthenticationTokenResponse> SetAdminPasswordAsync(SetAdminPasswordRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SetAutoLoadSessionNameAsync(SetAutoLoadSessionNameRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<RunCommandResponse?> RunCommandAsync(RunCommandRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ShutdownAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UploadSaveGameAsync(UploadSaveGameRequest request, Stream saveGameContent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DownloadSaveGameAsync(DownloadSaveGameRequest request, Stream destination, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<JsonElement?> InvokeRawAsync(string function, JsonElement? data, bool allowRetry = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
