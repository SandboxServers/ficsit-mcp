---
name: frm-version-pin
description: Pinned FRM mod version and source-of-truth for JSON shapes (verified 2026-06-06)
metadata:
  type: reference
---

# FRM supported version pin

- **Pinned mod version:** `1.4.10-437` (latest GitHub release as of 2026-03-05; verified 2026-06-06).
- **Repo:** https://github.com/porisius/FicsitRemoteMonitoring — default branch `main` (NOT `master`; `master` 404s on the API).
- **Default web server port:** 8080. Endpoints are GET at `http://host:8080/<endpoint>`.
- **docs.ficsit.app blocks WebFetch (HTTP 403).** The authoritative source for exact JSON field names is the C++ source, not the docs site. Read shapes from:
  - Endpoint route table: `Source/FicsitRemoteMonitoring/Private/FicsitRemoteMonitoring.cpp` (search `RegisterEndpoint(FAPIEndpoint(`). This is the complete, canonical endpoint registry.
  - Shared JSON helpers: `Source/FicsitRemoteMonitoring/Private/RemoteMonitoringLibrary.cpp` (`getActorJSON`, `CreateBuildableBaseJsonObject`, `getActorFeaturesJSON`, `getPowerConsumptionJSON`, `GetItemValueObject`).
  - Per-area builders under `Source/FicsitRemoteMonitoring/Private/Endpoints/{Factory,Travel,World}/*.cpp`.

## Fetch recipe (gh API, raw content)
```
base="https://raw.githubusercontent.com/porisius/FicsitRemoteMonitoring/main"
curl -fsSL "$base/<path>"
gh api repos/porisius/FicsitRemoteMonitoring/git/trees/main?recursive=1 --jq '.tree[].path'
```

## Casing quirk (CONFIRMED from source)
FRM JSON casing is INCONSISTENT and must be matched per-field with explicit `[JsonPropertyName]`:
- Most object fields are PascalCase: `Name`, `ClassName`, `CircuitGroupID`, `PowerConsumed`, `Derailed`.
- BUT nested coordinate/geometry sub-objects are lowercase: `location` -> `{ "x","y","z","rotation" }`, `features` -> `{ "properties", "geometry" }`, `production`/`ingredients` arrays on getFactory are lowercase.
- Never assume a global naming policy. STJ `PropertyNameCaseInsensitive` does NOT save you because PascalCase `Name` vs lowercase `location` differ in the FIRST letter — case-insensitive matching handles that, but be explicit anyway for clarity and to survive a future casing flip.

## location shape (getActorJSON)
`{ "x": <cm double>, "y": <cm double>, "z": <cm double>, "rotation": <0<=deg<360, 0=due north> }`
Coordinates are world centimetres as doubles (raw uses `long double`); rotation is normalized to 0..360 with 0 = due north (FRM already does the +450 fmod 360 conversion). Normalize to compact form in our DTO: round coords to whole cm (int-ish), rotation to 1 decimal.
