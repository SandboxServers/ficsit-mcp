using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.DedicatedServer.SaveManagement;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FicsitMcp.Tests.DedicatedServer;

/// <summary>
/// Backing-service tests for <see cref="SaveManagementService"/> against a faked
/// <see cref="FicsitMcp.Domain.DedicatedServer.IDedicatedServerApiClient"/>. They prove the safety
/// invariants the thin MCP tools rely on: validation short-circuits destructive calls, not-found
/// errors carry exact near-match suggestions, and rollback checkpoints BEFORE it loads (aborting the
/// load if the checkpoint fails).
/// </summary>
public sealed class SaveManagementServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 6, 13, 45, 7, TimeSpan.Zero);

    private static SaveManagementService CreateService(
        RecordingDedicatedServerApiClient client,
        TimeProvider? timeProvider = null,
        string? baseUrl = "https://127.0.0.1:7777")
    {
        var options = Options.Create(new DedicatedServerOptions { BaseUrl = baseUrl });
        return new SaveManagementService(
            client,
            options,
            timeProvider ?? new FakeTimeProvider(FixedNow),
            NullLogger<SaveManagementService>.Instance);
    }

    private static EnumerateSessionsResponse Sessions(
        int currentIndex,
        params (string Session, string[] Saves)[] sessions)
    {
        var structs = sessions
            .Select(s => new SessionSaveStruct(
                s.Session,
                s.Saves.Select(name => new SaveHeader(
                    SaveVersion: 1,
                    BuildVersion: 23300430,
                    SaveName: name,
                    MapName: "Persistent_Level",
                    MapOptions: null,
                    SessionName: s.Session,
                    PlayDurationSeconds: 3600,
                    SaveDateTime: "2026.06.06-13.00.00",
                    IsModdedSave: false,
                    IsEditedSave: false,
                    IsCreativeModeEnabled: false)).ToList()))
            .ToList();
        return new EnumerateSessionsResponse(structs, currentIndex);
    }

    // --- list_sessions --------------------------------------------------------------------------

    [Fact]
    public async Task ListSessions_FlattensSavesAndMarksCurrentSession()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(
                currentIndex: 1,
                ("Alpha", ["alpha_auto_0"]),
                ("Bravo", ["bravo_manual", "bravo_auto_0"])),
        };
        SaveManagementService service = CreateService(client);

        SessionListResult result = await service.ListSessionsAsync();

        Assert.Equal("Bravo", result.CurrentSessionName);
        Assert.Equal(3, result.Saves.Count);
        Assert.True(result.Saves.Single(s => s.SaveName == "bravo_manual").IsCurrentSession);
        Assert.False(result.Saves.Single(s => s.SaveName == "alpha_auto_0").IsCurrentSession);
    }

    [Fact]
    public async Task ListSessions_WithNoActiveSession_ReportsNullCurrentName()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(currentIndex: -1, ("Alpha", ["alpha_auto_0"])),
        };
        SaveManagementService service = CreateService(client);

        SessionListResult result = await service.ListSessionsAsync();

        Assert.Null(result.CurrentSessionName);
    }

    // --- save_game (happy path, no validation) --------------------------------------------------

    [Fact]
    public async Task SaveGame_SendsSaveGameWithTheGivenName()
    {
        var client = new RecordingDedicatedServerApiClient();
        SaveManagementService service = CreateService(client);

        await service.SaveGameAsync("checkpoint-1");

        Assert.Equal(["SaveGame:checkpoint-1"], client.Calls);
    }

    // --- surface dormancy -----------------------------------------------------------------------

    [Fact]
    public async Task AnyOperation_OnDormantSurface_FailsFastNamingTheEnvVar()
    {
        var client = new RecordingDedicatedServerApiClient();
        SaveManagementService service = CreateService(client, baseUrl: null);

        SurfaceNotConfiguredException ex =
            await Assert.ThrowsAsync<SurfaceNotConfiguredException>(() => service.ListSessionsAsync());

        Assert.Contains("FICSITMCP_DedicatedServer__BaseUrl", ex.Message, StringComparison.Ordinal);
        Assert.Empty(client.Calls);
    }

    // --- load_save: validation + happy path -----------------------------------------------------

    [Fact]
    public async Task LoadSave_WhenSaveExists_EnumeratesThenLoads()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["good_save"])),
        };
        SaveManagementService service = CreateService(client);

        await service.LoadSaveAsync("good_save");

        Assert.Equal(["EnumerateSessions", "LoadGame:good_save"], client.Calls);
    }

    [Fact]
    public async Task LoadSave_WhenSaveMissing_ThrowsWithNearMatches_AndDoesNotLoad()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["my_factory", "my_factory_backup", "unrelated"])),
        };
        SaveManagementService service = CreateService(client);

        SaveNotFoundException ex =
            await Assert.ThrowsAsync<SaveNotFoundException>(() => service.LoadSaveAsync("my_factor"));

        Assert.Equal("save", ex.Kind);
        Assert.Equal("my_factor", ex.RequestedName);
        Assert.Equal(["my_factory", "my_factory_backup"], ex.NearMatches);
        // The destructive load must NOT have fired: only the enumeration call happened.
        Assert.Equal(["EnumerateSessions"], client.Calls);
    }

    // --- delete_save: validation short-circuit --------------------------------------------------

    [Fact]
    public async Task DeleteSave_WhenSaveExists_Deletes()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["doomed"])),
        };
        SaveManagementService service = CreateService(client);

        await service.DeleteSaveAsync("doomed");

        Assert.Equal(["EnumerateSessions", "DeleteSaveFile:doomed"], client.Calls);
    }

    [Fact]
    public async Task DeleteSave_WhenSaveMissing_ThrowsAndDoesNotDelete()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["keepme"])),
        };
        SaveManagementService service = CreateService(client);

        await Assert.ThrowsAsync<SaveNotFoundException>(() => service.DeleteSaveAsync("typo"));

        Assert.Equal(["EnumerateSessions"], client.Calls);
    }

    // --- delete_session: validates against SESSION names, not save names ------------------------

    [Fact]
    public async Task DeleteSession_WhenSessionExists_Deletes()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["a1"]), ("Bravo", ["b1"])),
        };
        SaveManagementService service = CreateService(client);

        await service.DeleteSessionAsync("Bravo");

        Assert.Equal(["EnumerateSessions", "DeleteSaveSession:Bravo"], client.Calls);
    }

    [Fact]
    public async Task DeleteSession_WhenSessionMissing_ThrowsWithSessionKindAndNearMatches()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["a1"]), ("Bravo", ["b1"])),
        };
        SaveManagementService service = CreateService(client);

        SaveNotFoundException ex =
            await Assert.ThrowsAsync<SaveNotFoundException>(() => service.DeleteSessionAsync("Bravoo"));

        Assert.Equal("session", ex.Kind);
        Assert.Equal(["Bravo"], ex.NearMatches);
        Assert.Equal(["EnumerateSessions"], client.Calls);
    }

    // --- set_auto_load_session: validates the session ------------------------------------------

    [Fact]
    public async Task SetAutoLoadSession_WhenSessionExists_Sets()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["a1"])),
        };
        SaveManagementService service = CreateService(client);

        await service.SetAutoLoadSessionAsync("Alpha");

        Assert.Equal(["EnumerateSessions", "SetAutoLoadSessionName:Alpha"], client.Calls);
    }

    [Fact]
    public async Task SetAutoLoadSession_WhenSessionMissing_ThrowsAndDoesNotSet()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["a1"])),
        };
        SaveManagementService service = CreateService(client);

        await Assert.ThrowsAsync<SaveNotFoundException>(() => service.SetAutoLoadSessionAsync("Alfa"));

        Assert.Equal(["EnumerateSessions"], client.Calls);
    }

    // --- download_save: validates, then streams -------------------------------------------------

    [Fact]
    public async Task DownloadSave_WhenSaveExists_ValidatesThenDownloads()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["pull_me"])),
        };
        SaveManagementService service = CreateService(client);
        using var destination = new MemoryStream();

        await service.DownloadSaveAsync("pull_me", destination);

        Assert.Equal(["EnumerateSessions", "DownloadSaveGame:pull_me"], client.Calls);
    }

    [Fact]
    public async Task DownloadSave_WhenSaveMissing_ThrowsAndDoesNotDownload()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["pull_me"])),
        };
        SaveManagementService service = CreateService(client);
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<SaveNotFoundException>(() => service.DownloadSaveAsync("pll_me", destination));

        Assert.Equal(["EnumerateSessions"], client.Calls);
    }

    // --- upload_save: no validation (creating a new name), passes load flag through -------------

    [Fact]
    public async Task UploadSave_StreamsWithLoadFlag_AndDoesNotEnumerate()
    {
        var client = new RecordingDedicatedServerApiClient();
        SaveManagementService service = CreateService(client);
        var content = new MemoryStream([1, 2, 3]);

        await service.UploadSaveAsync("fresh", content, loadImmediately: true);

        Assert.Equal(["UploadSaveGame:fresh:load=True"], client.Calls);
    }

    // --- rollback_to: ordering + abort-on-checkpoint-failure ------------------------------------

    [Fact]
    public async Task RollbackTo_CheckpointsBeforeLoading_AndReturnsBothNames()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["target"])),
        };
        SaveManagementService service = CreateService(client, new FakeTimeProvider(FixedNow));

        RollbackResult result = await service.RollbackToAsync("target");

        // Enumerate (validate), THEN SaveGame (checkpoint), THEN LoadGame — strictly in that order.
        Assert.Equal(
            ["EnumerateSessions", "SaveGame:pre-rollback-20260606T134507Z", "LoadGame:target"],
            client.Calls);
        Assert.Equal("pre-rollback-20260606T134507Z", result.CheckpointSaveName);
        Assert.Equal("target", result.LoadedSaveName);
    }

    [Fact]
    public async Task RollbackTo_WhenCheckpointSaveFails_AbortsWithoutLoading()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["target"])),
            OnSaveGame = _ => throw new InvalidOperationException("disk full"),
        };
        SaveManagementService service = CreateService(client, new FakeTimeProvider(FixedNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RollbackToAsync("target"));

        // The load must NOT have run: the checkpoint failure aborts the rollback. No LoadGame in the log.
        Assert.Equal(
            ["EnumerateSessions", "SaveGame:pre-rollback-20260606T134507Z"],
            client.Calls);
        Assert.DoesNotContain(client.Calls, c => c.StartsWith("LoadGame", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RollbackTo_WhenTargetMissing_ThrowsBeforeCheckpointing()
    {
        var client = new RecordingDedicatedServerApiClient
        {
            SessionsResponse = Sessions(0, ("Alpha", ["target"])),
        };
        SaveManagementService service = CreateService(client);

        await Assert.ThrowsAsync<SaveNotFoundException>(() => service.RollbackToAsync("targt"));

        // No checkpoint save and no load when the target name is invalid.
        Assert.Equal(["EnumerateSessions"], client.Calls);
    }
}
