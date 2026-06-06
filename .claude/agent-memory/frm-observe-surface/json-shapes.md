---
name: json-shapes
description: Exact FRM JSON field shapes (v1.4.10-437) for the 8 implemented endpoints + anomaly heuristics
metadata:
  type: reference
---

# FRM JSON field shapes (v1.4.10-437, from C++ source)

All endpoints return a JSON ARRAY at the top level (the route builders push into `OutJsonArray`). One object per entity. Shapes below are the exact `Values.Add("Key", ...)` keys.

## Shared sub-shapes
- `location` (getActorJSON): `{ x, y, z, rotation }` — doubles, world cm; rotation 0..360, 0=north.
- `PowerInfo` (getPowerConsumptionJSON): `{ CircuitGroupID:int, CircuitID:int, FuseTriggered:bool, PowerConsumed:float(MW), MaxPowerConsumed:float(MW) }`. Defaults to -1 IDs / 0 power when no circuit.
- inventory item (GetItemValueObject): `{ Name, ClassName, Amount, MaxAmount }`. Amount already form-converted (fluids in m^3).
- `features` (getActorFeaturesJSON, GeoJSON-ish): `{ properties:{name,type}, geometry:{coordinates:{x,y,z}, type:"Point"} }`. DROP this in our DTOs — redundant with `location`, pure map-overlay payload bloat.

## /getProdStats -> FrmProdStatsItem
`{ Name, ClassName, ProdPerMin:string("P: 30/ min - C: 12/ min"), ProdPercent:double, ConsPercent:double, CurrentProd:double, MaxProd:double, CurrentConsumed:double, MaxConsumed:double, Type:string(form) }`
- ProdPerMin is a DISPLAY STRING, not numeric. The numeric truth is CurrentProd/MaxProd/CurrentConsumed/MaxConsumed. Parse those, ignore ProdPerMin.

## /getFactory -> FrmFactoryBuilding (HEAVY — thousands of objects)
Base (CreateBuildableBaseJsonObject): `{ ID:string, Name, ClassName, location, BoundingBox, ColorSlot }` then:
`{ Recipe:string, RecipeClassName:string, production:[{Name,ClassName,Amount,CurrentProd,MaxProd,ProdPercent}], ingredients:[{Name,ClassName,Amount,CurrentConsumed,MaxConsumed,ConsPercent}], InputInventory:[item], OutputInventory:[item], Productivity:double(0-100), ManuSpeed:double(clock%), Somersloops:int, PowerShards:int, IsConfigured:bool, IsProducing:bool, IsPaused:bool, PowerInfo, features }`
- Drop BoundingBox, ColorSlot, features, raw inventories in DTO (keep production/ingredients rates). This is the cap/paginate endpoint — tools MUST filter.

## /getPower -> FrmPowerCircuit
`{ CircuitGroupID:int, PowerProduction:double(MW), PowerConsumed:double(MW), PowerCapacity:double(MW), PowerMaxConsumed:double(MW), BatteryInput:double, BatteryOutput:double, BatteryDifferential:double, BatteryPercent:double(0-100), BatteryCapacity:double, BatteryTimeEmpty:string(HH:MM:SS), BatteryTimeFull:string, AssociatedCircuits:[int], FuseTriggered:bool }`
- Note: top-level getPower uses `CircuitGroupID` (NOT `CircuitID`); the per-building PowerInfo uses both. BatteryTime* are display strings.

## /getTrains -> FrmTrain  (anomaly source)
`{ Name, ClassName, location, TotalMass, PayloadMass, MaxPayloadMass, ForwardSpeed:double(km/h, raw*0.036), ThrottlePercent:double, TrainStation:string(current/target station), Derailed:bool, PendingDerail:bool, Status:string(form), TimeTable:[{StationName,...}], TimeTableIndex:int, SelfDriving:string(ESelfDrivingLocomotiveError name), Docking:string(ETrainDockingState), Path:string(EPathDiagnosticsError), Vehicles:[railcar], features, PowerInfo }`
- SelfDriving="NoError" is healthy; anything else = self-driving fault. Path != "NoError"/"" = no path. Derailed/PendingDerail explicit. Idle = SelfDriving fine + ForwardSpeed~0 + not docking.

## /getDrone -> FrmDrone  (anomaly source)
`{ Name, ClassName, HomeStation:string, PairedStation:string, HasPairedStation:bool, CurrentDestination:string, FlyingSpeed:double(km/h), MaxSpeed:double(km/h), CurrentFlyingMode:string(form), location, features }`
- No fuel concept for drones (stations hold batteries). Anomaly: !HasPairedStation = no route; CurrentFlyingMode tells flying/docked state. Stuck heuristic limited — flag NoPairedStation only, note the rest.

## /getVehicles -> FrmVehicle  (anomaly source, richest)
`{ Name, ClassName, location, PathName:string, Status:string(form), Driver:string(player or ""), CurrentGear:int, ForwardSpeed:double(km/h), EngineRPM:double, ThrottlePercent:double, Airborne:bool, FollowingPath:bool, FollowingPath, Autopilot:bool, HasFuel:bool, HasFuelForRoundtrip:bool, TotalFuelEnergy:double, MaxFuelEnergy:double, Inventory:[item], FuelInventory:[item], features }`
- Anomaly: !HasFuel = out of fuel; HasFuel && !HasFuelForRoundtrip = low fuel; Autopilot && !FollowingPath = lost path; Autopilot && FollowingPath && ForwardSpeed~0 && HasFuel = stuck/idle.

## /getPlayer -> FrmPlayer
`{ Name, ClassName, location, Speed:double(km/h), Online:bool, PlayerHP:double, Dead:bool, Inventory:[item], features }`

## /getResourceNode -> FrmResourceNode (getResourceNodeJSON in RemoteMonitoringLibrary)
`{ Name, ClassName, Purity:string(Pure/Normal/Impure), EnumPurity:string(raw enum), ResourceForm:string, NodeType:string, Exploited:bool, location, features }`
- `.RequiresGameThread()` — heavier call. Exploited = node already has an extractor on it.

## Stuck heuristic gotcha (train)
FRM's `Docking` field serializes the full enum path, e.g. `"ETrainDockingState::TDS_None"`. The
not-docking sentinel ENDS WITH `None`. The normalizer's `IsIdleDocking` checks `EndsWith("None")`,
so a train mid-dock (`TDS_Docking`/`TDS_WaitForTransfer`) is correctly NOT flagged stuck. A
self-driving train healthy state is `SelfDriving="NoError"` (NOT empty); empty SelfDriving would mean
manual driving. Keep both conditions when tuning.

## Anomaly enum design
Surface a `Anomalies` flags/list per mobile entity so a SUMMARY reveals trouble without a raw dump. Tuned-out false positives to remember: a parked/idle vehicle with a driver (Driver != "") is NOT an anomaly (player is manually driving); only flag idle when Autopilot is on. A train with Status="Parked" at its station mid-timetable is normal, not stuck.
