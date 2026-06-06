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
can't be reused). Also takes an optional `HttpCompletionOption` (default `ResponseContentRead`) so the
download path can pass `ResponseHeadersRead`. On 401:
- **Idempotent** function → re-auth once and replay exactly once. Re-auth only succeeds when a config
  token exists (we re-present it via `TryReauthenticateAsync`); a session token from password login
  CANNOT be silently renewed (no password retained) → surfaces the 401 as `DedicatedServerAuthException`.
  Replays at most once (no infinite loop): test asserts AttemptCount==2.
  - **Double-401 (config token also rejected) → `errorCode "config_token_rejected"`** (PR #34 G2): after
    re-presenting the config token, a SECOND 401 throws a `DedicatedServerAuthException` whose message
    names `FICSITMCP_DedicatedServer__AdminToken` + PasswordLogin/ClaimServer remedies (token rotated/
    revoked server-side). Distinct from a generic bare-401.
- **Non-idempotent** function → NEVER replay. The call may have executed before the token was rejected,
  so blind replay could double-fire. Throws `DedicatedServerAmbiguousResultException(functionName,...)`
  after a SINGLE attempt. This is the load-bearing safety invariant. `DedicatedServerAmbiguousResultException`
  XML doc lists per-function recovery (query EnumerateSessions/QueryServerState/HealthCheck before retrying).

**Request/response disposal (PR #34 G1):** `SendWithReauthAsync` does NOT dispose the request before the
response is decided — `HttpResponseMessage.RequestMessage` back-references it (and under
`ResponseHeadersRead` keeps the content stream open). The request is handed to the returned response;
the caller's `using` on the response disposes both. Only the discarded first request (on replay) is
disposed explicitly.

**SetAdminPassword special case:** changing the admin password invalidates the OLD token. The request
carries the NEW token; the client sends with `allowReauth:false` (don't race the invalidation we cause)
and on success adopts the new token. Returns a SYNTHETIC `AuthenticationTokenResponse(request token)` —
server returns 204 (no body) per OAS; fragile if a future server adds body fields (a test asserts the
204 contract). On a server REJECTION (error envelope), the client throws and does NOT adopt the new
token (test G11 asserts the original token still presents on the next call).

**AllowRetry opt-in** (`SurfaceHttpRequestOptions.AllowRetry`, consumed by infra resilience pipeline):
set ONLY on idempotent functions (QueryServerState, HealthCheck, VerifyAuthenticationToken,
EnumerateSessions, PasswordLogin/PasswordlessLogin, GetServerOptions, GetAdvancedGameSettings,
**DownloadSaveGame**) and the `InvokeRawAsync(allowRetry:true)` opt-in. NEVER on SaveGame/LoadGame/
RunCommand/Shutdown/Claim/Delete*/Apply*/CreateNewGame/uploads. Tests assert the option key on outgoing
requests (Theory over both sets).

**DownloadSaveGame (PR #34 C2/C3/C4):** is a PURE READ → `idempotent:true`, `allowRetry:true`,
`allowReauth:true`, sent with `HttpCompletionOption.ResponseHeadersRead` (true streaming, never buffers
the save) via the new `SurfaceHttpClient.SendAsync(req, completionOption, ct)` overload. Branch on
content-type: JSON error envelope → map+throw; JSON that's NOT an error → throw
`unexpected_response_shape` (don't write empty file); bare 401 survived re-auth → auth exception;
other non-success non-JSON (e.g. text/plain) → `http_<code>` naming the content type; else stream the
binary body to the destination in chunks. The earlier `d51fa21` "documented buffering honestly"
compromise was REPLACED by this real streaming fix.

See [[project-api-surface]] and [[infra-retry-opt-in]] (infra memory).
