using System.ComponentModel;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.Http;
using FicsitMcp.Domain.ServerObservation;

using ModelContextProtocol.Server;

namespace FicsitMcp.Tools;

/// <summary>
/// MCP tools for read-only observation of the running Satisfactory dedicated server: a state
/// summary, a health probe, and a cross-surface connection diagnostic. Each method is thin — it
/// delegates to <see cref="IServerObservationService"/> and returns the synthesized typed result.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exception mapping.</b> The backing service surfaces the dedicated-server client's typed
/// exceptions: <see cref="SurfaceNotConfiguredException"/> (surface dormant),
/// <see cref="DedicatedServerAuthException"/> (auth failed even after transparent re-auth),
/// <see cref="DedicatedServerApiException"/> (other server error envelope), and
/// <see cref="SurfaceUnreachableException"/> (transport-level failure). The MCP SDK converts a thrown
/// exception into a tool error and surfaces its <c>Message</c> to the model, so each of those messages
/// is ALREADY written to be actionable and secret-free (the typed exceptions never carry a password or
/// token). <c>get_server_state</c> and <c>health_check</c> therefore let those propagate unchanged —
/// the message is the actionable part. <c>verify_connection</c> is the deliberate exception: it never
/// throws on a per-surface failure, instead reporting reachability per surface, because its whole job
/// is to diagnose connectivity rather than fail on it.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class ServerStateTool
{
    /// <summary>
    /// Returns a compact summary of the live game state. Read-only, idempotent, and not open-world
    /// in the MCP sense (it talks only to the configured dedicated server, never the wider internet).
    /// </summary>
    [McpServerTool(Name = "get_server_state", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Returns a compact summary of the running Satisfactory dedicated server's live game state: " +
        "active session (save) name, connected player count and player limit, highest unlocked tech " +
        "tier, current game phase, whether the game is running and whether it is paused, the average " +
        "tick rate (ticks/second), and total in-game session duration in seconds. Use this when you " +
        "need a snapshot of WHAT the server is currently doing (who is on, how far the save has " +
        "progressed, is it paused). For a fast 'is it up and keeping up?' check use health_check " +
        "instead; to diagnose whether surfaces are configured and reachable use verify_connection. " +
        "Requires the dedicated-server HTTPS API to be configured; if it is not, or it cannot be " +
        "reached, the call fails with a message explaining how to fix it.")]
    public static async Task<ServerStateSummary> GetServerState(
        IServerObservationService observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await observation.GetServerStateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Probes the server's responsiveness and reports a typed health verdict. Read-only, idempotent,
    /// closed-world.
    /// </summary>
    [McpServerTool(Name = "health_check", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Probes the Satisfactory dedicated server's responsiveness and reports whether it is HEALTHY " +
        "or DEGRADED. The server reports DEGRADED ('slow') when its simulation is ticking below about " +
        "10 ticks per second — it is up and answering, but lagging (commonly a very large factory or " +
        "an overloaded host). Use this for a quick liveness/performance check. It does NOT report game " +
        "details (players, tier, phase) — use get_server_state for those — and it does not check the " +
        "FRM or FIN surfaces — use verify_connection for a full multi-surface diagnostic. Requires the " +
        "dedicated-server HTTPS API to be configured and reachable; otherwise the call fails with an " +
        "actionable message. This probe needs no admin token, so it also works on a not-yet-claimed " +
        "server.")]
    public static async Task<ServerHealth> HealthCheck(
        IServerObservationService observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await observation.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Diagnoses, per surface, whether it is configured and reachable. Read-only, idempotent,
    /// closed-world. Never throws on a per-surface failure — that is the whole point of the tool.
    /// </summary>
    [McpServerTool(Name = "verify_connection", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Diagnoses client setup across ALL THREE server surfaces and reports, for each, whether it is " +
        "configured and (if configured) whether it is currently reachable: the Dedicated Server HTTPS " +
        "API (probed with a cheap health check), Ficsit Remote Monitoring / FRM (probed with its " +
        "cheapest read), and the FicsIt-Networks / FIN bridge (an inbound listener — reports whether " +
        "the listener is up and whether any in-world agent has connected). Use this FIRST when a tool " +
        "fails with a connection or 'not configured' error, or when setting up the server, to see " +
        "which surfaces are missing or unreachable. It degrades gracefully: an unconfigured surface is " +
        "reported as 'not configured' with the exact environment variable to set, and an unreachable " +
        "surface is reported with the reason — the call itself does not fail, so one broken surface " +
        "never hides the status of the others.")]
    public static async Task<ConnectionDiagnostics> VerifyConnection(
        IServerObservationService observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return await observation.VerifyConnectionAsync(cancellationToken).ConfigureAwait(false);
    }
}
