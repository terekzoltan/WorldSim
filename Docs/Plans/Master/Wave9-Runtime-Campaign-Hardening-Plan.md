# Wave 9 Runtime Campaign Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Wave 9 audit notes into executable runtime, AI handoff, graphics snapshot, and SMR evidence gates for army supply carriers, foraging, and the campaign skeleton.

**Architecture:** Track B owns runtime state, commands, counters, and read-model export. Track C consumes explicit runtime hooks and must not infer behavior from generic jobs. Track A consumes structured snapshot data only; event-string parsing is not accepted for campaign or supply visuals.

**Tech Stack:** C#/.NET 8, `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.ScenarioRunner`, xUnit, MonoGame read-model snapshots.

---

## Current Scope

This plan expands the audit hardening notes in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` for Wave 9 only.

The implementation frontier is still `P5-G (B part)`:
- `P5-F` and `P5-I` are complete.
- `P5-G (B part)` may start.
- `P5-G (C part)`, `P5-H`, and `P6-A` must wait for their Combined gates.
- No persistent `Army`, `Campaign`, or `CampaignManager` entity should be created before `P6-A`.
- No army supply behavior should be wired into organic `World.Update` before Meta explicitly opens that scope.

## File Ownership Map

- Modify: `WorldSim.Runtime/Simulation/Military/ArmySupplyModel.cs`
- Modify: `WorldSim.Runtime/Simulation/Military/ArmyRationPoolSupplyModel.cs`
- Modify: `WorldSim.Runtime/Simulation/PersonRole.cs`
- Modify: `WorldSim.Runtime/Simulation/Person.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.Runtime/Diagnostics/ScenarioAiTelemetry.cs`
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Modify later, after runtime hooks exist: `WorldSim.AI/Abstractions.cs`
- Modify later, after runtime hooks exist: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- Modify later, after `P6-D (B part)`: `WorldSim.Graphics/UI/Panels/CampaignPanelRenderer.cs`
- Test: `WorldSim.Runtime.Tests/Wave9ArmySupplyModelTests.cs`
- Test: `WorldSim.Runtime.Tests/Wave9ArmyRationPoolSupplyModelTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave9SupplyCarrierRuntimeTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave9ForagingRuntimeTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave9CampaignReadModelTests.cs`
- Test new: `WorldSim.ScenarioRunner.Tests/Wave9CampaignSupplyEvidenceTests.cs`

## Non-Negotiable Gates

- One army tick must use exactly one supply source: carried member inventory through `ArmySupplyModel`, or fallback ration pool through `ArmyRationPoolSupplyModel`. Running both in the same tick is a failure.
- Supply-carrier state must be durable runtime state or a runtime-owned command result. It cannot exist only as a debug row, planner label, or profession fallback.
- Foraging must have dedicated runtime counters and state. Generic `GatherFood` telemetry does not prove Wave 9 campaign foraging.
- Campaign/supply read-model output must be structured data. Track A must not parse `RecentEvents` strings to rediscover campaign routes, supply source, or tactical effects.
- ScenarioRunner evidence must expose dedicated fields for carrier assignment, carrier delivery, forage attempts, forage success/failure, supply mode, campaign phase, route progress, and encounter outcome.

## Task 1: P5-G Runtime Supply Carrier Hooks

**Files:**
- Modify: `WorldSim.Runtime/Simulation/PersonRole.cs`
- Modify: `WorldSim.Runtime/Simulation/Person.cs`
- Modify: `WorldSim.Runtime/Simulation/Military/ArmySupplyModel.cs`
- Modify: `WorldSim.Runtime/Simulation/Military/ArmyRationPoolSupplyModel.cs`
- Test: `WorldSim.Runtime.Tests/Wave9SupplyCarrierRuntimeTests.cs`

- [ ] **Step 1: Write mutual-exclusion tests**

Create tests that model a caller trying to run both carried-inventory and ration-pool supply in one tick. The expected result is deterministic rejection or a single selected source, not double consumption.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9SupplyCarrierRuntimeTests --no-restore
```

