using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.FinBridge;
using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.ServerObservation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FicsitMcp.ServerObservation;

/// <summary>
/// Registers the <see cref="IServerObservationService"/> the state/health tools (#6) depend on.
/// Resolves the bound surface options and the OPTIONAL <see cref="IFinBridge"/> (absent from the
/// container when the bridge is unconfigured) so the service can report each surface accurately.
/// </summary>
public static class ServerObservationRegistration
{
    /// <summary>
    /// Adds the server-observation service. Requires the surface options, the dedicated-server client,
    /// and the FRM client to be registered first; the FIN bridge is optional.
    /// </summary>
    public static IServiceCollection AddServerObservation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IServerObservationService>(static provider => new ServerObservationService(
            provider.GetRequiredService<IDedicatedServerApiClient>(),
            provider.GetRequiredService<IFrmClient>(),
            provider.GetRequiredService<IOptions<DedicatedServerOptions>>().Value,
            provider.GetRequiredService<IOptions<FrmOptions>>().Value,
            provider.GetRequiredService<IOptions<FinBridgeOptions>>().Value,
            // Optional: the bridge is only registered when configured (its registration is gated), so
            // GetService returns null on a dormant bridge — exactly the "not wired up" signal the
            // service reports for that surface.
            provider.GetService<IFinBridge>()));

        return services;
    }
}
