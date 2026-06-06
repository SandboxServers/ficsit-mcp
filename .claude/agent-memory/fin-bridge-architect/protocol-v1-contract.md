---
name: protocol-v1-contract
description: FIN bridge wire contract v1 — envelopes, endpoints, timing constants, and where they're defined (ADR-001 + schemas under docs/fin-bridge)
metadata:
  type: project
---

FIN bridge protocol v1 designed in issue #17 (design-only). Lives in
`docs/fin-bridge/`: `adr-001-fin-bridge-protocol.md`, `schema-rationale.md`,
`README.md`, and `schemas/*.schema.json` (draft 2020-12, `$id` base
`https://ficsit-mcp.dev/schemas/fin-bridge/`).

**Why:** FIN computers cannot accept inbound connections (InternetCard is outbound-only),
so the in-world Lua agent long-polls; commands flow down in poll responses, results/events
up via the next POST.

**How to apply:** This is the source-of-truth contract. Any FIN/Lua-side change updates
schema docs FIRST, then defer Lua impl to fin-agent-lua-author. Bump protocolVersion
(integer, starts at 1) on any wire-shape change.

Endpoints: `POST /fin/v1/hello` (boot/version gate), `POST /fin/v1/poll` (long-poll;
results+events up, commands down). Both require `X-FIN-Token` header, 401 before work.

Envelopes (each schema is its own file, wrappers `$ref` leaves):
- command (server→agent): id, target (oneOf byNick/byId/byClass), operation (open string),
  args (open; allowMultiple is the one fixed cross-cutting arg), issuedAt, deadlineMs.
- result (agent→server): id (echoes command id), ok, payload XOR error (enforced via
  if/then). Writes return before+after state.
- event (agent→server): seq (per-agent monotonic), signal, source (componentRef), data,
  agentTimestamp (advisory; server stamps authoritative receivedAt).
- poll-request wraps results[]+events[]+droppedEvents(running count)+version/identity.
- poll-response wraps commands[]+protocolVersion.

Chosen timing constants (agent-side configurable, names fixed in ADR):
serverHoldMs=25000, agentLivenessMs=40000 (~1.5x hold), pollIntervalMs=250,
maxCommandsPerWake=8, maxQueuedCommands=64 (reject on full, never drop a mutation),
maxBufferedEvents=256 (drop-oldest ring).

Reserved error codes (enum in common.schema.json): AGENT_OFFLINE, QUEUE_FULL,
QUEUED_NOT_PICKED_UP, DELIVERED_NO_RESULT, AMBIGUOUS_TARGET, TARGET_NOT_FOUND,
PROTOCOL_VERSION_MISMATCH (HTTP 426), UNAUTHORIZED, INVALID_ARGS, OPERATION_FAILED.

See [[at-most-once-decision]] and [[fin-capabilities-verified]].
