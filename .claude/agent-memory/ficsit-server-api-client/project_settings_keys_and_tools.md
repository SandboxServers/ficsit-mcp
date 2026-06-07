---
name: project-settings-keys-and-tools
description: Discovered FG.* key set for server options / advanced game settings, the typed-properties+passthrough mapping pattern, and the #8 settings/create-new-game tools and their hints
metadata:
  type: project
---

Issue #8 (server options + Advanced Game Settings + create_new_game tools). Branch
`feat/8-server-options-ags`. No sibling #6/#7 tools had landed when #8 was built, so #8 set the
dedicated-server tool conventions. No #22 schema-snapshot harness existed yet (snapshot files in repo
are all game-data, not MCP-tool schemas) — nothing to register tools into.

**Verified FG.* key set** (cross-confirmed 2026-06-06 against community OAS spec
satisfactory-oas.github.io/spec AND the idebeijer Go client struct `AdvancedGameSettings`/`ServerOptions`).
Both maps are `map<string,string>` per the official docs — every value is stringified, booleans come as
`"True"/"False"` (the server's own convention). Live in
`src/FicsitMcp.Domain/DedicatedServer/Settings/SettingKeys.cs` (internal consts).

- Server options (`FG.*`): `FG.DSAutoPause` (bool), `FG.DSAutoSaveOnDisconnect` (bool),
  `FG.AutosaveInterval` (int seconds), `FG.DisableSeasonalEvents` (bool),
  `FG.ServerRestartTimeSlot` (int, minutes past midnight; -1 = none), `FG.SendGameplayData` (bool),
  `FG.NetworkQuality` (int 0=Low..3=Ultra).
- AGS (`FG.GameRules.*` / `FG.PlayerRules.*`): GameRules = `NoPower` (bool), `StartingTier` (int),
  `DisableArachnidCreatures` (bool), `NoUnlockCost` (bool), `SetGamePhase` (int),
  `UnlockAllResearchSchematics` (bool), `UnlockInstantAltRecipes` (bool),
  `UnlockAllResourceSinkSchematics` (bool); PlayerRules = `NoBuildCost` (bool), `GodMode` (bool),
  `FlightMode` (bool).

**Typed-properties + passthrough mapping pattern** (`Settings/` folder, zero MCP refs):
- Typed records (`ServerOptions`, `AdvancedGameSettings`) have NULLABLE typed props (null = "unspecified
  / leave unchanged") + an `IReadOnlyDictionary<string,string>? Passthrough` for unmodeled keys.
- `IServerSettingsMapper`/`ServerSettingsMapper` (stateless singleton) does both-way mapping. READ:
  parse leniently (`True/False/true/false/1/0`), invariant-culture ints; a present-but-UNPARSEABLE
  modeled value is preserved into passthrough, never dropped; every non-modeled key → passthrough
  verbatim. WRITE: emit only non-null props (`"True"/"False"`, invariant ints) then `TryAdd` passthrough
  entries so a MODELED key WINS over a passthrough entry of the same key.
- Pending-vs-applied: `ServerOptionsView(Applied, Pending, HasPendingChanges)`. AGS has no pending map
  (applies immediately) → single `AdvancedGameSettingsView(CreativeModeEnabled, Settings)`.
- `NewGameSettings` → `ToCreateNewGameRequest`: an all-null/empty AGS flattens to NULL (not empty map)
  so a vanilla new game stays achievement-eligible; non-empty AGS flags the save.

**Tool hints decided** (`src/FicsitMcp/Tools/ServerSettingsTool.cs`, static methods, parameter-injected
`IDedicatedServerApiClient` + `IServerSettingsMapper`, matching GameDataTool's parameter-injection style;
async tools take an auto-injected `CancellationToken`):
- `get_server_options` / `get_advanced_game_settings`: ReadOnly=true, Idempotent=true, OpenWorld=false.
- `set_server_options` / `set_advanced_game_settings`: ReadOnly=false, Idempotent=true, OpenWorld=true,
  Destructive=false (the issue explicitly says false is defensible for settings; reversible by
  reapplying, no data loss / no player disconnect). The AGS permanent-flag/achievements-off consequence
  is surfaced as the FIRST sentence of the description, not via the Destructive hint. No safety-auditor
  ruling existed yet — revisit if #24 rules otherwise.
- `create_new_game`: Destructive=true, ReadOnly=false, Idempotent=false, OpenWorld=true. Description
  states up front it replaces the running session and disconnects every connected player.
- Both `set_*` tools re-read after applying and return the fresh view, satisfying the issue's
  "set then get reflects the change" acceptance criterion in one call.

**Tests:** `tests/FicsitMcp.Tests/DedicatedServer/` — `ServerSettingsMapperTests` (both-way mapping,
passthrough round-trip, lenient bool, unparseable→passthrough, pending/applied, create-new-game
validation) + `ServerSettingsToolTests` (delegation, set-then-get round-trip, AGS flag flip,
create_new_game validation). New fake `FakeDedicatedServerApiClient` implements the WHOLE interface;
only the four settings/create methods do real work (mutable in-memory store so round-trip is
observable), the rest throw NotSupportedException. This is the FIRST interface-level fake of
`IDedicatedServerApiClient` (prior #5 tests used an HTTP-level RecordingHandler) — reuse it for #6/#7/#9
tool tests. 29 new tests; full suite 238 green.

See [[project-api-surface]] (the GetServerOptions/ApplyServerOptions/AGS wire records already existed
from #5: `ServerOptionsRecords.cs` carries `serverOptions`/`pendingServerOptions` and
`creativeModeEnabled`/`advancedGameSettings`; `CreateNewGameRequest`/`NewGameData` with the
`bSkipOnboarding` quirk are in `SaveGameRecords.cs`). [[project-client-layout]].
