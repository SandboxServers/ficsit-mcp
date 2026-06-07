namespace FicsitMcp.Tools.Results;

/// <summary>
/// Result of a password-changing tool (<c>set_client_password</c> / <c>set_admin_password</c>).
/// Confirms success WITHOUT ever restating the password: the secret goes in as input and is never
/// surfaced in any result, log, or exception. The boolean flags describe the change in non-secret
/// terms so the model can explain the consequence to the user.
/// </summary>
/// <param name="Message">A human-readable confirmation of what changed (never contains the password).</param>
/// <param name="PasswordCleared">
/// True when the change removed the password (an empty input). For the client password this means
/// the server no longer requires a password to join; for the admin password it is not a valid state
/// and the tool rejects it before sending.
/// </param>
public sealed record SetPasswordResult(string Message, bool PasswordCleared);
