using FicsitMcp.Domain.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FicsitMcp.Configuration;

/// <summary>
/// Wires the three surface options (<see cref="DedicatedServerOptions"/>,
/// <see cref="FrmOptions"/>, <see cref="FinBridgeOptions"/>) into DI: bound from configuration,
/// validated with DataAnnotations, and validated at startup so a misconfiguration fails fast on
/// boot rather than on the first tool call.
/// </summary>
public static class SurfaceOptionsRegistration
{
    /// <summary>
    /// Registers all surface options against their configuration sections. Each surface is
    /// independently optional — an absent section binds to defaults and stays dormant — but a
    /// present-but-invalid section (bad URL, active surface with no credential) fails
    /// <c>ValidateOnStart</c>.
    /// </summary>
    public static IServiceCollection AddSurfaceOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSurface<DedicatedServerOptions>(configuration, DedicatedServerOptions.SectionName);
        services.AddSurface<FrmOptions>(configuration, FrmOptions.SectionName);
        services.AddSurface<FinBridgeOptions>(configuration, FinBridgeOptions.SectionName);

        return services;
    }

    private static void AddSurface<TOptions>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
