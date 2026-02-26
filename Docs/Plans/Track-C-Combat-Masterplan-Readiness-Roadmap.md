# Track C Combat Masterplan Readiness Roadmap

Status: accepted and refined for Phase 0 readiness
Owner: Track C
Date: 2026-02-26

## Goal

Track C should be ready to start Combat Master Plan Phase 0 with clean boundaries, explicit dependencies, and no premature coupling to unfinished Track B/Track D subsystems.

## Phase definitions (C-R1..C-R6)

### C-R1 - Contract freeze and extension staging

Scope split:

- Freeze the current economy AI contract now (`NpcCommand` + `NpcAiContext` economy baseline).
- Define (but do not force-implement) a minimum combat extension contract expected from Phase 0 runtime primitives:
  - `Fight`
  - `Flee`
  - `Guard` (and patrol variants when runtime support lands)

Notes:

- Combat command minimum does not fully exist yet in runtime behavior.
- Extension contract is staged now and frozen as soon as Track B Phase 0 exposes stable primitives.

### C-R2 - GOAP combat hardening (gated)

This phase is blocked until Track B Phase 0 combat primitives are complete.

- Before dependency lands: only define GOAP precondition/effect schemas as stubs.
- After dependency lands: wire real fight/flee/guard/patrol action semantics against runtime damage/cooldown model.

### C-R3 - HTN combat method quality

- Method scoring for combat tasks.
- Winner/runner-up audit fields mandatory in trace.
- Explicit fallback behavior for no-method situations.

### C-R4 - Runtime context convergence (partially complete)

Already completed in part:

- Context fallback fields now come from runtime-backed sources (diplomacy flag, hostile overlap, role flag heuristics).

Remaining focus:

- Replace current diplomacy heuristic with real diplomacy state when Track B Phase 1 Sprint 2+ lands.
- Replace territory proxy with true contested/ownership state once available.

### C-R5 - Directive/policy integration (late, optional)

- Earliest intended start: Phase 4 (Sprint 7), after Track D Season Director becomes active.
- Keep this behind soft-bias policy (no hard action override).

### C-R6 - UI/debug fit and budget

- Can proceed anytime after C-R2 starts.
- Track A budget remains authoritative for what stays in compact HUD vs debug panel.

## Dependency map to Combat Master Plan

| C-R Phase | Earliest start | Depends on |
| --- | --- | --- |
| C-R1 | Now (before Phase 0) | -- |
| C-R2 | After Phase 0 | Track B combat primitives |
| C-R3 | After C-R2 | -- |
| C-R4 | After Phase 1 Sprint 2 | Track B diplomacy/territory state |
| C-R5 | Phase 4 (Sprint 7) | Track D Director |
| C-R6 | Anytime after C-R2 starts | Track A UI budget |

## Immediate objective

Primary objective now is Phase 0 start readiness:

1. Lock C-R1 economy contract and combat extension expectations.
2. Keep C-R2 as schema-level prep only until Track B primitive delivery is done.
3. Avoid premature coupling to unfinished diplomacy/territory/director systems.
