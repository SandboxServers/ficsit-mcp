using FicsitMcp.Domain.Configuration;
using FicsitMcp.Domain.FinBridge;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FicsitMcp.FinBridge;

/// <summary>
/// Runs the FIN bridge HTTP listener as its own <see cref="IHostedService"/>, completely separate
/// from the MCP stdio transport. It builds a dedicated Kestrel <see cref="WebApplication"/> bound to
/// the configured <c>ListenUrl</c>, mounts the token-auth middleware and the two bridge endpoints,
/// and shares the singleton <see cref="IFinBridge"/> resolved from the parent host. This keeps the
/// bridge and MCP as neighbours, never roommates: the HTTP wiring never touches MCP tool
/// registration or the JSON-RPC stdout stream.
/// </summary>
internal sealed class FinBridgeHostedService : IHostedService
{
    private readonly FinBridgeOptions _options;
    private readonly IFinBridge _bridge;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FinBridgeHostedService> _logger;

    private WebApplication? _app;

    /// <summary>Captures the validated options and the shared bridge + logging from the parent host.</summary>
    public FinBridgeHostedService(
        IOptions<FinBridgeOptions> options,
        IFinBridge bridge,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<FinBridgeHostedService>();
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Require() turns "bridge listener started but not configured" into one actionable error
        // naming the exact env var, rather than a null ListenUrl deep in Kestrel binding. The host
        // only registers this service when configured, so this is a belt-and-braces guard.
        FinBridgeOptions configured = _options.Require();

        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

        // Bind ONLY to the configured URL (defaults to localhost/LAN per the operator's choice);
        // never the wildcard implicitly.
        builder.WebHost.UseUrls(configured.ListenUrl!);

        // Reuse the parent host's logging so bridge logs follow the same sinks — critically, stderr,
        // never stdout (which belongs to the MCP JSON-RPC stream). Clear the web defaults first.
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(_loggerFactory);
        builder.Logging.Services.AddSingleton(_loggerFactory);

        // Share the singleton bridge so commands enqueued by MCP tools and results posted by the
        // agent meet in the same instance.
        builder.Services.AddSingleton(_bridge);

        WebApplication app = builder.Build();

        // Token auth runs before routing so an unauthenticated request is rejected before any
        // endpoint work or body parsing.
        string token = configured.SharedSecret.Reveal()
            ?? throw new InvalidOperationException("FIN bridge shared secret is unexpectedly empty after validation.");
        app.UseMiddleware<FinTokenAuthMiddleware>(token);

        app.MapFinBridge();

        _app = app;
        await app.StartAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("FIN bridge listening on {ListenUrl}", configured.ListenUrl);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync(cancellationToken).ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
        _app = null;
    }
}
