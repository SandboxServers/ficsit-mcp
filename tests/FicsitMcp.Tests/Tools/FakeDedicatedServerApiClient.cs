using System.Text.Json;

using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;

namespace FicsitMcp.Tests.Tools;

/// <summary>
/// A hand-rolled fake <see cref="IDedicatedServerApiClient"/> for the tool tests: it records the
/// records each tool passed and returns scripted responses (or throws scripted exceptions), with no
/// HTTP anywhere. Only the lifecycle/console members the tools exercise are wired with behavior; the
/// rest throw <see cref="NotSupportedException"/> so an accidental call is loud.
/// </summary>
internal sealed class FakeDedicatedServerApiClient : IDedicatedServerApiClient
{
    // ----- Captured inputs (assert the tool delegated the right record) --------------------------
    public RunCommandRequest? LastRunCommand { get; private set; }
    public RenameServerRequest? LastRename { get; private set; }
    public SetClientPasswordRequest? LastSetClientPassword { get; private set; }
    public SetAdminPasswordRequest? LastSetAdminPassword { get; private set; }
    public bool ShutdownCalled { get; private set; }

    // ----- Scripted outcomes ---------------------------------------------------------------------
    public RunCommandResponse? RunCommandResult { get; set; } = new("ok", true);
    public Exception? ThrowOnRunCommand { get; set; }
    public Exception? ThrowOnShutdown { get; set; }
    public Exception? ThrowOnRename { get; set; }
    public Exception? ThrowOnSetClientPassword { get; set; }
    public Exception? ThrowOnSetAdminPassword { get; set; }

    public Task<RunCommandResponse?> RunCommandAsync(
        RunCommandRequest request, CancellationToken cancellationToken = default)
    {
        LastRunCommand = request;
        if (ThrowOnRunCommand is not null)
        {
            return Task.FromException<RunCommandResponse?>(ThrowOnRunCommand);
        }

        return Task.FromResult(RunCommandResult);
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        ShutdownCalled = true;
        return ThrowOnShutdown is not null ? Task.FromException(ThrowOnShutdown) : Task.CompletedTask;
    }

    public Task RenameServerAsync(RenameServerRequest request, CancellationToken cancellationToken = default)
    {
        LastRename = request;
        return ThrowOnRename is not null ? Task.FromException(ThrowOnRename) : Task.CompletedTask;
    }

    public Task SetClientPasswordAsync(SetClientPasswordRequest request, CancellationToken cancellationToken = default)
    {
        LastSetClientPassword = request;
        return ThrowOnSetClientPassword is not null
            ? Task.FromException(ThrowOnSetClientPassword)
            : Task.CompletedTask;
    }

    public Task<AuthenticationTokenResponse> SetAdminPasswordAsync(
        SetAdminPasswordRequest request, CancellationToken cancellationToken = default)
    {
        LastSetAdminPassword = request;
        if (ThrowOnSetAdminPassword is not null)
        {
            return Task.FromException<AuthenticationTokenResponse>(ThrowOnSetAdminPassword);
        }

        // The real client returns a response carrying the new token; the tool ignores it (it never
        // surfaces the token), so the exact value only matters for not-leaking assertions.
        return Task.FromResult(new AuthenticationTokenResponse(request.AuthenticationToken));
    }

    // ----- Unused members: loud on accidental use ------------------------------------------------
    public Task<AuthenticationTokenResponse> PasswordlessLoginAsync(PasswordlessLoginRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<AuthenticationTokenResponse> PasswordLoginAsync(PasswordLoginRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task VerifyAuthenticationTokenAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<HealthCheckResponse> HealthCheckAsync(HealthCheckRequest? request = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<QueryServerStateResponse> QueryServerStateAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GetServerOptionsResponse> GetServerOptionsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<GetAdvancedGameSettingsResponse> GetAdvancedGameSettingsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<EnumerateSessionsResponse> EnumerateSessionsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<AuthenticationTokenResponse> ClaimServerAsync(ClaimServerRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task SetAutoLoadSessionNameAsync(SetAutoLoadSessionNameRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task ApplyServerOptionsAsync(ApplyServerOptionsRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task ApplyAdvancedGameSettingsAsync(ApplyAdvancedGameSettingsRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task CreateNewGameAsync(CreateNewGameRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task SaveGameAsync(SaveGameRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task LoadGameAsync(LoadGameRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteSaveFileAsync(DeleteSaveFileRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteSaveSessionAsync(DeleteSaveSessionRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task UploadSaveGameAsync(UploadSaveGameRequest request, Stream saveGameContent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DownloadSaveGameAsync(DownloadSaveGameRequest request, Stream destination, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<JsonElement?> InvokeRawAsync(string function, JsonElement? data, bool allowRetry = false, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
