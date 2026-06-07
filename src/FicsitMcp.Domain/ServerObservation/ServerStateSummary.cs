namespace FicsitMcp.Domain.ServerObservation;

/// <summary>
/// A compact, typed summary of the running dedicated server's live game state, distilled from the
/// API's <c>QueryServerState</c> payload. This deliberately does NOT echo the raw response: it
/// surfaces only the operator-relevant fields so the model gets an at-a-glance picture instead of a
/// wall of JSON.
/// </summary>
/// <param name="ActiveSessionName">The session (save) currently loaded, or null if none is active.</param>
/// <param name="ConnectedPlayers">Number of players currently connected.</param>
/// <param name="PlayerLimit">Maximum players the server allows.</param>
/// <param name="TechTier">The highest tech tier unlocked in the active session.</param>
/// <param name="GamePhase">The current game phase (project-assembly progression), or null if unknown.</param>
/// <param name="IsGameRunning">True when a game is loaded and simulating (false during load/transition).</param>
/// <param name="IsGamePaused">True when the simulation is paused (e.g. no players online and auto-pause on).</param>
/// <param name="AverageTickRate">The server's recent average tick rate, in ticks per second.</param>
/// <param name="TotalGameDurationSeconds">Total elapsed in-game time for the session, in seconds.</param>
public sealed record ServerStateSummary(
    string? ActiveSessionName,
    int ConnectedPlayers,
    int PlayerLimit,
    int TechTier,
    string? GamePhase,
    bool IsGameRunning,
    bool IsGamePaused,
    double AverageTickRate,
    int TotalGameDurationSeconds);
