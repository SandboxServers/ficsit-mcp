---
name: bridge-host-impl
description: How issue #18 implemented the FIN bridge host — IFinBridge two-faced seam, endpoint/envelope mapping, hosted-service separation, ASP.NET framework-reference gotcha
metadata:
  type: project
---

Issue #18 (PR feat/18-fin-bridge-host) implemented the FIN bridge host against the merged ADR-001.

**Why:** Turns the design contract into a running listener + the IFinBridge seam machine-control
tools (#20) and event notifications (#21) build on.

**How to apply:** When extending the bridge, respect these load-bearing facts:

- `IFinBridge` is two-faced and lives in `src/FicsitMcp.Domain/FinBridge` (no HTTP/MCP deps).
  Tool-facing: `SendAsync(agentId, command, ct)`, `GetLiveness`, `GetAgents`, `RecentEvents`,
  `Subscribe(out ChannelReader<FinEvent>)`. Transport-facing (driven by HTTP endpoints):
  `HelloAsync(hello)`, `PollAsync(poll, ct)`.
- `PollAsync` does BOTH directions in one call (matches ADR): ingests results+events from the body
  FIRST (completes waiters / fills ring / fans out), refreshes liveness, THEN holds up to
  ServerHoldMs for queued commands and returns them. Consequence for tests with FakeTimeProvider:
  a poll with no queued command BLOCKS until time is advanced — advance past ServerHoldMs to release.
- Two endpoints only (ADR, NOT the 4 in the #18 issue text): `POST /fin/v1/hello`, `POST /fin/v1/poll`.
  Discrepancy resolved in favor of the ADR; flagged in the PR body.
- Host layer in `src/FicsitMcp/FinBridge`: FinBridgeHostedService (own Kestrel WebApplication via
  CreateSlimBuilder, bound to ListenUrl, shares parent ILoggerFactory so logs stay on stderr),
  FinBridgeEndpoints (status mapping; 426 for ProtocolVersionMismatchException), FinTokenAuthMiddleware
  (CryptographicOperations.FixedTimeEquals on X-FIN-Token, 401 before any work),
  FinBridgeRegistration.AddFinBridge (registers IFinBridge + TimeProvider.System + hosted service
  ONLY when IsConfigured).
- ASP.NET gotcha: host has `<FrameworkReference Microsoft.AspNetCore.App>`. That makes
  Microsoft.Extensions.Hosting and Microsoft.Extensions.Options.DataAnnotations framework-provided,
  so they were REMOVED as explicit PackageReferences (NU1510 fires under warnings-as-errors). Their
  versions still pin in Directory.Packages.props. Domain project added Microsoft.Extensions.Options
  (abstractions) for IOptions<FinBridgeOptions>.
- Timing/back-pressure knobs are on FinBridgeOptions (extended in #18): ServerHoldMs (25000),
  AgentLivenessMs (40000, validated > ServerHoldMs), MaxQueuedCommands (64), MaxBufferedEvents (256),
  DefaultCommandDeadlineMs (8000). maxCommandsPerWake (8) is a private const in FinBridge.cs
  (protocol-cadence fact, not an operator knob).
- At-most-once mechanics in FinBridge.cs: per-agent bounded Channel queue with FullMode.Wait +
  TryWrite (false ⇒ QUEUE_FULL, reject not drop); per-id PendingCommand with TCS +
  TimeProvider ITimer deadline; on deadline → tombstone id then complete with DELIVERED_NO_RESULT
  (if marked delivered during a poll) or QUEUED_NOT_PICKED_UP; tombstoned-id results discarded on
  ingest. Tombstone set bounded at 4096 (drop-oldest). TimeProvider is injected for testability.
- Tests in `tests/FicsitMcp.Tests/FinBridge`: FinBridgeTests (13, FakeTimeProvider unit) +
  FinBridgeHostedServiceTests (5, real Kestrel on ephemeral port driven by plain HttpClient as fake
  agent). Host exposes internals via InternalsVisibleTo("FicsitMcp.Tests").

See [[protocol-v1-contract]] and [[at-most-once-decision]].
