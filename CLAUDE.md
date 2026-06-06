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
    Program.cs               #   stderr logging + config + DI + AddMcpServer().WithStdioServerTransport()
    appsettings.json         #   non-secret defaults; copied to output (PreserveNewest)
    Configuration/
      SurfaceOptionsRegistration.cs  # binds+validates the three surfaces (ValidateOnStart)
    Http/                    #   IHttpClientFactory wiring (host owns DI + resilience packages)
      SurfaceHttpClientRegistration.cs # named clients + TOFU handler + ONE resilience pipeline
    FinBridge/               #   FIN bridge HTTP listener (Kestrel) — transport only, separate from MCP
      FinBridgeRegistration.cs   # AddFinBridge(): wires IFinBridge + listener ONLY when configured
      FinBridgeHostedService.cs  # IHostedService: own Kestrel WebApplication on ListenUrl
      FinBridgeEndpoints.cs      # maps POST /fin/v1/hello + /fin/v1/poll; status mapping
      FinTokenAuthMiddleware.cs  # constant-time X-FIN-Token check; 401 before any work
    Tools/                   #   thin [McpServerToolType] tools that delegate to Domain
      ServerInfoTool.cs      #   placeholder `server_info` tool
      GameDataTool.cs        #   `lookup_recipe` / `lookup_item` (delegate to IGameDataService)
  FicsitMcp.Domain/          # Satisfactory domain + surface clients; NO MCP references
    ServerInfo.cs            #   record returned by server_info
    IServerInfoProvider.cs   #   service contract (tools depend on this, not reflection)
    ServerInfoProvider.cs    #   default impl, registered in DI
    Configuration/           # surface options POCOs + contracts (no MCP, no DI)
      DedicatedServerOptions.cs  FrmOptions.cs  FinBridgeOptions.cs
      FrmTransportMode.cs        # Direct | DedicatedApiPassthrough
      IConfigurableSurface.cs    # SurfaceName / ActivatingEnvVar / IsConfigured contract
      SurfaceConfigurationExtensions.cs  # .Require() -> actionable error if dormant
      SurfaceNotConfiguredException.cs
      Secret.cs Secret*Converter.cs      # redacting credential wrapper (never logs raw)
    Http/                    # surface HTTP plumbing (BCL/Polly only; NO MCP) — testable core
      SurfaceHttpClients.cs           # canonical named-client constants (DedicatedServer/Frm)
      SurfaceHttpClient.cs            # SHELL: send + map transport faults to friendly errors
      TofuCertificateValidator.cs     # trust-on-first-use self-signed cert pinning
      ICertificatePinStore.cs FileCertificatePinStore.cs  # %LocalAppData% thumbprint store
      SurfaceUnreachableException.cs CertificatePinMismatchException.cs  # actionable errors
    FinBridge/               #   FIN bridge core (long-poll command channel); NO MCP, NO ASP.NET
      IFinBridge.cs          #     tool-facing (SendAsync/GetLiveness/Subscribe) + transport-facing (Hello/Poll)
      FinBridge.cs           #     impl: per-agent reject-on-full queue, TCS registry, tombstones, event ring
      FinProtocol.cs         #     fixed wire constants: version, X-FIN-Token, /fin/v1 paths
      FinCommand/FinResult/FinEvent/FinTarget/ComponentRef/FinError.cs  # envelope DTOs (match schemas)
      HelloRequest/HelloResponse/PollRequest/PollResponse.cs            # handshake + poll wrappers
      FinErrorCode.cs        #     typed error enum (matches common.schema.json enum)
      AgentLiveness.cs       #     liveness snapshot record
      FinBridgeException.cs ProtocolVersionMismatchException.cs  # surfaced failures (carry FinError / 426 info)
    GameData/                #   canonical game-data layer (Docs.json -> immutable model)
      Model/                 #     immutable records: GameItem/GameRecipe/GameBuilding/...
      DocsJsonParser.cs      #     UTF-16 Docs.json -> GameDataSnapshot (rate math here)
      GameDataService.cs     #     IGameDataService impl; O(1) class/display-name indexes
      GameDataSnapshotLoader.cs #  override-vs-embedded resolution; SnapshotVersion const
      SnapshotGenerator.cs   #     repeatable snapshot-refresh procedure (see README.md)
      game-data.v1.json      #     embedded UTF-8 snapshot (build 23300430); ~277 KB
      README.md              #     encoding gotchas, config override, refresh workflow
