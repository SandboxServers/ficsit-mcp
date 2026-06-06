# FIN Bridge Schema Rationale

Companion to [`adr-001-fin-bridge-protocol.md`](adr-001-fin-bridge-protocol.md). This
document explains *why* each envelope is shaped the way it is and pins down field
semantics that #18 (host) and #19 (agent) implement against. The normative definitions
are the JSON Schema files in [`schemas/`](schemas/); this prose explains intent. Where
the two ever disagree, the schema wins and this doc is the bug.

All schemas are JSON Schema **draft 2020-12**, carry a stable `$id` under
`https://ficsit-mcp.dev/schemas/fin-bridge/`, and document every property with a
`description` that is itself contract documentation.

## Shared building blocks

### `protocolVersion` (integer, ≥ 1)

Present in `hello`, every `poll-request`, and every `poll-response`. Starts at **1**.
The server matches the agent's value against its supported range and refuses with
`PROTOCOL_VERSION_MISMATCH` (HTTP 426) outside it. It is mandatory from day one because
EEPROM scripts and the server update independently — skew is the normal case. A single
integer (not semver) because the *wire contract* changes in discrete, server-gated
steps; agent *script* evolution is tracked separately by `agentScriptVersion`.

### `agentId` (string)

Stable identifier the agent chooses for itself and repeats on every request. Scopes the
command queue, liveness state, and event sequence on the server. Distinct from FIN
component UUIDs — it names the *computer running the agent*, not a machine it controls.

### `target` (object, exactly one of `byNick` / `byId` / `byClass`)

How a command names what it acts on, mirroring FIN's `findComponent` query model:

- `byNick` — a player-assigned nick/group string, resolved agent-side via
  `findComponent`. Convenient but **not unique**; a single-target write against an
  ambiguous nick is an `AMBIGUOUS_TARGET` error, never a silent first-match.
- `byId` — a stable FIN component UUID, resolved via `component.proxy`. Preferred once
  known; tools cache `nick → uuid` from a `discover`.
- `byClass` — a class query (e.g. `Build_SmelterMk1_C`) for reads/broadcasts.

`oneOf` is enforced so a command cannot smuggle two addressing modes and leave the
agent guessing.

### `componentRef` (object: `id`, optional `nick`, `class`, `displayName`)

The *resolved* form a component takes in discovery results and event sources. Always
carries the authoritative `id` (UUID); `nick`/`class`/`displayName` are advisory and
may be absent if FIN does not supply them.

### `errorObject` (object: `code`, `message`, optional `details`)

Uniform error shape across results and HTTP error bodies. `code` is a typed enum so
callers branch on it; `message` is human-actionable (it names the real-world remedy for
operator-facing failures); `details` is free-form structured context (e.g. the
`candidates` array for `AMBIGUOUS_TARGET`, version bounds for a mismatch).

Reserved `code` values: `AGENT_OFFLINE`, `QUEUE_FULL`, `QUEUED_NOT_PICKED_UP`,
`DELIVERED_NO_RESULT`, `AMBIGUOUS_TARGET`, `TARGET_NOT_FOUND`, `PROTOCOL_VERSION_MISMATCH`,
`UNAUTHORIZED`, `INVALID_ARGS`, `OPERATION_FAILED`. The schema constrains `code` to this
set so new codes are a deliberate, versioned addition.

## Command envelope

`command.schema.json`. Server → agent, delivered inside `poll-response.commands[]`.

- `id` — server-generated, unique, opaque (ULID/UUID). The correlation key. The agent
  dedups on it (recently-seen set) so a re-delivered command is acked-not-re-executed;
  the server tombstones it after timeout so a late result is discarded.
- `target` — see shared blocks.
- `operation` — string naming the action (e.g. `discover`, `getState`, `setStandby`,
  `setPotential`, `setRecipe`, `execute`). The exact catalogue is **#20's** to define;
  the schema deliberately keeps `operation` an open string so adding a tool does not
  require a protocol bump.
- `args` — object, operation-specific. Open by design (`additionalProperties` allowed)
  for the same reason. `allowMultiple` (bool, default false) is the one cross-cutting
  arg fixed here: it opts a `byNick`/`byClass` operation into intentional fan-out over
  all matches, suppressing `AMBIGUOUS_TARGET`. Reads/broadcasts set it; single-target
  writes must not.
