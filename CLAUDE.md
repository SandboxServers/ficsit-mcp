# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Solution layout

```
FicsitMcp.sln
global.json                  # pins the .NET SDK (major 10, rollForward latestMinor)
Directory.Build.props        # shared build settings for every project (see below)
Directory.Packages.props     # central package management — all versions live here
.editorconfig                # style/analyzer rules; every severity override has a why-comment
src/
  FicsitMcp/                 # console host (Exe). Program.cs = host/transport wiring only
    Program.cs               #   stderr logging + AddMcpServer().WithStdioServerTransport()
    Tools/                   #   thin [McpServerToolType] tools that delegate to Domain
      ServerInfoTool.cs      #   placeholder `server_info` tool
  FicsitMcp.Domain/          # Satisfactory domain + surface clients; NO MCP references
    ServerInfo.cs            #   record returned by server_info
    IServerInfoProvider.cs   #   service contract (tools depend on this, not reflection)
    ServerInfoProvider.cs    #   default impl, registered in DI
tests/
  FicsitMcp.Tests/           # xUnit; references both projects
```

Target framework is **`net9.0`**. Shared settings in `Directory.Build.props`:
`Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`,
`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true` (so build, IDE, and
`dotnet format` agree), and `ManagePackageVersionsCentrally=true`. Package versions
are **only** in `Directory.Packages.props`; per-project `<PackageReference>` entries
carry no `Version`.

Conventions to keep: file-scoped namespaces (enforced as a warning, i.e. an error
here), one public type per file, and — in every config file — a one-line comment on
each rule-severity override explaining *why* it deviates from the default.

### Critical: stdout belongs to JSON-RPC

The server speaks MCP over **stdio**, so `stdout` is the JSON-RPC transport. A single
`Console.WriteLine` (or any log written to stdout) corrupts the stream and shows up to
clients as a baffling "client disconnected". All logging is routed to **stderr** in
`Program.cs` via `LogToStandardErrorThreshold = LogLevel.Trace`. Never write to stdout.

## What this is

`ficsit-mcp` is an **MCP (Model Context Protocol) server written in C# / .NET**.
The name and README slogan ("Ficsit does not waste") theme it around FICSIT Inc. from the game
*Satisfactory*, so the server's tools/resources are expected to expose Satisfactory-related
data or actions to MCP clients.

It is built on the official **`ModelContextProtocol`** NuGet package (the .NET MCP SDK)
and `Microsoft.Extensions.Hosting`. The host (`src/FicsitMcp`) is a console app that
registers tools via `.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`
over the stdio transport for local clients.

## Commands

```sh
dotnet build                                           # build the solution (warnings = errors)
dotnet run --project src/FicsitMcp                     # run the MCP server (stdio)
dotnet test                                            # run all tests
dotnet test --filter "FullyQualifiedName~ServerInfoProviderTests"   # run a single test class
dotnet format                                          # apply formatting / style fixes
dotnet format --verify-no-changes                      # CI check: fail if formatting is off
```

The quality gate (run locally before pushing; CI runs the same): `dotnet build`,
`dotnet test`, and `dotnet format --verify-no-changes` must all pass cleanly.

## Architecture notes for MCP servers

When building this out, keep the MCP concepts distinct — future readers will look for them:

- **Tools** — callable actions the model can invoke (the primary surface). In the .NET SDK
  these are typically methods annotated with `[McpServerTool]` on a class marked
  `[McpServerToolType]`, with `[Description]` attributes that become the model-facing schema.
- **Resources / Prompts** — read-only context and prompt templates, if exposed.
- **Transport** — how clients connect (stdio for local CLI clients like Claude Desktop;
  HTTP/SSE for remote). Keep transport setup in the host entry point, separate from tool logic.

Keep tool implementations free of transport/host concerns so they stay unit-testable in
isolation from the MCP plumbing.

## Local client configuration

MCP clients launch the server as a subprocess over stdio. Point the client at
`dotnet run --project src/FicsitMcp` (uses the absolute path to this repo).

Claude Desktop (`claude_desktop_config.json`) / Claude Code (`.mcp.json`) block:

```json
{
  "mcpServers": {
    "ficsit-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/ficsit-mcp/src/FicsitMcp"]
    }
  }
}
```

For a faster launch, publish the host (`dotnet publish src/FicsitMcp -c Release`) and
point `command` at the produced executable instead of `dotnet run`.

Smoke-test without a client by piping JSON-RPC into the server; the placeholder tool
proves the pipeline:

```sh
dotnet run --project src/FicsitMcp
# then send: initialize -> notifications/initialized -> tools/list
# tools/list returns the `server_info` tool. All logs go to stderr; stdout is JSON-RPC only.
```

## Agent team

Specialized agents live in `.claude/agents/`, mapped to the filed issues. Route work to the
narrowest matching agent; anything that spans surfaces or doesn't fit below is handled
directly (no generalist agent).

| Agent | Owns | Issues |
|---|---|---|
| `dotnet-mcp-infrastructure` | Solution skeleton, host/DI/config, HTTP plumbing, CI gate, UDP query, logging conventions | #1–#4, #10, #26 |
| `ficsit-server-api-client` | Official HTTPS API client + its tools (state, saves, settings, console) | #5–#9 |
| `frm-observe-surface` | Ficsit Remote Monitoring client + observe/power/logistics tools | #11–#14 |
| `ficsit-domain-engineer` | Static game data (`Docs.json`), production-graph analysis tools | #15, #16 |
| `fin-bridge-architect` | FIN bridge protocol, host, machine-control tools, event notifications | #17, #18, #20, #21 |
| `fin-agent-lua-author` | The in-world FIN Lua agent script (`agent/`) | #19 |
| `mcp-test-harness-engineer` | Fixtures, fakes, end-to-end MCP tests, schema snapshots | #22 |
| `mcp-tool-safety-auditor` | Behavioral-hint policy, destructive-tool review, ReadOnlyMode (review-only) | #24 |
| `release-packaging-engineer` | Publish profiles, release workflow, client quickstarts, container | #23 |
