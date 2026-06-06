using FicsitMcp.Domain.Frm;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FicsitMcp.Http;

/// <summary>
/// Registers <see cref="IFrmClient"/> in DI over the named FRM <c>HttpClient</c> wired by
/// <see cref="SurfaceHttpClientRegistration"/>. Kept separate from the <c>HttpClient</c> plumbing so
/// the transport registration stays surface-agnostic and the typed client is wired here.
/// </summary>
/// <remarks>
/// The <c>HttpClient</c> is created from <see cref="IHttpClientFactory"/> by its canonical name and
/// wrapped in a <see cref="SurfaceHttpClient"/> (uniform transport-fault mapping). The client is
/// resolved lazily per request via the factory, so registering it never forces the FRM surface to
/// be configured — an unconfigured FRM surface only fails when an FRM tool is actually invoked,
/// leaving HTTPS-API-only operation unaffected.
/// </remarks>
public static class FrmClientRegistration
{
    /// <summary>Registers <see cref="IFrmClient"/> as a transient over the named FRM client.</summary>
    public static IServiceCollection AddFrmClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient<IFrmClient>(static provider =>
        {
            HttpClient httpClient = provider
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(SurfaceHttpClients.Frm);

            var surfaceClient = new SurfaceHttpClient(httpClient, SurfaceHttpClients.Frm);
            ILogger<FrmClient> logger = provider.GetRequiredService<ILogger<FrmClient>>();

            return new FrmClient(surfaceClient, logger);
        });

        return services;
    }
}
