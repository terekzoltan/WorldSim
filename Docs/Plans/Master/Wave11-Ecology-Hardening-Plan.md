# Wave 11 Ecology Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the closed-loop ecology redesign cache-aware, land-safe, observable, and evidence-gated before replacing current replenishment behavior.

**Architecture:** Track B owns ecology state, plant/animal lifecycle, region caches, rescue demotion, and supply bridge counters. Track C consumes ecology context for animal/NPC behavior. Track A renders ecology overlays from snapshot fields only. SMR owns hard invariant evidence and baseline promotion.

**Tech Stack:** C#/.NET 8, `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.ScenarioRunner`, xUnit, MonoGame snapshot overlays.

---

## Current Scope

Wave 11 remains blocked until Wave 10.5 closeout. This plan expands the Combined Wave 11 runtime performance and hard evidence notes.

The goal is not to add farms, domestication, milk, eggs, or a complete food taxonomy. Wave 11 is wild ecology first:
- tile fertility,
- region carrying capacity,
- plant biomass,
- herbivore energy/lifecycle,
- predator energy/lifecycle,
- explicit emergency rescue counters,
- bounded predator-human interaction,
- staged plant/meat supply bridge.

## File Ownership Map

- Create: `WorldSim.Runtime/Simulation/Ecology/EcologyState.cs`
- Create: `WorldSim.Runtime/Simulation/Ecology/EcologyRegionCache.cs`
- Create: `WorldSim.Runtime/Simulation/Ecology/PlantBiomassModel.cs`
- Create: `WorldSim.Runtime/Simulation/Ecology/AnimalLifecycleModel.cs`
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Modify: `WorldSim.Runtime/Simulation/Animal.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.AI/Abstractions.cs`
- Modify: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- Modify: `WorldSim.Graphics/UI/EcologyPanelRenderer.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Test new: `WorldSim.Runtime.Tests/Wave11EcologyStateTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave11PlantBiomassTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave11AnimalLifecycleTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave11EmergencyRescueTests.cs`
- Test new: `WorldSim.ScenarioRunner.Tests/Wave11EcologyEvidenceTests.cs`

## Non-Negotiable Gates

- Region/tile ecology caches must be designed in `E11-A`/`E11-B`, before animal lifecycle loops grow.
- Normal animal viability must not depend on emergency rescue/replenishment.
- Spawn and migration must prefer land-safe, region-valid tiles. Water-only fallback must be explicit and counted.
- Predator-human baseline is enabled in Wave 11 evidence lanes, but it must be bounded and observable.
- Track A ecology overlays must use snapshot/read-model fields; renderer-side ecology computation is not accepted.
- Closeout requires hard invariants: `ECO-SPECIES`, `ECO-PLANT`, `ECO-OSC`, `ECO-RESCUE`, `ECO-SUPPLY`, and `ECO-HUMAN`.

## Task 1: E11-A Ecology State Contract

**Files:**
- Create: `WorldSim.Runtime/Simulation/Ecology/EcologyState.cs`
- Create: `WorldSim.Runtime/Simulation/Ecology/EcologyRegionCache.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Test: `WorldSim.Runtime.Tests/Wave11EcologyStateTests.cs`

- [ ] **Step 1: Write state contract tests**

Cover tile fertility, region capacity, plant biomass, animal energy, starvation counters, reproduction counters, migration counters, rescue counters, and snapshot export.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave11EcologyStateTests --no-restore
```

Expected before implementation: fail because ecology state contract does not exist.

- [ ] **Step 2: Add region cache shape**

The cache should expose:
- region id,
- land tile count,
- water tile count,
- plant biomass total,
- herbivore count,
- predator count,
- carrying capacity,
- overgrazing pressure,
- drought/season modifier.

- [ ] **Step 3: Export snapshot fields**

Snapshot must provide enough fields for Track A overlay and SMR drilldown without querying mutable runtime state.

## Task 2: E11-B Plant Growth and Carrying Capacity

**Files:**
- Create: `WorldSim.Runtime/Simulation/Ecology/PlantBiomassModel.cs`
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Test: `WorldSim.Runtime.Tests/Wave11PlantBiomassTests.cs`

- [ ] **Step 1: Write deterministic plant tests**

Cover growth, seasonal modifier, drought modifier, overgrazing reduction, lower/upper clamp, and no negative biomass.

- [ ] **Step 2: Add bounded update path**

Plant growth should update by region/tile cache, not by per-animal full-map scans. Any full-map recompute should be a periodic or dirty-cache rebuild path with counters.

- [ ] **Step 3: Add cache counters**

Counters:
- `ecologyRegionCacheRebuilds`,
- `plantGrowthTicks`,
- `overgrazedRegionTicks`,
- `droughtPlantPenaltyTicks`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave11PlantBiomassTests --no-restore
```

Expected: pass with deterministic plant biomass.

## Task 3: E11-C/E11-D Animal Lifecycle

