using FicsitMcp.Domain;
using FicsitMcp.Domain.GameData;
using FicsitMcp.Domain.GameData.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout is reserved for the JSON-RPC stream over stdio. Route ALL log output to
// stderr so a stray log line can never corrupt the protocol stream (which surfaces
// to clients as a baffling "client disconnected"). Never use Console.WriteLine here.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Domain services the tools depend on. Tools stay thin and resolve these via DI.
builder.Services.AddSingleton<IServerInfoProvider, ServerInfoProvider>();

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

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
