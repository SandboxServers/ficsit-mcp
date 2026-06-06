# FRM fixtures

These JSON files back the `IFrmClient` deserialization tests — one per consumed FRM endpoint.

## Provenance and honesty

- **Mod version reflected:** FRM **`1.4.10-437`** (latest release as of 2026-03-05).
- **These are doc/source-derived, NOT live captures.** Each fixture was hand-built to match the
  exact field names and value shapes emitted by the FRM C++ source
  (`Source/FicsitRemoteMonitoring/Private/...`, `RegisterEndpoint` route table +
  `RemoteMonitoringLibrary.cpp` shared helpers) at tag `1.4.10-437`. **No fixture here was captured
  from a running Satisfactory instance** — the author cannot run the game.
- They are intentionally small (1–3 entities each) but structurally faithful: real key casing
  (PascalCase fields, lowercase `location`), real nested sub-objects, and **deliberately include an
  extra unknown field** in some elements to prove unknown-field tolerance.

## Capture procedure (to replace these with real captures)

The recapture pipeline is owned by issue #22. To refresh a fixture from a live instance:

1. Install FRM in a running game/dedicated server and start its web server (`/frmweb start`).
2. `curl http://localhost:8080/<endpoint> -o <endpoint>.json` (e.g. `getPower.json`).
3. Trim to a handful of representative entities (keep at least one with an anomaly: a derailed
   train, an out-of-fuel vehicle, an unexploited resource node) so the tests stay fast and the
   anomaly heuristics keep coverage.
4. **Record the exact FRM/game version** captured (mod version + game build id) here in this README
   alongside the file, and update the version line above. Fixtures are only meaningful with their
   version tag.

## Files

| File | Endpoint | Notes |
|---|---|---|
| `getProdStats.json` | `/getProdStats` | Production/consumption rate lines. |
| `getFactory.json` | `/getFactory` | Two machines: one producing, one paused/unconfigured. |
| `getPower.json` | `/getPower` | Two circuits: one healthy, one with `FuseTriggered`. |
| `getTrains.json` | `/getTrains` | Healthy auto-train + a derailed train (anomaly). |
| `getDrone.json` | `/getDrone` | Paired drone + an unpaired drone (`NoStation`). |
| `getVehicles.json` | `/getVehicles` | Truck on autopilot + an out-of-fuel truck (`NoFuel`). |
| `getPlayer.json` | `/getPlayer` | One online player. |
| `getResourceNode.json` | `/getResourceNode` | Exploited + unexploited node. |
| `not-frm-200.html` | (negative) | A non-JSON 200 body (mod absent, other server on the port). |
