namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Thrown when a tool needs a surface that the operator never configured. The message
/// names the surface and the exact environment variable to set, so the failure tells the
/// user how to fix it instead of leaking an internal null/connection error.
/// </summary>
public sealed class SurfaceNotConfiguredException : InvalidOperationException
{
    /// <summary>The surface that was requested but not configured.</summary>
    public string SurfaceName { get; }

    /// <summary>The environment variable the user must set to activate the surface.</summary>
    public string ActivatingEnvVar { get; }

    /// <summary>
    /// Builds the standard not-configured message, for example:
    /// "FRM endpoint not configured; set FICSITMCP_Frm__BaseUrl".
    /// </summary>
    public SurfaceNotConfiguredException(string surfaceName, string activatingEnvVar)
        : base($"{surfaceName} not configured; set {activatingEnvVar}")
    {
        SurfaceName = surfaceName;
        ActivatingEnvVar = activatingEnvVar;
    }
}
