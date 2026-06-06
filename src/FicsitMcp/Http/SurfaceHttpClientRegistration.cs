using System.Net.Http;

using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

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

        // The resilience pipeline reads time through TimeProvider so its timeouts/backoff can be
        // virtualized in tests (FakeTimeProvider). Register the real clock if the host hasn't
        // already supplied one — TryAdd keeps a test-provided FakeTimeProvider in place.
        services.TryAddSingleton(TimeProvider.System);

        // One pin store for the process, persisted under LocalApplicationData by default (the
        // dedicated-server surface can override the path via CertPinFilePath, e.g. a container's
        // mounted writable volume). Singleton so the in-memory pin cache is shared across every
        // connection attempt within a run. The path is resolved here, at singleton construction.
        services.AddSingleton<ICertificatePinStore>(static provider =>
        {
            DedicatedServerOptions options = provider
                .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
            return new FileCertificatePinStore(
                options.CertPinFilePath ?? FileCertificatePinStore.DefaultPinFilePath);
        });

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
                                GetPinKey(provider),
                                certificate as System.Security.Cryptography.X509Certificates.X509Certificate2),
                    },
                };
            })
            .AddSurfaceResilience();
    }

    private static void AddFrmClient(IServiceCollection services)
    {
        // FRM Direct mode only. DedicatedApiPassthrough routing (reuse the dedicated-server client +
        // creds for POST /api/v1 function 'frm') is deferred to #11; FrmOptions.TransportMode is
        // validated but not yet consulted here.
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

    // The pin key is the AUTHORITY (host:port), not just the host. Two different services can sit
    // on the same host at different ports (e.g. dev fixtures), so pinning by host alone would let
    // one port's cert be (mis)matched against another's. Uri.Authority is host[:port].
    private static string GetPinKey(IServiceProvider provider)
    {
        DedicatedServerOptions options = provider
            .GetRequiredService<IOptions<DedicatedServerOptions>>().Value;
        return new Uri(options.BaseUrl!, UriKind.Absolute).Authority;
    }

    /// <summary>
    /// Adds the SINGLE shared resilience handler. Per MS guidance (and the issue's verified
    /// comment), add exactly one handler per client; we use the custom-pipeline overload so retries
    /// can be gated on a per-REQUEST opt-in rather than HTTP method alone. The
    /// <see cref="ResilienceHandlerContext"/> overload gives us DI access so the pipeline reads time
    /// through the registered <see cref="TimeProvider"/> (real in production, fake in tests).
    /// </summary>
    private static void AddSurfaceResilience(this IHttpClientBuilder builder) =>
        builder.AddResilienceHandler("surface", static (pipeline, context) =>
        {
            // Drive every timeout/backoff delay through the registered TimeProvider so a
            // FakeTimeProvider can advance virtual time in tests and exercise the total budget at
            // ~zero wall-clock. In production this resolves TimeProvider.System.
            pipeline.TimeProvider = context.ServiceProvider.GetRequiredService<TimeProvider>();

            // Order matters: total timeout is OUTERMOST so it caps the whole call (all retries);
            // retry is in the middle; attempt timeout is INNERMOST so each try is individually bounded.
            pipeline.AddTimeout(TotalTimeout);

            pipeline.AddRetry(BuildRetryOptions());

            pipeline.AddTimeout(AttemptTimeout);
        });

    private static HttpRetryStrategyOptions BuildRetryOptions() => new()
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(500),

        // Retry altitude is the dangerous bit. The dedicated-server API is POST-only (one endpoint,
        // a function envelope), so idempotency is per-FUNCTION, not per-METHOD: a method-only gate
        // (the old DisableForUnsafeHttpMethods) would mean NOTHING on that surface ever retries.
        // Instead retry transient faults when EITHER the method is safe (GET/HEAD/OPTIONS/TRACE)
        // OR the request explicitly opted in via SurfaceHttpRequestOptions.AllowRetry. The
        // dedicated-server client (#5) sets that flag only on idempotent functions
        // (QueryServerState/HealthCheck/VerifyAuthenticationToken) and NEVER on SaveGame/Shutdown/
        // RunCommand — a replayed shutdown/command is a real outage. Do NOT weaken this in a refactor.
        ShouldHandle = static args =>
        {
            if (!HttpClientResiliencePredicates.IsTransient(args.Outcome))
            {
                // Not a transient fault (e.g. a 404 or a non-transport exception): never retry,
                // regardless of method or opt-in.
                return ValueTask.FromResult(false);
            }

            // The request is reliably on the resilience context for both response and exception
            // outcomes (the resilience handler stamps it before invoking the pipeline). Fall back to
            // the response's RequestMessage if ever absent. If we cannot identify the request at all,
            // fail safe: do NOT retry (assume it might be non-idempotent).
            HttpRequestMessage? request =
                args.Context.GetRequestMessage() ?? args.Outcome.Result?.RequestMessage;
            return ValueTask.FromResult(request is not null && MayRetry(request));
        },
    };

    // A request may be retried on a transient fault when its method is safe/idempotent, OR when it
    // carries the explicit AllowRetry opt-in (set by callers only on idempotent POST functions).
    private static bool MayRetry(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(SurfaceHttpRequestOptions.AllowRetry, out bool allowRetry)
            && allowRetry)
        {
            return true;
        }

        return IsSafeMethod(request.Method);
    }

    // RFC 9110 safe methods are inherently retry-safe (no observable side effect on the server).
    private static bool IsSafeMethod(HttpMethod method) =>
        method == HttpMethod.Get
        || method == HttpMethod.Head
        || method == HttpMethod.Options
        || method == HttpMethod.Trace;
}
