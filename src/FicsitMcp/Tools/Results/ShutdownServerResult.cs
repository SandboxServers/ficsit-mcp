namespace FicsitMcp.Tools.Results;

/// <summary>
/// Result of <c>shutdown_server</c>. Confirms the shutdown request was accepted; the real-world
/// outcome (restart vs stay-down) depends on how the server process is supervised, which this MCP
/// server cannot observe from the API alone, so the result states the request was sent rather than
/// claiming a specific final state.
/// </summary>
/// <param name="Message">A human-readable confirmation that the shutdown request was accepted.</param>
public sealed record ShutdownServerResult(string Message);
