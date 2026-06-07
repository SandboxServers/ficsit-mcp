namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// The server-sent <c>errorCode</c> string values the client recognises as AUTH failures (mapped to
/// <see cref="DedicatedServerAuthException"/> rather than the generic
/// <see cref="DedicatedServerApiException"/>). Named here, beside <see cref="ApiFunctions"/>, so the
/// auth-vs-not classification is one greppable list instead of bare literals buried in a switch.
/// </summary>
/// <remarks>
/// These are the codes the server itself returns. Codes the CLIENT synthesizes (e.g.
/// <c>no_credentials</c>, <c>config_token_rejected</c>) are intentionally not listed here — they are
/// constructed directly at their throw sites with their own actionable messages.
/// </remarks>
internal static class ApiErrorCodes
{
    /// <summary>The supplied password was incorrect (PasswordLogin).</summary>
    public const string WrongPassword = "wrong_password";

    /// <summary>The request was not authorized — also the synthetic code for a bare 401.</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>The bearer token was not a valid token.</summary>
    public const string InvalidToken = "invalid_token";

    /// <summary>The bearer token has expired.</summary>
    public const string TokenExpired = "token_expired";

    /// <summary>The token's privilege level is too low for the requested function.</summary>
    public const string InsufficientPrivilege = "insufficient_privilege";

    /// <summary>Passwordless login is not available in the server's current state.</summary>
    public const string PasswordlessLoginNotPossible = "passwordless_login_not_possible";
}
