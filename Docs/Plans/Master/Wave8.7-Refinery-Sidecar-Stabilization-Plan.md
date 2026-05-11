# Wave 8.7 - Refinery Sidecar Stabilization Plan

Status: planned, pending approval
Owner: Meta Coordinator
Primary implementer: Track D

## Goal

Stabilize the optional Tools.Refinery director solver-sidecar before Wave 9 starts, without running more paid LLM calls.

The concrete W8.6 finding is:

- paid `paid_micro_total2` passed runtime/paid guardrails,
- but paid checkpoints reported `directorSolverStatus=load_failure` and `directorSolverValidatedCoverage=none`,
- Track D consult suspects an extraction/observability bug around `major` / `epic` story severity nodes, not a paid/runtime failure.

Wave 8.7 should turn that suspicion into a no-paid reproducible test, then fix the smallest real sidecar bug.

## Non-Goals

- Do not run `paid_probe_2x2x2`.
- Do not run any paid LLM request.
- Do not redesign the whole director formal model.
- Do not change C# runtime, ScenarioRunner paid guardrails, or bridge contract semantics unless a tiny diagnostic field is explicitly approved.
- Do not make Tools.Refinery sidecar validation a hard Wave 9 gameplay gate.
- Do not expand campaign/causalChain solver support in this mini-wave.

## Confirmed Facts

- W8.6 no-cost validator rehearsal was GREEN with solver coverage `story_core=2`, `directive_core=2`.
- W8.6 paid micro was YELLOW accepted: paid cap, request/apply, and secret gates passed.
- Paid-only caveat: solver-sidecar load failure on both paid checkpoints.
- Track D consult identified a likely severity-node extraction path:
  - output mapping may emit only one concrete severity node, such as `severity_major`,
  - extractor compares via fixed `trace.getNodeId("severity_minor")`, `severity_major`, `severity_epic`,
  - missing fixed nodes may throw and be reported as `LOAD_FAILURE`.
- Existing Java tests mostly cover `minor` happy path and canonical assembly, not major/epic extraction.

## Assumptions

- The paid patch itself can be represented as no-paid `DirectorOutputAssertions` in a Java unit test.
- The right first reproducer is Java-only and deterministic.
- If the suspected cause is wrong, the next useful artifact is the assembled `.problem` text plus exact exception class/message, not another paid run.
- The human reviewer may know Tools.Refinery modeling better than the assistant, so the plan includes explicit pause points for formal-model review.

## Open Questions

- Does Tools.Refinery require all enum-like nodes (`severity_minor`, `severity_major`, `severity_epic`) to exist before `ProblemTrace.getNodeId` is safe?
- Should the fix be in the `.problem` vocabulary, the output mapper, the extractor, or status taxonomy?
- Should extraction exceptions be classified as `non_success` / `extraction_failed` instead of `load_failure`?
- Do we want to persist assembled diagnostic problem text in test failure output only, or add a guarded debug artifact later?

## Dependency Mapping

- Upstream complete:
  - W8.5 TR2 sidecar marker contract.
  - W8.6 paid guardrails and evidence summary.
- Track D owns:
  - Java `refinery-service-java` sidecar code.
  - `.problem` resources.
  - marker/status semantics for `directorSolver*`.
- SMR Analyst owns:
  - optional no-paid evidence rerun after Track D fix.
- Meta owns:
  - go/no-go decision for Wave 9 after Wave 8.7 result.
- Track B/A/C should not be involved unless the fix unexpectedly changes ScenarioRunner/read-model contracts.

## Files In Scope

Primary Track D files:

