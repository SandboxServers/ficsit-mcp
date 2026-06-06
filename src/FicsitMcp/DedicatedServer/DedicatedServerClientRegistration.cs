using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.DedicatedServer;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FicsitMcp.DedicatedServer;

/// <summary>
/// Registers the typed <see cref="IDedicatedServerApiClient"/> in DI, built on the named
/// dedicated-server <see cref="HttpClient"/> from <c>IHttpClientFactory</c> (never <c>new</c>).
/// </summary>
/// <remarks>
/// The client is resolved lazily: the underlying <see cref="HttpClient"/> takes its base address
/// from <see cref="DedicatedServerOptions"/> at factory-creation time, so resolving the client for
/// a dormant surface fails fast with the actionable env-var message rather than nulling out in a
/// send. The client is registered as a transient that wraps the factory-produced client in the
/// infrastructure <see cref="SurfaceHttpClient"/> shell.
/// </remarks>
public static class DedicatedServerClientRegistration
{
    /// <summary>Adds the dedicated-server typed API client. Requires <c>AddSurfaceHttpClients</c> first.</summary>
    public static IServiceCollection AddDedicatedServerApiClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IDedicatedServerApiClient>(static provider =>
        {
            HttpClient httpClient = provider
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(SurfaceHttpClients.DedicatedServer);

            var shell = new SurfaceHttpClient(httpClient, DedicatedServerOptions.SurfaceName);

            DedicatedServerOptions options = provider
                .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;

            return new DedicatedServerApiClient(shell, options);
        });

        return services;
    }
}
