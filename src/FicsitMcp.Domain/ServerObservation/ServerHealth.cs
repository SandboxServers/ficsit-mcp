namespace FicsitMcp.Domain.ServerObservation;

/// <summary>
/// The server's responsiveness, as reported by the API's <c>HealthCheck</c> function. The API itself
/// decides the verdict: it reports <c>"slow"</c> when the simulation is ticking below ~10 tps, which
/// this maps to <see cref="ServerHealthStatus.Degraded"/>.
/// </summary>
public enum ServerHealthStatus
{
    /// <summary>The server is reachable but its self-reported health string was not recognised.</summary>
    Unknown = 0,

    /// <summary>The server reports it is ticking normally (the API's <c>"healthy"</c>).</summary>
    Healthy,

    /// <summary>
    /// The server reports it is running slowly — below ~10 ticks per second (the API's <c>"slow"</c>).
    /// The server is up and answering, but the simulation is lagging.
    /// </summary>
    Degraded,
}

/// <summary>
/// A typed health verdict for the dedicated server, distilled from the API's <c>HealthCheck</c>
/// response so the model gets a status it can branch on rather than a raw <c>"healthy"</c>/<c>"slow"</c>
/// string.
/// </summary>
/// <param name="Status">The mapped health status.</param>
/// <param name="RawHealth">The server's raw health string (e.g. <c>"healthy"</c>, <c>"slow"</c>).</param>
public sealed record ServerHealth(ServerHealthStatus Status, string RawHealth);
