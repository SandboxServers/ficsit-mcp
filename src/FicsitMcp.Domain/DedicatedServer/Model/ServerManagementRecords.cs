using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Request <c>data</c> for <c>ClaimServer</c> (one-shot: only a never-claimed server accepts it).</summary>
/// <param name="ServerName">The display name to give the freshly claimed server.</param>
/// <param name="AdminPassword">The initial admin password to set. Never logged or echoed.</param>
public sealed record ClaimServerRequest(
    [property: JsonPropertyName("serverName")] string ServerName,
    [property: JsonPropertyName("adminPassword")] string AdminPassword);

/// <summary>Request <c>data</c> for <c>RenameServer</c>.</summary>
/// <param name="ServerName">The new server display name.</param>
public sealed record RenameServerRequest(
    [property: JsonPropertyName("serverName")] string ServerName);

/// <summary>Request <c>data</c> for <c>SetClientPassword</c> (empty password removes it).</summary>
/// <param name="Password">The client password to set, or empty to clear. Never logged or echoed.</param>
public sealed record SetClientPasswordRequest(
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Request <c>data</c> for <c>SetAdminPassword</c>. Note: changing the admin password invalidates
/// the token used to make the change, so the request also supplies the NEW token the server should
/// honor going forward.
/// </summary>
/// <param name="Password">The new admin password. Never logged or echoed.</param>
/// <param name="AuthenticationToken">The new bearer token to take effect after the change.</param>
public sealed record SetAdminPasswordRequest(
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("authenticationToken")] string AuthenticationToken);

/// <summary>Request <c>data</c> for <c>SetAutoLoadSessionName</c>.</summary>
/// <param name="SessionName">The session to auto-load on next server start.</param>
public sealed record SetAutoLoadSessionNameRequest(
    [property: JsonPropertyName("sessionName")] string SessionName);