- `issuedAt` — RFC 3339 timestamp the server stamps at enqueue (telemetry/debugging).
- `deadlineMs` — relative deadline (ms) from delivery, after which the server stops
  waiting for this command's result and tombstones the id. Distinct from the agent's
  own execution timeout; this is the *server's* patience.

## Result envelope

`result.schema.json`. Agent → server, inside `poll-request.results[]`.

- `id` — echoes the command `id` it answers. The only correlation; HTTP pairing cannot
  be used because the result rides a *different* request than the command delivery.
- `ok` — boolean. `true` ⇒ `payload` present, `error` absent; `false` ⇒ `error`
  present, `payload` absent. Enforced by schema `if/then` so a result is never
  ambiguously both/neither.
- `payload` — operation-specific success data. **Write operations return both `before`
  and `after` state** so the model and operator can see the effect of a mutation; this
  convention is mandated in the ADR and realised per-operation by #20.
- `error` — `errorObject` on failure.

A result whose `id` the server has already tombstoned (deadline passed) is recognised
and discarded — never re-applied to a caller who already gave up. That discard is host
behaviour (#18), not a schema constraint, but the schema's stable `id` correlation is
what makes it possible.

## Event envelope

`event.schema.json`. Agent → server, inside `poll-request.events[]`.

- `seq` — monotonically increasing per-agent sequence number. Lets the server detect
  gaps (combined with `droppedEvents`) and order events independently of the
  untrusted agent clock.
- `signal` — the FIN signal name (e.g. `ItemTransfer`, `PowerFuseChanged`,
  `ProductionChanged`), i.e. the first element of FIN's `event.pull` tuple.
- `source` — `componentRef` of the emitting component (the `sender` from `event.pull`).
- `data` — signal-specific payload (the trailing `event.pull` args), object-shaped.
- `agentTimestamp` — the agent's `computer.millis()`-style clock, **advisory only**.
  The server stamps authoritative `receivedAt` on ingest; correlation and ordering use
  `seq` + `receivedAt`, never `agentTimestamp`.

Events are observational, so under buffer pressure the agent drops *oldest* events and
reports a running `droppedEvents` count — losing telemetry is acceptable where losing a
mutation would not be. Tool-layer throttling/burst-collapse (e.g. 47 `ItemTransfer`
signals in 10 s → one collapsed event) lives in **#21**, above this envelope; the wire
carries raw events and the count of what was dropped.

## Poll request / response wrappers

`poll-request.schema.json` and `poll-response.schema.json` are the only bodies actually
sent on `/fin/v1/poll`. They wrap batches of the above plus version and liveness fields.

### `poll-request` (agent → server)

- `protocolVersion`, `agentId`, `agentScriptVersion` — identity + version on every poll;
  liveness is refreshed by the mere arrival of a valid poll.
- `results` — array of result envelopes for commands executed since the last poll
  (possibly empty).
- `events` — array of buffered event envelopes (possibly empty).
- `droppedEvents` — running count of events dropped by the agent's drop-oldest ring
  since boot, so the server knows telemetry gaps exist even when `events` looks healthy.

### `poll-response` (server → agent)

- `protocolVersion` — the server's, so the agent can detect skew even on a 200.
- `commands` — array of command envelopes to execute (possibly empty on a hold that
  expired with nothing queued).

`hello` uses its own minimal request/response (`hello.schema.json`): it carries the
same version/identity fields and the server replies with the negotiated
`serverHoldMs` / `agentLivenessMs` so the agent learns the timing contract at boot
rather than hard-coding it. `hello` is also where `PROTOCOL_VERSION_MISMATCH` is most
likely surfaced (HTTP 426), before any command is ever queued.

## Why these are split into separate files

Each envelope is independently `$ref`-able so #22's snapshot harness can validate a bare
command, result, or event in isolation, and so #18/#19 can reference exactly one schema
per concern. The `poll-request`/`poll-response` wrappers `$ref` the leaf envelopes
rather than re-declaring them, keeping a single source of truth per envelope shape.
