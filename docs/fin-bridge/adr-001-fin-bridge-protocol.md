# ADR-001: FIN Bridge Protocol

## Status

Accepted (design). Implementation deferred to issues #18 (bridge host), #19 (Lua
agent), #20 (machine-control tools), #21 (event notifications).

## Context

[FicsIt-Networks](https://docs.ficsit.app/ficsit-networks/latest/) (FIN) adds
programmable in-game computers to *Satisfactory*. Each computer runs a cooperative
Lua runtime that shares the game thread, exposes a reflection system over most game
mechanics (read/write machine state, inventories, signals), and can mount an
**InternetCard** for outbound HTTP.

The single hard constraint that shapes everything below:

> **FIN computers cannot accept inbound connections.** The InternetCard only makes
> *outbound* HTTP requests. Nothing in the game can `listen()` on a socket.

Therefore the MCP server cannot push a command to a machine. The in-world agent must
**poll out** to the MCP server, pull any pending commands, execute them, and post
results and signals back. Commands flow *down* inside long-poll responses; results
and events flow *up* via POST. This is a strict client-initiated topology, and the
asymmetry is load-bearing in every decision area.

### Verified FIN capabilities

Citations are tagged with confidence. **VERIFIED** = confirmed against the FIN docs
or source repo; **PROBABLE** = strongly implied but not quoted verbatim;
**SPECULATIVE** = assumption with a documented fallback.

- **VERIFIED** — `InternetCard:request(url, method, body, ...headers)` returns a
  **Future**. Awaiting it (`req:await()`) yields `(statusCode: number, body: string)`.
  Headers are passed as flat trailing string pairs. Source: FIN `lua/examples`
  InternetCard, `lua/Futures`.
- **VERIFIED** — Futures expose `await()`, `poll()`, `get()`, `canGet()`;
  `future.join(...)` runs several in parallel; `future.run()` services scheduled
  futures inside a loop. Source: FIN `lua/Futures`.
- **VERIFIED** — `event.pull(timeout)`: no argument blocks indefinitely; `timeout > 0`
  blocks up to N seconds then returns `nil`; `timeout == 0` returns immediately
  (non-blocking) in the same tick. Returns the tuple `(eventName, sender, ...args)`.
  Source: FIN `lua/api/EventModule`.
- **VERIFIED** — `event.listen(...)`, `event.ignore(...)`, `event.clear()`,
  `event.filter{event=, sender=, values=}` manage which components a context receives
  signals from. Source: FIN `lua/api/EventModule`.
- **VERIFIED** — `component.findComponent(query | class) -> string[]` returns a list
  of component UUIDs; a nick/group query string or a class is accepted.
  `component.proxy(id | ids) -> Object | Object[]` resolves UUIDs to live references.
  Source: FIN `lua/api/ComponentModule`.
- **VERIFIED** — The canonical cooperative loop interleaves signals and futures:
  `while true do local e = event.pull(0); future.run(); if not e then computer.skip() end end`.
  Source: FIN `lua/Events` / `lua/Futures`.
- **PROBABLE** — `computer.millis()` / `computer.time()` give a monotonic-ish clock
  the agent can use for poll cadence and timestamps. The bridge does **not** trust
  agent timestamps for correlation (see Decision 6); it stamps its own `receivedAt`.
- **PROBABLE** — EEPROM/script storage is limited (single Lua source on the computer).
  The agent is therefore kept small; heavy logic lives server-side. Issue #19 owns the
  concrete EEPROM size budget.
- **SPECULATIVE** — Exact InternetCard concurrency (how many in-flight requests per
  card) is not pinned down. The protocol assumes **one in-flight long-poll plus
  short-lived result/event POSTs** per agent. Fallback if only one request can be in
  flight at a time: the agent serialises POSTs between long-poll cycles (see
  Decision 1, "single-flight fallback").

## Decision

The bridge is a **long-poll command channel with an at-most-once execution model**,
JSON over HTTP, addressed by FIN nick/UUID, guarded by a shared token, versioned from
the first byte. Each of the six decision areas follows.

The JSON Schemas live in `schemas/`. Field-level contract documentation and the
rationale for envelope shapes live in
[`schema-rationale.md`](schema-rationale.md) so this ADR stays within the repo's
file-size budget. Every example below validates against those schemas.

### Decision 1 — Topology: agent long-polls the MCP server

The MCP server process hosts an HTTP listener (Kestrel, mounted as its own
`IHostedService` per issue #18). The FIN agent runs one cooperative loop:

1. **boot** → POST `hello` (announce identity, script + protocol version).
2. **long-poll** → POST `poll` with empty `results`/`events`; the server holds the
   request open up to `serverHoldMs` and returns any queued commands.
3. **execute** → run each command against the addressed component.
4. **report** → the *next* `poll` carries `results` for executed commands and any
   buffered `events`. Result reporting and the next command pull share one round trip.

There is exactly one endpoint family (`/fin/v1/hello`, `/fin/v1/poll`). Results and
events ride the `poll` body so a healthy agent needs only one outstanding HTTP
request. This keeps within the **SPECULATIVE** single-in-flight assumption above.

**Poll cadence vs game-tick budget.** FIN Lua is cooperative — a blocked loop blocks
nothing but that computer, but a tight busy-loop wastes its tick budget. The design:

- The agent uses the InternetCard request Future for the long-poll and awaits it. The
  long-poll **server-side** hold (`serverHoldMs`, default **25 s**) does the waiting,
  so the agent is not spinning — it is parked on `req:await()`.
- While a command executes, signals are drained with `event.pull(0)` (non-blocking)
  into the bounded event buffer, so signal handling never blocks command execution.
- Agent-side configurable constants (issue #19 implements, this ADR fixes the names
  and defaults):
  - `pollIntervalMs` (default **250 ms**) — minimum gap between poll cycles when the
    server returns early, to avoid hammering on errors.
  - `maxCommandsPerWake` (default **8**) — cap on commands executed before yielding
    back to drain signals and re-poll, so a flood of commands cannot starve the tick.
  - `serverHoldMs` (default **25 s**) — server long-poll hold; must be < the agent's
    InternetCard request timeout and < any HTTP idle timeout.

**Single-flight fallback** (if InternetCard proves single-in-flight): the agent
finishes its long-poll, then sends a *separate, short* `poll` carrying results/events
between holds. Same envelopes, one extra round trip. No protocol change required.

**Alternatives considered.**

- *Server-push via WebSocket/SSE* — **rejected**: requires the agent to accept or hold
  a server-initiated stream; the InternetCard is request/response outbound only.
- *Server connects to the agent* — **rejected**: impossible, no inbound listener.
- *Short fixed-interval polling (no hold)* — **rejected**: either high latency
  (poll every few seconds) or wasteful request volume (poll every 250 ms forever).
  Long-poll gives low command latency with near-zero idle traffic.

### Decision 2 — Protocol: JSON over HTTP, three envelopes

Three envelope types, each versioned, each correlated by id:

- **Command** (server → agent): `id`, `target`, `operation`, `args`, `issuedAt`,
  `deadlineMs`. Carried inside the `poll` *response*.
- **Result** (agent → server): `id` (echoes the command id), `ok`, `payload` *or*
  `error`. Carried inside the next `poll` *request*.
- **Event** (agent → server): `seq`, `signal`, `source`, `data`, `agentTimestamp`.
  Carried inside the `poll` request; the server adds `receivedAt`.

The wrapper is the **poll request/response** (schemas `poll-request` /
`poll-response`). Both carry `protocolVersion` and agent identity/liveness fields.
See [`schema-rationale.md`](schema-rationale.md) for field semantics.

Why correlate by explicit `id` rather than HTTP request/response pairing: the request
that *delivers* a command is not the request that *returns* its result (results ride
the following poll). Correlation must therefore live in the payload, not the transport.

**Alternatives considered.** Protobuf/MessagePack — **rejected**: Lua-side
ergonomics and debuggability win; FIN's InternetCard speaks string bodies and JSON is
trivially inspectable. Per-command HTTP request from the agent — **rejected**:
defeats the long-poll, multiplies request volume.

### Decision 3 — Addressing: nick-first, UUID-stable, discovery on request

MCP tools name targets the way a player thinks: by **nick** ("the smelter bank
nicknamed `iron-smelters`"). Internally a command's `target` is one of:

- `{ "byNick": "iron-smelters" }` — resolved agent-side via `findComponent`.
- `{ "byId": "<uuid>" }` — a previously discovered stable UUID.
- `{ "byClass": "Build_SmelterMk1_C" }` — class query (read/broadcast operations).

**Discovery** is itself an operation (`discover`) whose result payload lists matching
components with `{ id, nick, class, displayName }`. Tools cache `nick → uuid` from a
discovery and prefer `byId` afterward, because nicks are player-editable and not
guaranteed unique.

**Ambiguity is an error, never a silent pick.** If a `byNick` resolves to more than
one component for a *single-target write*, the agent returns
`error.code = "AMBIGUOUS_TARGET"` with the candidate list in `error.details`. The MCP
tool surfaces the candidates so the operator (or model) disambiguates by UUID. This
rule is owned here and enforced both agent-side (authoritative) and tool-side
(fail-fast before queueing). Broadcast/read operations may intentionally fan out over
all nick matches; those operations set `allowMultiple: true` in `args`.

**Alternatives considered.** UUID-only addressing — **rejected**: unusable for humans
and models, UUIDs are opaque. Nick-only with first-match — **rejected**: violates the
at-most-once safety stance (a duplicate nick would silently mis-target a write).

### Decision 4 — Auth: static shared token, LAN-bound

Every inbound request (including `hello`) must carry the shared token in the
`X-FIN-Token` header. The server compares it with a **constant-time** comparison and
rejects mismatches with HTTP 401 **before any other work** — no queueing, no parsing
of the body for side effects. The token lives in the agent's Lua source (issue #19)
and in the bridge config (issue #18).

The listener **binds to `127.0.0.1` or a LAN address by default**, never `0.0.0.0`
publicly. Operators who expose it beyond the LAN do so explicitly.

**Threat model, stated honestly.** This is a tool for a *single operator's*
Satisfactory server on a trusted LAN. The token stops accidental cross-talk between
agents and casual same-LAN tampering. It is **not** designed to resist a determined
network attacker: the token sits in plaintext Lua, traffic is plain HTTP by default,
and there is no per-command signing. That is an accepted trade-off for a LAN
game-automation tool, not an oversight. TLS and rotating tokens are out of scope for
#17–#21 and noted as future work.

**Alternatives considered.** Per-request HMAC signing — **rejected**: overkill for the
threat model and awkward in EEPROM-constrained Lua. mTLS — **rejected**: certificate
management on an in-game computer is impractical.

### Decision 5 — Failure modes: at-most-once, two distinct timeouts

Commands that mutate the world are **never auto-retried**. A replayed `flip-switch` is
a real-world double-toggle — "pause the smelters" becomes "unpause the smelters." On
any timeout, ambiguity, or transport failure the bridge **surfaces the failure to the
caller and stops**. Retry, if ever wanted, is an explicit new command from the
operator, never automatic.

Mechanics that make at-most-once real:

- **Unique command ids** (server-generated UUID/ULID). The agent keeps a small set of
  **recently-seen command ids**; a command id it has already executed is **acked but
  not re-executed** (idempotent delivery, single execution). This protects against a
  result POST that was lost: the server may re-deliver the *same* command id, and the
  agent recognises and skips it rather than toggling twice.
- **Result-after-timeout discard.** Each command has a server-side
  `TaskCompletionSource` keyed by id with a deadline. If the deadline passes, the TCS
  is completed as a *timeout* and the id is moved to a `tombstoned` set. A result that
  arrives later for a tombstoned id is **recognised and discarded**, never re-applied
  to a caller who has already given up.

**Two timeouts with different safety meaning** (critical for the #24 safety model):

| Outcome | What the caller sees | Safety meaning |
|---|---|---|
| `QUEUED_NOT_PICKED_UP` | Command was enqueued but the agent never pulled it before deadline (agent offline/slow). | **Almost certainly did not execute.** Safe-ish to consider a no-op, but caller must confirm. |
| `DELIVERED_NO_RESULT` | A poll response delivered the command, but no result arrived before deadline. | **May have executed.** Ambiguous — must be treated as possibly-applied. Do **not** reissue blindly. |

The bridge distinguishes these by tracking, per command, whether it was ever included
in a `poll` response. **Liveness fast-fail:** a command enqueued against an agent that
is **not currently alive** (no recent `hello`/`poll` within `agentLivenessMs`, default
**40 s** ≈ 1.5 × `serverHoldMs`) fails immediately with
`error.code = "AGENT_OFFLINE"` and a human-actionable message — *"FIN agent not
responding. Is the FIN computer powered and running the agent script?"* — rather than
hanging until the command deadline.

**Back-pressure.** Both queues are bounded:

- **Command queue** per agent is bounded (`maxQueuedCommands`, default **64**).
  Enqueue beyond the bound fails fast with `error.code = "QUEUE_FULL"` rather than
  growing unbounded. (Commands are not dropped — dropping a mutation silently is
  unsafe; the *enqueue* is rejected instead.)
- **Event buffer** agent-side is a bounded ring (`maxBufferedEvents`, default
  **256**) with **drop-oldest** eviction. On overflow the agent increments
  `droppedEvents` and reports the running count in each `poll` request so the server
  knows telemetry was lost. Dropping *oldest events* under load is acceptable; events
  are observational, not mutations.

**Alternatives considered.** At-least-once with retries — **rejected** outright on the
double-toggle hazard. Unbounded queues — **rejected**: a Lua VM on the game thread
cannot absorb unbounded growth; explicit overflow with a signal beats an OOM.

### Decision 6 — Versioning: protocol version in every exchange

Every `hello` and `poll` request carries `protocolVersion` (integer, **starts at 1**)
and `agentScriptVersion` (semver string the agent reports). The server also advertises
its `protocolVersion` in every `poll` response.

**Mismatch behaviour.** If the agent's `protocolVersion` is one the server cannot
serve, the server refuses with a **typed error** `PROTOCOL_VERSION_MISMATCH` (HTTP
426) whose body names the server's supported range, so the agent can `print()` an
actionable message in-game ("Update the FIN agent script: server speaks protocol vN").
The server never silently downgrades a mutation path on a version it does not fully
understand. EEPROM scripts update on a different cadence than the server, so version
skew is the *normal* case, not an exception — this is why the field is mandatory from
day one rather than added later.

`receivedAt` is always stamped by the **server**, never trusted from the agent, because
the agent clock is a game clock and not authoritative for correlation or ordering of
results. `agentTimestamp` on events is retained as advisory telemetry only.

**Alternatives considered.** Implicit/best-effort versioning — **rejected**: skew is
guaranteed, so silence would mean a new server misinterpreting an old agent's mutation
args. Negotiation handshake with capability flags — **deferred**: a single integer is
enough for #17–#21; capability negotiation can layer on at protocol v2 without breaking
the envelope shape.

## Poll loop sequence

```text
 FIN Lua Agent                              MCP Bridge (Kestrel IHostedService)
     |                                                |
     |  POST /fin/v1/hello                            |
     |  {protocolVersion, agentId,                    |
     |   agentScriptVersion}                          |
     |----------------------------------------------->|  validate token (const-time)
     |                                                |  check protocol version
     |  200 {protocolVersion, sessionAccepted,        |  register agent, mark ALIVE
     |       serverHoldMs, agentLivenessMs}           |
     |<-----------------------------------------------|
     |                                                |
     |  POST /fin/v1/poll                             |
     |  {protocolVersion, agentId,                    |
     |   agentScriptVersion, results:[],              |
     |   events:[], droppedEvents:0}                  |
     |----------------------------------------------->|  hold up to serverHoldMs
     |                                                |  (await queued command)
     |        ...... long-poll held open ......       |
     |                                                |  tool enqueues command C1
     |  200 {commands:[C1], protocolVersion}          |
     |<-----------------------------------------------|  C1 marked DELIVERED, TCS armed
     |                                                |
     |  execute C1 (findComponent/proxy, set state)   |
     |  drain signals via event.pull(0) -> buffer     |
     |                                                |
     |  POST /fin/v1/poll                             |
     |  {results:[R1{id:C1, ok, payload}],            |
     |   events:[E.., E..], droppedEvents:N}          |
     |----------------------------------------------->|  complete TCS(C1) with R1
     |                                                |  (if C1 tombstoned: discard R1)
     |        ...... next long-poll held ......        |  fan events to subscribers
     |  200 {commands:[], protocolVersion}            |
     |<-----------------------------------------------|
```

The second `poll` body above elides `protocolVersion`, `agentId`, and
`agentScriptVersion` for brevity; they are required on **every** poll request — see
`schemas/poll-request.schema.json`.

## Example exchanges

### hello (agent → server) and response

```json
{
  "protocolVersion": 1,
  "agentId": "factory-floor-1",
  "agentScriptVersion": "0.3.1",
  "capabilities": ["discover", "read", "write", "events"]
}
```

```json
{
  "protocolVersion": 1,
  "sessionAccepted": true,
  "serverHoldMs": 25000,
  "agentLivenessMs": 40000
}
```

### poll request (agent → server), reporting one result and two events

```json
{
  "protocolVersion": 1,
  "agentId": "factory-floor-1",
  "agentScriptVersion": "0.3.1",
  "droppedEvents": 0,
  "results": [
    {
      "id": "01J9ZC8Q9K6N7P0R2S4T6V8W0X",
      "ok": true,
      "payload": {
        "before": { "standby": true },
        "after": { "standby": false }
      }
    }
  ],
  "events": [
    {
      "seq": 1841,
      "signal": "ItemTransfer",
      "source": { "id": "9F3A...C21", "nick": "belt-3" },
      "data": { "item": "Desc_IronIngot_C", "count": 1 },
      "agentTimestamp": 183421
    },
    {
      "seq": 1842,
      "signal": "PowerFuseChanged",
      "source": { "id": "1B22...7AE", "nick": "main-fuse" },
      "data": { "triggered": true },
      "agentTimestamp": 183455
    }
  ]
}
```

### poll response (server → agent) delivering one command

```json
{
  "protocolVersion": 1,
  "commands": [
    {
      "id": "01J9ZC8Q9K6N7P0R2S4T6V8W0X",
      "issuedAt": "2026-06-05T18:14:02.500Z",
      "deadlineMs": 8000,
      "target": { "byNick": "iron-smelters" },
      "operation": "setStandby",
      "args": { "standby": false, "allowMultiple": false }
    }
  ]
}
```

### result envelope, error variant (ambiguous nick)

```json
{
  "id": "01J9ZD11M2N3P4Q5R6S7T8U9V0",
  "ok": false,
  "error": {
    "code": "AMBIGUOUS_TARGET",
    "message": "Nick 'iron-smelters' matched 3 components; specify a UUID.",
    "details": {
      "candidates": [
        { "id": "AAAA...001", "nick": "iron-smelters", "class": "Build_SmelterMk1_C" },
        { "id": "BBBB...002", "nick": "iron-smelters", "class": "Build_SmelterMk1_C" },
        { "id": "CCCC...003", "nick": "iron-smelters", "class": "Build_SmelterMk1_C" }
      ]
    }
  }
}
```

### event envelope (standalone shape, as embedded in `poll.events[]`)

```json
{
  "seq": 1843,
  "signal": "ProductionChanged",
  "source": { "id": "DDDD...004", "nick": "constructor-7" },
  "data": { "producing": false },
  "agentTimestamp": 183502
}
```

### protocol-version mismatch (server → agent, HTTP 426)

HTTP error bodies are the bare `errorObject` (`common.schema.json#/$defs/errorObject`) — the same
shape `result.error` carries — with **no** `{ "error": { ... } }` wrapper. The HTTP status conveys
that this is an error; the body is the error object itself. The status is derived from the `code`
(`PROTOCOL_VERSION_MISMATCH` → 426, `UNAUTHORIZED` → 401, `INVALID_ARGS` → 400); see the host's
`FinHttpError` helper, the single place that maps code → status and serializes the body, so endpoints
and the auth middleware cannot drift.

```json
{
  "code": "PROTOCOL_VERSION_MISMATCH",
  "message": "Agent speaks protocol 1; server supports 2-2. Update the FIN agent script.",
  "details": { "agentVersion": 1, "serverSupportedMin": 2, "serverSupportedMax": 2 }
}
```

## Consequences

**Positive.**

- Low command latency with near-zero idle traffic; the long-poll hold does the waiting,
  not an agent busy-loop, respecting the cooperative game-thread budget.
- At-most-once is structural, not best-effort: unique ids + agent dedup set +
  server tombstoning means a lost result or re-delivered command cannot double-toggle a
  switch.
- The `QUEUED_NOT_PICKED_UP` vs `DELIVERED_NO_RESULT` split gives the #24 safety model a
  real signal about whether a mutation *might* have happened.
- Versioning from byte one makes the guaranteed EEPROM/server skew a typed, displayable
  condition instead of silent misbehaviour.
- Bounded queues + drop-oldest events protect the Lua VM from OOM under signal storms.

**Negative / accepted.**

- Result latency is bounded below by one poll round trip (results ride the *next*
  poll). Acceptable: machine-control is not latency-critical and correctness wins.
- Plain-HTTP + plaintext token is not attacker-resistant; accepted for the LAN threat
  model, with TLS/rotation flagged as future work.
- The SPECULATIVE single-in-flight InternetCard assumption could force the single-flight
  fallback; the envelopes are unchanged either way, so the risk is bounded to the
  agent's loop structure (issue #19), not the wire contract.

**Left to downstream issues.**

- #18 — concrete Kestrel binding/config, `TaskCompletionSource` registry, tombstone
  retention window (bounded oldest-first eviction), constant-time token compare
  implementation. The server-side event ring mirrors the agent's drop-oldest design but
  is kept **per agent** (cap applies per agent, with a per-agent dropped count surfaced on
  liveness) so one chatty agent cannot evict another's recent events; `RecentEvents()`
  merges all agents' rings ordered by server-stamped `receivedAt`.
- #19 — agent loop structure, EEPROM size budget, recently-seen-id set sizing, the
  single-flight fallback decision once InternetCard concurrency is measured in-game.
- #20 — the specific `operation` names and `args` shapes per machine-control tool, and
  nick-cache strategy in the tool layer.
- #21 — event throttling/burst-collapse windows at the tool layer (e.g. collapsing 47
  `ItemTransfer` signals in 10 s into one), and MCP notification mapping per client.
- #22 — snapshot/validation of these schemas and every example above.
- #24 — policy use of the two-timeout distinction in the destructive-tool safety model.
