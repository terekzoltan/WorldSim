# Track C Phase 0 Preflight Checklist

Status: in progress
Owner: Track C
Date: 2026-02-26

## Objective

Ensure Track C can start Combat Master Plan Phase 0 without contract drift or premature coupling.

## C-R1 freeze (before Phase 0)

- [x] Economy command baseline is frozen in AI contract (`NpcCommand` economy set).
- [x] Combat extension minimum is staged in contract (`Fight`, `Flee`, `GuardColony`, `PatrolBorder`).
- [x] Runtime maps staged combat commands to runtime jobs (placeholder-safe behavior).
- [x] Command parity test covers all `NpcCommand` enum values.

## Dependency guardrails

- [x] C-R2 marked as Track B Phase 0 dependent in readiness roadmap.
- [x] C-R4 marked partial-complete with remaining diplomacy/territory proxy replacement.
- [x] Cross-track handshake docs available for A/B/D alignment.

## Test gates

- [x] Runtime tests include command parity coverage.
- [x] AI + runtime tests pass after contract staging changes.
- [ ] Add dedicated contract drift test for combat extension schema versioning (follow-up).

## Not in scope before Phase 0

- Real combat damage model integration in GOAP actions.
- Cooldown/fight resolution logic bound to runtime combat primitives.
- Director-driven directive coupling (Phase 4 timing).