Expected before implementation: fail because the caller-level guard does not exist.

- [ ] **Step 2: Add runtime supply source selection**

Add a small runtime-owned type for supply source selection, for example `ArmySupplySourceMode.CarriedInventory` and `ArmySupplySourceMode.RationPool`. Keep this close to the military supply models so callers cannot accidentally invoke both models.

The guard should make this invariant easy to assert:

```csharp
// Pseudocode shape, adapt names to local style.
if (state.LastSupplyTick == tick && state.LastSupplySource != requestedSource)
{
    return ArmySupplyTickResult.RejectedMixedSupplySource(state.LastSupplySource, requestedSource);
}
```

- [ ] **Step 3: Add carrier role/state on actors**

Expose `PersonRole.SupplyCarrier` through runtime state that can survive at least a tick and appear in snapshots/telemetry. Do not rely on a one-frame AI command string.

- [ ] **Step 4: Add focused runtime tests**

Cover:
- a carrier can be assigned and read back,
- a carrier assignment is visible through runtime state,
- carried-inventory mode consumes member food but not ration pool food,
- ration-pool mode consumes pool food but not member inventory,
- a mixed-source tick is rejected or deterministically resolved once.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9SupplyCarrierRuntimeTests --no-restore
```

Expected after implementation: pass.

## Task 2: P5-G Track C Handoff Surface

**Files:**
- Modify after Task 1: `WorldSim.AI/Abstractions.cs`
- Modify after Task 1: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- Test: `WorldSim.Runtime.Tests/RuntimeNpcBrainTests.cs`

- [ ] **Step 1: Add explicit AI command vocabulary**

Add commands only after Track B runtime hooks exist. Required command concepts:
- assign or maintain supply carrier,
- deliver supply,
- abort delivery if supply source is invalid,
- expose a carrier decision cause for telemetry.

- [ ] **Step 2: Add context fields from runtime state**

Context should include army supply ratio, active supply source, carrier role availability, carrier delivery target, and whether fallback ration pool is allowed for the current caller.

- [ ] **Step 3: Add planner-mode tests**

Run the same carrier decision scenario against simple, GOAP, and HTN lanes when those lanes are available in local test helpers.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter RuntimeNpcBrainTests --no-restore
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --no-restore
```

Expected: carrier decisions use the new command/context fields and do not masquerade as generic `GatherFood`.

## Task 3: P5-H Runtime Foraging Hooks

**Files:**
- Modify: `WorldSim.Runtime/Simulation/Person.cs`
- Modify: `WorldSim.Runtime/Diagnostics/ScenarioAiTelemetry.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Test: `WorldSim.Runtime.Tests/Wave9ForagingRuntimeTests.cs`

- [ ] **Step 1: Write forage-state tests**

Cover allowed terrain/resource state, blocked forage conditions, capped forage yield, forage failure reason, and no duplicate food creation.

- [ ] **Step 2: Add runtime forage command/state**

Foraging must be distinct from normal colony gathering. It should have:
- attempt counter,
- success counter,
- failure counter by reason,
- capped food yield,
- source tile or route context,
- supply/campaign consumer key when applicable.

- [ ] **Step 3: Export forage counters**

Expose counters through ScenarioRunner and, when useful, the runtime snapshot. Keep the field names explicit, such as `campaignForageAttempts` and `campaignForageFoodGained`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9ForagingRuntimeTests --no-restore
```

Expected: pass with deterministic food conservation.

## Task 4: P6-A/P6-B/P6-C Campaign Runtime Foundation

**Files:**
- Create when `P6-A` opens: `WorldSim.Runtime/Simulation/Military/CampaignState.cs`
- Create when `P6-A` opens: `WorldSim.Runtime/Simulation/Military/ArmyState.cs`
- Modify when `P6-A` opens: `WorldSim.Runtime/SimulationRuntime.cs`
- Modify when `P6-C` opens: `WorldSim.Runtime/Simulation/Navigation/NavigationGrid.cs`
- Test new: `WorldSim.Runtime.Tests/Wave9CampaignRuntimeTests.cs`

