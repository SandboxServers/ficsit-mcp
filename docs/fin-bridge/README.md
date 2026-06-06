# FIN Bridge

The **FIN bridge** connects the `ficsit-mcp` server to programmable in-game computers
running [FicsIt-Networks](https://docs.ficsit.app/ficsit-networks/latest/) (FIN) Lua. It
lets MCP tools read and write live machine state (standby, clock, inventory, recipe,
potential) and receive in-world signals as events.

## The asymmetry that shapes everything

FIN computers **cannot accept inbound connections** — their InternetCard makes only
*outbound* HTTP requests. So the MCP server cannot push to a machine. Instead:

- The in-world Lua agent **long-polls** the MCP server.
- **Commands flow down** inside the long-poll response.
- **Results and events flow up** via the next POST.

This client-initiated, poll-out topology is the foundation of every design decision.

## Design stance in one breath

- **At-most-once execution.** Commands are never auto-retried — a replayed `flip-switch`
  is a real-world double-toggle. Unique ids + agent dedup + server tombstoning enforce it.
- **Fail fast when offline.** A command for a dead agent returns `AGENT_OFFLINE` with a
  human remedy, not a hang.
- **Two distinct timeouts.** "Queued but never picked up" (probably didn't run) vs
  "delivered, no result" (may have run) carry different safety meaning.
- **Bounded everything.** Bounded command queue (reject on full) and bounded event ring
  (drop-oldest with a reported count) protect the cooperative game-thread Lua VM.
- **Versioned from byte one.** EEPROM/server skew is the normal case; mismatch is a typed,
  displayable error.
- **LAN-bound, shared-token.** Honest threat model: a single-operator LAN tool, not an
  internet-facing service.

## Documents

- [`adr-001-fin-bridge-protocol.md`](adr-001-fin-bridge-protocol.md) — the architecture
  decision record: topology, protocol, addressing, auth, failure modes, versioning, with
  alternatives, a poll-loop sequence diagram, FIN-capability citations, and worked JSON
  examples for every envelope.
- [`schema-rationale.md`](schema-rationale.md) — field-by-field intent behind each
  envelope; the *why* the schemas formalize.
- [`schemas/`](schemas/) — JSON Schema (draft 2020-12), the normative wire contract:
  - `common.schema.json` — shared `$defs` (version, ids, target, componentRef, errors).
  - `command.schema.json` — command envelope (server → agent).
  - `result.schema.json` — result envelope (agent → server).
  - `event.schema.json` — event envelope (agent → server).
  - `hello.schema.json` — boot handshake request/response.
  - `poll-request.schema.json` / `poll-response.schema.json` — the `/fin/v1/poll`
    wrappers carrying batches of the above plus version and liveness fields.

## Who implements what

This issue (**#17**) is design-only. Downstream issues implement against these contracts.

| Part | Issue | Owns |
|---|---|---|
| Bridge host | **#18** | Kestrel listener as its own `IHostedService`, command queue, `TaskCompletionSource` registry, tombstoning, liveness tracking, constant-time token compare, binding/config. |
| Lua agent | **#19** | The in-world agent script: poll loop, `findComponent`/`proxy` execution, recently-seen-id dedup set, bounded event ring, agent-side timing constants, single-flight fallback. |
| Machine-control tools | **#20** | The `[McpServerTool]` surface and the concrete `operation`/`args`/`payload` shapes (discover, read state/clock/inventory/recipe, write standby/potential/recipe, execute), nick caching. |
| Event notifications | **#21** | Forwarding events as MCP notifications, tool-layer throttling and burst-collapse, per-client notification behaviour. |
| Test harness | **#22** | Snapshot/validation of these schemas and every ADR example. |
| Safety model | **#24** | Uses the two-timeout distinction and destructive-tool hints in the safety policy. |

## Endpoints (defined here, hosted by #18)

- `POST /fin/v1/hello` — boot handshake; version gate.
- `POST /fin/v1/poll` — long-poll: results/events up, commands down.

Both require the shared token in the `X-FIN-Token` header and are rejected with HTTP 401
before any work if it is missing or wrong.
