namespace FicsitMcp.Domain.ServerObservation;

/// <summary>
/// Read-only observation of the live server across the configured surfaces. The MCP state/health
/// tools (#6) are thin wrappers over this service; the distillation of raw API payloads into compact
/// typed summaries, and the per-surface connection-diagnosis aggregation, live here so they are
/// unit-testable without the MCP host.
/// </summary>
public interface IServerObservationService
{
    /// <summary>
    /// Queries the dedicated server's live game state and distils it into a compact
    /// <see cref="ServerStateSummary"/>. Pure read. May throw the dedicated-server client's typed
    /// exceptions (auth, protocol, unreachable) — the tool layer turns those into actionable errors.
    /// </summary>
    Task<ServerStateSummary> GetServerStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes the dedicated server's responsiveness and maps the API's self-reported health to a
    /// typed <see cref="ServerHealth"/> verdict (healthy vs degraded/slow). Pure read.
    /// </summary>
    Task<ServerHealth> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Diagnoses connectivity across all three surfaces, reporting for each whether it is configured
    /// and, if so, whether a cheap probe reached it. Degrades gracefully: a surface that is
    /// unconfigured or unreachable is reported in the result, never thrown, so one bad surface cannot
    /// mask the others.
    /// </summary>
    Task<ConnectionDiagnostics> VerifyConnectionAsync(CancellationToken cancellationToken = default);
}