- [ ] **Step 1: Add minimal campaign entities only after `P6-A` opens**

The first campaign entity should model identity, owning faction, origin, target, phase, route intent, and supply state reference. Do not add multi-front, convoy, scout, or siege-unit behavior in Wave 9.

- [ ] **Step 2: Add route and march counters**

Required counters:
- path requests,
- path cache hits,
- blocked movement checks,
- route recomputes,
- march progress ticks,
- encounter ticks,
- no-progress ticks.

- [ ] **Step 3: Add topology-aware path invalidation evidence**

Add a test where a route is cached, a blocking structure changes topology, and a later movement request invalidates or recomputes correctly.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9CampaignRuntimeTests --no-restore
```

Expected: route changes are deterministic and no stale path crosses newly blocked tiles.

## Task 5: P6-D Structured Snapshot and Track A Contract

**Files:**
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify after Track B close: `WorldSim.Graphics/UI/Panels/CampaignPanelRenderer.cs`
- Test: `WorldSim.Runtime.Tests/Wave9CampaignReadModelTests.cs`

- [ ] **Step 1: Add structured render records**

Required read-model records:
- `CampaignRenderData`,
- `ArmyRenderData`,
- `ArmySupplyRenderData`,
- route waypoint records,
- encounter/tactical effect records with source and target tiles.

- [ ] **Step 2: Keep graphics snapshot-only**

Track A may draw route lines, army badges, supply badges, and encounter/tactical effects only from snapshot records. It must not query mutable runtime state and must not parse `RecentEvents`.

- [ ] **Step 3: Add read-model tests**

Assert that campaign phase, route progress, supply source mode, low/out-of-supply state, carrier counters, forage counters, and outcome fields are present in the snapshot after deterministic runtime setup.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9CampaignReadModelTests --no-restore
```

Expected: pass.

## Task 6: Wave 9 SMR Evidence Surface

**Files:**
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Test: `WorldSim.ScenarioRunner.Tests/Wave9CampaignSupplyEvidenceTests.cs`
- Artifact docs: `Docs/Evidence/SMR/wave9-campaign-supply/README.md`

- [ ] **Step 1: Add deterministic lanes**

Required lanes:
- `army_supply_depletion`,
- `carrier_resupply`,
- `campaign_foraging`,
- `campaign_assembly_march_encounter`.

- [ ] **Step 2: Add artifact fields**

Required artifact fields:
- `carrierAssignments`,
- `carrierDeliveries`,
- `supplySourceMode`,
- `rationPoolConsumed`,
- `memberInventoryConsumed`,
- `campaignForageAttempts`,
- `campaignForageFoodGained`,
- `campaignPhaseTicks`,
- `campaignRouteProgress`,
- `campaignEncounterCount`.

- [ ] **Step 3: Run focused evidence tests**

Run:

```powershell
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave9CampaignSupplyEvidenceTests --no-restore
```

Expected: pass and assert that generic movement/food counters are not the only proof of carrier or forage behavior.

## Verification Matrix

Minimum closeout commands for this plan:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave9 --no-restore
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --no-restore
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave9CampaignSupplyEvidenceTests --no-restore
dotnet build WorldSim.sln --no-restore
git diff --check
```

Scenario smoke:

```powershell
$env:WORLDSIM_SCENARIO_MODE="all"
$env:WORLDSIM_SCENARIO_SEEDS="101,202,303"
$env:WORLDSIM_SCENARIO_PLANNERS="simple,goap,htn"
$env:WORLDSIM_SCENARIO_TICKS="1200"
dotnet run --project WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj -c Release
```

Expected: no invariant failure, no mixed supply-source tick, and dedicated Wave 9 carrier/forage/campaign counters populated.
