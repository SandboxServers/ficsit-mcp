using FicsitMcp.Configuration;
using FicsitMcp.Domain;

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

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
