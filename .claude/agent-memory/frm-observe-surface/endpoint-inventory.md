---
name: endpoint-inventory
description: FRM endpoint -> DTO -> planned tool mapping for v1.4.10-437, and what #11 implemented vs skipped
metadata:
  type: project
---

# FRM endpoint inventory (v1.4.10-437)

Full GET route table lives in `FicsitRemoteMonitoring.cpp`. ~80 GET endpoints + 5 POST (write) endpoints exist. Issue #11 implements only the endpoints that filed tool issues #12-#14 consume.

**Why:** #11 owns `IFrmClient`+DTOs only. Tools live in #12-#14. The cardinal rule (never dump raw payloads) means the client returns typed lists but the TOOLS aggregate/filter — that gating is #12-#14's job, but the client must expose enough to summarize.

**How to apply:** When a new tool issue needs an endpoint not yet implemented, add the DTO+method following the same patterns; do not pre-build DTOs for unconsumed endpoints.

## Implemented in #11 (8 read endpoints)
| Endpoint | DTO record | Planned tool issue |
|---|---|---|
| `/getProdStats` | `FrmProdStatsItem` | #12 production summary |
| `/getFactory` | `FrmFactoryBuilding` | #12 per-building production detail |
| `/getPower` | `FrmPowerCircuit` | #12/#15 power summary |
| `/getTrains` | `FrmTrain` (+anomaly flags) | #14 logistics/trains |
| `/getDrone` | `FrmDrone` (+anomaly flags) | #14 logistics/drones |
| `/getVehicles` | `FrmVehicle` (+anomaly flags) | #14 logistics/vehicles |
| `/getPlayer` | `FrmPlayer` | #14 players |
| `/getResourceNode` | `FrmResourceNode` | #14 resources |

## Write endpoints (POST, RequiresAuthentication) — NOT in #11, land with #13
- `setSwitches` (UPower::setSwitches) — circuit switch toggle + priority. **This is the `set_switch`/circuit-priority write surface.** Request shape: array of `{ "ID": <switch ID>, ... }`; response includes `ID`, `Name`, `Status` (bool IsSwitchOn), `Priority`. Auth required.
- `setEnabled`, `sendChatMessage`, `createPing`, `setModSetting` — out of scope entirely.

## Deliberately SKIPPED reads (inventory only) and why
- Per-machine-type factory aliases (`getConstructor`, `getAssembler`, `getSmelter`, `getFoundry`, `getRefinery`, `getManufacturer`, `getBlender`, `getPackager`, `getParticle`, `getConverter`, `getEncoder`, `getFoundry`): all return the SAME shape as `/getFactory` filtered by class. #12 can filter `/getFactory` client-side; no need for 11 DTOs. Revisit only if a tool needs server-side filtering for payload size.
- `getGenerators` (rich generator DTO ~30 fields): power PRODUCTION detail. `/getPower` circuit rollup is enough for #12's power summary. Add if #15 bottleneck analysis needs per-generator fuel state.
- Logistics infrastructure: `getBelts`, `getLifts`, `getPipes`, `getPipeJunctions`, `getSplitterMerger`, `getThroughputCounter`, `getHypertube*`, `getTrainRails`, `getTrainStation`, `getTrainSignals`, `getDroneStation`, `getTruckStation`, `getVehiclePaths` — huge arrays, not needed by the named tools. `getThroughputCounter` is a likely #15 input later.
- World/misc: `getSinkList`, `getResourceSink`, `getExplorationSink`, `getResourceSinkBuilding` (AWESOME sink — only `getResourceSink` gives the coupon/points summary; skipped because no filed tool needs it yet), `getStorageInv`/`getWorldInv`/`getCloudInv`/`getCrateInv` (inventory), `getSchematics`/`getRecipes`/`getResearchTrees`/`getResearchTrees` (research), `getSessionInfo`, `getModList`, `getMapMarkers`, `getChatMessages`, `getSwitches` (read side of switches — implement with #13 alongside setSwitches), creatures/doggo/slugs/artifacts/tapes/giftbundles.
- `getResourceWell`, `getResourceGeyser`, `getResourceDeposit`, `getFrackingActivator`, `getExtractor` — resource extraction detail; `/getResourceNode` covers node discovery for #14.

## getSinkList note
`/getSinkList` is `.RequiresGameThread()` and returns per-item sink points (`Name`,`ClassName`,`Points`,`PointsOverride`). The AWESOME-sink COUPON progress is a separate endpoint `/getResourceSink`. Issue body said "getSinkList" — but no #12-#14 tool consumes it, so it is inventory-only here. Flagged for whoever files the sink tool.
