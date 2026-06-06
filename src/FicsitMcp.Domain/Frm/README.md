# FRM observe surface (`IFrmClient`)

This folder is the typed client over the **Ficsit Remote Monitoring (FRM)** mod web server — the
live-world *observe* surface. FRM runs an HTTP/JSON server inside the running game process
(started in-game with `/frmweb start`, or via autostart config; default port `8080`) and exposes
world state as GET endpoints. `IFrmClient` is the **one place** FRM's JSON shapes are known; MCP
tools depend on it and never touch raw FRM HTTP or raw JSON.

## Supported mod version

Designed against **FRM `1.4.10-437`** (latest GitHub release, published 2026-03-05; shapes verified
from source on 2026-06-06). FRM drifts between game updates — fields appear and disappear — so the
client is built to **survive change**, not assume a frozen schema (see "Drift tolerance" below).

The authoritative source for exact JSON field names is the mod's C++ source, **not** the docs site
(`docs.ficsit.app` blocks automated fetches). The endpoint route table lives in
`Source/FicsitRemoteMonitoring/Private/FicsitRemoteMonitoring.cpp`
(`RegisterEndpoint(FAPIEndpoint(...))`); the shared JSON helpers live in
`RemoteMonitoringLibrary.cpp`. Repo: <https://github.com/porisius/FicsitRemoteMonitoring> (default
branch `main`).

## What ships here

| File | Role |
|---|---|
| `IFrmClient` / `FrmClient` | Interface + implementation; one method per consumed endpoint. |
| `Model/Frm*.cs` | Compact **normalized** records the model consumes. |
| `Model/Raw/Raw*.cs` | **Raw** DTOs matching FRM JSON exactly (unknown-field-tolerant). |
| `FrmJsonContext` | Source-generated `JsonSerializerContext` (AOT-friendly, reflection-free). |
| `FrmNormalizer` | Pure raw → normalized mapping + anomaly derivation. |
| `FrmUnreachableException` | Actionable error when the mod is absent / web server not started. |

## Endpoints consumed (v1.4.10-437)

| Endpoint | Method | Normalized record |
|---|---|---|
| `/getProdStats` | `GetProdStatsAsync` | `FrmProdStatsItem` |
| `/getFactory` | `GetFactoryAsync` | `FrmFactoryBuilding` |
| `/getPower` | `GetPowerAsync` | `FrmPowerCircuit` |
| `/getTrains` | `GetTrainsAsync` | `FrmTrain` |
| `/getDrone` | `GetDronesAsync` | `FrmDrone` |
| `/getVehicles` | `GetVehiclesAsync` | `FrmVehicle` |
| `/getPlayer` | `GetPlayersAsync` | `FrmPlayer` |
| `/getResourceNode` | `GetResourceNodesAsync` | `FrmResourceNode` |

Every FRM read endpoint returns a **JSON array** at the top level. ~80 GET endpoints exist; only the
ones a filed tool issue (#12–#14) needs are implemented here. The full inventory (including the
deliberately-skipped endpoints and the POST/write endpoints that land with #13, e.g. `setSwitches`)
is recorded in the `frm-observe-surface` agent memory.

> **`/getFactory` is the heavy one.** A large save returns thousands of buildings. Consuming tools
> MUST filter (by area, class, or id) and cap/paginate — never hand the whole array to the model.

## Drift tolerance (why this survives game patches)

- **Unknown fields are ignored.** Raw DTOs declare only the fields the normalizer consumes;
  System.Text.Json drops unmatched JSON members, so a mod update that *adds* fields cannot break
  parsing. We only depend on the handful of fields each record needs.
- **Case-insensitive + string-number tolerant.** FRM's casing is inconsistent between endpoints
  (PascalCase object fields, but lowercase `location`/`features`/`production`). `FrmJsonContext`
  enables `PropertyNameCaseInsensitive` and `AllowReadingFromString` as defence in depth.
- **Coordinates/rotations are normalized.** FRM emits world centimetres at sub-millimetre precision
  and a rotation already converted to 0–360° (0 = due north). `FrmLocation` rounds coords to whole
  cm and rotation to one decimal; verbose `BoundingBox` / `ColorSlot` / GeoJSON `features` are
  dropped entirely.

## Derived anomaly flags

Mobile/logistics entities (trains, drones, vehicles) carry a derived `FrmMobileAnomaly` flags value
so a single summary call reveals trouble without cross-referencing a dump: `Derailed`, `NoFuel`,
`LowFuel`, `NoPath`, `Stuck`, `SelfDrivingError`, `NoStation`. The derivation (and the false
positives tuned out — e.g. a manually-driven idle vehicle is *not* "stuck", a train parked at a
station mid-timetable is *not* "stuck") lives in `FrmNormalizer`.

## Graceful degradation

When FRM is unreachable — connection refused, timeout, a 404 on a known endpoint (wrong server on
the port / version mismatch), or a non-JSON 2xx body (something other than FRM answering) — the
client logs the degradation with structured fields (`surface`, `endpoint`, `reason`) and throws
`FrmUnreachableException` naming the base URL and the in-game fix
(*"Run `/frmweb start` in-game, or enable autostart"*). No stack trace reaches the model. FRM being
unavailable **never** affects the HTTPS-API surfaces — the FRM surface is independently optional.

## Transport modes

`FrmOptions.TransportMode` models `Direct` (hit the mod's port directly, the common case) and
`DedicatedApiPassthrough` (route through the dedicated-server HTTPS API). Only **Direct** is wired
today; the passthrough path is a future enhancement and does not change `IFrmClient`'s contract.
