# Wave7.5 LC1-C1 Track C Alignment Note

Status: closed (`LC1-C1`)
Owner: Track C
Last updated: 2026-04-19

## Purpose

Lock the Track C interpretation of Wave 7.5 Step 3 `LC1-C1` as an additive compatibility slice:

- audit low-cost profile compatibility for AI/planner evidence,
- add deterministic guardrails for `Headless` vs `DevLite`,
- avoid expanding into renderer or profile-owned gameplay scope.

Source of truth:

- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`

## Scope (in)

1. Track C compatibility audit for low-cost profile lanes.
2. Cross-lane guardrail tests using ScenarioRunner artifacts.
3. Optional minimal runtime normalization fixes only if a new guardrail test proves a drift.

## Scope (out)

1. No renderer feature work (`Track A` lanes).
2. No new profile-owned game logic in planner/runtime behavior.
3. No telemetry platform rewrite.
4. No compare-mode identity changes.

## Compatibility Assertions

1. ScenarioRunner lane metadata may differ between `Headless` and `DevLite`.
2. For the same seed/config/planner, AI/planner/contact evidence should remain lane-invariant.
3. Track C changes must remain snapshot-boundary safe and runner-friendly (headless evidence first).

## Risks

1. `DebugTargetKey` prefix drift can silently change `targetKind` distribution evidence.
2. `ScenarioAiTelemetrySnapshot.DecisionCount` is a decision snapshot aggregate, not a total run-volume counter.
3. Compare-mode is intentionally lane-aware; it is not a cross-lane equivalence tool.

## Guardrails

1. Cross-lane audit uses direct artifact JSON subset comparison, not compare-mode.
2. Lane metadata is explicitly allowed to differ.
3. AI/planner/contact evidence is expected to match exactly for current runner semantics.
4. If exact equality fails, only minimal deterministic normalization fixes are allowed.

## Validation Target

Focused gate for `LC1-C1`:

1. `WorldSim.Runtime.Tests`
2. Focused `WorldSim.ScenarioRunner.Tests` for compatibility and artifact seams

Full ScenarioRunner suite is optional for this slice because unrelated known-red tests may exist outside LC1-C1 scope.

Proof shape used by this closeout:

1. Cross-lane guardrail test runs `simple`, `goap`, and `htn` planners.
2. Compatibility pair uses a nontrivial combat-enabled config (`EnableCombatPrimitives=true`, `EnableDiplomacy=true`, `EnableSiege=true`, `Ticks=300`) so contact telemetry is exercised.
3. Compatibility runs require ScenarioRunner exit code `0` (no assertion/anomaly fail acceptance).
4. Allowed difference is lane metadata only (`Headless` vs `DevLite`); `ai` and `contact` artifact blocks are exact-equality guarded.

## Closeout Checklist

1. Alignment note committed.
2. Cross-lane compatibility tests added and green in focused gate.
3. `AGENTS.md` updated with `LC1-C1` closeout note.
4. Sequencing status updated (`LC1-C1` -> `✅`).
