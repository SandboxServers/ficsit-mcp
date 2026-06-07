---
name: project-lifecycle-console-tools
description: Issue #9 MCP tools (run_console_command, shutdown_server, rename_server, set_client/admin_password) — hint sets, token-minting, secret non-leak design
metadata:
  type: project
---

Issue #9 (worktree-issue-9-lifecycle-console, branched from main @85c7fab + #35). Established the FIRST
dedicated-server tool-layer pattern — no sibling tool PRs (#6/#7/#8) had merged when this landed, so
`WithToolsFromAssembly()` auto-discovers `[McpServerToolType]` classes and DI injects services
(IDedicatedServerApiClient, ILogger<T>) into static tool methods. GameDataTool is the shape template.

**Files (host layer only; protocol stays in Domain client):**
- `src/FicsitMcp/Tools/LifecycleConsoleTool.cs` — the 5 tools (static methods).
- `src/FicsitMcp/Tools/Results/` — RunConsoleCommandResult, ShutdownServerResult, RenameServerResult,
  SetPasswordResult (one public record per file; model-facing shapes live in host, NOT Domain).
- Tests: `tests/FicsitMcp.Tests/Tools/` — LifecycleConsoleToolTests (23 tests),
  FakeDedicatedServerApiClient (hand-rolled, no HTTP), CapturingLogger<T> (records msg+state+exception,
  `.AllText` is the leak-search haystack; no FakeLogger package available).

**Hint sets (safety-auditor #24 will audit; these are honest first cut):**
- run_console_command: Destructive=true, OpenWorld=true, Idempotent=false.
- shutdown_server: Destructive=true, Idempotent=false.
- rename_server: Destructive=false, Idempotent=true (reversible metadata; not ReadOnly — it writes).
- set_client_password: Destructive=true, Idempotent=true (same pw twice → same state; locks out players/
  empty pw opens server).
- set_admin_password: Destructive=true, Idempotent=false (token rotation is a one-way side effect).

**set_admin_password token-minting (the sharp corner):** `SetAdminPasswordRequest` requires BOTH a new
Password AND a new `AuthenticationToken` — the API model has the CALLER choose the next token (akin to
server.GenerateAPIToken; the old token is invalidated by the change). The TOOL mints it:
`RandomNumberGenerator.GetBytes(32)` → base64url (no padding). The minted token is a SECRET — never
logged, never returned in the result, never in an exception. The client (`SetAdminPasswordAsync`) adopts
it on success (allowReauth:false so it doesn't race the invalidation it causes). An EMPTY admin password
is rejected by the tool BEFORE sending (would leave no admin credential); empty CLIENT password is valid
(clears it → open server).

**Error handling pattern:** tools catch `DedicatedServerApiException` (+ subtypes) and rethrow
`ModelContextProtocol.McpException(string, innerEx)` with a curated, secret-free, actionable message
(no stack trace text). Ambiguous-result case gets a specific "re-query state, NOT retried" message.
ArgumentException (blank/null guards) propagates as-is (carries no secret). Source exceptions already
carry only the server's own errorCode/message, so mapping adds no leak vector.

**Leak tests (the contract #9 demands):** invoke set_admin_password/set_client_password through the tool
with FakeDedicatedServerApiClient + CapturingLogger, assert the password string (and the minted admin
token) appear NOWHERE in logger.AllText, result.Message, or thrown McpException.ToString() — on BOTH
success and failure paths. Production log templates only ever interpolate the ACTION ("setting"/"clearing"/
"rotating"), never the secret value.

**Shutdown description split:** states up front it disconnects all players, then the supervised→RESTART
vs bare-process→STAYS-DOWN distinction (this MCP server can't detect which from the API, so the result
text declares both rather than claiming one). Also notes the API has NO save-before-shutdown affordance
(verified against the client surface) — recommends saving first.

Gate at finish: build 0/0, test 232 passed (was 209), `dotnet format --verify-no-changes` exit 0.
NOTE: worktree shares git with main checkout — `cd` in the bash sandbox lands in the MAIN repo dir, not
the worktree; run dotnet against the worktree's absolute .sln path explicitly.

See [[project-auth-lifecycle]] (SetAdminPassword token dance) and [[project-api-surface]].
