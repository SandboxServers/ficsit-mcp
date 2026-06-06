# Agent Memory Index — dotnet-mcp-infrastructure

- [HTTP plumbing conventions](project_http_plumbing.md) — IHttpClientFactory, TOFU TLS pinning, one-handler resilience (issue #4)
- [Resilience package reference](reference_resilience_package.md) — MS-Learn doc + AddResilienceHandler API gotchas
- [Retry opt-in convention](infra_retry_opt_in.md) — how surface clients enable retries on idempotent POST functions
- [TOFU pin format](infra_tofu_pin_format.md) — SHA-256 hash, host:port authority key, atomic GetOrPin
