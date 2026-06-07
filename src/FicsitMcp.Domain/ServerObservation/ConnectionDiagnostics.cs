using System.Collections.Immutable;

namespace FicsitMcp.Domain.ServerObservation;

/// <summary>
/// How a single surface looks from a connection-diagnosis run: whether the operator configured it
/// and, if so, whether a cheap probe could actually reach it. An unconfigured surface is a valid
/// opt-out (<see cref="IsConfigured"/> false), never an error.
/// </summary>
/// <param name="Surface">Human-readable surface name (e.g. <c>"Dedicated Server HTTPS API"</c>).</param>
/// <param name="IsConfigured">True when the operator supplied this surface's activating setting.</param>
/// <param name="IsReachable">
/// True when a cheap probe succeeded. Null when reachability was not tested because the surface is
/// not configured (there is nothing to probe).
/// </param>
/// <param name="Detail">
/// A short human-readable explanation: for an unconfigured surface, which env var to set to enable
/// it; for a configured-and-reachable surface, a brief confirmation; for a configured-but-unreachable
/// surface, the actionable reason the probe failed.
/// </param>
public sealed record SurfaceConnectionStatus(
    string Surface,
    bool IsConfigured,
    bool? IsReachable,
    string Detail);

/// <summary>
/// The result of <c>verify_connection</c>: a per-surface report across all three surfaces
/// (Dedicated Server HTTPS API, FRM, FIN bridge) for diagnosing client setup. The run degrades
/// gracefully — an unconfigured or unreachable surface is reported, never thrown — so a single bad
/// surface never hides the state of the others.
/// </summary>
/// <param name="Surfaces">One status per surface, in a stable order.</param>
public sealed record ConnectionDiagnostics(ImmutableArray<SurfaceConnectionStatus> Surfaces);
