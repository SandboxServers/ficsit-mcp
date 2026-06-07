namespace FicsitMcp.Domain.DedicatedServer;

/// <summary>
/// How a function relates to the bearer token, per the dedicated-server API's per-function privilege
/// requirements (the API authorizes by privilege level per function, not uniformly). The client uses
/// this to decide whether to attach the cached token and whether to fail fast when none is held.
/// </summary>
/// <remarks>
/// Verified against the Satisfactory wiki HTTPS API privilege table
/// (satisfactory.wiki.gg/wiki/Dedicated_servers/HTTPS_API):
/// <list type="bullet">
///   <item><see cref="None"/> — the login functions (no token can exist yet to attach).</item>
///   <item><see cref="AttachIfAvailable"/> — functions the spec marks as requiring NO privilege
///     (<c>HealthCheck</c>, <c>QueryServerState</c>, <c>GetServerOptions</c>,
///     <c>GetAdvancedGameSettings</c>). Sending a token is harmless and lets a privileged caller see
///     privileged fields, but a tokenless bootstrap host MUST still be able to call them (e.g. to
///     answer "is this server unclaimed?" before any token exists).</item>
///   <item><see cref="Required"/> — functions that need a privileged token (e.g.
///     <c>EnumerateSessions</c> requires Admin; all management/console/save writes; and
///     <c>VerifyAuthenticationToken</c>, which exists to validate the held token).</item>
/// </list>
/// </remarks>
internal enum AuthMode
{
    /// <summary>Never attach a bearer; never require one. The login functions.</summary>
    None,

    /// <summary>
    /// Attach the cached bearer when one is held, but do NOT require it — the function works tokenless
    /// (no privilege required by the spec). Enables pre-claim/bootstrap reads on a tokenless host.
    /// </summary>
    AttachIfAvailable,

    /// <summary>Attach the cached bearer and require one — fail fast with an auth error if none is held.</summary>
    Required,
}
