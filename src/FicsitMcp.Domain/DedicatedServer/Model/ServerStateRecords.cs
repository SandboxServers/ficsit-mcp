using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>
/// Response <c>data</c> for <c>QueryServerState</c>. The server nests the actual state under a
/// <c>serverGameState</c> sub-object (a sub-state envelope), so this wrapper exists to match that
/// shape rather than flattening it.
/// </summary>
/// <param name="ServerGameState">The current game state of the running server.</param>
public sealed record QueryServerStateResponse(
    [property: JsonPropertyName("serverGameState")] ServerGameState ServerGameState);

/// <summary>
/// The dedicated server's live game state, as returned inside <c>QueryServerState</c>'s
/// <c>serverGameState</c> object. Fields are camelCase per the OpenAPI spec.
/// </summary>
public sealed record ServerGameState(
    [property: JsonPropertyName("activeSessionName")] string? ActiveSessionName,
    [property: JsonPropertyName("numConnectedPlayers")] int NumConnectedPlayers,
    [property: JsonPropertyName("playerLimit")] int PlayerLimit,
    [property: JsonPropertyName("techTier")] int TechTier,
    [property: JsonPropertyName("activeSchematic")] string? ActiveSchematic,
    [property: JsonPropertyName("gamePhase")] string? GamePhase,
    [property: JsonPropertyName("isGameRunning")] bool IsGameRunning,
    [property: JsonPropertyName("totalGameDuration")] int TotalGameDuration,
    [property: JsonPropertyName("isGamePaused")] bool IsGamePaused,
    [property: JsonPropertyName("averageTickRate")] double AverageTickRate,
    [property: JsonPropertyName("autoLoadSessionName")] string? AutoLoadSessionName);