tests/
  FicsitMcp.Tests/           # xUnit; references both projects
    Fixtures/                #   docs-slice.utf16.json — real UTF-16 Docs.json slice
    GameData/                #   parser/golden-rate/name-resolution/loader/tool tests
    Http/                    #   fake HttpMessageHandler; TOFU/pin-store/resilience/error-mapping
    FinBridge/               #   FakeTimeProvider unit tests + real-Kestrel integration (fake HttpClient agent)
```

Target framework is **`net10.0`** (current LTS). Shared settings in `Directory.Build.props`:
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

## Configuration

The server fans out to up to three independent HTTP surfaces. Each is **independently
optional**: configure any subset and the rest stay dormant. Settings bind to strongly-typed
options in `FicsitMcp.Domain/Configuration`, validated with DataAnnotations and
`ValidateOnStart` (a bad value crashes the host on boot, not on the first tool call).

| Surface | Section | Activating env var | Options type |
|---|---|---|---|
| Dedicated Server HTTPS API | `DedicatedServer` | `FICSITMCP_DedicatedServer__BaseUrl` | `DedicatedServerOptions` |
| Ficsit Remote Monitoring (FRM) | `Frm` | `FICSITMCP_Frm__BaseUrl` | `FrmOptions` |
| FicsIt-Networks bridge | `FinBridge` | `FICSITMCP_FinBridge__ListenUrl` | `FinBridgeOptions` |

**Config sources & precedence.** `appsettings.json` (non-secret defaults) is the base layer;
`FICSITMCP_`-prefixed environment variables override it (added last in `Program.cs`). The
prefix is stripped and `__` is the section delimiter, so `FICSITMCP_Frm__BaseUrl` binds to
`Frm:BaseUrl`. MCP clients pass config — including secrets — via env vars in their
`mcpServers` block, which is why env wins.

**Options keys** (env var = `FICSITMCP_<Section>__<Key>`):

- `DedicatedServer`: `BaseUrl` (e.g. `https://127.0.0.1:7777`), `AdminToken` (secret, bearer
  auth), `DangerousAcceptAnyCert` (bool, **dev only** — skips TLS thumbprint pinning),
  `CertPinFilePath` (optional, **default** `%LocalAppData%/ficsit-mcp/cert-pins.json` — override the
  TOFU pin file location; containers/read-only deployments should point this at a mounted writable
  path so pins survive restarts).
- `Frm`: `BaseUrl` (e.g. `http://127.0.0.1:8080`), `TransportMode` (`Direct` default, or
  `DedicatedApiPassthrough` to route through the dedicated-server API instead of the FRM port).
- `FinBridge`: `ListenUrl` (e.g. `http://0.0.0.0:8421`), `SharedSecret` (secret the in-world
  Lua agent must present).

**Surface-optionality contract.** A surface is *configured* when its activating URL is set.
A configured-but-incomplete surface (URL set, credential missing) is a validation error; a
fully-absent surface is a valid opt-out. Tools assert their surface with
`options.Require()` (`SurfaceConfigurationExtensions`), which throws
`SurfaceNotConfiguredException` naming the exact env var, e.g.
`"FRM endpoint not configured; set FICSITMCP_Frm__BaseUrl"`.

**Secrets discipline.** `AdminToken` / `SharedSecret` are typed `Secret`, not `string`:
`ToString()` returns `***`, and both the JSON and TypeConverter serializers emit the redacted
form, so logging or echoing an options object cannot leak a credential. Read the raw value
only via the deliberately blunt `secret.Reveal()` — greppable and obvious in review. Never put
secrets in `appsettings.json`; pass them through env vars.

### Example MCP client config (Claude Desktop / Code)

```json
{
  "mcpServers": {
    "ficsit-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/ficsit-mcp/src/FicsitMcp"],
      "env": {
        "FICSITMCP_DedicatedServer__BaseUrl": "https://127.0.0.1:7777",
        "FICSITMCP_DedicatedServer__AdminToken": "<admin-api-token>",
        "FICSITMCP_Frm__BaseUrl": "http://127.0.0.1:8080",
        "FICSITMCP_Frm__TransportMode": "Direct",
        "FICSITMCP_FinBridge__ListenUrl": "http://0.0.0.0:8421",
        "FICSITMCP_FinBridge__SharedSecret": "<bridge-shared-secret>"
      }
    }
  }
}
```

