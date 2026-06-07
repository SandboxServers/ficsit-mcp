using System.Collections.Immutable;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.DedicatedServer.Model;
using FicsitMcp.Domain.FinBridge;
using FicsitMcp.Domain.Frm;

namespace FicsitMcp.Domain.ServerObservation;

/// <summary>
/// Default <see cref="IServerObservationService"/>. Reads through the typed surface clients and
/// distils their payloads; owns the per-surface aggregation for connection diagnosis.
/// </summary>
/// <remarks>
/// <para>
/// The FIN bridge is injected as an OPTIONAL dependency (<c>IFinBridge?</c>): its registration is
/// gated on the bridge being configured, so when the surface is dormant there is simply no
/// <see cref="IFinBridge"/> in the container. A null bridge therefore means "not configured" (or, in
/// theory, configured-but-not-yet-registered); the FIN-bridge config presence is read from
/// <see cref="FinBridgeOptions"/> independently so the diagnostic reports configuration accurately.
/// </para>
/// <para>
/// State and health reads let the dedicated-server client's typed exceptions propagate — their
/// messages are already actionable, and the tool layer relies on that. Only
/// <see cref="VerifyConnectionAsync"/> swallows per-surface failures, because its whole purpose is to
/// REPORT reachability rather than fail on it.
/// </para>
/// </remarks>
public sealed class ServerObservationService : IServerObservationService
{
    /// <summary>The health string the API returns when the server is ticking normally.</summary>
    private const string HealthyString = "healthy";

    /// <summary>The health string the API returns when the simulation is below ~10 tps.</summary>
    private const string SlowString = "slow";

    private readonly IDedicatedServerApiClient _dedicatedServer;
    private readonly IFrmClient _frm;
    private readonly IFinBridge? _finBridge;
    private readonly DedicatedServerOptions _dedicatedServerOptions;
    private readonly FrmOptions _frmOptions;
    private readonly FinBridgeOptions _finBridgeOptions;

    /// <summary>Creates the service over the typed surface clients and their bound options.</summary>
    /// <param name="dedicatedServer">The dedicated-server HTTPS API client.</param>
    /// <param name="frm">The FRM observe-surface client.</param>
    /// <param name="dedicatedServerOptions">Dedicated-server surface config (for configured-state).</param>
    /// <param name="frmOptions">FRM surface config (for configured-state).</param>
    /// <param name="finBridgeOptions">FIN bridge surface config (for configured-state).</param>
    /// <param name="finBridge">
    /// The FIN bridge, or null when the surface is unconfigured (its DI registration is gated on
    /// configuration, so it is absent from the container when dormant).
    /// </param>
    public ServerObservationService(
        IDedicatedServerApiClient dedicatedServer,
        IFrmClient frm,
        DedicatedServerOptions dedicatedServerOptions,
        FrmOptions frmOptions,
        FinBridgeOptions finBridgeOptions,
        IFinBridge? finBridge = null)
    {
        ArgumentNullException.ThrowIfNull(dedicatedServer);
        ArgumentNullException.ThrowIfNull(frm);
        ArgumentNullException.ThrowIfNull(dedicatedServerOptions);
        ArgumentNullException.ThrowIfNull(frmOptions);
        ArgumentNullException.ThrowIfNull(finBridgeOptions);

        _dedicatedServer = dedicatedServer;
        _frm = frm;
        _dedicatedServerOptions = dedicatedServerOptions;
        _frmOptions = frmOptions;
        _finBridgeOptions = finBridgeOptions;
        _finBridge = finBridge;
    }

    /// <inheritdoc />
    public async Task<ServerStateSummary> GetServerStateAsync(CancellationToken cancellationToken = default)
    {
        QueryServerStateResponse response =
            await _dedicatedServer.QueryServerStateAsync(cancellationToken).ConfigureAwait(false);
        ServerGameState state = response.ServerGameState;

        return new ServerStateSummary(
            ActiveSessionName: state.ActiveSessionName,
            ConnectedPlayers: state.NumConnectedPlayers,
            PlayerLimit: state.PlayerLimit,
            TechTier: state.TechTier,
            GamePhase: state.GamePhase,
            IsGameRunning: state.IsGameRunning,
            IsGamePaused: state.IsGamePaused,
            AverageTickRate: state.AverageTickRate,
            TotalGameDurationSeconds: state.TotalGameDuration);
    }

