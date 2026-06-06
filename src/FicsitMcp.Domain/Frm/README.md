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
- **Coordinates/rotations are normalized.** FRM emits world centimetres (UE world space, 1 m = 100
  units; verified) at sub-millimetre precision and a rotation already folded to the half-open range
  `[0, 360)` (verified). The zero heading is documented by FRM as "due north" and surfaced as such,
  but the exact world axis north maps to is **assumed**, not re-derived here — treat the heading as a
  stable relative bearing. `FrmLocation` rounds coords to whole cm and rotation to one decimal
  (wrapping a rounded-up `360.0` back to `0.0`); a missing FRM `location` normalizes to a **null**
  `FrmLocation` (distinct from a real origin), and verbose `BoundingBox` / `ColorSlot` / GeoJSON
  `features` are dropped entirely. Full coordinate-system notes live on the `FrmLocation` record.

## Derived anomaly flags

Mobile/logistics entities (trains, drones, vehicles) carry a derived `FrmMobileAnomaly` flags value
so a single summary call reveals trouble without cross-referencing a dump: `Derailed`, `NoFuel`,
`LowFuel`, `NoPath`, `Stuck`, `SelfDrivingError`, `NoStation`. The derivation lives in
`FrmNormalizer`; the exact trigger for each flag, and the false positives/negatives deliberately
tuned out, are spelled out below for tool consumers.

### Anomaly derivation (exact triggers + known blind spots)

A flag is a **transient hint** computed per poll, not a latched state — a momentary trigger clears on
the next read once the underlying condition passes. `Stuck` in particular is recomputed every call.

**Trains** (`/getTrains` → `FrmTrain`):

| Flag | Triggers when | Notes / false-positive window |
|---|---|---|
| `Derailed` | `Derailed` **or** `PendingDerail` is true | Direct FRM flags; pending-collision counts. |
| `SelfDrivingError` | `SelfDriving` present and not `"NoError"` | Empty `SelfDriving` = manual driving, **not** a fault. |
| `NoPath` | `Path` present and not `"NoError"` | Auto-train cannot route to its next stop. |
| `Stuck` | self-driving healthy (`SelfDriving="NoError"`, non-empty) **and** not derailed **and** `\|ForwardSpeed\| ≤ 0.1 km/h` **and** docking is a not-docking state | **FP window:** a train legitimately crawling at 0 – 0.1 km/h through a junction reads as stationary and can flag Stuck for that poll (see `StationarySpeedKmh`). A train mid-dock (`TDS_Docking`, `TDS_WaitForTransfer`, …) is correctly **not** stuck — only the exact `TDS_None` sentinel counts as not-docking. **FN:** a manually-driven train (empty `SelfDriving`) is never evaluated for Stuck. |

**Vehicles** (`/getVehicles` → `FrmVehicle`):

| Flag | Triggers when | Notes / false-positive window |
|---|---|---|
| `NoFuel` | `HasFuel` is false | Hard failure: stranded. |
| `LowFuel` | `HasFuel` true **and** `Autopilot` true **and** `HasFuelForRoundtrip` false | Soft "will strand" warning; only for autopilot vehicles. |
| `NoPath` | `Autopilot` true **and** `FollowingPath` false | Autopilot on but off its path. |
| `Stuck` | `Autopilot` true **and** `FollowingPath` true **and** `HasFuel` true **and** `\|ForwardSpeed\| ≤ 0.1 km/h` | **Intentional FN:** a manually-driven idle vehicle (`Autopilot=false`, even with an AFK driver) is **never** flagged Stuck — a parked manual vehicle is a normal player state, not an automation fault. The Autopilot gate enforces this. The accepted blind spot is a genuinely stuck manual vehicle, which FRM cannot distinguish from a deliberately parked one. **FP window:** same 0 – 0.1 km/h crawl window as trains. |

**Drones** (`/getDrone` → `FrmDrone`):

| Flag | Triggers when | Notes |
|---|---|---|
| `NoStation` | `HasPairedStation` is false | Drone has no paired port → nowhere to fly. Drones have no fuel/path concept in FRM, so those flags never apply. |

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
