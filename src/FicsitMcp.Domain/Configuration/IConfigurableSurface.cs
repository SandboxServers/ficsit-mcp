namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// A configurable HTTP surface the server can fan out to (the dedicated-server HTTPS
/// API, Ficsit Remote Monitoring, or the FicsIt-Networks bridge). Each surface is
/// independently optional: the server runs with any subset configured, and a tool that
/// needs an unconfigured surface must fail fast with an actionable message rather than
/// throwing an opaque NullReferenceException deep in a client call.
/// </summary>
public interface IConfigurableSurface
{
    /// <summary>
    /// Human-readable surface name used in "not configured" diagnostics
    /// (for example, "Dedicated Server HTTPS API").
    /// </summary>
    static abstract string SurfaceName { get; }

    /// <summary>
    /// The environment variable a user must set to activate this surface, named exactly
    /// (for example, <c>FICSITMCP_Frm__BaseUrl</c>). Surfaced verbatim in the
    /// not-configured error so the fix is copy-pasteable.
    /// </summary>
    static abstract string ActivatingEnvVar { get; }

    /// <summary>
    /// True when enough has been supplied for the surface to be used. False means every
    /// value is absent (the surface was simply not configured) — distinct from a
    /// partially-configured surface, which is a validation error, not an opt-out.
    /// </summary>
    bool IsConfigured { get; }
}
