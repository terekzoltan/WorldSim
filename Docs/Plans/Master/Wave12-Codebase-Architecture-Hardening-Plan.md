## Wave 12 Codebase Architecture Hardening Parking Lot

> **For agentic workers:** This is a parking-lot brief, not an executable implementation plan. Use repo-valid workflow skills such as `sequence-planning` only after Meta explicitly promotes one of these areas into a real wave or sidecar slice.

**Goal:** Preserve the 2026-05-12 architecture audit as future-facing guidance without accidentally opening implementation scope before Meta assigns epics, ownership, dependencies, and evidence gates.

**Status:** Not launchable.

Promotion requirements before any execution:
- Meta creates real wave/epic codes.
- Owners are assigned per track.
- Dependencies are placed relative to Wave 9-11 closeouts.
- Evidence gates are selected.
- `ops/PROJECT_STATE.md` explicitly points to the promoted work.

## Candidate Scope Areas

The audit identified six candidate architecture hardening areas:
1. Align `SimulationRuntime` and `ScenarioRunner` command/runtime boundaries.
2. Add real `GameHost` boundary arch tests.
3. Add snapshot caching or dirty-slice/static-layer separation.
4. Add spatial indexes for occupancy, blockage, local threat, and crowd deconfliction.
5. Add structured render data for tactical/campaign/logistics/ecology effects.
6. Harden CI/test matrix and artifact hygiene.

## Candidate File Surface

These file areas are likely relevant when one of the areas above is promoted, but this list is not yet an implementation contract:
- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.App/GameHost.cs`
- `WorldSim.ArchTests/BoundaryRulesTests.cs`
- `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- `WorldSim.Runtime/Simulation/World.cs`
- `WorldSim.Runtime/Simulation/Person.cs`
- `WorldSim.Runtime/Simulation/Navigation/NavigationGrid.cs`
- `WorldSim.Graphics/Rendering/*`
- `.github/workflows/smr-headless.yml`
- future targeted runtime/scenario/arch tests as needed by the promoted slice

## Promotion Guardrails

Any future Wave 12 promotion must preserve these boundaries:
- No renderer may become source of truth for gameplay state.
- No ScenarioRunner evidence lane may accidentally bypass the runtime command boundary when the app/refinery path uses that boundary.
- Snapshot caching must preserve deterministic replay and invalidate on state changes.
- Spatial indexes must expose deterministic rebuild/dirty counters.
- Tactical/campaign/ecology render effects must be structured snapshot records, not prose-event parsing.
- CI hardening must not introduce paid refinery calls or require a GUI app.

## Candidate Area Notes

### A. Runtime Boundary Alignment
- Candidate outcome: parity lanes use `SimulationRuntime`, while direct `World` lanes are explicitly labeled and excluded from parity evidence.
- Candidate evidence: runtime-boundary mode marker in ScenarioRunner artifacts and dedicated boundary tests.

### B. App/GameHost Boundary Tests
- Candidate outcome: arch tests scan real `GameHost.cs` and flag direct mutable runtime/domain manipulation from App.

### C. Snapshot Caching and Static Layers
- Candidate outcome: deterministic cache hits/misses and invalidation counters, with clear separation between static and dynamic snapshot layers.

### D. Spatial Indexes
- Candidate outcome: actor/blockage/path queries move off repeated full scans and expose deterministic rebuild/dirty counters.

### E. Structured Render Effects
- Candidate outcome: tactical/campaign/logistics/ecology visuals render from structured snapshot records instead of event-string parsing.

### F. CI and Artifact Hygiene
- Candidate outcome: non-GUI verification matrix is explicit, artifact tests operate in isolated directories, and no paid refinery calls are required by default CI.

## Meta Checklist For Future Promotion

Before this parking lot becomes executable, Meta should decide:
- whether the work becomes a real Wave 12 or several smaller sidecar slices,
- which candidate areas are required versus optional,
- which track owns each promoted epic,
- whether runtime boundary, snapshot caching, or spatial indexes should come first,
- which evidence package closes the promoted work.
