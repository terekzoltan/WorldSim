# Post-Wave7 AI Debug -> SMR Observability Plan

Status: approved non-wave follow-up after Wave 7 closeout
Owner: SMR Analyst
Last updated: 2026-04-12

## 1. Purpose

Wave 7 closeout left the campaign/director integration technically green, but manual real-sim observation exposed a different problem class:

- the simulation can still feel locally defensive and inert,
- outward pressure is hard to judge from aggregate combat counters alone,
- and the current `ScenarioRunner` cannot directly show which AI goals/commands/causes dominate a suspicious run.

This follow-up exists to bridge the gap between:

- headless SMR evidence, which is currently aggregate-heavy,
- and the in-app AI debug panel, which is decision-centric but manual.

The approved v1 direction is:

- aggregate AI telemetry,
- plus one deterministic latest-decision sample.

## 2. Why This Is Not A Combined-Plan Epic

This slice is intentionally not added as a new Wave item in `Combined-Execution-Sequencing-Plan.md`.

Reasoning:

- it is an operational observability follow-up, not a new gameplay feature wave,
- it extends the existing SMR/productization layer,
- and it fits the same category as other post-wave telemetry/reporting refinements documented outside the main wave sequence.

Source-of-truth policy for this slice:

- this document is the implementation plan,
- `AGENTS.md` gets a cross-track note after implementation,
- `SMR-Minimum-Ops-Checklist.md` remains the operational usage guide rather than the feature design doc.

## 3. Problem Statement

Current `ScenarioRunner` AI observability is limited to a few counters:

- `AiNoPlanDecisions`
- `AiReplanBackoffDecisions`
- `AiResearchTechDecisions`
- plus combat/backoff/routing aggregate metrics.

These signals are useful, but insufficient for questions such as:

- is `DefendSelf` actually dominating the problematic runs,
- are suspicious runs mostly `Fight`, `Flee`, or `RaidBorder`,
- are we seeing `ThreatResponse`, `ReplanBackoff`, or some other replan reason,
- what kind of target is the AI acting against,
- and what single latest decision would a human compare against the in-app AI debug panel.

## 4. Approved Scope

### 4.1 Mandatory v1

1. Run-level AI aggregate summary exported in SMR artifacts.
2. One deterministic latest-decision sample exported per run.
3. `targetKind` normalization exported both in aggregate and latest-decision shape.
4. Compact AI top fields exported into timeline/drilldown samples.

### 4.2 Nice-to-have included in v1

1. Run-level top-3 goal summary.
2. Run-level top-3 debug-cause summary.

### 4.3 Explicit non-goals for v1

This plan does not aim to implement:

- full AI debug panel parity,
- manual tracked-NPC mode,
- paging/history export,
- score-heavy or histogram-heavy per-sample telemetry,
- multi-sample latest-decision history,
- or UI/Graphics changes.

## 5. Design Principles

1. Prefer a runtime-owned telemetry seam over `ScenarioRunner` poking through internal actor state.
2. Keep artifact schema additive.
3. Preserve deterministic ordering and diff-friendly output.
4. Export only the smallest useful decision slice for behavior diagnosis.
5. Keep timeline payload compact; reserve richer detail for run-level summary.

## 6. Data Model

## 6.1 Runtime telemetry records

Introduce public runtime-side records for headless AI telemetry:

- `ScenarioAiCountEntry`
- `ScenarioAiLatestDecisionSample`
- `ScenarioAiTelemetrySnapshot`
- `ScenarioAiTimelineSnapshot`
- `ScenarioAiTargetKindClassifier`

These live in `WorldSim.Runtime`, not in `ScenarioRunner`, so the data source remains runtime-owned.

## 6.2 Run-level SMR shape

Each `ScenarioRunResult` gains an additive `ai` block.

Minimum fields:

- `decisionCount`
- `goalCounts`
- `commandCounts`
- `replanReasonCounts`
- `methodCounts`
- `debugCauseCounts`
- `targetKindCounts`
- `topGoals`
- `topDebugCauses`
- `latestDecision`

`latestDecision` fields:

- `actorId`
- `colonyId`
- `x`
- `y`
- `selectedGoal`
- `nextCommand`
- `planLength`
- `planCost`
- `replanReason`
- `methodName`
- `debugDecisionCause`
- `debugTargetKey`
- `targetKind`

## 6.3 Timeline / drilldown shape

Each `ScenarioTimelineSample` gains a compact additive `ai` block.

Fields:

- `topGoal`
- `topGoalCount`
- `topCommand`
- `topCommandCount`
- `topReplanReason`
- `topReplanReasonCount`
- `topDebugCause`
- `topDebugCauseCount`

This is intentionally compact and omits full count tables and full latest-decision payload.

## 7. Deterministic Selection Policy

The latest-decision sample follows the same intent as the runtime AI debug panel's latest-selection logic.

Ordering:

1. `WorldTick` descending
2. `Sequence` descending
3. `ActorId` ascending

