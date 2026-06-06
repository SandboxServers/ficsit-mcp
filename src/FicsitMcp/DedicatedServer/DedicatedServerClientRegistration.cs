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
/// <para>
/// <b>Lifetime: SINGLETON.</b> The client holds mutable auth state — the bearer token adopted at
/// runtime from <c>PasswordLogin</c>/<c>ClaimServer</c>/<c>SetAdminPassword</c>, guarded by an
/// internal <see cref="System.Threading.SemaphoreSlim"/>. A transient registration would discard that
/// token on the next resolution, so a login in one tool call would not carry into the next. A
/// singleton keeps the adopted token (and its lock) stable for the host's lifetime; the container
/// owns disposal of the client's <see cref="System.IDisposable"/> (the token lock) at teardown.
/// </para>
/// <para>
/// <b>HttpClient lifetime stays coherent.</b> Making the client a singleton must NOT pin one
/// <see cref="HttpClient"/>/handler for the whole process (that would defeat
/// <see cref="IHttpClientFactory"/>'s handler rotation). So we do not capture a client; we hand the
/// singleton a FACTORY delegate that calls <c>CreateClient</c> PER SEND and wraps the result in the
/// infrastructure <see cref="SurfaceHttpClient"/> shell. The base address is still read from
/// <see cref="DedicatedServerOptions"/> at create-client time, so a send against a dormant surface
/// fails fast with the actionable env-var message. <see cref="IHttpClientFactory"/> is itself a
/// singleton, so capturing it in the singleton is safe.
/// </para>
/// </remarks>
public static class DedicatedServerClientRegistration
{
    /// <summary>Adds the dedicated-server typed API client. Requires <c>AddSurfaceHttpClients</c> first.</summary>
    public static IServiceCollection AddDedicatedServerApiClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDedicatedServerApiClient>(static provider =>
        {
            IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

            // Per-send shell factory: a fresh factory-produced HttpClient each call preserves handler
            // rotation; the shell is a cheap wrapper. Captures only the singleton IHttpClientFactory.
            SurfaceHttpClient ShellFactory() => new(
                httpClientFactory.CreateClient(SurfaceHttpClients.DedicatedServer),
                DedicatedServerOptions.SurfaceName);

            DedicatedServerOptions options = provider
                .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;

            return new DedicatedServerApiClient(ShellFactory, options);
        });

        return services;
    }
}
