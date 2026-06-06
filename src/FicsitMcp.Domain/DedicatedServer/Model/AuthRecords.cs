using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Request <c>data</c> for <c>PasswordlessLogin</c>.</summary>
/// <param name="MinimumPrivilegeLevel">The privilege level to request (e.g. <c>Administrator</c>).</param>
public sealed record PasswordlessLoginRequest(
    [property: JsonPropertyName("minimumPrivilegeLevel")] ApiPrivilegeLevel MinimumPrivilegeLevel);

/// <summary>Request <c>data</c> for <c>PasswordLogin</c>.</summary>
/// <param name="MinimumPrivilegeLevel">The privilege level to request.</param>
/// <param name="Password">The admin (or client) password. Never logged or echoed.</param>
public sealed record PasswordLoginRequest(
    [property: JsonPropertyName("minimumPrivilegeLevel")] ApiPrivilegeLevel MinimumPrivilegeLevel,
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Response <c>data</c> carrying a bearer token. Shared by <c>PasswordlessLogin</c>,
/// <c>PasswordLogin</c>, <c>ClaimServer</c>, and <c>SetAdminPassword</c> — all return the field
/// <c>authenticationToken</c> (camelCase, per the OpenAPI spec).
/// </summary>
/// <param name="AuthenticationToken">The bearer token to attach on subsequent calls.</param>
public sealed record AuthenticationTokenResponse(
    [property: JsonPropertyName("authenticationToken")] string AuthenticationToken);
