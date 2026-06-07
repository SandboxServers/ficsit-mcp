namespace FicsitMcp.Tools.Results;

/// <summary>
/// Result of <c>rename_server</c>. Confirms the server's display name was set. The new name is the
/// model's own input, so echoing it back is safe and useful confirmation.
/// </summary>
/// <param name="ServerName">The display name the server was renamed to.</param>
public sealed record RenameServerResult(string ServerName);
