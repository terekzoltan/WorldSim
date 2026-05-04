# Refinery Live SMR Plan

Status: planning approved
Owner: Track B primary for `WorldSim.ScenarioRunner` lane work, Track D primary for refinery semantics and live-path policy
Last updated: 2026-05-04

## 1. Purpose

Add a refinery-specific headless evidence lane to `WorldSim.ScenarioRunner` without destabilizing the current deterministic SMR core workflow.

This plan exists to make the real director/refinery path measurable in headless runs while keeping:

- the current `core` SMR lane stable and deterministic,
- the real `SimulationRuntime + RefineryPatchRuntime` integration path testable without the app,
- paid live runs explicit, capped, and operationally safe,
- and future solver-backed director work aligned with the existing TR2/TR3 migration path.

This document is the detailed source of truth for refinery live SMR execution details. The Combined plan should only carry:

- ownership,
- prerequisites,
- gates,
- and where the work belongs in the wave sequence.

## 2. Non-Goals

This plan does not aim to:

- replace the current deterministic `core` SMR lane,
- replatform all of `WorldSim.ScenarioRunner` onto `SimulationRuntime` in one step,
- emulate keyboard input or literally press `F6` in headless mode,
- make paid live refinery runs part of default CI gating,
- introduce exact-output baseline comparison for LLM-backed live runs,
- or make raw/full live response capture the default operating mode.

## 3. Decision Lock

The following decisions are locked unless a later explicit planning pass changes them:

- One executable remains: `WorldSim.ScenarioRunner`.
- Two execution lanes exist inside that executable:
  - `core`
  - `refinery`
- The refinery lane uses the real runtime/adapter path, not keyboard emulation.
- The first shipped refinery slice includes both:
  - `refinery_fixture`
  - `refinery_live_mock`
- Later refinery profiles are:
  - `refinery_live_validator`
  - `refinery_live_paid`
- Default checkpoint policy:
  - `fixture/mock/validator` -> `every_n_ticks`
  - `live_paid` -> `tick_list`
- Future domain-semantic recommended policy:
  - `season_boundary`
- Default capture policy:
  - `fixture/mock/validator` -> `redacted`
  - `live_paid` -> `hash`
- Paid live run safety:
  - default max triggers = `1`
  - hard cap max triggers = `3`
  - explicit opt-in only
- Wave 8.6 paid pilot presets:
  - `paid_micro_total2` -> 2 seeds, 1 checkpoint/run, 1 completion/checkpoint, estimated completions 2
  - `paid_probe_2x2x2` -> 2 seeds, 2 checkpoints/run, 2 completions/checkpoint, estimated completions 8
  - paid concurrency remains `1`
  - no-cost rehearsal is mandatory before paid

## 4. Why A Separate Refinery Lane Exists

Current WorldSim has two different execution surfaces.

`WorldSim.ScenarioRunner` today is a direct `World` runner:

- `WorldSim.ScenarioRunner/Program.cs`

The real live director/refinery trigger path goes through:

- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.RefineryAdapter/Integration/RefineryPatchRuntime.cs`
- `WorldSim.RefineryAdapter/RefineryTriggerAdapter.cs`
- `WorldSim.App/GameHost.cs`

Because of this, the correct direction is not "make core SMR pretend to press F6". The correct direction is:

- keep the current deterministic `core` lane intact,
- add a separate `refinery` lane that uses the real runtime/adapter integration path,
- and keep the operational meaning of each lane explicit.

## 5. Lane Model

### 5.1 Core lane

`core` keeps the existing deterministic SMR responsibilities:

- survival/economy/combat assertions,
- compare/baseline flows,
- perf and drilldown,
- and standard multi-seed / multi-planner headless evidence.

The `core` lane remains the default lane.

### 5.2 Refinery lane

The `refinery` lane exists to exercise the real director/refinery path headlessly.

Profiles:

| Profile | Java service | LLM | Cost | Intended use |
|---------|--------------|-----|------|--------------|
| `refinery_fixture` | no | no | none | deterministic scheduler/apply/artifact smoke |
| `refinery_live_mock` | yes | no | none | real HTTP + marker + apply seam smoke |
| `refinery_live_validator` | yes | no | none | validator/fallback/marker evidence on the live Java path |
| `refinery_live_paid` | yes | yes | real | advisory live evidence only, tightly capped |

Operational rule:

- `refinery_live_paid` is never part of generic default runs.
- `refinery_live_paid` is not part of CI until a later explicit policy change.

## 6. Execution Model

### 6.1 Trigger model

The refinery lane does not emulate keyboard input. It uses a checkpoint scheduler that calls the real trigger semantics on the runtime/adapter path.

Supported checkpoint policies:

- `every_n_ticks`
- `tick_list`
- `season_boundary`

Defaults:

- `refinery_fixture` -> `every_n_ticks`
- `refinery_live_mock` -> `every_n_ticks`
- `refinery_live_validator` -> `every_n_ticks`
- `refinery_live_paid` -> `tick_list`

Future direction:

- once solver-backed semantics and fallback policy mature, `season_boundary` becomes the recommended domain-semantic policy for director evidence runs.

### 6.2 Wait model

Default wait mode for the refinery lane is:

- `block_until_settled`

Meaning:

- a scheduled checkpoint triggers the refinery request,
- the runner pumps the runtime/adapter path until the request settles,
- then the run records the terminal checkpoint outcome,
- and only then continues simulation.

This is intentional. Headless evidence should attribute outcomes to concrete checkpoints rather than let requests float asynchronously across later ticks.

### 6.3 Terminal checkpoint outcomes

Each scheduled refinery checkpoint must end in one of these terminal states:

- `applied`
- `apply_failed`
- `request_failed`

The runner should never silently skip or lose a scheduled checkpoint after it has been accepted for execution.

## 7. Enforced Operational Guardrails

These are code-enforced defaults, not documentation-only preferences.

### 7.1 General lane guardrails

- `core` remains the default lane.
- `refinery` is explicit opt-in.
- `refinery_live_paid` is explicit opt-in.
- generic `all` mode must not silently include `refinery_live_paid`.

### 7.2 Paid live guardrails

- default `maxTriggers = 1`
- hard cap `maxTriggers = 3`
- default `REFINERY_RETRY_COUNT = 0`
- default capture mode = `hash`
- explicit timeout required
- explicit settle timeout required
- explicit cost estimate logged before the run starts
- explicit paid confirmation required
- no-cost rehearsal proof required before paid
- paid concurrency hard-locked to `1` in Wave 8.6
- Wave 8.6 estimated completion hard cap = `8`

### 7.3 Cost ceiling model

For `refinery_live_paid`, the runner should estimate an upper-bound completion budget before launch.

Recommended estimate:

`run_count * checkpoints_per_run * (REFINERY_RETRY_COUNT + 1) * (PLANNER_DIRECTOR_MAX_RETRIES + 1)`

This estimate does not need to be financially exact. Its purpose is to stop obviously unsafe run shapes before they start.

Recommended policy:

- warn when the estimated completion count exceeds a low advisory threshold,
- fail fast when it exceeds a hard threshold unless an explicit override is present.

### 7.4 Capture guardrails

- `fixture/mock/validator` default capture = `redacted`
- `live_paid` default capture = `hash`
- `full` capture must never be a default profile behavior

## 8. Runner Env Contract

The runner should expose a refinery-specific env surface instead of overloading raw app/live variables directly.

Recommended runner-facing env vars:

| Env var | Meaning |
|---------|---------|
| `WORLDSIM_SCENARIO_LANE` | `core`, `refinery_fixture`, `refinery_live_mock`, `refinery_live_validator`, `refinery_live_paid` |
| `WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY` | `every_n_ticks`, `tick_list`, `season_boundary` |
| `WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY` | interval for `every_n_ticks` |
| `WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS` | explicit checkpoint list for `tick_list` |
| `WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS` | upper bound for scheduled triggers |
| `WORLDSIM_SCENARIO_REFINERY_WAIT_MODE` | default `block_until_settled` |
| `WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS` | max wait before a checkpoint is treated as failed |
| `WORLDSIM_SCENARIO_REFINERY_CAPTURE` | `none`, `hash`, `redacted`, `full` |
| `WORLDSIM_SCENARIO_REFINERY_CAPTURE_TELEMETRY` | whether to persist Java telemetry snapshots when available |
| `WORLDSIM_SCENARIO_REFINERY_ABORT_ON_APPLY_FAIL` | abort policy toggle |
| `WORLDSIM_SCENARIO_REFINERY_ABORT_ON_REQUEST_FAIL` | abort policy toggle |
| `WORLDSIM_SCENARIO_REFINERY_PAID_PRESET` | `paid_micro_total2`, `paid_probe_2x2x2`, or bounded `custom` |
| `WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM` | explicit local confirmation string for paid runs |
| `WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT` | path to accepted no-cost rehearsal artifact required before paid |
| `WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS` | hard cap for estimated paid completions |
| `WORLDSIM_SCENARIO_REFINERY_COST_ESTIMATE_ONLY` | preflight/dry-run mode for cost estimate without paid calls |

Internal mapping rule:

- the runner may translate these values into the existing `REFINERY_*` runtime options,
- but the runner-facing control surface should remain explicit and lane-aware.

## 9. Artifact Contract

The refinery lane extends the existing SMR artifact bundle. It does not replace it.

Recommended additive layout:

```text
.artifacts/smr/<run-name>/
  manifest.json
  summary.json
  assertions.json
  anomalies.json
  runs/*.json
  refinery/
    summary.json
    index.json
    telemetry.json
    <runKey>/
      checkpoints.json
      checkpoints/
        001.json
        002.json
      responses/
        001.hash.json
        002.hash.json
```

### 9.1 Manifest additions

Recommended additive manifest fields:

- `laneType`
- `refineryEnabled`
- `refineryGoal`
- `checkpointPolicy`
- `checkpointCount`
- `maxTriggers`
- `waitMode`
- `captureMode`
- `requestTimeoutMs`
- `retryCount`
- `directorMaxBudget`
- `refineryAppliedCount`
- `refineryApplyFailedCount`
- `refineryRequestFailedCount`
- `refineryFallbackCount`
- `refineryValidatedCount`

### 9.2 Run summary additions

The refinery summary should include:

- checkpoint count,
- stage histogram,
- apply-status histogram,
- output-mode histogram,
- output-source histogram,
- total and max budget used,
- average and max settle latency,
- directive frequency,
- story severity frequency,
- story hash frequency,
- and response hash frequency.

### 9.3 Checkpoint payload

Each checkpoint record should include:

- trigger index,
- trigger tick,
- season,
- elapsed wall-clock duration,
- stage,
- apply status,
- output mode,
- output source,
- budget used,
- budget marker presence,
- warnings count,
- explain markers,
- action status,
- capture payload according to capture mode,
- optional telemetry snapshot link or data.

## 10. Refinery Assertion And Anomaly Layer

The refinery lane should define its own evidence rules instead of pretending that the existing survival/combat invariant catalog is enough.

### 10.1 Initial invariant family

Recommended `RDIR-*` invariants:

- `RDIR-01`: every scheduled checkpoint reaches a terminal outcome
- `RDIR-02`: every successful director response includes a parseable `directorStage:*` marker
- `RDIR-03`: `apply_failed == 0`
- `RDIR-04`: budget marker presence is consistent with director-path responses
- `RDIR-05`: output mode values stay within the supported set
- `RDIR-06`: output source values stay within the supported set
- `RDIR-07`: bridge output remains applyable by the existing C# runtime
- `RDIR-08`: checkpoint bookkeeping is internally consistent

TR2-D Track D marker contract for Track B consumption:

- `directorStage:*` remains pipeline truth and is not equivalent to solver-backed validation.
- Solver-sidecar truth, when enabled, is exposed only through normalized `directorSolver*` markers.
- `directorSolverPath` values: `unwired`, `sidecar`, `validated_core`, `unavailable`.
- `directorSolverStatus` values: `success`, `non_success`, `load_failure`, `not_run`.
- `directorSolverGeneratorResult` values: Refinery `GeneratorResult` lowercased, or `none`.
- `directorSolverExtraction` values: `success`, `failed`, `empty`, `not_run`.
- `directorSolverValidatedCoverage` is repeated per value: `none`, `story_core`, `directive_core`.
- `directorSolverUnsupported` is repeated per value: `none`, `campaign`, `causalChain`.
- `directorSolverDiagnostic` carries stable diagnostic codes only; raw Java exception text is not a schema field.

Validated coverage is core-only in TR2-D:

- story core: `beatId`, `text`, `durationTicks`, `severity`
- directive core: `colonyId`, `directive`, `durationTicks`
- not solver-validated: `effects`, `biases`, `campaign`, `causalChain`

Track B should parse/store these markers but must not redefine their meaning.

### 10.2 Initial anomaly family

Recommended `ANOM-RDIR-*` anomalies:

- `ANOM-RDIR-REQUEST-FAIL-HIGH`
- `ANOM-RDIR-FALLBACK-HIGH`
- `ANOM-RDIR-LATENCY-HIGH`
- `ANOM-RDIR-STAGE-MISSING`
- `ANOM-RDIR-BUDGET-MARKER-MISSING`
- `ANOM-RDIR-DIRECTIVE-MONOTONY`
- `ANOM-RDIR-STORY-MONOTONY`
- `ANOM-RDIR-COMPLETION-COUNT-HIGH`

Policy note:

- monotony and fallback-rate signals should begin as advisory evidence, not immediate hard blockers,
- while `apply_failed` can become a stricter gate much earlier.

## 11. Rollout Plan

### Phase 1a - `refinery_fixture`

Goal:

- prove the scheduler, wait semantics, artifact flow, and checkpoint accounting on a deterministic lane.

Acceptance:

- one executable / lane split exists,
- refinery fixture checkpoints are scheduled deterministically,
- checkpoint outcomes settle cleanly,
- refinery artifacts are written additively,
- and the new `RDIR-*` layer works without paid/live dependencies.

### Phase 1b - `refinery_live_mock`

Goal:

- prove the real Java HTTP + marker + apply seam while keeping the lane cost-free and operationally simple.

Acceptance:

- the live mock lane runs through the real runtime/adapter path,
- stage/mode/source/apply markers are captured headlessly,
- failures are classified cleanly,
- and the fixture/live_mock profiles share one stable operational contract.

### Phase 2 - `refinery_live_validator`

Goal:

- validate the non-paid live path where validation/fallback semantics are real but LLM cost is still absent.

Acceptance:

- `validated` vs `fallback` outcomes are visible in artifacts,
- budget and warning markers remain operator-readable,
- the validator path is diagnosable without the app,
- and Wave 8.6 paid runs cannot start until this no-cost rehearsal or equivalent staged rehearsal is GREEN.

### Phase 3 - `refinery_live_paid`

Goal:

- add a real paid live evidence lane with explicit caps and no CI-default pressure.

Acceptance:

- paid runs are opt-in,
- trigger cap and cost estimate enforcement works,
- capture defaults remain safe,
- the lane is advisory evidence only,
- `paid_micro_total2` is the first blocking paid preset,
- `paid_probe_2x2x2` is optional unless Meta explicitly promotes it after micro evidence,
- and no paid run may exceed 8 estimated completions in Wave 8.6.

### Phase 4 - Policy maturation

Goal:

- evolve from purely mechanical trigger schedules toward recommended domain-semantic schedules such as `season_boundary`.

Acceptance:

- season-boundary triggering is documented and trustworthy,
- fallback/request-failure policies are operationally explicit,
- and the lane is still bounded and safe.

### Phase 5 - Family expansion prep

Goal:

- prepare the evidence/schema contract to grow beyond director-only usage when combat/campaign refinery families arrive.

Acceptance:

- refinery evidence artifacts are not locked forever to director-only assumptions,
- shared schema pieces are identified conservatively,
- and family-specific overlays remain possible without flattening everything into one monolith.

## 12. Combined Plan Integration

This plan maps onto the future Combined wave structure as follows.

### 12.1 TR2-D

`TR2-D` should remain the Combined-level home for the first refinery headless evidence lane foundation.

Combined should stay high level and only record:

- that the work belongs under `TR2-D`,
- that `TR2-C` is the gate,
- that Track D owns refinery semantics and observability meaning,
- that Track B owns the ScenarioRunner lane/tooling surface,
- and that the first shipped slice is `fixture + live_mock`.

### 12.2 Wave 8.6

`Wave 8.6` is the Combined-level home for the first paid-live LLM Director SMR pilot.

Combined should record:

- that the work is serialized before Wave 9 by current Meta decision,
- that Track D owns paid/validator semantics and scorecard meaning,
- that Track B owns paid/validator ScenarioRunner guardrails and artifacts,
- that SMR Analyst owns the no-cost rehearsal plus paid micro evidence review,
- that `refinery_live_paid` remains local-only and advisory,
- and that paid is still excluded from default `core`, generic `all`, CI, and deterministic baselines.

### 12.3 TR3-B

`TR3-B` should be the Combined-level home for:

- follow-up paid-live guardrail hardening after the Wave 8.6 pilot,
- fallback boundary cleanup as it affects live evidence policy,
- and the shift toward domain-semantic scheduling such as `season_boundary`.

### 12.4 TR3-C

`TR3-C` should be the Combined-level home for:

- family-agnostic refinery evidence/schema preparation,
- conservative reuse across future `combat/` and `campaign/` families,
- and avoiding a director-only dead end for headless refinery evidence.

## 13. Test Strategy

Expected test layers:

- unit tests for scheduler and trigger-cap logic,
- unit tests for wait/settle timeout logic,
- unit tests for capture-mode and artifact serialization rules,
- fixture-lane integration tests,
- live-mock integration tests,
- and targeted bridge/runtime no-regression tests.

Operational policy:

- `refinery_fixture` is the first CI-safe refinery lane,
- `refinery_live_mock` may later become manual or workflow-dispatch-safe,
- `refinery_live_paid` is not CI-default and not part of the first shipped slice.

## 14. Relationship To Existing SMR Operational Docs

This plan complements, not replaces:

- `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md`
- `Docs/Plans/Master/SMR-M2-Evidence-Review-Protocol.md`

Rule:

- those docs remain the project-level operating standard for naming, artifacts, review, and retention,
- while this document defines the refinery-specific lane model, guardrails, and rollout.
