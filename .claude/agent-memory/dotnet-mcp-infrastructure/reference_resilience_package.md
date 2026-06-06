---
name: reference-resilience-package
description: Authoritative source + API notes for HTTP resilience in this .NET project
metadata:
  type: reference
---

HTTP resilience authoritative doc: learn.microsoft.com/dotnet/core/resilience/http-resilience (fetched via the microsoft-learn MCP tool).

Key API facts confirmed there:
- Package: `Microsoft.Extensions.Http.Resilience` (built on Polly). `Microsoft.Extensions.Http.Polly` is DEPRECATED.
- Add ONLY ONE resilience handler per client; for custom semantics use `AddResilienceHandler(name, builder => ...)` which gives a `ResiliencePipelineBuilder<HttpResponseMessage>` (extension methods `AddTimeout`, `AddRetry`, `AddCircuitBreaker`).
- `AddResilienceHandler` returns `IHttpResiliencePipelineBuilder`, NOT `IHttpClientBuilder` (don't chain it expecting the client builder back).
- Retry: `HttpRetryStrategyOptions` has `DisableForUnsafeHttpMethods()` (POST/PATCH/PUT/DELETE/CONNECT) and `DisableFor(params HttpMethod[])`.
- `DelayBackoffType` and Polly types come from the `Polly` namespace (needs `using Polly;`).

**When to use:** any time outbound HTTP resilience is touched in this repo — verify against this doc, not memory, as the API evolves.
