using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.DedicatedServer.Model;

/// <summary>Request <c>data</c> for <c>HealthCheck</c>.</summary>
/// <param name="ClientCustomData">
/// Opaque caller-defined string echoed to mods' health handlers; empty when unused.
/// </param>
public sealed record HealthCheckRequest(
    [property: JsonPropertyName("clientCustomData")] string ClientCustomData = "");

/// <summary>Response <c>data</c> for <c>HealthCheck</c>.</summary>
/// <param name="Health">Server health: <c>"healthy"</c> or <c>"slow"</c>.</param>
/// <param name="ServerCustomData">Opaque server-defined string (mod-populated; usually empty).</param>
public sealed record HealthCheckResponse(
    [property: JsonPropertyName("health")] string Health,
    [property: JsonPropertyName("serverCustomData")] string ServerCustomData = "");
