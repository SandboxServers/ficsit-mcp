using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Request <c>data</c> for <c>RunCommand</c>.</summary>
/// <param name="Command">The console command line to execute on the server.</param>
public sealed record RunCommandRequest(
    [property: JsonPropertyName("command")] string Command);

/// <summary>
/// Response <c>data</c> for <c>RunCommand</c>. The server returns the captured console output and a
/// boolean result. (Some server builds reply 204 with no body for fire-and-forget commands; callers
/// should treat a null response as "executed, no output".)
/// </summary>
/// <param name="CommandResult">Captured console output; lines separated by <c>\n</c>.</param>
/// <param name="ReturnValue">The command's boolean return value.</param>
public sealed record RunCommandResponse(
    [property: JsonPropertyName("commandResult")] string CommandResult,
    [property: JsonPropertyName("returnValue")] bool ReturnValue);
