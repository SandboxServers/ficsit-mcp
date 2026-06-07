using FicsitMcp.Configuration;
using FicsitMcp.DedicatedServer;
using FicsitMcp.Domain;
using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;
using FicsitMcp.FinBridge;
using FicsitMcp.Http;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP clients launch this server with an arbitrary working directory (their own, not ours),
// and the default content root is the cwd — which would silently skip appsettings.json.
// Anchor the content root to the binary so the config file next to it is always found.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// MCP clients pass surface config (including secrets) via env vars in their mcpServers
// block, so FICSITMCP_-prefixed env vars must WIN over appsettings.json. Adding this
// source last makes it last-wins; the prefix is stripped and "__" becomes the section
// delimiter, so FICSITMCP_Frm__BaseUrl binds to Frm:BaseUrl.
builder.Configuration.AddEnvironmentVariables("FICSITMCP_");

// stdout is reserved for the JSON-RPC stream over stdio. Route ALL log output to
// stderr so a stray log line can never corrupt the protocol stream (which surfaces
// to clients as a baffling "client disconnected"). Never use Console.WriteLine here.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Surface options: bound from config, DataAnnotations-validated, validated at startup.
// Each surface is independently optional; an unconfigured surface stays dormant.
builder.Services.AddSurfaceOptions(builder.Configuration);

// Domain services the tools depend on. Tools stay thin and resolve these via DI.
builder.Services.AddSingleton<IServerInfoProvider, ServerInfoProvider>();

// Per-surface HTTP plumbing: named/typed clients via IHttpClientFactory (one per surface), the
// trust-on-first-use TLS pin store, and the shared resilience pipeline. Each client takes its
// BaseAddress from its surface options at resolution time, so a client built for an unconfigured
// surface fails fast naming the env var. Surface clients NEVER new up HttpClient.
builder.Services.AddSurfaceHttpClients();

// Typed client for the official Dedicated Server HTTPS API, layered on the named dedicated-server
// HttpClient above. Tools (#6-#9) depend on IDedicatedServerApiClient, never on raw HTTP.
builder.Services.AddDedicatedServerApiClient();

// FRM observe surface: the typed client over the FRM mod web server (live world state). Resolved
// lazily over the named FRM HttpClient, so an unconfigured FRM surface fails only when an FRM tool
// is invoked — HTTPS-API-only operation is unaffected.
builder.Services.AddFrmClient();

// Game-data layer: bind the optional Docs.json override, load the snapshot ONCE at
// startup (shipped embedded asset, or the override file if configured), and expose it as
// an immutable singleton. The snapshot is loaded eagerly here so a misconfigured override
// path fails loudly at startup rather than on the first tool call.
builder.Services
    .Configure<GameDataOptions>(builder.Configuration.GetSection(GameDataOptions.SectionName));

builder.Services.AddSingleton<IGameDataService>(serviceProvider =>
{
    GameDataOptions options =
        serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<GameDataOptions>>()?.Value
        ?? new GameDataOptions();
    GameDataSnapshot snapshot = GameDataSnapshotLoader.Load(options);
    return new GameDataService(snapshot);
});

// FIN bridge: an HTTP listener for the in-world FicsIt-Networks Lua agent, mounted as its OWN
// IHostedService (separate Kestrel WebApplication), completely separate from the MCP stdio
// transport below. Registered only when the surface is configured — an unconfigured bridge adds no
// listener and no service, leaving the rest of the server untouched. It shares the host's logger
// factory, so its logs stay on stderr and never corrupt the JSON-RPC stdout stream.
builder.Services.AddFinBridge(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
