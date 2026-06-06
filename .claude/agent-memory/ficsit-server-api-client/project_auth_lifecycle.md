---
name: project-auth-lifecycle
description: DedicatedServerApiClient auth lifecycle — token precedence, 401 re-auth/replay, non-idempotent ambiguity, AllowRetry gating
metadata:
  type: project
---

Auth design implemented in `DedicatedServerApiClient` (issue #5).

**Token precedence:** config `DedicatedServerOptions.AdminToken` (the `server.GenerateAPIToken`
token, a `Secret` read via `.Reveal()`) is seeded into the cache at construction and PREFERRED —
password login is skipped when present. With no config token, authenticated calls throw
`DedicatedServerAuthException("no_credentials")` until a caller establishes a token via
`PasswordLoginAsync`/`ClaimServerAsync` (those adopt the returned token into the cache).

**Attach:** `Authorization: Bearer <token>` on authenticated functions only. HealthCheck /
PasswordLogin / PasswordlessLogin / ClaimServer send NO bearer (authenticated:false).

**401 re-auth/replay (`SendWithReauthAsync`):** takes a request *factory* (a sent HttpRequestMessage
can't be reused). On 401:
- **Idempotent** function → re-auth once and replay exactly once. Re-auth only succeeds when a config
  token exists (we re-present it via `TryReauthenticateAsync`); a session token from password login
  CANNOT be silently renewed (no password retained) → surfaces the 401 as `DedicatedServerAuthException`.
  Replays at most once (no infinite loop): test asserts AttemptCount==2.
- **Non-idempotent** function → NEVER replay. The call may have executed before the token was rejected,
  so blind replay could double-fire. Throws `DedicatedServerAmbiguousResultException(functionName,...)`
  after a SINGLE attempt. This is the load-bearing safety invariant.

**SetAdminPassword special case:** changing the admin password invalidates the OLD token. The request
carries the NEW token; the client sends with `allowReauth:false` (don't race the invalidation we cause)
and on success adopts the new token. Returns the new token to the caller.

**AllowRetry opt-in** (`SurfaceHttpRequestOptions.AllowRetry`, consumed by infra resilience pipeline):
set ONLY on idempotent functions (QueryServerState, HealthCheck, VerifyAuthenticationToken,
EnumerateSessions, PasswordLogin/PasswordlessLogin, GetServerOptions, GetAdvancedGameSettings) and the
`InvokeRawAsync(allowRetry:true)` opt-in. NEVER on SaveGame/LoadGame/RunCommand/Shutdown/Claim/Delete*/
Apply*/CreateNewGame/uploads. Tests assert the option key on outgoing requests (Theory over both sets).

See [[project-api-surface]] and [[infra-retry-opt-in]] (infra memory).
