---
name: at-most-once-decision
description: Why FIN commands are at-most-once with two distinct timeouts, and the mechanics (unique id + agent dedup + server tombstone) that enforce it
metadata:
  type: project
---

FIN bridge commands are at-most-once and NEVER auto-retried.

**Why:** A replayed flip-switch is a real-world double-toggle ("pause smelters" becomes
"unpause smelters"). Correctness beats latency for machine control.

**How to apply:** On timeout/ambiguity/transport failure, surface to caller and stop —
never silently resend. Three mechanics make it real:
1. Server-generated unique command id (ULID/UUID) = sole correlation key.
2. Agent keeps a recently-seen-id set: a re-delivered command is acked, not re-executed.
3. Server tombstones an id once its deadlineMs passes; a result arriving after timeout is
   recognised and discarded, never re-applied to a caller who gave up.

Two timeouts with DIFFERENT safety meaning (load-bearing for #24 safety model):
- QUEUED_NOT_PICKED_UP: enqueued, agent never pulled it before deadline → almost
  certainly did NOT execute.
- DELIVERED_NO_RESULT: a poll response delivered it, no result before deadline → MAY have
  executed; treat as possibly-applied, do not blindly reissue.
Server distinguishes by tracking whether the command was ever included in a poll response.

Liveness fast-fail: command against a non-alive agent (no hello/poll within
agentLivenessMs) returns AGENT_OFFLINE immediately with operator remedy ("Is the FIN
computer powered and running the agent script?"), never hangs to deadline.

Back-pressure asymmetry: command queue REJECTS on full (QUEUE_FULL) — dropping a mutation
is unsafe. Event ring DROPS oldest — events are observational, telemetry loss is OK, but
report a running droppedEvents count.

See [[protocol-v1-contract]].
