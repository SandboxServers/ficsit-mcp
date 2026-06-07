---
name: at-most-once-implementation-patterns
description: Concrete C# patterns that make FIN bridge at-most-once correct under concurrency — drain-loop reconciliation, TrySetResult-first, per-command lock, TimeProvider CTS over manual ITimer
metadata:
  type: project
---

Hard-won correctness patterns for FinBridge.cs (Domain/FinBridge), validated by the
PR #35 review (commit b5072ce). These are the load-bearing details behind the
[[at-most-once-decision]] contract.

**Per-command lifecycle lock is the heart of it.** Each `PendingCommand` owns a lock and
a small state machine (Pending → Delivered → terminal {Completed, TimedOut, Cancelled,
ShutDown}). Every transition — admit-to-batch (`TryMarkDelivered`), result
(`TryComplete`), deadline (`TryTimeout`), cancel (`TryCancel`), shutdown (`TryShutdown`)
— goes through that lock so they are mutually exclusive. Without this the
delivered-vs-not decision races the deadline and a command can mis-report
QUEUED_NOT_PICKED_UP vs DELIVERED_NO_RESULT.

**Drain-loop reconciliation (the stale-delivery bug).** A timed-out / cancelled /
caller-gone command is NOT auto-removed from the agent's command Channel, so the drain
loop MUST guard every command it reads: skip+log if id is tombstoned OR has no live
`_pending` entry OR `TryMarkDelivered` returns false (already terminal). Never deliver a
command that failed this guard — that was a real-world double-toggle hole. Mark delivered
*inside* the same admit guard the deadline path consults, as each command enters the
batch, not in a separate loop afterward.

**TrySetResult-first rule.** In IngestResults, attempt completion FIRST (TCS completion
is atomic, first-writer-wins). Use the tombstone check ONLY to classify/log a late
straggler, never as a gate before completion — gating first loses an in-time result to
the deadline. Consequence: a succeeded command is never tombstoned (deadline's TryTimeout
sees terminal state and no-ops).

**Cancellation must tombstone.** Caller cancel is as binding as a timeout: tombstone the
id so a later re-read command is dropped and a straggling result discarded.

**Timer ordering.** Wire the OnTimeout handler BEFORE arming the deadline timer; a tiny
deadline (or already-advanced FakeTimeProvider) otherwise fires before the handler is
attached → silent no-op, waiter hangs forever.

**Prefer TimeProvider-backed CTS over a manual ITimer for the long-poll hold.** Use
`new CancellationTokenSource(TimeSpan, timeProvider)` linked to the caller token via
`CreateLinkedTokenSource`. The manual-ITimer-callback-calls-Cancel pattern races its own
Dispose (unobserved ObjectDisposedException). NOTE: `CancelAfter(TimeSpan, TimeProvider)`
does NOT exist; the constructor overload is the one that takes a TimeProvider.

**Dispose fails waiters.** Dispose() must fail every outstanding waiter with a typed
shutdown error AND tombstone each id, or SendAsync callers hang past host shutdown.

**Per-agent event rings.** Ring + dropped count live on AgentState (cap per agent, so one
chatty agent can't evict another's events). RecentEvents() merges all agents ordered by
server-stamped receivedAt. Dropped count is exposed on AgentLiveness.DroppedEvents.
Subscriber fan-out stays global (its own lock).

**Result invariant enforced on ingest.** result.schema.json allOf (ok⇒payload∧!error,
!ok⇒error∧!payload) is validated in IngestResults; a malformed body is DROPPED (logged),
never used to complete a waiter — so the deadline still fires with the right outcome. A
malformed body must not be allowed to resolve a command's safety state.

**HTTP error bodies are the bare errorObject** (no `{"error":{...}}` wrapper). One helper
(`FinHttpError` in host) owns code→status mapping + serialization, used by both endpoints
and auth middleware so the three sites can't drift. Status map: PROTOCOL_VERSION_MISMATCH
→426, UNAUTHORIZED→401, INVALID_ARGS→400.

See [[at-most-once-decision]] and [[protocol-v1-contract]].
