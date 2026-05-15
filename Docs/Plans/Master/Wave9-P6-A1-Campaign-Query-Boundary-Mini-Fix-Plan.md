# Wave 9 P6-A1 Campaign Query Boundary Mini-Fix Plan

## Status

- Target: `P6-A1` Track B mini-fix, between `P6-A` and `P6-B`.
- State: planned, not implemented.
- Owner: Track B.
- Blocks: `P6-B` assembly/rally kickoff.

## Why this exists

`P6-A` added persistent runtime campaign/army entities and a `SimulationRuntime.Campaigns` query seam. The current seam copies the campaign list, but the list items are live `CampaignState` / `ArmyState` runtime objects. That is acceptable for a short-lived runtime-internal test seam, but it is too easy for later P6-B/P6-C/P6-D work to accidentally treat it as a stable snapshot boundary.

This mini-fix closes that gap before P6-B mutates campaign state.

## Goal

Make campaign querying return immutable, detached runtime snapshots instead of live runtime entity references.

The runtime may keep mutable `CampaignState` / `ArmyState` internally, but consumers of `SimulationRuntime.Campaigns` must not receive live state objects or mutable nested state references.

## Non-goals

- No actor assignment or rally behavior.
- No march/pathfinding/encounter behavior.
- No `World.Update` campaign progression.
- No organic supply/carrier/forage ticking.
- No ScenarioRunner artifact export.
- No Graphics/UI consume.
- No P6-D render snapshot/read-model work.
- No Track C campaign AI behavior.

## In-scope files

- Modify: `WorldSim.Runtime/SimulationRuntime.cs`
- Modify/add: `WorldSim.Runtime/Simulation/Military/*Snapshot*.cs` or equivalent runtime snapshot DTO file
- Modify: `WorldSim.Runtime.Tests/Wave9CampaignRuntimeTests.cs`
- Update after green implementation: `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- Update after green implementation: `ops/PROJECT_STATE.md`

## Proposed contract

Keep internal runtime state:

- `_campaigns: List<CampaignState>` remains the authoritative mutable runtime registry.
- P6-B/P6-C should mutate campaign state through explicit runtime methods, not by consuming the public query seam.

Change public query seam:

- `SimulationRuntime.Campaigns` should return `IReadOnlyList<CampaignRuntimeSnapshot>` or similarly named immutable DTOs.
- It must not return `CampaignState`, `ArmyState`, `ArmySupplyState`, `ArmyRationPoolState`, `ArmySupplyCarrierState`, or `ArmyForagingState` references.

Suggested DTO shape:

- `CampaignRuntimeSnapshot`
  - `CampaignId`
  - `ArmyId`
  - `OwnerFaction`
  - `TargetFaction`
  - `OriginColonyId`
  - `TargetColonyId`
  - `Phase`
  - `CreatedTick`
  - `RouteIntent`
  - `RouteCounters`
  - `Army`
- `ArmyRuntimeSnapshot`
  - `ArmyId`
  - `OwnerFaction`
  - `HomeColonyId`
  - `OriginX`, `OriginY`, `TargetX`, `TargetY`
  - `RequestedMemberCount`
  - `MemberActorIds` as a copied read-only array/list
  - `ForageConsumerKey`
  - value snapshots for supply/ration/carrier/forage state
- `CampaignRouteCountersSnapshot`
  - same counter values as `CampaignRouteCounters`
- Optional small value snapshots:
  - `ArmySupplyRuntimeSnapshot`
  - `ArmyRationPoolRuntimeSnapshot`
  - `ArmySupplyCarrierRuntimeSnapshot`
  - `ArmyForagingRuntimeSnapshot`

Use records/readonly DTOs where practical.

## Implementation steps

1. Add immutable runtime snapshot DTOs under `WorldSim.Runtime/Simulation/Military`.
2. Add mapper methods from `CampaignState` / `ArmyState` to snapshots.
3. Change `SimulationRuntime.Campaigns` to return detached snapshots.
4. Update existing `Wave9CampaignRuntimeTests` to inspect snapshot fields instead of live runtime objects.
5. Add explicit boundary tests:
   - `Campaigns` does not expose `CampaignState` or `ArmyState` objects.
   - Retained `Campaigns` list does not grow after later campaign creation.
   - Nested `MemberActorIds` is a copied read-only collection.
   - Snapshot state values do not expose live `ArmySupplyState`, `ArmyRationPoolState`, `ArmySupplyCarrierState`, or `ArmyForagingState` references.
6. Keep all P6-A no-progression tests green.
7. Update Combined sequencing only after implementation is green: mark `P6-A1` complete and open `P6-B`.
8. Update `ops/PROJECT_STATE.md` after implementation is green.

## Acceptance criteria

- `SimulationRuntime.Campaigns` no longer returns live `CampaignState` / `ArmyState` objects.
- Consumers cannot mutate or retain live campaign/army runtime entities through `Campaigns`.
- Existing P6-A creation behavior remains unchanged.
- Existing `TryCreateCampaign(...)` result contract remains unchanged.
- `P6-A` no-op tick behavior remains unchanged.
- No P6-B/P6-C/P6-D behavior is introduced.
- No AI, Graphics, ScenarioRunner, Refinery, Java, or `World.Update` campaign progression changes.
- Tests prove detached query behavior and existing P6-A behavior.

## Verification plan

Run:

```powershell
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --filter "Wave9CampaignRuntimeTests" --no-restore
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --filter "Wave9" --no-restore
dotnet test "WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj" --no-restore
dotnet build "WorldSim.sln" --no-restore
git diff --check
```

Scope checks:

- `git diff --name-only` must not include `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.ScenarioRunner`, `WorldSim.Refinery*`, or `refinery-service-java`.
- Search confirms no new `TryForageToRationPool(...)` organic callsite outside existing model/tests.
- Search confirms no `World.Update` campaign progression hook.

## Handoff after completion

If green, update:

- Combined plan: mark `P6-A1` ✅ and keep `P6-B` as next open step.
- `ops/PROJECT_STATE.md`: next expected role Track B, next action `P6-B` assembly/rally.

If not green, keep `P6-B` blocked and document the exact failing boundary/test.
