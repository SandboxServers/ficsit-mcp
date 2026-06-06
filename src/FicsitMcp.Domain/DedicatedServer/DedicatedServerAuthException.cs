namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// An authentication/authorization failure on the dedicated-server surface: a bad password, an
/// expired or rejected bearer token, or insufficient privilege. Derives from
/// <see cref="DedicatedServerApiException"/> so callers can catch the broad protocol error or this
/// narrow auth case specifically.
/// </summary>
/// <remarks>
/// Thrown after the client's transparent re-auth-and-retry has already been attempted and still
/// failed (or when re-auth is impossible — e.g. no credentials configured). It never carries the
/// password or token that failed; only the server's error code/message.
/// </remarks>
public sealed class DedicatedServerAuthException : DedicatedServerApiException
{
    /// <summary>Creates an auth exception from a parsed error envelope or a bare 401.</summary>
    public DedicatedServerAuthException(
        string errorCode,
        string? serverMessage,
        string? errorData = null,
        int? httpStatusCode = null,
        Exception? innerException = null)
        : base(errorCode, serverMessage, errorData, httpStatusCode, innerException)
    {
    }
}
