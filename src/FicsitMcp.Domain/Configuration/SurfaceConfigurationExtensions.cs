namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Helpers a tool uses to assert its surface is configured before doing any work, turning the
/// "surface not set up" case into one actionable error instead of a deep null/connection fault.
/// </summary>
public static class SurfaceConfigurationExtensions
{
    /// <summary>
    /// Returns the surface if it is configured; otherwise throws
    /// <see cref="SurfaceNotConfiguredException"/> naming the surface and the exact environment
    /// variable to set. Call this at the top of any tool that needs the surface.
    /// </summary>
    /// <typeparam name="TSurface">The surface options type.</typeparam>
    /// <exception cref="SurfaceNotConfiguredException">The surface is not configured.</exception>
    public static TSurface Require<TSurface>(this TSurface surface)
        where TSurface : IConfigurableSurface
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (!surface.IsConfigured)
        {
            throw new SurfaceNotConfiguredException(TSurface.SurfaceName, TSurface.ActivatingEnvVar);
        }

        return surface;
    }
}
