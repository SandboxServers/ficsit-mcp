# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status: greenfield

As of this writing the repository contains only `README.md`. There is no source code,
project file, or build tooling yet. **When you scaffold the project, update this file** with
the real solution layout, commands, and architecture — the sections below describe the
*intended* design and the conventional .NET commands that will apply once a project exists.

## What this is

`ficsit-mcp` is intended to be an **MCP (Model Context Protocol) server written in C# / .NET**.
The name and README slogan ("Ficsit does not waste") theme it around FICSIT Inc. from the game
*Satisfactory*, so the server's tools/resources are expected to expose Satisfactory-related
data or actions to MCP clients.

The standard C# implementation uses the official **`ModelContextProtocol`** NuGet package
(the .NET MCP SDK). A typical host is a console app using `Microsoft.Extensions.Hosting`,
registering MCP tools via `.AddMcpServer()` and transports such as stdio
(`.WithStdioServerTransport()`) for local clients.

## Commands (once a project is scaffolded)

```sh
dotnet build                              # build the solution
dotnet run --project <ServerProject>      # run the MCP server
dotnet test                               # run all tests
dotnet test --filter "FullyQualifiedName~<TestName>"   # run a single test / class
dotnet format                             # apply formatting / lint fixes
```

Suggested scaffolding if starting from scratch:

```sh
dotnet new console -n FicsitMcp          # or `dotnet new sln` + a class library
dotnet add package ModelContextProtocol
```

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

MCP clients (e.g. Claude Desktop) launch the server as a subprocess over stdio. Once built,
the server is registered in the client's MCP config by pointing at `dotnet run --project ...`
or the published executable. Document the exact config block here once the entry point exists.

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
