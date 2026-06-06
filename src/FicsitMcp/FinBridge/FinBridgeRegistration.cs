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

        // Registration gate (G9): a surface is "configured" iff its activating URL is present, so we
        // gate on ONLY that one value — never on the tunables. A full Bind() of a throwaway options
        // instance would also bind ServerHoldMs/etc. and could surface a bad-tunable error here,
        // ahead of (and divergent from) the validated IOptions pipeline that owns validation. Reading
        // the single key keeps this a presence check; binding + DataAnnotations + ValidateOnStart for
        // the real options happens in SurfaceOptionsRegistration, and the service asserts the surface
        // with Require() at start. The chicken-and-egg constraint — we must decide whether to register
        // BEFORE the options pipeline runs — is why a tiny raw read lives here at all.
        string? listenUrl = configuration
            .GetSection(FinBridgeOptions.SectionName)[nameof(FinBridgeOptions.ListenUrl)];

        if (string.IsNullOrWhiteSpace(listenUrl))
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