**Files:**
- Create: `WorldSim.Runtime/Simulation/Ecology/AnimalLifecycleModel.cs`
- Modify: `WorldSim.Runtime/Simulation/Animal.cs`
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Test: `WorldSim.Runtime.Tests/Wave11AnimalLifecycleTests.cs`

- [ ] **Step 1: Write herbivore lifecycle tests**

Cover grazing, energy gain, starvation, reproduction, migration pressure, land-safe spawn, and no normal respawn dependency.

- [ ] **Step 2: Write predator lifecycle tests**

Cover prey-linked hunting, capture gain, starvation, reproduction, predator capacity, and bounded predator-human interaction flags.

- [ ] **Step 3: Add land-safe spawn/migration policy**

Spawn/migration should prefer:
1. same-region valid land tile with capacity,
2. nearby valid land tile,
3. deterministic fallback with explicit counter.

- [ ] **Step 4: Add lifecycle counters**

Counters:
- `herbivoreBirths`,
- `predatorBirths`,
- `herbivoreStarvations`,
- `predatorStarvations`,
- `herbivoreMigrations`,
- `predatorMigrations`,
- `landSafeSpawnFallbacks`,
- `predatorHumanAttackCount`,
- `predatorHumanDeathCount`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave11AnimalLifecycleTests --no-restore
```

Expected: pass.

## Task 4: E11-E Emergency Rescue Demotion

**Files:**
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Test: `WorldSim.Runtime.Tests/Wave11EmergencyRescueTests.cs`

- [ ] **Step 1: Write rescue demotion tests**

Cover normal lane no-rescue, debug lane rescue allowed, rescue counter increment, and evidence failure when normal acceptance lane depends on rescue.

- [ ] **Step 2: Add explicit policy**

Emergency rescue may remain as debug/safety fallback, but it must have:
- policy enum,
- counter,
- reason,
- lane visibility in ScenarioRunner artifacts.

- [ ] **Step 3: Add acceptance guard**

Normal acceptance lanes must fail if emergency rescue count is nonzero unless the lane is explicitly a rescue-test lane.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave11EmergencyRescueTests --no-restore
```

Expected: pass.

## Task 5: E11-F/E11-G Behavior and Supply Bridge

**Files:**
- Modify: `WorldSim.AI/Abstractions.cs`
- Modify: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- Modify: `WorldSim.Runtime/Diagnostics/ScenarioAiTelemetry.cs`
- Test: `WorldSim.Runtime.Tests/RuntimeNpcBrainTests.cs`

- [ ] **Step 1: Add ecology context to AI**

Context should include local predator pressure, herbivore availability, plant biomass pressure, recent predator-human attack count, and safe retreat/refuge hints.

- [ ] **Step 2: Add plant/meat supply bridge counters**

Required counters:
- `plantFoodProduced`,
- `meatFoodProduced`,
- `plantFoodConsumedByAnimals`,
- `meatFromHunt`,
- `supplyBridgeSkippedByNoBiomass`.

- [ ] **Step 3: Add behavior tests**

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter RuntimeNpcBrainTests --no-restore
```

Expected: NPC/animal behavior consumes ecology context without relying on renderer or event text.

## Task 6: E11-H/E11-I/E11-J Evidence and Debug Overlay

**Files:**
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Modify: `WorldSim.Graphics/UI/EcologyPanelRenderer.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- Test: `WorldSim.ScenarioRunner.Tests/Wave11EcologyEvidenceTests.cs`

- [ ] **Step 1: Add required evidence lanes**

Required lanes:
- default,
- medium-stress,
- drought,
- predator-human,
- long-run.

Each lane must run seeds `101`, `202`, `303` and planner lanes `simple`, `goap`, `htn`.

- [ ] **Step 2: Add invariant fields**

Artifacts must expose:
- `ECO-SPECIES`,
- `ECO-PLANT`,
- `ECO-OSC`,
- `ECO-RESCUE`,
- `ECO-SUPPLY`,
- `ECO-HUMAN`.

- [ ] **Step 3: Add overlay layers**

Track A overlay layers:
- fertility,
- plant biomass,
- overgrazing,
- region pressure,
- herbivore lifecycle markers,
- predator lifecycle markers,
- rescue counters.

Overlay drawing must be visible-bound aware or region-level when dense.

- [ ] **Step 4: Run evidence tests**

```powershell
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave11EcologyEvidenceTests --no-restore
```

Expected: invariant pack fails loudly when rescue is required in normal lanes.

## Verification Matrix

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave11 --no-restore
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave11EcologyEvidenceTests --no-restore
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore
dotnet build WorldSim.sln --no-restore
git diff --check
```

Scenario closeout:

```powershell
$env:WORLDSIM_SCENARIO_MODE="all"
$env:WORLDSIM_SCENARIO_SEEDS="101,202,303"
$env:WORLDSIM_SCENARIO_PLANNERS="simple,goap,htn"
$env:WORLDSIM_SCENARIO_TICKS="2400"
dotnet run --project WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj -c Release
```

Expected: hard ecology invariants pass, normal lanes do not rely on emergency rescue, and predator-human interaction remains bounded.
