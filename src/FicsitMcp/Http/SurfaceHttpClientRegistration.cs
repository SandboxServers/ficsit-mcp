using System.Net.Http;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace FicsitMcp.Http;

/// <summary>
/// Wires the per-surface named/typed <see cref="HttpClient"/>s through <c>IHttpClientFactory</c>:
/// the dedicated-server client (self-signed TLS, trust-on-first-use) and the FRM client (plain
/// HTTP). Both share one custom resilience pipeline. No surface client is ever constructed via
/// <c>new HttpClient()</c>; the factory owns handler lifetime and pooling.
/// </summary>
/// <remarks>
/// Each client's <c>BaseAddress</c> is taken from its surface options at resolution time via
/// <see cref="SurfaceConfigurationExtensions.Require{TSurface}"/>, so resolving a client for an
/// unconfigured surface fails fast with the exact env var to set — never a null base address that
/// would blow up deep in a send.
/// </remarks>
public static class SurfaceHttpClientRegistration
{
    // Total budget for a logical call INCLUDING retries. A hung game server must not hang a tool
    // call indefinitely; ~10s is generous for a LAN host yet bounded. (Issue: "sane total ~10s".)
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(10);

    // Per-attempt ceiling, strictly less than the total so the total can actually fit retries.
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Registers the dedicated-server and FRM named clients plus their shared TOFU certificate
    /// pin store. Call once from the host during service configuration.
    /// </summary>
    public static IServiceCollection AddSurfaceHttpClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // One pin store for the process, persisted under LocalApplicationData. Singleton so the
        // in-memory pin cache is shared across every connection attempt within a run.
        services.AddSingleton<ICertificatePinStore>(
            _ => new FileCertificatePinStore(FileCertificatePinStore.DefaultPinFilePath));

        AddDedicatedServerClient(services);
        AddFrmClient(services);

        return services;
    }

    private static void AddDedicatedServerClient(IServiceCollection services)
    {
        services
            .AddHttpClient(SurfaceHttpClients.DedicatedServer)
            .ConfigureHttpClient(static (provider, client) =>
            {
                // Require() throws SurfaceNotConfiguredException naming the exact env var if the
                // dedicated-server surface is dormant, so a tool that needs it fails actionably.
                DedicatedServerOptions options = provider
                    .GetRequiredService<IOptions<DedicatedServerOptions>>().Value
                    .Require();
                client.BaseAddress = new Uri(options.BaseUrl!, UriKind.Absolute);
            })
            .ConfigurePrimaryHttpMessageHandler(static provider =>
            {
                DedicatedServerOptions options = provider
                    .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
                ICertificatePinStore pinStore = provider.GetRequiredService<ICertificatePinStore>();
                ILogger<TofuCertificateValidator> logger =
                    provider.GetRequiredService<ILogger<TofuCertificateValidator>>();

                var validator = new TofuCertificateValidator(
                    pinStore,
                    options.DangerousAcceptAnyCert,
                    logger);

                // The dedicated server uses a self-signed cert; replace the default chain check
                // with the TOFU validator. The callback's bool args (chain/errors) are ignored on
                // purpose — TOFU trusts the thumbprint, not a CA chain.
                return new SocketsHttpHandler
                {
                    SslOptions =
                    {
                        RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                            validator.Validate(
                                GetHost(provider),
                                certificate as System.Security.Cryptography.X509Certificates.X509Certificate2),
                    },
                };
            })
            .AddSurfaceResilience();
    }

    private static void AddFrmClient(IServiceCollection services)
    {
        services
            .AddHttpClient(SurfaceHttpClients.Frm)
            .ConfigureHttpClient(static (provider, client) =>
            {
                FrmOptions options = provider
                    .GetRequiredService<IOptions<FrmOptions>>().Value
                    .Require();
                client.BaseAddress = new Uri(options.BaseUrl!, UriKind.Absolute);
            })
            .AddSurfaceResilience();
    }

    private static string GetHost(IServiceProvider provider)
    {
        DedicatedServerOptions options = provider
            .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
        return new Uri(options.BaseUrl!, UriKind.Absolute).Host;
    }

    /// <summary>
    /// Adds the SINGLE shared resilience handler. Per MS guidance (and the issue's verified
    /// comment), add exactly one handler per client; we use the custom-pipeline overload instead
    /// of <c>AddStandardResilienceHandler</c> so retries can be disabled for non-idempotent HTTP
    /// methods (POST <c>SaveGame</c>/<c>Shutdown</c>/<c>RunCommand</c> must never be replayed).
    /// </summary>
    private static void AddSurfaceResilience(this IHttpClientBuilder builder) =>
        builder.AddResilienceHandler("surface", static pipeline =>
        {
            // Order matters: total timeout is OUTERMOST so it caps the whole call (all retries);
            // retry is in the middle; attempt timeout is INNERMOST so each try is individually bounded.
            pipeline.AddTimeout(TotalTimeout);

            pipeline.AddRetry(BuildRetryOptions());

            pipeline.AddTimeout(AttemptTimeout);
        });

    private static HttpRetryStrategyOptions BuildRetryOptions()
    {
        var retry = new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(500),
        };

        // CRITICAL: never retry non-idempotent methods. A replayed POST SaveGame/Shutdown/
        // RunCommand is a real outage (a second shutdown, a duplicated command). DisableForUnsafe-
        // HttpMethods turns off retries for POST/PATCH/PUT/DELETE/CONNECT per RFC 7231; only safe
        // idempotent calls (GET, etc.) are retried on transient faults. Do NOT remove in a refactor.
        retry.DisableForUnsafeHttpMethods();
        return retry;
    }
}
