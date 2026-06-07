# FIN Bridge Architect Memory

- [Protocol v1 Contract](protocol-v1-contract.md) — envelopes, endpoints, timing constants; source of truth in docs/fin-bridge.
- [At-Most-Once Decision](at-most-once-decision.md) — why no auto-retry, two-timeout split, queue-reject vs event-drop.
- [FIN Capabilities Verified](fin-capabilities-verified.md) — InternetCard futures, event.pull, findComponent facts + confidence tags.
- [Bridge Host Impl (#18)](bridge-host-impl.md) — IFinBridge seam, endpoint/envelope mapping, hosted-service separation, ASP.NET framework-ref gotcha.
- [At-Most-Once Implementation Patterns](at-most-once-implementation-patterns.md) — drain-loop reconciliation, TrySetResult-first, per-command lock, TimeProvider CTS; the C# how behind the contract.
