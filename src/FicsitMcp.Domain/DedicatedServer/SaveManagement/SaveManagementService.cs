using System.Globalization;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer.Model;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FicsitMcp.Domain.DedicatedServer.SaveManagement;

/// <summary>
/// Default <see cref="ISaveManagementService"/>: orchestrates the dedicated-server save functions on
/// top of <see cref="IDedicatedServerApiClient"/>, adding the live-name validation, near-match
/// suggestions, and rollback checkpoint-then-load composition the thin MCP tools must not own.
/// </summary>
/// <remarks>
/// Every failure path logs ONCE with a message template and the canonical fields (<c>Surface</c>,
/// <c>Tool</c>, <c>Reason</c>) before the exception propagates — no silent catches. Secrets never
/// appear here: this service only handles save/session names, never tokens or passwords.
/// </remarks>
public sealed class SaveManagementService : ISaveManagementService
{
    /// <summary>The canonical surface label used in structured logs.</summary>
    private const string Surface = "DedicatedServer";

    private readonly IDedicatedServerApiClient _client;
    private readonly IOptions<DedicatedServerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SaveManagementService> _logger;

    /// <summary>Creates the service over the typed API client.</summary>
    /// <param name="client">The dedicated-server API client (the only wire seam).</param>
    /// <param name="options">
    /// Dedicated-server surface options, asserted with <c>Require()</c> at the top of each operation so
    /// a dormant surface fails fast naming <c>FICSITMCP_DedicatedServer__BaseUrl</c> rather than deep
    /// in a send.
    /// </param>
    /// <param name="timeProvider">Clock for the deterministic rollback checkpoint name; testable.</param>
    /// <param name="logger">Structured logger (stderr-routed by the host).</param>
    public SaveManagementService(
        IDedicatedServerApiClient client,
        IOptions<DedicatedServerOptions> options,
        TimeProvider timeProvider,
        ILogger<SaveManagementService> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SessionListResult> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        RequireSurface();
        EnumerateSessionsResponse response = await EnumerateAsync("list_sessions", cancellationToken)
            .ConfigureAwait(false);
        return Flatten(response);
    }