Include only the surfaces you use; omit a surface's env vars to leave it dormant.

## HTTP plumbing (outbound surface calls)

All outbound HTTP goes through `IHttpClientFactory` — **never `new HttpClient()`**. The host
registers one named client per surface in `SurfaceHttpClientRegistration.AddSurfaceHttpClients()`
(`src/FicsitMcp/Http`), keyed by the constants in `Domain/Http/SurfaceHttpClients.cs`
(`DedicatedServer`, `Frm`). Each client's `BaseAddress` is read from its surface options **at
resolution time** via `options.Require()`, so resolving a client for an unconfigured surface fails
fast naming the exact env var rather than nulling out deep in a send.

**Surface clients build on the shell, not on `HttpClient` directly.** `Domain/Http/SurfaceHttpClient`
is the typed shell the surface engineers (`ficsit-server-api-client`, `frm-observe-surface`) layer
their request/response semantics onto. It threads `CancellationToken` and maps transport faults to
actionable errors: connection-refused / DNS / timeout → `SurfaceUnreachableException`
("<Surface> unreachable at https://host:port — is it running?"); a genuine caller cancellation
propagates as `OperationCanceledException` (never disguised as unreachable); a TOFU
`CertificatePinMismatchException` is unwrapped from its `HttpRequestException` and surfaced as-is.

**Resilience (one handler per client).** Per MS guidance we add exactly **one** resilience handler.
Because we need fine-grained control over which calls retry, we use the custom-pipeline
`AddResilienceHandler` (not `AddStandardResilienceHandler`) from `Microsoft.Extensions.Http.Resilience`
(the supported package — `Microsoft.Extensions.Http.Polly` is deprecated, do not reintroduce it).
The pipeline, outermost→innermost: **total timeout 10s** → **retry** (max 3, exponential backoff +
jitter) → **attempt timeout 3s**. No circuit breaker: the targets are single LAN hosts, where a
breaker would block legitimate retries after a brief blip without the multi-replica benefit it's
designed for. The pipeline reads time through the DI-registered `TimeProvider` (real in production,
`FakeTimeProvider` in the budget test) so timeouts/backoff are testable at ~zero wall-clock.

