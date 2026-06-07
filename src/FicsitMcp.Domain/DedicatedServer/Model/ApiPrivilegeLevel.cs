namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>
/// Privilege levels the dedicated-server API recognises, sent as the <c>minimumPrivilegeLevel</c>
/// field on the login functions and encoded in the <c>pl</c> claim of an authentication token.
/// </summary>
/// <remarks>
/// Serialized by NAME (the exact PascalCase strings the server expects), not by integer, so the
/// JSON contract is the documented identifier. <see cref="InitialAdmin"/> is claim-only (a
/// never-claimed server grants it so <c>ClaimServer</c> can set the first admin password);
/// <see cref="APIToken"/> is the level carried by a pre-provisioned <c>server.GenerateAPIToken</c>
/// token.
/// </remarks>
public enum ApiPrivilegeLevel
{
    /// <summary>No authentication; only the unauthenticated functions are reachable.</summary>
    NotAuthenticated,

    /// <summary>A connected game client (client-password level access).</summary>
    Client,

    /// <summary>Full administrative access to management functions.</summary>
    Administrator,

    /// <summary>Granted only by a never-claimed server, used to perform <c>ClaimServer</c>.</summary>
    InitialAdmin,

    /// <summary>A third-party/pre-provisioned API token (from <c>server.GenerateAPIToken</c>).</summary>
    APIToken,
}
