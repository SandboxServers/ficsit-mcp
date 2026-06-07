using System.ComponentModel;

using FicsitMcp.Domain.DedicatedServer.SaveManagement;

using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// MCP tools for managing dedicated-server save games: list, save, load, transfer (upload/download),
/// delete, set auto-load, and roll back. Thin by design — each method validates its arguments and
/// delegates to <see cref="ISaveManagementService"/>, which owns the live-name validation, near-match
/// suggestions, and rollback checkpoint-then-load composition. No HTTP, envelope, or token detail
/// reaches this layer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Behavioral hints are set HONESTLY per the real consequence.</b> <c>load_save</c>,
/// <c>delete_save</c>, <c>delete_session</c>, <c>upload_save</c>, and <c>rollback_to</c> are
/// <c>Destructive = true</c>; the destructive descriptions lead with the consequence (player
/// disconnect for loads; permanent deletion for deletes) so a well-behaved model warns the user before
/// invoking. All tools are <c>OpenWorld = true</c> — they act on a live external game server, not a
/// static snapshot.
/// </para>
/// <para>
/// <b>Validation before destruction.</b> Loads, deletes, and rollback resolve the named save/session
/// against the live <c>EnumerateSessions</c> list first; an unknown name fails with a
/// <see cref="SaveNotFoundException"/> listing the closest existing names, so the model can self-correct
/// in one round trip rather than firing a destructive call against a typo.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class SaveManagementTool
{
    /// <summary>
    /// Lists all save files on the server across every session, marking the currently-loaded one.
    /// Read-only.
    /// </summary>
    [McpServerTool(Name = "list_sessions", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description(
        "Lists every save file on the Satisfactory dedicated server, grouped by session, and marks "
        + "which session is currently loaded. Read-only: it changes nothing on the server. Use this to "
        + "discover the exact save/session names that the load, download, delete, and rollback tools "
        + "require, and to see save timestamps and play durations before choosing one.")]
    public static Task<SessionListResult> ListSessions(
        ISaveManagementService saves,
        CancellationToken cancellationToken)
        => saves.ListSessionsAsync(cancellationToken);

    /// <summary>Saves the current game under the given name. Not idempotent (writes a new save each call).</summary>
    [McpServerTool(Name = "save_game", ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description(
        "Saves the current running game on the server under the given name (creating a manual "
        + "checkpoint). Players are NOT disconnected. Not idempotent: each call writes a save, and "
        + "reusing an existing name overwrites that save. Returns when the server confirms the save.")]
    public static Task SaveGame(
        ISaveManagementService saves,
        [Description("The name to save under (e.g. \"before-nuclear-rebuild\"). The server may sanitize it.")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.SaveGameAsync(name, cancellationToken);
    }

    /// <summary>
    /// Loads a save — DESTRUCTIVE: disconnects all connected players. Validates the name first.
    /// </summary>
    [McpServerTool(Name = "load_save", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Loading a save disconnects every connected player while the server reloads the world. Replaces "
        + "the running game with the named save. The save name is validated against the live save list "
        + "first; an unknown name fails with the closest matching names so you can retry. Because this "
        + "interrupts everyone playing, confirm with the user before invoking. To preserve the current "
        + "world first, use rollback_to instead, which checkpoints before loading.")]
    public static Task LoadSave(
        ISaveManagementService saves,
        [Description("The exact save name to load (get valid names from list_sessions).")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.LoadSaveAsync(name, cancellationToken);
    }

    /// <summary>Permanently deletes one save file — DESTRUCTIVE. Validates the name first.</summary>
    [McpServerTool(Name = "delete_save", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Permanently and irreversibly deletes a single save file from the server. The save name is "
        + "validated against the live save list first; an unknown name fails with the closest matching "
        + "names. Because deletion cannot be undone, confirm with the user before invoking. To delete "
        + "all saves in a session at once, use delete_session.")]
    public static Task DeleteSave(
        ISaveManagementService saves,
        [Description("The exact save name to delete (get valid names from list_sessions).")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.DeleteSaveAsync(name, cancellationToken);
    }

    /// <summary>Permanently deletes every save in a session — DESTRUCTIVE. Validates the name first.</summary>
    [McpServerTool(Name = "delete_session", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Permanently and irreversibly deletes ALL save files belonging to a session. The session name is "
        + "validated against the live session list first; an unknown name fails with the closest matching "
        + "names. This removes multiple saves at once and cannot be undone, so confirm with the user "
        + "before invoking. To delete a single save instead, use delete_save.")]
    public static Task DeleteSession(
        ISaveManagementService saves,
        [Description("The exact session name whose saves should all be deleted (get valid names from list_sessions).")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.DeleteSessionAsync(name, cancellationToken);
    }

    /// <summary>Downloads a save off-box to a local path. Read-only with respect to server state.</summary>
    [McpServerTool(Name = "download_save", ReadOnly = true, Idempotent = true, OpenWorld = true)]
    [Description(
        "Downloads a save file off the server to a local file path, streaming it (never buffered whole "
        + "in memory). Read-only with respect to the server: it changes nothing on the server. The save "
        + "name is validated against the live save list first; an unknown name fails with the closest "
        + "matching names. Use this to back up or transfer a save before a risky operation.")]
    public static async Task<string> DownloadSave(
        ISaveManagementService saves,
        [Description("The exact save name to download (get valid names from list_sessions).")]
        string name,
        [Description("Absolute local file path to write the save to. An existing file is overwritten.")]
        string localPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        // The destination file is a LOCAL artifact owned by this tool, not server state; create/truncate
        // it and stream into it. Opened here (not in the service) so the service stays free of file I/O
        // policy and the stream lifetime is unambiguous.
        await using FileStream destination = new(
            localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await saves.DownloadSaveAsync(name, destination, cancellationToken).ConfigureAwait(false);
        return localPath;
    }

    /// <summary>
    /// Uploads a local save to the server — DESTRUCTIVE when load_immediately disconnects players or a
    /// name is overwritten.
    /// </summary>
    [McpServerTool(Name = "upload_save", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Uploads a local save file to the server, streaming it via multipart (never buffered whole). "
        + "When load_immediately is true the server LOADS the uploaded save on receipt, which disconnects "
        + "every connected player — confirm with the user in that case. Reusing an existing save name "
        + "overwrites it. Returns when the server confirms receipt.")]
    public static async Task UploadSave(
        ISaveManagementService saves,
        [Description("The save name to store the uploaded file under on the server (overwrites if it exists).")]
        string name,
        [Description("Absolute local file path of the save file to upload.")]
        string localPath,
        [Description(
            "When true, the server loads the uploaded save immediately, DISCONNECTING all connected "
            + "players. When false (default), the save is only stored.")]
        bool loadImmediately,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        // Stream the local file straight to the multipart body; the service/client own its disposal.
        FileStream content = new(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await saves.UploadSaveAsync(name, content, loadImmediately, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets the session to auto-load on next start. Validates the name first; not destructive.</summary>
    [McpServerTool(Name = "set_auto_load_session", ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Sets which session the server auto-loads the next time it starts. Does NOT load anything now "
        + "and does not disconnect players. The session name is validated against the live session list "
        + "first; an unknown name fails with the closest matching names. Idempotent: setting the same "
        + "session again has no further effect.")]
    public static Task SetAutoLoadSession(
        ISaveManagementService saves,
        [Description("The exact session name to auto-load on next start (get valid names from list_sessions).")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.SetAutoLoadSessionAsync(name, cancellationToken);
    }

    /// <summary>
    /// Rolls back to a save, checkpointing the current world first — DESTRUCTIVE: the load disconnects
    /// all players. Validates the target first.
    /// </summary>
    [McpServerTool(Name = "rollback_to", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
    [Description(
        "Rolling back disconnects every connected player while the server reloads the world. Safely rolls "
        + "the server back to the named save by FIRST saving the current state to a 'pre-rollback-<utc>' "
        + "checkpoint, then loading the target — so the world you rolled back from is recoverable. The "
        + "target save name is validated against the live save list first; an unknown name fails with the "
        + "closest matching names, and a failed checkpoint aborts the rollback without loading anything. "
        + "Because this interrupts everyone playing, confirm with the user before invoking. Returns the "
        + "checkpoint name created and the save loaded.")]
    public static Task<RollbackResult> RollbackTo(
        ISaveManagementService saves,
        [Description("The exact save name to roll back to (get valid names from list_sessions).")]
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return saves.RollbackToAsync(name, cancellationToken);
    }
}