**Retry altitude — per-request opt-in (important).** The retry strategy's `ShouldHandle` retries a
**transient** fault only when **either** the HTTP method is safe (GET/HEAD/OPTIONS/TRACE) **or** the
request carries an explicit opt-in. The dedicated-server API is **POST-only** (one endpoint, a
function envelope), so idempotency is per-**function**, not per-method — a method-only gate (the old
`DisableForUnsafeHttpMethods`) would mean *nothing* on that surface ever retries. Surface clients
opt an idempotent POST into retries by setting `SurfaceHttpRequestOptions.AllowRetry` (key
`"FicsitMcp.AllowRetry"`, in `Domain/Http`) on the request:
`request.Options.Set(SurfaceHttpRequestOptions.AllowRetry, true)`. The dedicated-server client (#5)
sets it on idempotent functions (`QueryServerState`/`HealthCheck`/`VerifyAuthenticationToken`) and
**never** on `SaveGame`/`Shutdown`/`RunCommand` — a replayed shutdown/command is a real outage.

**Self-signed TLS — trust-on-first-use (TOFU).** The dedicated server ships a self-signed cert, so
its primary handler (`SocketsHttpHandler`) uses `TofuCertificateValidator` instead of chain
validation: on first contact the cert's **SHA-256** hash (`GetCertHashString(SHA256)` — not the
collision-prone SHA-1 `Thumbprint`) is pinned; later contacts must match or the connection is
refused with `CertificatePinMismatchException` (names the pin file so a deliberate rotation can be
re-pinned). **Pins are keyed by authority (`host:port`)**, not host alone, so two services on the
same host at different ports can't be confused. First-contact pinning is **atomic** via
`ICertificatePinStore.GetOrPin` (returns the effective pin — existing if present, else the offered
one — under the store's write lock), so a first-contact race becomes a deterministic mismatch rather
than two writers clobbering each other. Pins persist in `%LocalAppData%/ficsit-mcp/cert-pins.json`
(`FileCertificatePinStore.DefaultPinFilePath`, overridable via `DedicatedServer:CertPinFilePath`) —
chosen over the content root because a published host may sit in a read-only dir, and a pin is
per-user local state, not committed config. A corrupt pin file is treated as "no pins" (re-pin on
next contact), never a startup crash. The dev escape hatch `DangerousAcceptAnyCert=true` (on
`DedicatedServerOptions`) accepts any cert **without** pinning; its alarming name is intentional —
never use it against a server you don't fully control.

## FIN bridge (inbound listener for the in-world Lua agent)

Unlike the three outbound surfaces above, the FIN bridge is an **inbound HTTP listener**. FicsIt-Networks
computers cannot accept connections (the InternetCard is outbound-only), so the in-world Lua agent
**long-polls out** to us: commands flow *down* inside long-poll responses, results and events *up*
via the next POST. The wire contract is **ADR-001** and the JSON Schemas under `docs/fin-bridge/`;
those documents are the source of truth — schemas change first, code follows.

**Hosted separately from MCP.** The listener is its own Kestrel `WebApplication` mounted as an
`IHostedService` (`FinBridgeHostedService`), completely separate from the MCP stdio transport. It
shares the host's logger factory so its logs stay on **stderr** (never the JSON-RPC stdout stream).
The bridge is **independently optional**: with `FinBridge:ListenUrl` unset, `AddFinBridge` registers
nothing — no `IFinBridge`, no listener — and the rest of the server is untouched. The host pulls in
the shared ASP.NET Core framework (`<FrameworkReference Microsoft.AspNetCore.App>`); adding it means
`Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Options.DataAnnotations` are now framework-
provided and were removed as explicit `PackageReference`s (NU1510, warnings-as-errors).

**Endpoints** (both require the `X-FIN-Token` header; auth runs before any work):

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/fin/v1/hello` | Boot handshake: agent announces id + versions; server returns the timing contract or 426 on protocol skew. |
| `POST` | `/fin/v1/poll` | Long-poll: ingests results+events from the body, holds up to `ServerHoldMs` for queued commands, returns them. |

**`IFinBridge`** (in `FicsitMcp.Domain/FinBridge`, no HTTP/MCP deps) is the seam. Tool-facing:
`SendAsync` (enqueue + await result with timeout), `GetLiveness`, `RecentEvents`, `Subscribe`.
Transport-facing (driven by the endpoints): `HelloAsync`, `PollAsync`.

**At-most-once, dogmatically.** Commands are **never** auto-retried — a replayed flip-switch is a
real-world double-toggle. Command ids are the sole correlation key; on deadline the waiter is
completed as a timeout and the id is **tombstoned** so a late result is recognised and discarded,
never re-applied. The two timeout outcomes carry different safety meaning:
`QUEUED_NOT_PICKED_UP` (agent never pulled it; almost certainly did not execute) vs
`DELIVERED_NO_RESULT` (delivered, no result; **may** have executed — do not blindly reissue).

**Liveness fast-fail.** A command against an agent that has not said hello/polled within
`AgentLivenessMs` fails immediately with `AGENT_OFFLINE` and an operator remedy ("Is the FIN
computer powered and running the agent script?"), rather than hanging to the deadline.

**Back-pressure asymmetry.** The per-agent command queue **rejects** on full (`QUEUE_FULL`) — never
drop a mutation. The event ring **drops oldest** on overflow — telemetry loss is acceptable — and
events fan out to subscribers via per-subscriber drop-oldest channels (issue #21 consumes this).

**Config knobs** (`FinBridge` section, env `FICSITMCP_FinBridge__<Key>`): `ListenUrl`,
`SharedSecret` (secret), and tunable `ServerHoldMs` (25000), `AgentLivenessMs` (40000, must exceed
`ServerHoldMs`), `MaxQueuedCommands` (64), `MaxBufferedEvents` (256), `DefaultCommandDeadlineMs`
(8000). Defaults track ADR-001.

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

To connect the server to real surfaces, add an `"env"` block with the `FICSITMCP_` vars —
see the example under [Configuration](#configuration). The block above runs the server with
every surface dormant.

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
