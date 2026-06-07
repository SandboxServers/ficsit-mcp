using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Request <c>data</c> for <c>SaveGame</c>.</summary>
/// <param name="SaveName">The name to save under (the server may sanitize it).</param>
public sealed record SaveGameRequest(
    [property: JsonPropertyName("saveName")] string SaveName);

/// <summary>Request <c>data</c> for <c>LoadGame</c>.</summary>
/// <param name="SaveName">The save to load.</param>
/// <param name="EnableAdvancedGameSettings">Whether to enable advanced game settings on load.</param>
public sealed record LoadGameRequest(
    [property: JsonPropertyName("saveName")] string SaveName,
    [property: JsonPropertyName("enableAdvancedGameSettings")] bool EnableAdvancedGameSettings = false);

/// <summary>Request <c>data</c> for <c>DeleteSaveFile</c>.</summary>
/// <param name="SaveName">The save file to delete.</param>
public sealed record DeleteSaveFileRequest(
    [property: JsonPropertyName("saveName")] string SaveName);

/// <summary>Request <c>data</c> for <c>DeleteSaveSession</c>.</summary>
/// <param name="SessionName">The session whose saves should be deleted.</param>
public sealed record DeleteSaveSessionRequest(
    [property: JsonPropertyName("sessionName")] string SessionName);

/// <summary>
/// Request <c>data</c> for <c>CreateNewGame</c>. Wraps the new-game settings under
/// <c>newGameData</c>, matching the server's nested shape.
/// </summary>
/// <param name="NewGameData">The settings for the new game.</param>
public sealed record CreateNewGameRequest(
    [property: JsonPropertyName("newGameData")] NewGameData NewGameData);

/// <summary>
/// New-game settings carried inside <c>CreateNewGame</c>. Note the field is <c>bSkipOnboarding</c>
/// (the Unreal boolean prefix), not <c>skipOnboarding</c> — a known server quirk that callers must
/// match exactly or onboarding is not skipped.
/// </summary>
public sealed record NewGameData(
    [property: JsonPropertyName("sessionName")] string SessionName,
    [property: JsonPropertyName("mapName")] string? MapName = null,
    [property: JsonPropertyName("startingLocation")] string? StartingLocation = null,
    [property: JsonPropertyName("bSkipOnboarding")] bool SkipOnboarding = false,
    [property: JsonPropertyName("advancedGameSettings")] IReadOnlyDictionary<string, string>? AdvancedGameSettings = null,
    [property: JsonPropertyName("customOptionsOnlyForModding")] IReadOnlyDictionary<string, string>? CustomOptionsOnlyForModding = null);

/// <summary>Response <c>data</c> for <c>EnumerateSessions</c>.</summary>
/// <param name="Sessions">The sessions present on the server, each with its save headers.</param>
/// <param name="CurrentSessionIndex">Index into <paramref name="Sessions"/> of the active session, or -1.</param>
public sealed record EnumerateSessionsResponse(
    [property: JsonPropertyName("sessions")] IReadOnlyList<SessionSaveStruct> Sessions,
    [property: JsonPropertyName("currentSessionIndex")] int CurrentSessionIndex);

/// <summary>A session and its save files, as returned by <c>EnumerateSessions</c>.</summary>
public sealed record SessionSaveStruct(
    [property: JsonPropertyName("sessionName")] string SessionName,
    [property: JsonPropertyName("saveHeaders")] IReadOnlyList<SaveHeader> SaveHeaders);

/// <summary>Metadata for a single save file within a session.</summary>
public sealed record SaveHeader(
    [property: JsonPropertyName("saveVersion")] int SaveVersion,
    [property: JsonPropertyName("buildVersion")] int BuildVersion,
    [property: JsonPropertyName("saveName")] string SaveName,
    [property: JsonPropertyName("mapName")] string? MapName,
    [property: JsonPropertyName("mapOptions")] string? MapOptions,
    [property: JsonPropertyName("sessionName")] string SessionName,
    [property: JsonPropertyName("playDurationSeconds")] int PlayDurationSeconds,
    [property: JsonPropertyName("saveDateTime")] string? SaveDateTime,
    [property: JsonPropertyName("isModdedSave")] bool IsModdedSave,
    [property: JsonPropertyName("isEditedSave")] bool IsEditedSave,
    [property: JsonPropertyName("isCreativeModeEnabled")] bool IsCreativeModeEnabled);

/// <summary>
/// The JSON <c>data</c> part of an <c>UploadSaveGame</c> multipart request. The binary save is a
/// separate <c>saveGameFile</c> part; this carries only the metadata.
/// </summary>
/// <param name="SaveName">The name to store the uploaded save under.</param>
/// <param name="LoadSaveGame">Whether to immediately load the uploaded save.</param>
/// <param name="EnableAdvancedGameSettings">Whether to enable advanced settings if loading.</param>
public sealed record UploadSaveGameRequest(
    [property: JsonPropertyName("saveName")] string SaveName,
    [property: JsonPropertyName("loadSaveGame")] bool LoadSaveGame = false,
    [property: JsonPropertyName("enableAdvancedGameSettings")] bool EnableAdvancedGameSettings = false);

/// <summary>Request <c>data</c> for <c>DownloadSaveGame</c>.</summary>
/// <param name="SaveName">The save to download.</param>
public sealed record DownloadSaveGameRequest(
    [property: JsonPropertyName("saveName")] string SaveName);
