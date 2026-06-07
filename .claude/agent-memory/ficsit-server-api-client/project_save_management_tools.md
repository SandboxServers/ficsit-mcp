---
name: project-save-management-tools
description: Issue #7 save-management MCP tools — backing-service pattern, tool hints chosen, near-match algorithm, tool-introspection test technique
metadata:
  type: project
---

Issue #7 (branch `https-api/7-save-management-tools`, commit on top of #5's `85c7fab`). First MCP
tool surface built over the dedicated-server client; establishes the surface-tool pattern.

**Backing-service layering (the pattern future surface tools should copy).** Thin
`[McpServerTool]` methods in `src/FicsitMcp/Tools/SaveManagementTool.cs` delegate to
`FicsitMcp.Domain/DedicatedServer/SaveManagement/ISaveManagementService` +
`SaveManagementService`. Domain has NO MCP reference. The service ctor takes
`IDedicatedServerApiClient`, `IOptions<DedicatedServerOptions>`, `TimeProvider`,
`ILogger<SaveManagementService>`. Registered SINGLETON in `Program.cs`
(`AddSingleton<ISaveManagementService, SaveManagementService>()`) right after
`AddDedicatedServerApiClient()`. `TimeProvider.System` is already DI-registered via
`AddSurfaceHttpClients()` (TryAddSingleton), so the service resolves it for free.

**Surface dormancy fail-fast:** service calls `_options.Value.Require()` at the top of EVERY op →
`SurfaceNotConfiguredException` naming `FICSITMCP_DedicatedServer__BaseUrl`. Don't rely solely on
the client's per-function auth check; the Require() gives the env-var name explicitly.

**Tool hints (final, set on `[McpServerTool(...)]`).** All OpenWorld=true (live external server).
- `list_sessions`: ReadOnly=true, Idempotent=true.
- `save_game`: ReadOnly=false, Idempotent=false (writes a save each call).
- `set_auto_load_session`: ReadOnly=false, Idempotent=true (no destructive; just sets a pointer).
- `download_save`: ReadOnly=true, Idempotent=true (no server mutation).
- `upload_save`: Destructive=true, Idempotent=false (can load→disconnect, overwrites name).
- `load_save`: Destructive=true, Idempotent=false. Description FIRST sentence states player disconnect.
- `delete_save` / `delete_session`: Destructive=true, Idempotent=false. Description leads with
  "Permanently and irreversibly deletes". (Idempotent=false because our validation makes a repeat
  call fail not-found, so it's not observably idempotent.)
- `rollback_to`: Destructive=true, Idempotent=false. Description FIRST sentence states player disconnect.
NOTE: safety-auditor (#24) hasn't reviewed yet — these are my honest defaults; defer to their ruling.

**Validation + near-match.** `NearMatchFinder` (pure, no I/O): `ContainsExact` = OrdinalIgnoreCase
membership (the exact-match test). `FindNearMatches` ranking, best-first: tier 0 = substring
containment (either direction), tier 1 = Levenshtein distance ≤ `max(1, queryLen/3)`; ties by
distance then input order; capped at `MaxSuggestions=5`; collapses case-insensitive dup names keeping
first casing. `SaveManagementService.ValidateSaveExists`/`ValidateSessionExists` throw
`SaveNotFoundException(kind, requestedName, nearMatches)` BEFORE any destructive client call — the
message already embeds the suggestions in prose. `delete_session`/`set_auto_load_session` validate
SESSION names; load/delete_save/download/rollback validate SAVE names (flattened across sessions).

**Rollback composition.** `RollbackToAsync`: (1) EnumerateSessions + validate target, (2) SaveGame
checkpoint named `pre-rollback-<utc>` (`yyyyMMddTHHmmssZ` via injected TimeProvider) — if this throws,
ABORT (no load), (3) LoadGame target. Returns `RollbackResult(CheckpointSaveName, LoadedSaveName)`.

**download_save / upload_save stream policy.** The TOOL opens/owns the local `FileStream`
(download: FileMode.Create; upload: FileMode.Open read) — keeps file-I/O policy out of the service.
Upload hands the stream to the client which owns+disposes it (matches client contract). Service
methods take `Stream destination` / `Stream saveGameContent`.

**Tool-introspection test technique (no live server, snapshot stand-in).** There is NO tools/list
snapshot harness / TESTING.md / `tests/.../Harness/` on this branch (that's #22, unmerged). To guard
the hint contract I build each tool's protocol `Tool` directly:
`McpServerTool.Create(methodInfo, target: null, new McpServerToolCreateOptions { Services = sp })
.ProtocolTool` (MCP SDK 1.4.0). `.ProtocolTool` is `ModelContextProtocol.Protocol.Tool` with `.Name`,
`.Description`, `.Annotations` (`ReadOnlyHint`/`DestructiveHint`/`IdempotentHint`/`OpenWorldHint`,
all `bool?`), `.InputSchema` (JsonElement). CRITICAL: you MUST pass `Services` with the injected
service type registered, else the SDK treats the `ISaveManagementService` param as a MODEL input and
it leaks into `InputSchema` (test caught this). With Services set, it's excluded — matches production
`WithToolsFromAssembly`. `SaveManagementToolHintsTests` asserts the full hint matrix + leading-
consequence wording + no-leak.

**Tests:** `tests/FicsitMcp.Tests/DedicatedServer/`: `FakeDedicatedServerApiClient` (records call log
like `"SaveGame:name"`, `"LoadGame:name"`; unused functions throw NotSupported), `NearMatchFinderTests`,
`SaveManagementServiceTests` (ordering/short-circuit/dormancy), `SaveManagementToolHintsTests`.
39 new tests; full suite 248 green.

**Gate:** main had NOT moved (origin/main == branch base f012a45) at push time — no re-merge needed.

See [[project-api-surface]], [[project-auth-lifecycle]], [[project-di-lifetime-and-validation]].