This is the stable tiebreak policy for headless artifacts.

## 8. TargetKind Taxonomy

V1 `targetKind` is deliberately narrow and based on the currently stable `DebugTargetKey` prefixes already emitted by runtime logic.

Supported values:

- `none`
- `build`
- `resource`
- `retreat`
- `move`
- `other`

Important constraint:

- do not invent a richer taxonomy such as `enemy_actor`, `colony`, or `self` unless runtime starts emitting a stable target-key scheme that supports it.

## 9. File-Level Work Plan

### 9.1 `WorldSim.Runtime`

Files in scope:

- add `WorldSim.Runtime/Diagnostics/ScenarioAiTelemetry.cs`
- update `WorldSim.Runtime/Simulation/World.cs`

Work:

- define public telemetry records,
- define `ScenarioAiTargetKindClassifier`,
- add `World.BuildScenarioAiTelemetrySnapshot()`,
- aggregate decision metadata from actor `LastAiDecision` and current debug cause/target fields,
- sort all count tables deterministically (`count desc`, then `name asc`),
- and expose the deterministic latest-decision sample.

### 9.2 `WorldSim.ScenarioRunner`

Files in scope:

- update `WorldSim.ScenarioRunner/Program.cs`

Work:

- import runtime telemetry types,
- add `ai` block to `ScenarioRunResult`,
- add compact `ai` block to `ScenarioTimelineSample`,
- call `world.BuildScenarioAiTelemetrySnapshot()` for run-level results,
- call `.ToTimelineSnapshot()` for drilldown/timeline samples,
- keep all artifact output additive and backward compatible.

### 9.3 Tests

Runtime tests:

- add `WorldSim.Runtime.Tests/ScenarioAiTelemetryTests.cs`

ScenarioRunner tests:

- add `WorldSim.ScenarioRunner.Tests/AiTelemetryArtifactTests.cs`
- update `ArtifactBundleTests.cs`
- update `DrilldownTests.cs`

## 10. Acceptance Criteria

The implementation is accepted when all of the following hold:

1. `ScenarioRunResult` JSON includes an additive `ai` block.
2. `ScenarioTimelineSample` JSON includes an additive compact `ai` block.
3. Runtime exports deterministic count tables sorted by count descending then name ascending.
4. Runtime exports a deterministic latest-decision sample for non-empty runs.
5. `targetKind` is present in aggregate counts and latest sample.
6. Existing artifact/drilldown tests continue to pass with additive assertions only.
7. New runtime and ScenarioRunner tests pass.
8. A small AI telemetry smoke run produces readable `ai` artifacts.
9. A follow-up behavior-probe SMR rerun can directly inspect goals/causes rather than only proxy counters.

## 11. Test Plan

### 11.1 Runtime unit tests

Required coverage:

1. known target-key prefixes normalize correctly,
2. empty world returns empty telemetry,
3. aggregate counts reflect selected goal / next command / replan reason / method / debug cause / target kind,
4. latest sample is deterministic,
5. tied counts sort by name.

### 11.2 ScenarioRunner artifact tests

Required coverage:

1. `summary.json` run entries contain `ai`,
2. `runs/*.json` contain `ai`,
3. `drilldown/timeline.json` samples contain compact `ai`,
4. repeated same-seed runs emit deterministic AI telemetry payloads.

## 12. Evidence Runs After Implementation

### 12.1 Schema smoke

Recommended run:

- `planner-compare-ai-telemetry-smoke-001`

Goal:

- verify artifact shape and readability,
- confirm `ai` blocks are present in summary, runs, and drilldown.

### 12.2 Behavior rerun

Recommended run:

- `planner-compare-wave7-behavior-002`

Goal:

- revisit the current `DefendSelf` / defensive-loop hypothesis,
- rank worst runs using the new direct AI telemetry,
- and separate planner effects from shared runtime/threat-context effects more confidently.

## 13. Risks And Mitigations

### Risk 1 — Schema bloat

Mitigation:

- keep full counts at run-level only,
- keep timeline compact,
- avoid score tables and history dumps.

### Risk 2 — Runtime/UI drift in latest-decision logic

Mitigation:

- explicitly document the latest-sample ordering,
- and keep the runner dependent on runtime-owned telemetry, not on reimplemented runner-only decision logic.

### Risk 3 — Overfitting target taxonomy too early

Mitigation:

- use only stable prefix-derived categories in v1,
- leave richer taxonomy as future work.

## 14. Deferred Follow-Ups

Future, explicitly deferred ideas:

- goal score export in latest sample,
- multiple decision samples or sample history,
- timeline target-kind summary,
- richer AI anomaly rules on top of direct goal/cause dominance,
- and eventual AI-focused SMR visualization tooling.

## 15. Approval Note

This scope was reviewed against the current manual findings and approved as the preferred v1 shape:

- `aggregate + 1 deterministic latest-decision sample`
- mandatory `targetKind`
- optional but included run-level top-3 summaries
- score-heavy, panel-parity, and multi-sample features deferred