    /// <inheritdoc />
    public async Task SaveGameAsync(string saveName, CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);
        try
        {
            await _client.SaveGameAsync(new SaveGameRequest(saveName), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("save_game", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetAutoLoadSessionAsync(string sessionName, CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);

        EnumerateSessionsResponse sessions =
            await EnumerateAsync("set_auto_load_session", cancellationToken).ConfigureAwait(false);
        ValidateSessionExists("set_auto_load_session", sessionName, sessions);

        try
        {
            await _client.SetAutoLoadSessionNameAsync(
                new SetAutoLoadSessionNameRequest(sessionName), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("set_auto_load_session", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task LoadSaveAsync(string saveName, CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);

        EnumerateSessionsResponse sessions =
            await EnumerateAsync("load_save", cancellationToken).ConfigureAwait(false);
        ValidateSaveExists("load_save", saveName, sessions);

        await LoadValidatedAsync("load_save", saveName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteSaveAsync(string saveName, CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);

        EnumerateSessionsResponse sessions =
            await EnumerateAsync("delete_save", cancellationToken).ConfigureAwait(false);
        ValidateSaveExists("delete_save", saveName, sessions);

        try
        {
            await _client.DeleteSaveFileAsync(new DeleteSaveFileRequest(saveName), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("delete_save", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(string sessionName, CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);

        EnumerateSessionsResponse sessions =
            await EnumerateAsync("delete_session", cancellationToken).ConfigureAwait(false);
        ValidateSessionExists("delete_session", sessionName, sessions);

        try
        {
            await _client.DeleteSaveSessionAsync(new DeleteSaveSessionRequest(sessionName), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("delete_session", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DownloadSaveAsync(
        string saveName,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);
        ArgumentNullException.ThrowIfNull(destination);

        EnumerateSessionsResponse sessions =
            await EnumerateAsync("download_save", cancellationToken).ConfigureAwait(false);
        ValidateSaveExists("download_save", saveName, sessions);

        try
        {
            await _client.DownloadSaveGameAsync(
                new DownloadSaveGameRequest(saveName), destination, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("download_save", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UploadSaveAsync(
        string saveName,
        Stream saveGameContent,
        bool loadImmediately,
        CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);
        ArgumentNullException.ThrowIfNull(saveGameContent);

        try
        {
            await _client.UploadSaveGameAsync(
                new UploadSaveGameRequest(saveName, LoadSaveGame: loadImmediately),
                saveGameContent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("upload_save", ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RollbackResult> RollbackToAsync(
        string saveName,
        CancellationToken cancellationToken = default)
    {
        RequireSurface();
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);

        // Validate the target up front so a bad name is caught before we burn a checkpoint save.
        EnumerateSessionsResponse sessions =
            await EnumerateAsync("rollback_to", cancellationToken).ConfigureAwait(false);
        ValidateSaveExists("rollback_to", saveName, sessions);

        // Checkpoint FIRST. If this throws, we must NOT load — the pre-rollback world would be lost.
        string checkpointName = BuildCheckpointName();
        try
        {
            await _client.SaveGameAsync(new SaveGameRequest(checkpointName), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure("rollback_to", ex, reason: "checkpoint_save_failed");
            throw;
        }

        // Checkpoint succeeded; now perform the destructive load of the target.
        await LoadValidatedAsync("rollback_to", saveName, cancellationToken).ConfigureAwait(false);
        return new RollbackResult(checkpointName, saveName);
    }

    private void RequireSurface() => _options.Value.Require();

    private async Task<EnumerateSessionsResponse> EnumerateAsync(string tool, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.EnumerateSessionsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure(tool, ex, reason: "enumerate_sessions_failed");
            throw;
        }
    }

    private async Task LoadValidatedAsync(string tool, string saveName, CancellationToken cancellationToken)
    {
        try
        {
            await _client.LoadGameAsync(new LoadGameRequest(saveName), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogFailure(tool, ex, reason: "load_game_failed");
            throw;
        }
    }

    private static SessionListResult Flatten(EnumerateSessionsResponse response)
    {
        string? currentSessionName =
            response.CurrentSessionIndex >= 0 && response.CurrentSessionIndex < response.Sessions.Count
                ? response.Sessions[response.CurrentSessionIndex].SessionName
                : null;

        var saves = new List<SaveFileInfo>();
        foreach (SessionSaveStruct session in response.Sessions)
        {
            bool isCurrent = string.Equals(session.SessionName, currentSessionName, StringComparison.Ordinal);
            foreach (SaveHeader header in session.SaveHeaders)
            {
                saves.Add(new SaveFileInfo(
                    session.SessionName,
                    header.SaveName,
                    isCurrent,
                    header.SaveDateTime,
                    header.PlayDurationSeconds,
                    header.IsModdedSave,
                    header.IsCreativeModeEnabled));
            }
        }

        return new SessionListResult(currentSessionName, saves);
    }

    private void ValidateSaveExists(string tool, string saveName, EnumerateSessionsResponse sessions)
    {
        var saveNames = sessions.Sessions
            .SelectMany(static s => s.SaveHeaders.Select(static h => h.SaveName))
            .ToList();

        if (!NearMatchFinder.ContainsExact(saveNames, saveName))
        {
            IReadOnlyList<string> nearMatches = NearMatchFinder.FindNearMatches(saveNames, saveName);
            LogValidationMiss(tool, "save", saveName, nearMatches);
            throw new SaveNotFoundException("save", saveName, nearMatches);
        }
    }

    private void ValidateSessionExists(string tool, string sessionName, EnumerateSessionsResponse sessions)
    {
        var sessionNames = sessions.Sessions.Select(static s => s.SessionName).ToList();

        if (!NearMatchFinder.ContainsExact(sessionNames, sessionName))
        {
            IReadOnlyList<string> nearMatches = NearMatchFinder.FindNearMatches(sessionNames, sessionName);
            LogValidationMiss(tool, "session", sessionName, nearMatches);
            throw new SaveNotFoundException("session", sessionName, nearMatches);
        }
    }

    private string BuildCheckpointName()
    {
        string utc = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        return $"pre-rollback-{utc}";
    }

    // Reason: a destructive op was short-circuited because the requested name did not exist. The
    // requested name is in-the-clear (it's a save/session name, never a secret); the suggestion count
    // is logged rather than the names to keep log volume bounded.
    private void LogValidationMiss(string tool, string kind, string requestedName, IReadOnlyList<string> nearMatches) =>
        _logger.LogWarning(
            "{Surface} {Tool} aborted: {Reason} (requested {Kind} '{RequestedName}', {SuggestionCount} near-match(es) offered)",
            Surface,
            tool,
            "name_not_found",
            kind,
            requestedName,
            nearMatches.Count);

    private void LogFailure(string tool, Exception ex, string reason = "client_call_failed") =>
        _logger.LogError(
            ex,
            "{Surface} {Tool} failed: {Reason}",
            Surface,
            tool,
            reason);
}
