# Wave 12 Codebase Architecture Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture the 2026-05-12 architecture audit as a non-launchable parking-lot plan that can later become a real wave after Meta defines epics, ownership, dependencies, and evidence gates.

**Architecture:** This plan preserves the project boundary model: runtime owns simulation state, graphics owns rendering from snapshots, app owns host/input wiring, AI consumes explicit interfaces, and refinery crosses only contract/adapter boundaries. It should not be executed until promoted by Meta after Wave 11 or a separate architecture planning session.

**Tech Stack:** C#/.NET 8, MonoGame, `WorldSim.Runtime`, `WorldSim.Graphics`, `WorldSim.App`, `WorldSim.AI`, `WorldSim.ScenarioRunner`, `WorldSim.ArchTests`, Java refinery tests where boundary changes touch adapter contracts.

---

## Status

This is not a launchable wave. It is a parking-lot source plan referenced by Combined so future agents do not have to reconstruct the deep audit findings.

Promotion requirements before execution:
- Meta creates real wave/epic codes.
- Owners are assigned per track.
- Dependencies are placed relative to Wave 9-11 closeouts.
- Evidence gates are selected.
- `ops/PROJECT_STATE.md` points to this work explicitly.

## Candidate Scope

The 2026-05-12 audit identified six architecture hardening areas:
1. Align `SimulationRuntime` and `ScenarioRunner` command/runtime boundaries.
2. Add real `GameHost` boundary arch tests.
3. Add snapshot caching or dirty-slice/static-layer separation.
4. Add spatial indexes for occupancy, blockage, local threat, and crowd deconfliction.
5. Add structured render data for tactical/campaign/logistics/ecology effects.
6. Harden CI/test matrix and artifact hygiene.

## File Ownership Map

- Modify: `WorldSim.Runtime/SimulationRuntime.cs`
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Modify: `WorldSim.App/GameHost.cs`
- Modify: `WorldSim.ArchTests/BoundaryRulesTests.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Modify: `WorldSim.Runtime/Simulation/Person.cs`
- Modify: `WorldSim.Runtime/Simulation/Navigation/NavigationGrid.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- Modify: `WorldSim.Graphics/Rendering/TerrainRenderPass.cs`
- Modify: `WorldSim.Graphics/Rendering/ResourceRenderPass.cs`
- Modify: `WorldSim.Graphics/Rendering/ActorRenderPass.cs`
- Modify: `WorldSim.Graphics/Rendering/CombatOverlayPass.cs`
- Modify: `WorldSim.Graphics/Rendering/StructureRenderPass.cs`
- Modify: `.github/workflows/smr-headless.yml`
- Test: `WorldSim.ArchTests/BoundaryRulesTests.cs`
- Test new: `WorldSim.Runtime.Tests/SpatialIndexTests.cs`
- Test new: `WorldSim.Runtime.Tests/SnapshotCachingTests.cs`
- Test new: `WorldSim.ScenarioRunner.Tests/ScenarioRuntimeBoundaryTests.cs`
- Test new: `WorldSim.Graphics.Tests` only if a graphics test project is created by a promoted plan.

## Non-Negotiable Gates

- No renderer may become source of truth for gameplay state.
- No ScenarioRunner evidence lane may accidentally bypass the runtime command boundary when the app/refinery path uses that boundary.
- Snapshot caching must preserve deterministic replay and must invalidate on tick/state changes.
- Spatial indexes must expose deterministic rebuild/dirty counters.
- Tactical/campaign/ecology render effects must be structured snapshot records, not prose-event parsing.
- CI hardening must not introduce paid refinery calls or require a GUI app.

## Candidate Epic A: Runtime Boundary Alignment

**Files:**
- Modify: `WorldSim.Runtime/SimulationRuntime.cs`
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Test: `WorldSim.ScenarioRunner.Tests/ScenarioRuntimeBoundaryTests.cs`

- [ ] **Step 1: Add boundary tests**

Write tests proving ScenarioRunner can execute through `SimulationRuntime` for lanes that need command/runtime parity.

- [ ] **Step 2: Keep direct `World` lanes explicit**

If a lane intentionally instantiates `World` directly for focused unit-like performance, the lane must label itself as direct-world and not be used as app/refinery parity evidence.

- [ ] **Step 3: Add artifact field**

Artifacts should include `runtimeBoundaryMode` with values such as `simulation_runtime` or `direct_world`.

Run:

```powershell
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter ScenarioRuntimeBoundaryTests --no-restore
```

Expected: parity lanes use `SimulationRuntime`.

## Candidate Epic B: App/GameHost Boundary Tests

**Files:**
- Modify: `WorldSim.ArchTests/BoundaryRulesTests.cs`
- Modify if needed: `WorldSim.App/GameHost.cs`

- [ ] **Step 1: Add real `GameHost` source scan**

Current app boundary coverage must scan `WorldSim.App/GameHost.cs`, not only legacy shims.

