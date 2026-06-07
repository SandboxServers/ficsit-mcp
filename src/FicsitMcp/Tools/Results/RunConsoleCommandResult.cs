namespace FicsitMcp.Tools.Results;

/// <summary>
/// Result of <c>run_console_command</c>. Carries the console output and the command's boolean
/// return value back to the model. Never echoes anything secret (the command text itself is the
/// model's own input, so it is safe to surface).
/// </summary>
/// <param name="Output">
/// The captured console output (lines separated by <c>\n</c>). Empty when the server executed the
/// command without producing output (some builds reply 204 for fire-and-forget commands).
/// </param>
/// <param name="ReturnValue">
/// The command's boolean return value, or <c>true</c> when the server returned no body (treated as
/// "executed, no output").
/// </param>
public sealed record RunConsoleCommandResult(string Output, bool ReturnValue);
