using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.FinBridge;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FicsitMcp.FinBridge;

/// <summary>
/// Wires the FIN bridge into the host, but only when the surface is configured. An unconfigured
/// bridge contributes nothing — no <see cref="IFinBridge"/>, no listener — so the rest of the server
/// runs untouched (the independently-optional surface contract). Transport setup stays here in the
/// host; the bridge logic itself lives in <c>FicsitMcp.Domain</c>, separate from MCP wiring.
/// </summary>
internal static class FinBridgeRegistration
{
    /// <summary>
    /// Registers the singleton <see cref="IFinBridge"/>, a <see cref="TimeProvider"/> (if not already
    /// present), and the listener <see cref="FinBridgeHostedService"/> — only if
    /// <see cref="FinBridgeOptions.IsConfigured"/>. Safe to call unconditionally.
    /// </summary>
    public static IServiceCollection AddFinBridge(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Read the bound-and-normalized options to decide whether to wire anything at all. Binding
        // here mirrors AddSurfaceOptions so blank/placeholder URLs read as "dormant".
        var options = new FinBridgeOptions();
        configuration.GetSection(FinBridgeOptions.SectionName).Bind(options);

        if (!options.IsConfigured)
        {
            return services;
        }

        // TimeProvider drives bridge deadlines and liveness; register the system clock unless a test
        // or another surface already supplied one.
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IFinBridge, Domain.FinBridge.FinBridge>();
        services.AddHostedService<FinBridgeHostedService>();

        return services;
    }
}
