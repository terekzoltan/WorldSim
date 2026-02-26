# Track B Prep Roadmap

Status: active
Owner: Track B
Date: 2026-02-26

## Purpose

Prepare `WorldSim.Runtime` for the cross-track Combat/Fortification/Campaign master program with low-risk foundational changes.

## B-Prep phases

- `B-Prep 0` Determinism hardening
  - World seed support and deterministic entity RNG propagation
  - Stable/clamped AI context shaping for reproducible decisions
- `B-Prep 1` Runtime command and feature-flag scaffold
  - Runtime feature flags with OFF-by-default behavior
  - Command endpoints scaffold for future diplomacy/directive flows
- `B-Prep 2` AI context contract lock (implemented baseline)
  - P0/P1 context fields wired in runtime adapter
  - mixed cadence support (per-tick + periodic strategic sampling)
  - fallback terulet/warrior jelek helyett runtime war state + contested tile + warrior count forras
- `B-Prep 3` Snapshot vNext lock (implemented baseline)
  - `PersonRenderData` expanded with combat-relevant fields (HP/combat/role/defense)
- `B-Prep 4` Navigation/occupancy foundation (implemented baseline)
  - topology versioning in world + navigation grid/pathfinder/path cache scaffold
  - runtime tests for version invalidation and BFS detour behavior

## Follow-up shipped after B-Prep 2/3/4

- Territory ownership baseline model landed:
  - tile owner + contested flags computed periodically in runtime.
- Role mobilization baseline landed:
  - colony war state driven warrior assignment policy and counts.
- Snapshot contract updated:
  - person combat fields + colony war state/warrior count + tile ownership/contested fields.

## Additional pre-masterplan hardening

- Territory influence formula refined and tunable runtime constants introduced:
  - `TerritoryBaseColonyInfluence`, `TerritoryPopulationInfluenceWeight`,
    `TerritoryWarriorInfluenceWeight`, `TerritoryContestedThreshold`.
- Runtime-facing planner/policy labels exposed as strings on `SimulationRuntime`
  to reduce `NpcPlannerMode` enum leakage at host boundary.

## Implemented mini plans

- Headless scenario runner added:
  - `WorldSim.ScenarioRunner` project with multi-seed KPI output.
- Parameter catalog baseline:
  - `RuntimeBalanceOptions` introduced in runtime for hunger/starvation/aging thresholds.
- Event feed catalog baseline:
  - `EventFeedCatalog` constants used by runtime event emission.
- Runtime state encapsulation start:
  - read-only runtime views (`People`, `Colonies`, `Animals`) exposed in `World`.

## Next suggested increments

- Add deterministic replay/hash assertion in CI for scenario runner output.
- Migrate remaining hard-coded thresholds to `RuntimeBalanceOptions`.
- Expand event catalog to combat/diplomacy/campaign phases from the master plan.
- Continue replacing direct mutable list usage with read-only runtime projections where safe.