- `refinery-service-java/src/main/resources/refinery/director/design.problem`
- `refinery-service-java/src/main/resources/refinery/director/model.problem`
- `refinery-service-java/src/main/resources/refinery/director/runtime.problem`
- `refinery-service-java/src/main/resources/refinery/director/output.problem`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorRefinerySolver.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorValidatedOutputExtractor.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorOutputAssertionsProblemMapper.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorSolverObservability.java`
- `refinery-service-java/src/test/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorRefinerySolverTest.java`
- `refinery-service-java/src/test/java/hu/zoltanterek/worldsim/refinery/planner/ComposedPatchPlannerSolverObservabilityTest.java`

Out of scope unless explicitly approved:

- `WorldSim.Runtime/*`
- `WorldSim.ScenarioRunner/*`
- paid/live API config
- campaign/causalChain solver promotion

## Execution Plan

### W8.7-D1 - Reproduce The Sidecar Failure Without Paid Calls

Owner: Track D

Steps:

1. Add failing Java tests for `major` and `epic` story severity through `DirectorRefinerySolver.solve(...)`.
2. Include story+directive assertions similar to paid micro shape.
3. Assert the desired stable result:
   - no `LOAD_FAILURE`,
   - `DirectorRefinerySolveStatus.SUCCESS` if the core candidate is valid,
   - `validatedCoverage:story_core`,
   - `validatedCoverage:directive_core`.
4. If the test fails differently than expected, capture the exact diagnostic and stop for Meta/Track D review.

Human review hook:

- If the failure is in `.problem` parsing/generation rather than Java extraction lookup, Track D should paste the assembled problem text excerpt and ask for formal-model review before changing constraints.

Acceptance:

- A no-paid test reproduces or falsifies the W8.6 paid caveat.
- No paid service/API is used.

### W8.7-D2 - Apply The Smallest Correct Fix

Owner: Track D

Candidate fix paths, chosen only after D1:

1. Vocabulary fix:
   - ensure known severity nodes exist in the assembled problem vocabulary even when only one severity is used.
2. Extractor fix:
   - avoid unsafe fixed `getNodeId` calls for nodes that may not exist.
   - resolve severity by actual relation target node/name if possible.
3. Status taxonomy fix:
   - distinguish true load/parse failure from extraction exception.
   - do not report extraction failures as `load_failure`.

Preference order:

1. First make extraction robust and correctly classified.
2. Only change `.problem` constraints if a test proves the model vocabulary is incomplete.
3. Avoid adding broad formal rules just to make tests pass.

Acceptance:

- `minor`, `major`, and `epic` story severity cases pass no-paid solver-sidecar tests.
- Invalid `.problem` still reports real `load_failure`.
- Duplicate slot extraction still reports explicit extraction failure / non-success.
- Existing mock/minor observability tests still pass.

### W8.7-D3 - No-Paid Integration Smoke

Owner: Track D, optional SMR Analyst validation

Steps:

1. Run focused Java test suite.
2. Optionally run no-cost `refinery_live_validator` through ScenarioRunner if Meta wants an artifact-level check.
3. Do not run paid.

Recommended Java commands:

```bash
./gradlew test --tests "hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorRefinerySolverTest" --tests "hu.zoltanterek.worldsim.refinery.planner.ComposedPatchPlannerSolverObservabilityTest" --tests "hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetrySolverObservabilityTest"
./gradlew test
```

Optional no-paid ScenarioRunner validation:

```text
PLANNER_MODE=pipeline
PLANNER_REFINERY_ENABLED=true
PLANNER_LLM_ENABLED=false
PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true
PLANNER_DIRECTOR_MAX_RETRIES=0

WORLDSIM_SCENARIO_LANE=refinery_live_validator
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/wave8.7-sidecar-validator-001
WORLDSIM_SCENARIO_SEEDS=101,202
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=4
WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=tick_list
WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS=2
WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS=1
WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0
REFINERY_BASE_URL=http://localhost:8091
```

Acceptance:

- Java tests pass.
- Optional no-paid artifact, if run, has no request/apply failure and no paid completions.

## Human-In-The-Loop Rules

Ask the human/formal-model reviewer before changing `.problem` constraints if any of these happen:

- A new `error` rule is proposed.
- The model needs new cardinality or scope semantics.
- A fix would make the solver accept outputs that Java validator rejects.
- A fix would promote nested `effects`, `biases`, `campaign`, or `causalChain` to solver-validated status.

Do not ask the human for routine Java test/extractor fixes where the behavior is already specified by W8.6 marker semantics.

## Risks / Edge Cases

- The suspected severity-node issue may be wrong; the plan must stop after D1 if the reproducer points elsewhere.
- Tools.Refinery APIs may throw from model trace lookup in ways that are hard to classify; status taxonomy should preserve stable diagnostics without exposing raw exceptions as schema.
- A too-broad `.problem` fix could accidentally weaken formal constraints.
- A too-narrow Java extractor fix could hide real model failures.
- Paid micro output is not stored raw, so reproduction must use a representative no-paid assertion shape, not exact paid text.

## Verification Plan

Minimum before closeout:

```bash
./gradlew test --tests "hu.zoltanterek.worldsim.refinery.planner.refinery.DirectorRefinerySolverTest" --tests "hu.zoltanterek.worldsim.refinery.planner.ComposedPatchPlannerSolverObservabilityTest" --tests "hu.zoltanterek.worldsim.refinery.planner.director.DirectorPipelineTelemetrySolverObservabilityTest"
./gradlew test
```

If C# artifacts are touched unexpectedly:

```bash
dotnet build "WorldSim.sln"
```

But expected W8.7 scope is Java-only.

## Done Criteria

- No-paid major/epic sidecar tests exist.
- The real failure is classified correctly.
- Valid major/epic core outputs no longer become `directorSolverStatus=load_failure`.
- Existing minor/mock/validator paths remain green.
- No paid probe was run.
- Meta records whether Wave 9 can proceed after W8.7.

## Readiness

Status: GREEN to plan-review / Track D kickoff.

Recommended next command:

```text
Target: W8.7-D1/D2
Track: Track D

Plan:
[this document]
```