    /// <inheritdoc />
    public async Task<ServerHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        HealthCheckResponse response =
            await _dedicatedServer.HealthCheckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        ServerHealthStatus status = MapHealth(response.Health);
        return new ServerHealth(status, response.Health);
    }

    /// <inheritdoc />
    public async Task<ConnectionDiagnostics> VerifyConnectionAsync(CancellationToken cancellationToken = default)
    {
        SurfaceConnectionStatus dedicatedServer =
            await ProbeDedicatedServerAsync(cancellationToken).ConfigureAwait(false);
        SurfaceConnectionStatus frm = await ProbeFrmAsync(cancellationToken).ConfigureAwait(false);
        SurfaceConnectionStatus finBridge = ProbeFinBridge();

        return new ConnectionDiagnostics([dedicatedServer, frm, finBridge]);
    }

    /// <summary>Maps the API's raw health string to the typed status (unknown if unrecognised).</summary>
    private static ServerHealthStatus MapHealth(string? rawHealth) => rawHealth switch
    {
        not null when rawHealth.Equals(HealthyString, StringComparison.OrdinalIgnoreCase)
            => ServerHealthStatus.Healthy,
        not null when rawHealth.Equals(SlowString, StringComparison.OrdinalIgnoreCase)
            => ServerHealthStatus.Degraded,
        _ => ServerHealthStatus.Unknown,
    };

    private async Task<SurfaceConnectionStatus> ProbeDedicatedServerAsync(CancellationToken cancellationToken)
    {
        if (!_dedicatedServerOptions.IsConfigured)
        {
            return NotConfigured(DedicatedServerOptions.SurfaceName, DedicatedServerOptions.ActivatingEnvVar);
        }

        try
        {
            // HealthCheck is the cheapest probe and requires no privilege, so it confirms reachability
            // even on a tokenless bootstrap host.
            HealthCheckResponse health =
                await _dedicatedServer.HealthCheckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return Reachable(
                DedicatedServerOptions.SurfaceName,
                $"Reachable; server reports health '{health.Health}'.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Unreachable(DedicatedServerOptions.SurfaceName, ex.Message);
        }
    }

    private async Task<SurfaceConnectionStatus> ProbeFrmAsync(CancellationToken cancellationToken)
    {
        if (!_frmOptions.IsConfigured)
        {
            return NotConfigured(FrmOptions.SurfaceName, FrmOptions.ActivatingEnvVar);
        }

        try
        {
            // getProdStats is FRM's cheapest read (in-memory aggregate), so it is the lightest
            // reachability probe for this surface.
            _ = await _frm.GetProdStatsAsync(cancellationToken).ConfigureAwait(false);
            return Reachable(FrmOptions.SurfaceName, "Reachable; FRM web server responded.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Unreachable(FrmOptions.SurfaceName, ex.Message);
        }
    }

    private SurfaceConnectionStatus ProbeFinBridge()
    {
        if (!_finBridgeOptions.IsConfigured)
        {
            return NotConfigured(FinBridgeOptions.SurfaceName, FinBridgeOptions.ActivatingEnvVar);
        }

        // The FIN bridge is an INBOUND listener, not an outbound client: there is nothing for us to
        // "call". Reachability here means the in-process listener was wired up (the bridge is
        // registered) — the in-world agent connects to US. Report the listener state plus how many
        // agents have ever checked in, which is the actionable signal for setup debugging.
        if (_finBridge is null)
        {
            return Unreachable(
                FinBridgeOptions.SurfaceName,
                "Configured but the bridge listener is not registered in this host. Restart the server " +
                "so the listener starts.");
        }

        int liveAgents = _finBridge.GetAgents().Count(agent => agent.IsAlive);
        int knownAgents = _finBridge.GetAgents().Count;
        string detail = knownAgents == 0
            ? $"Listener up on {_finBridgeOptions.ListenUrl}; no in-world FIN agent has connected yet. " +
              "Power the FIN computer and run the agent script."
            : $"Listener up on {_finBridgeOptions.ListenUrl}; {liveAgents} of {knownAgents} known agent(s) alive.";

        return Reachable(FinBridgeOptions.SurfaceName, detail);
    }

    private static SurfaceConnectionStatus NotConfigured(string surface, string activatingEnvVar) =>
        new(surface, IsConfigured: false, IsReachable: null, $"Not configured (set {activatingEnvVar}).");

    private static SurfaceConnectionStatus Reachable(string surface, string detail) =>
        new(surface, IsConfigured: true, IsReachable: true, detail);

    private static SurfaceConnectionStatus Unreachable(string surface, string detail) =>
        new(surface, IsConfigured: true, IsReachable: false, detail);
}