- [ ] **Step 2: Assert no direct mutable domain manipulation**

Arch tests should flag direct mutation of runtime world internals from App. Allowed App responsibilities remain host loop, input mapping, camera, command routing, refinery triggering, and snapshot handoff.

Run:

```powershell
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --filter AppGameHost --no-restore
```

Expected: arch coverage includes `GameHost.cs`.

## Candidate Epic C: Snapshot Caching and Static Layers

**Files:**
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.Runtime/SimulationRuntime.cs`
- Test: `WorldSim.Runtime.Tests/SnapshotCachingTests.cs`

- [ ] **Step 1: Add snapshot timing and cache tests**

Tests should prove that repeated `GetSnapshot()` calls in the same simulation tick reuse stable data or avoid rebuilding static tile data.

- [ ] **Step 2: Separate static and dynamic data**

Candidate split:
- static terrain/resource base layer,
- dynamic actors/structures,
- overlay data,
- HUD/event/director/campaign/ecology diagnostics.

- [ ] **Step 3: Add invalidation counters**

Counters:
- `snapshotCacheHits`,
- `snapshotCacheMisses`,
- `staticLayerRebuilds`,
- `dynamicLayerBuildMs`,
- `snapshotBuildMs`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter SnapshotCachingTests --no-restore
```

Expected: cache behavior is deterministic and invalidates on state changes.

## Candidate Epic D: Spatial Indexes

**Files:**
- Create: `WorldSim.Runtime/Simulation/Spatial/ActorSpatialIndex.cs`
- Create: `WorldSim.Runtime/Simulation/Spatial/StructureSpatialIndex.cs`
- Modify: `WorldSim.Runtime/Simulation/World.cs`
- Modify: `WorldSim.Runtime/Simulation/Person.cs`
- Test: `WorldSim.Runtime.Tests/SpatialIndexTests.cs`

- [ ] **Step 1: Add structure/blockage index tests**

Cover houses, specialized structures, defensive structures, gates, blocked tiles, friendly pass rules, and hostile block rules.

- [ ] **Step 2: Add actor occupancy index tests**

Cover actor tile occupancy, local neighbor count, no-progress/crowd deconfliction, and deterministic updates after movement.

- [ ] **Step 3: Add path/query counters**

Counters:
- `blockedTileLookups`,
- `actorOccupancyLookups`,
- `spatialIndexRebuilds`,
- `spatialIndexDirtyUpdates`,
- `pathRequests`,
- `pathCacheHits`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter SpatialIndexTests --no-restore
```

Expected: existing behavior is preserved with lower scan risk and observable counters.

## Candidate Epic E: Structured Render Effects

**Files:**
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.Graphics/Rendering/StructureRenderPass.cs`
- Modify: `WorldSim.Graphics/Rendering/CombatOverlayPass.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`

- [ ] **Step 1: Add tactical effect records**

Records should include:
- source tile,
- target tile,
- faction id,
- effect kind,
- age/tick,
- optional magnitude.

- [ ] **Step 2: Replace event-string parsing**

Tower beams, projectiles, route overlays, convoy movement, siege effects, and ecology overlays should render from structured records.

- [ ] **Step 3: Add visible-bound filtering**

Render passes should filter by viewport before sorting/grouping large lists.

Run:

```powershell
dotnet build WorldSim.sln --no-restore
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore
```

Expected: graphics stays snapshot-only and compiles.

## Candidate Epic F: CI and Artifact Hygiene

**Files:**
- Modify: `.github/workflows/smr-headless.yml`
- Modify: `WorldSim.ScenarioRunner.Tests/ArtifactBundleTests.cs`
- Modify or add docs: `Docs/Plans/Master/Verification-Gates.md`

- [ ] **Step 1: Add non-GUI test matrix**

Candidate CI jobs:
- runtime tests,
- arch tests,
- scenario runner smoke/perf,
- refinery client/adapter tests,
- Java refinery tests.

- [ ] **Step 2: Keep MonoGame full build separate if tool restore is fragile**

Full solution build can remain a local gate if CI cannot reliably restore MonoGame tools, but this must be explicit in docs.

- [ ] **Step 3: Fix artifact hygiene**

Tests should not delete root-level `manifest.json`, `summary.json`, `anomalies.json`, or `run.log` unless they created them in a temp directory.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --no-restore
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --no-restore
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore
Push-Location refinery-service-java
.\gradlew.bat test
Pop-Location
git diff --check
```

Expected: local non-GUI verification is reproducible and artifact tests operate in isolated directories.

## Promotion Checklist

Before this parking lot becomes an executable wave, Meta must decide:
- whether to run it as Wave 12 or as smaller sidecar waves,
- which candidate epics are required versus optional,
- whether snapshot caching or spatial indexes come first,
- whether CI hardening should happen before architecture runtime changes,
- which Track owns each epic,
- which evidence package closes the wave.
