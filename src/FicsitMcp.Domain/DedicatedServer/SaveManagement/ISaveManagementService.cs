namespace FicsitMcp.Domain.DedicatedServer.SaveManagement;

/// <summary>
/// Backing service for the MCP save-management tools. Owns the orchestration the thin tools must NOT
/// contain: validating a named save/session against the live <c>EnumerateSessions</c> list before any
/// destructive operation (with near-match suggestions on a miss), and the <c>rollback_to</c>
/// checkpoint-then-load composition. It depends on <see cref="IDedicatedServerApiClient"/> only, so it
/// is unit-testable against a faked client with no MCP, HTTP, or live server involved.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layering.</b> No MCP types appear here. The MCP tools in the host translate these methods and
/// records into the tool surface and behavioral hints; this service is the testable seam beneath them.
/// </para>
/// <para>
/// <b>Destructive-op safety.</b> <see cref="LoadSaveAsync"/>, <see cref="DeleteSaveAsync"/>,
/// <see cref="DeleteSessionAsync"/>, and <see cref="RollbackToAsync"/> first resolve the target name
/// against the live session list and throw <see cref="SaveNotFoundException"/> (carrying near-matches)
/// on a miss — so a bad name never reaches the destructive client call. <see cref="RollbackToAsync"/>
/// additionally takes the safety checkpoint BEFORE loading; a failed checkpoint aborts the load.
/// </para>
/// </remarks>
public interface ISaveManagementService
{
    /// <summary>
    /// Lists every save on the server, flattened across sessions and marked with the currently-loaded
    /// session. Read-only. (<c>EnumerateSessions</c>.)
    /// </summary>
    Task<SessionListResult> ListSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current game under <paramref name="saveName"/>. NON-idempotent (each call writes a
    /// new save). (<c>SaveGame</c>.)
    /// </summary>
    Task SaveGameAsync(string saveName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the session to auto-load when the server next starts. The session is validated against the
    /// live list first; an unknown name throws <see cref="SaveNotFoundException"/> with near-matches.
    /// (<c>SetAutoLoadSessionName</c>.)
    /// </summary>
    Task SetAutoLoadSessionAsync(string sessionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads <paramref name="saveName"/> — DESTRUCTIVE: it disconnects every connected player. The save
    /// is validated against the live list first; an unknown name throws
    /// <see cref="SaveNotFoundException"/> with near-matches before any load is attempted. (<c>LoadGame</c>.)
    /// </summary>
    Task LoadSaveAsync(string saveName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes the save file <paramref name="saveName"/>. Validated against the live list
    /// first (near-matches on a miss). DESTRUCTIVE and irreversible. (<c>DeleteSaveFile</c>.)
    /// </summary>
    Task DeleteSaveAsync(string saveName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes every save in the session <paramref name="sessionName"/>. Validated against
    /// the live session list first (near-matches on a miss). DESTRUCTIVE and irreversible.
    /// (<c>DeleteSaveSession</c>.)
    /// </summary>
    Task DeleteSessionAsync(string sessionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the save file <paramref name="saveName"/> to <paramref name="destination"/> in chunks
    /// (never buffered whole). The save is validated against the live list first (near-matches on a
    /// miss). Read-only with respect to server state. (<c>DownloadSaveGame</c>.)
    /// </summary>
    Task DownloadSaveAsync(string saveName, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a save file from <paramref name="saveGameContent"/> under <paramref name="saveName"/>,
    /// streamed via multipart (never buffered whole). When <paramref name="loadImmediately"/> is true
    /// the server loads it on receipt — which DISCONNECTS all connected players. Ownership of the
    /// stream transfers to this call. (<c>UploadSaveGame</c>.)
    /// </summary>
    Task UploadSaveAsync(
        string saveName,
        Stream saveGameContent,
        bool loadImmediately,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls the server back to <paramref name="saveName"/> by first taking a safety checkpoint of the
    /// CURRENT state, then loading the target. DESTRUCTIVE: the load disconnects every connected player.
    /// The target is validated against the live list first (near-matches on a miss); the checkpoint is
    /// taken BEFORE the load, and a failed checkpoint aborts the rollback without loading anything.
    /// </summary>
    /// <returns>The checkpoint save name created and the target save loaded.</returns>
    Task<RollbackResult> RollbackToAsync(string saveName, CancellationToken cancellationToken = default);
}
