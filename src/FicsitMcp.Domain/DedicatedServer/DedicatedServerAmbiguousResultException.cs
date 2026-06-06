namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// Raised when a <em>non-idempotent</em> function (e.g. <c>SaveGame</c>, <c>RunCommand</c>,
/// <c>Shutdown</c>, <c>LoadGame</c>) is rejected with a 401 and the client cannot safely determine
/// whether the original call already took effect on the server before the token was deemed invalid.
/// </summary>
/// <remarks>
/// <para>
/// The client re-authenticates on a 401 and replays the request <em>only</em> when replay is safe.
/// For an idempotent function, blind replay is fine. For a non-idempotent function the original
/// delivery is ambiguous: the server may have executed the command and only then rejected the
/// (now-rotated) token — replaying could run the command twice (a doubled console command, a second
/// save, a second shutdown). Rather than risk that, the client surfaces the ambiguity here so the
/// caller can re-query state and decide, instead of the client silently double-firing a side effect.
/// </para>
/// <para>
/// Carries the underlying <see cref="DedicatedServerApiException.ErrorCode"/> (typically the auth
/// failure) so tooling can explain the situation. The <see cref="FunctionName"/> identifies which
/// call was left in an unknown state.
/// </para>
/// <para>
/// <b>Recovery strategy for callers.</b> Do NOT blindly re-invoke the function — that is exactly the
/// double-fire this exception exists to prevent. Instead, QUERY state to determine whether the
/// operation already took effect, then act on the answer:
/// <list type="bullet">
///   <item><description><c>SaveGame</c> / <c>UploadSaveGame</c> / <c>DeleteSaveFile</c> /
///     <c>DeleteSaveSession</c>: call <see cref="IDedicatedServerApiClient.EnumerateSessionsAsync"/>
///     and check whether the target save now exists (or is gone) before retrying.</description></item>
///   <item><description><c>LoadGame</c> / <c>CreateNewGame</c>: call
///     <see cref="IDedicatedServerApiClient.QueryServerStateAsync"/> and compare the active session
///     name / game state to the intended target.</description></item>
///   <item><description><c>RunCommand</c>: there is no generic way to confirm an arbitrary command's
///     effect — surface the ambiguity to the human operator rather than re-running it.</description></item>
///   <item><description><c>Shutdown</c>: probe with
///     <see cref="IDedicatedServerApiClient.HealthCheckAsync"/> /
///     <see cref="IDedicatedServerApiClient.VerifyAuthenticationTokenAsync"/>; an unreachable/401
///     server indicates the shutdown likely took effect.</description></item>
/// </list>
/// In all cases, first re-establish a valid token (the 401 that triggered this may mean the token
/// rotated) before issuing the state query.
/// </para>
/// </remarks>
public sealed class DedicatedServerAmbiguousResultException : DedicatedServerApiException
{
    /// <summary>The API function whose delivery could not be confirmed.</summary>
    public string FunctionName { get; }

    /// <summary>Creates an ambiguous-result exception for the given non-idempotent function.</summary>
    public DedicatedServerAmbiguousResultException(
        string functionName,
        string errorCode,
        string? serverMessage,
        Exception? innerException = null)
        : base(errorCode, BuildMessage(functionName, serverMessage), errorData: null, httpStatusCode: 401, innerException)
    {
        FunctionName = functionName;
    }

    private static string BuildMessage(string functionName, string? serverMessage)
    {
        string suffix = string.IsNullOrWhiteSpace(serverMessage) ? string.Empty : $" ({serverMessage})";
        return
            $"Authentication for '{functionName}' was rejected after the call may already have been " +
            $"delivered. Because '{functionName}' is not idempotent, it was NOT retried to avoid " +
            $"executing it twice. Re-query server state to determine whether it took effect.{suffix}";
    }
}
