using System.Collections.Immutable;

using FicsitMcp.Domain.Frm.Model;

namespace FicsitMcp.Domain.Frm;

/// <summary>
/// The one typed client over the Ficsit Remote Monitoring (FRM) mod web server — the live-world
/// observe surface. One method per consumed FRM GET endpoint, each returning compact NORMALIZED
/// records (precision trimmed, verbose sub-objects dropped, mobile-entity anomaly flags derived).
/// Tools depend on this; no tool ever touches raw FRM HTTP or raw JSON.
/// </summary>
/// <remarks>
/// <para>
/// This is a READ surface returning full typed lists. The cardinal "never dump raw payloads"
/// aggregation/filtering/capping discipline lives in the MCP tools that consume these methods, not
/// here — but note <see cref="GetFactoryAsync"/> is the heavy one (thousands of buildings on a large
/// save) and its consumers must filter and paginate.
/// </para>
/// <para>
/// <b>Latency &amp; timeouts.</b> There is intentionally no per-endpoint timeout knob: the single
/// timeout that governs every call here is the one the host configured on the FRM
/// <c>SurfaceHttpClient</c>'s resilience pipeline. Most endpoints are cheap in-memory reads and
/// return in well under a second; two are heavier and worth budgeting that shared timeout against —
/// <see cref="GetFactoryAsync"/> (payload size scales with building count) and
/// <see cref="GetResourceNodesAsync"/> (served on the game thread, so it competes with the simulation
/// tick and can stall under load). If those need a longer ceiling than the cheap reads, widen the
/// surface-level timeout rather than expecting a per-call override.
/// </para>
/// <para>
/// Every method throws <see cref="FrmUnreachableException"/> with an actionable in-game remedy when
/// the mod is absent or its web server is not started, and <c>SurfaceNotConfiguredException</c> when
/// the FRM surface has no base URL configured. Neither failure affects other (HTTPS-API) surfaces.
/// </para>
/// </remarks>
public interface IFrmClient
{
    /// <summary>
    /// World-wide production/consumption rates per item (<c>/getProdStats</c>). The cheapest
    /// factory-wide production overview — prefer this over enumerating buildings.
    /// </summary>
    Task<ImmutableArray<FrmProdStatsItem>> GetProdStatsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every production machine with its recipe, clock, and live rates (<c>/getFactory</c>).
    /// HEAVY — thousands of buildings on a large save; consumers must filter and paginate.
    /// Use <see cref="GetProdStatsAsync"/> for factory-wide totals instead.
    /// </summary>
    Task<ImmutableArray<FrmFactoryBuilding>> GetFactoryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Per-circuit power state: production, draw, battery, and fuse status (<c>/getPower</c>).
    /// </summary>
    Task<ImmutableArray<FrmPowerCircuit>> GetPowerAsync(CancellationToken cancellationToken);

    /// <summary>
    /// All trains with derived anomaly flags (derailed / no path / stuck) (<c>/getTrains</c>).
    /// </summary>
    Task<ImmutableArray<FrmTrain>> GetTrainsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// All delivery drones with derived anomaly flags (<c>/getDrone</c>).
    /// </summary>
    Task<ImmutableArray<FrmDrone>> GetDronesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// All wheeled vehicles with derived anomaly flags (no fuel / low fuel / no path / stuck)
    /// (<c>/getVehicles</c>).
    /// </summary>
    Task<ImmutableArray<FrmVehicle>> GetVehiclesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// All players: position, health, online state (<c>/getPlayer</c>).
    /// </summary>
    Task<ImmutableArray<FrmPlayer>> GetPlayersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// All resource nodes: resource, purity, and whether already exploited (<c>/getResourceNode</c>).
    /// GAME-THREAD cost: FRM serves this on the game thread (<c>RequiresGameThread</c>), so it competes
    /// with the simulation tick and is heavier than the pure-data reads — call it sparingly (node
    /// layout is effectively static within a session) and budget it against the surface timeout.
    /// </summary>
    Task<ImmutableArray<FrmResourceNode>> GetResourceNodesAsync(CancellationToken cancellationToken);
}
