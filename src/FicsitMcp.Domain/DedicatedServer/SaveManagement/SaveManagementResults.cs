namespace FicsitMcp.Domain.DedicatedServer.SaveManagement;

using FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>
/// A flattened view of one save file as returned by <c>list_sessions</c>, annotated with whether it
/// belongs to the currently-loaded session. This is the MCP-facing shape: it deliberately omits the
/// transport details of <see cref="EnumerateSessionsResponse"/> and surfaces only what a model needs
/// to choose a save to load, roll back to, download, or delete.
/// </summary>
/// <param name="SessionName">The session this save belongs to.</param>
/// <param name="SaveName">The save file's name (the value to pass to load/download/delete tools).</param>
/// <param name="IsCurrentSession">True when this save's session is the one currently loaded on the server.</param>
/// <param name="SaveDateTime">The save's timestamp as the server reported it (raw string), if any.</param>
/// <param name="PlayDurationSeconds">In-game play duration recorded in the save header.</param>
/// <param name="IsModdedSave">Whether the save was produced with mods enabled.</param>
/// <param name="IsCreativeModeEnabled">Whether advanced/creative-mode settings were enabled in the save.</param>
public sealed record SaveFileInfo(
    string SessionName,
    string SaveName,
    bool IsCurrentSession,
    string? SaveDateTime,
    int PlayDurationSeconds,
    bool IsModdedSave,
    bool IsCreativeModeEnabled);

/// <summary>
/// Result of <c>list_sessions</c>: every save file on the server flattened across sessions, plus the
/// name of the currently-loaded session (or null if none is active).
/// </summary>
/// <param name="CurrentSessionName">The active session's name, or null when no session is loaded.</param>
/// <param name="Saves">All save files across all sessions; empty when the server has no saves.</param>
public sealed record SessionListResult(
    string? CurrentSessionName,
    IReadOnlyList<SaveFileInfo> Saves);

/// <summary>
/// Result of <c>rollback_to</c>: the composite checkpoint-then-load operation. The checkpoint save is
/// taken FIRST (before the destructive load) so the pre-rollback world is recoverable; this result
/// reports the checkpoint name that was created and the save that was subsequently loaded.
/// </summary>
/// <param name="CheckpointSaveName">The safety save created before loading (e.g. <c>pre-rollback-...</c>).</param>
/// <param name="LoadedSaveName">The target save that was loaded after the checkpoint succeeded.</param>
public sealed record RollbackResult(
    string CheckpointSaveName,
    string LoadedSaveName);
