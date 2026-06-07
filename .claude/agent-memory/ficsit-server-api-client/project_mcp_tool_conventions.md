---
name: project-mcp-tool-conventions
description: Conventions for MCP tools over the dedicated-server client — Domain-service backing, hint annotations, exception mapping, optional-IFinBridge injection (issue #6, PR #36)
metadata:
  type: project
---

Established by issue #6 (PR #36, the first real tools over `IDedicatedServerApiClient`): tools
`get_server_state`, `health_check`, `verify_connection`. Sets the pattern for #7/#8/#9.

**Layering: tool -> Domain service -> client.** Tools are thin `[McpServerTool]` static methods on a
`[McpServerToolType]` class in `src/FicsitMcp/Tools/` (here `ServerStateTool.cs`). They take the
backing service as the FIRST parameter (DI-injected by the MCP SDK) plus a trailing
`CancellationToken`, `ArgumentNullException.ThrowIfNull` the service, and delegate. The real logic
(payload distillation, multi-surface aggregation) lives in a Domain service so it is unit-testable
without the MCP host. For #6 that service is `IServerObservationService` /
`ServerObservationService` in `src/FicsitMcp.Domain/ServerObservation/`, returning typed records
(`ServerStateSummary`, `ServerHealth`, `ConnectionDiagnostics`/`SurfaceConnectionStatus`) — NEVER raw
API JSON. Registered via `AddServerObservation()` (`src/FicsitMcp/ServerObservation/`), called from
Program.cs AFTER `AddFinBridge` so the optional `IFinBridge` is in the container.

**`WithToolsFromAssembly` discovers tools** — no Program.cs edit to add a tool. Program.cs only
changes when you add a Domain SERVICE to DI.

**Behavioral hints (all three #6 tools): `ReadOnly = true, Idempotent = true, OpenWorld = false`.**
OpenWorld=false because they talk only to the configured server, not the wider internet. Match the
existing GameDataTool/ServerInfoTool shape. Mutating tools (#8 saves, #9 console) will differ — see
[[project-api-surface]] idempotency classification and defer hint disputes to the safety-auditor.

**`[Description]` is the model-facing schema** — write it to say WHEN to pick this tool vs the
siblings (state vs health vs connection-diagnosis), and state the failure mode (requires the surface
configured/reachable; fails with an actionable message otherwise).

**Exception-mapping decision (#6):** the MCP SDK turns a thrown exception into a tool error and
surfaces its `.Message`. The client's typed exceptions (`SurfaceNotConfiguredException`,
`DedicatedServerAuthException`, `DedicatedServerApiException`, `SurfaceUnreachableException`,
`FrmUnreachableException`) ALREADY carry actionable, secret-free messages, so read tools
(`get_server_state`/`health_check`) let them PROPAGATE UNCHANGED — do not re-wrap. The one tool that
must NOT throw on a surface failure is `verify_connection`: it catches per-surface (rethrowing only
`OperationCanceledException`) and reports `IsReachable=false` with the reason, because diagnosing
connectivity is its whole job. Decision documented in `ServerStateTool` XML remarks.

**verify_connection surface probes:** DedicatedServer reachability = `HealthCheckAsync` (no-privilege,
works tokenless). FRM = `IFrmClient.GetProdStatsAsync` (cheapest read). FIN bridge is INBOUND (a
listener) — there is nothing to "call"; reachability = listener wired up. `IFinBridge` is injected
OPTIONALLY (`IFinBridge?` ctor param, `provider.GetService<IFinBridge>()` not GetRequiredService)
because its DI registration is gated on `FinBridgeOptions.IsConfigured`; a null bridge while
configured = "listener not registered, restart". Configured-state for each surface is read from the
bound options (`IConfigurableSurface.IsConfigured` + `SurfaceName`/`ActivatingEnvVar`), injected as
plain options objects via `IOptions<T>.Value` in the registration (Domain service takes the POCOs,
not `IOptions`, so tests construct it plainly).

**Tests:** `tests/FicsitMcp.Tests/ServerObservation/` — hand-rolled fakes (no Moq in this repo) for
all three surface clients in `Fakes.cs` (every unused interface member throws `NotSupportedException`
so accidental use is loud; scriptable members use `*Response`/`*Exception` init props). AAA style.
verify_connection aggregation tested per surface incl. unconfigured / reachable / unreachable, plus a
whole-result test that one bad surface doesn't hide the others.

See [[project-api-surface]] (HealthCheck `health` is "healthy"/"slow"; QueryServerState nests under
`serverGameState`) and [[project-client-layout]].
