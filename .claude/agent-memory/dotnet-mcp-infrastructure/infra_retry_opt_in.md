---
name: infra-retry-opt-in
description: Per-request retry opt-in key the dedicated-server client (#5) must set on idempotent POST functions
metadata:
  type: project
---

The surface resilience pipeline gates retries on transient faults by EITHER HTTP-method safety
(GET/HEAD/OPTIONS/TRACE) OR an explicit per-request opt-in. Callers opt in on the request:

```csharp
request.Options.Set(SurfaceHttpRequestOptions.AllowRetry, true);
```

- Key name string: `"FicsitMcp.AllowRetry"`
- Type/location: `FicsitMcp.Domain.Http.SurfaceHttpRequestOptions.AllowRetry` (`HttpRequestOptionsKey<bool>`)

**Why:** the dedicated-server API is POST-only (single endpoint, function envelope), so idempotency
is per-FUNCTION not per-method. A method-only gate (the old `DisableForUnsafeHttpMethods`) meant
NOTHING on that surface ever retried.

**How to apply:** the #5 dedicated-server client sets the flag ONLY on idempotent functions
(`QueryServerState` / `HealthCheck` / `VerifyAuthenticationToken`) and NEVER on
`SaveGame` / `Shutdown` / `RunCommand` — a replayed shutdown/command is a real outage. Do not weaken
this gate in a refactor. The pipeline reads the request via `ResilienceContext.GetRequestMessage()`
(falls back to `Outcome.Result?.RequestMessage`; fails safe to no-retry if unidentifiable).
Pipeline order (outer→inner): total timeout 10s → retry (max 3, exp backoff + jitter) → attempt
timeout 3s. Pipeline reads time via DI `TimeProvider` (FakeTimeProvider in budget test). See
[[infra-tofu-pin-format]].
