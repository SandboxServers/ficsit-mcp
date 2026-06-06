---
name: project-di-lifetime-and-validation
description: DedicatedServerApiClient DI lifetime (SINGLETON + per-call HttpClient factory) and the relaxed BaseUrl-without-AdminToken validation contract
metadata:
  type: project
---

Decisions from PR #34 review (Copilot C5 + C6).

**DI lifetime: SINGLETON** (`DedicatedServerClientRegistration.AddDedicatedServerApiClient`).
**Why:** the client holds mutable adopted-token state (`_cachedToken` guarded by a `SemaphoreSlim`).
Transient discarded the token on every resolution, so a `PasswordLogin`/`ClaimServer` in one tool call
never carried into the next — the whole token-adoption lifecycle was dead. Singleton keeps the token +
lock stable for the host's lifetime.
**How to apply:** keep it singleton; if you add per-request scoped state, do NOT capture it in fields.

**Coherent HttpClient lifetime under a singleton:** the client must NOT capture one `HttpClient`/handler
for the whole process (that defeats `IHttpClientFactory` handler rotation). So the ctor takes a
`Func<SurfaceHttpClient>` FACTORY (not a captured instance); the registration's delegate calls
`IHttpClientFactory.CreateClient(SurfaceHttpClients.DedicatedServer)` PER SEND and wraps it in a fresh
`SurfaceHttpClient` shell (cheap wrapper). Only the singleton `IHttpClientFactory` is captured. All
`_httpFactory().SendAsync(...)` call sites resolve a fresh client. Test harness passes `() => shell`.

**Client is `IDisposable`** (G5): `Dispose()` disposes the `_tokenLock` SemaphoreSlim; container owns
disposal at teardown (singletons are disposed by the DI container). Does NOT dispose the HttpClient
(factory-owned).

**Validation contract relaxed (C6):** `DedicatedServerOptions.Validate` NO LONGER requires `AdminToken`
when `BaseUrl` is set. `Validate` now returns `[]` (empty) — only the `[Url]` attribute on `BaseUrl`
remains. Rationale: enables "no token yet" bootstrap (`ClaimServer`/`PasswordLogin` mint the first
token). Auth is enforced PER FUNCTION at call time, not at startup. The named-client HttpClient
registration only needs `BaseUrl` (via `.Require()` → `IsConfigured`), so tokenless resolution works.

**Asymmetry vs FIN bridge (intentional):** `FinBridgeOptions.Validate` STILL requires `SharedSecret`
when `ListenUrl` is set — an inbound open listener with no secret is an exposed attack surface, whereas
the dedicated-server client is OUTBOUND with a merely-deferred credential. Documented in CLAUDE.md
"Surface-optionality contract". Test `DedicatedServer_BaseUrlWithoutToken_ValidatesCleanly_ForBootstrapWorkflows`
replaced the old `Validation_Fails_WhenConfiguredSurfaceHasNoCredential`.

**G7 envelope build:** request envelope is now the `DedicatedServerRequestEnvelope(Function, JsonElement? Data)`
record (source-gen registered), serialized via `JsonSerializer.SerializeToElement` — replaced the old
MemoryStream+Utf8JsonWriter+JsonDocument.Clone hand-build. `SerializeToElement` helper likewise uses the
net10 `JsonSerializer.SerializeToElement(value, typeInfo)` directly (no MemoryStream).

See [[project-auth-lifecycle]] and [[project-client-layout]].
