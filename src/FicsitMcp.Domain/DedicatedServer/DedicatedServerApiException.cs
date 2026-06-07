namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// Raised when the dedicated-server API returns an error envelope
/// (<c>{ "errorCode": ..., "errorMessage": ..., "errorData": ... }</c>) instead of a success
/// payload. Carries the server-supplied <see cref="ErrorCode"/> and <see cref="ServerMessage"/>
/// so callers (and, later, MCP tools) can branch on the machine-readable code rather than parsing
/// prose. Transport-level failures are NOT this type — those surface as
/// <see cref="FicsitMcp.Domain.Http.SurfaceUnreachableException"/> from the HTTP shell.
/// </summary>
/// <remarks>
/// This is the single typed protocol error for the surface. Auth-specific shapes derive from it
/// (<see cref="DedicatedServerAuthException"/>) so a caller can catch either the broad type or the
/// narrow one. Secrets (passwords, tokens) are never placed in the message or any property — only
/// the server's own error code/message and optional structured data are surfaced.
/// </remarks>
public class DedicatedServerApiException : Exception
{
    /// <summary>
    /// The machine-readable error code from the envelope (e.g. <c>wrong_password</c>,
    /// <c>server_claimed</c>, <c>save_game_load_failed</c>). May be empty if the server omitted it.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>The human-readable <c>errorMessage</c> from the envelope, if any.</summary>
    public string? ServerMessage { get; }

    /// <summary>
    /// The raw <c>errorData</c> object from the envelope as JSON text, if the server supplied one.
    /// Kept as text (not a typed shape) because its contents vary per error code and are advisory.
    /// </summary>
    public string? ErrorData { get; }

    /// <summary>
    /// The HTTP status code the error arrived on, when known. Useful to distinguish a JSON error
    /// envelope returned with a 200/4xx from a bare status with no body.
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>Creates an exception from a parsed error envelope.</summary>
    public DedicatedServerApiException(
        string errorCode,
        string? serverMessage,
        string? errorData = null,
        int? httpStatusCode = null,
        Exception? innerException = null)
        : base(BuildMessage(errorCode, serverMessage), innerException)
    {
        ErrorCode = errorCode ?? string.Empty;
        ServerMessage = serverMessage;
        ErrorData = errorData;
        HttpStatusCode = httpStatusCode;
    }

    private static string BuildMessage(string? errorCode, string? serverMessage)
    {
        string code = string.IsNullOrWhiteSpace(errorCode) ? "unknown_error" : errorCode;
        return string.IsNullOrWhiteSpace(serverMessage)
            ? $"Dedicated server API error '{code}'."
            : $"Dedicated server API error '{code}': {serverMessage}";
    }
}
