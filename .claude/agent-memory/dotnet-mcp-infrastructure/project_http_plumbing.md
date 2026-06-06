---
name: project-http-plumbing
description: HTTP plumbing conventions established for ficsit-mcp (IHttpClientFactory, TOFU TLS, resilience) in issue #4
metadata:
  type: project
---

HTTP plumbing for outbound surface calls was built in issue #4 (PR branch `infra/4-http-plumbing`).

**Where it lives.** Testable core in `src/FicsitMcp.Domain/Http/` (BCL + Polly + Logging.Abstractions only, NO MCP). DI/registration in `src/FicsitMcp/Http/SurfaceHttpClientRegistration.cs` (host owns the resilience packages).

**Key facts future infra work must preserve:**
- Named clients keyed by `Domain/Http/SurfaceHttpClients` constants (`DedicatedServer`, `Frm`). Never `new HttpClient()`.
- BaseAddress is set at client-resolution time from surface options via `options.Require()` — resolving a client for a dormant surface fails fast naming the env var.
- Resilience: exactly ONE handler per client via custom `AddResilienceHandler` (NOT `AddStandardResilienceHandler`). Pipeline outer→inner: total timeout 10s → retry (max 3, exponential+jitter, transient only, `DisableForUnsafeHttpMethods()`) → attempt timeout 3s. No circuit breaker (single LAN hosts). Package: `Microsoft.Extensions.Http.Resilience` 10.6.0 (NOT the deprecated `.Polly`).
- No-retry on non-idempotent methods is the load-bearing requirement: a replayed POST SaveGame/Shutdown/RunCommand is a real outage.
- TOFU pin store persisted at `%LocalAppData%/ficsit-mcp/cert-pins.json` (`FileCertificatePinStore.DefaultPinFilePath`). Chosen over content root: published host may be read-only; pin is per-user local state. Corrupt file => treated as empty, never a crash. Atomic write via temp+move.
- Dev escape hatch is `DedicatedServerOptions.DangerousAcceptAnyCert` — accepts any cert WITHOUT pinning.
- Surface engineers (`ficsit-server-api-client`, `frm-observe-surface`) build request/response semantics on `Domain/Http/SurfaceHttpClient` (the SHELL), which threads CancellationToken and maps faults: transport→`SurfaceUnreachableException`, caller-cancel→`OperationCanceledException`, cert change→`CertificatePinMismatchException` (unwrapped from HttpRequestException).

**Why:** Issue #4 + its MS-Learn-verified comment. **How to apply:** When wiring a new HTTP surface, add a named client in `SurfaceHttpClientRegistration`, reuse `AddSurfaceResilience` and `SurfaceHttpClient`; do not stack a second resilience handler.

See also [[reference-resilience-package]].
