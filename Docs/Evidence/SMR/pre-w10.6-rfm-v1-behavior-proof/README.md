# Pre-W10.6 RFM-V1 Behavior Proof

Status: GREEN recommendation
Owner: Track D
Date: 2026-06-26

## Decision

RFM-V1 focused behavior proof passed on the required no-paid safe lanes:

- `refinery_fixture`
- `refinery_live_mock`

Both lanes reached one scheduled checkpoint, applied successfully, and recorded zero request/apply failures. This is sufficient for RFM-M2 review from the Track D evidence side.

## Artifact Paths

Raw artifact bundles are local-only and must not be committed.

- Fixture lane: `.artifacts/smr/rfm-v1-refinery-fixture-001/`
- Live mock lane: `.artifacts/smr/rfm-v1-refinery-live-mock-001/`
- Java mock service log: `.artifacts/rfm-v1-java-live-mock.log`

Inspected artifact files:

- `manifest.json`
- `summary.json`
- `refinery/summary.json`
- `runs/*.json`
- `refinery/*/checkpoints/001.json`
- `anomalies.json`

## Commands / Env Summary

Focused Java support check from `refinery-service-java`:

```powershell
.\gradlew.bat test --tests "*ApiControllerTest*" --tests "*PipelineDirectorRefineryEnabledTest*" --tests "*ComposedPatchPlannerSolverObservabilityTest*"
```

Focused C# fixture support check from repo root:

```powershell
dotnet test "WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj" --filter "FullyQualifiedName~DirectorFixtureParityTests"
```

Fixture lane env summary:

```text
WORLDSIM_SCENARIO_LANE=refinery_fixture
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/rfm-v1-refinery-fixture-001
WORLDSIM_SCENARIO_SEEDS=101
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=4
WORLDSIM_SCENARIO_DT=0.25
WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=tick_list
WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS=2
WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS=1
WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0
WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS=30000
WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS=60000
WORLDSIM_SCENARIO_REFINERY_WAIT_MODE=block_until_settled
WORLDSIM_SCENARIO_REFINERY_CAPTURE=redacted
```

Live mock Java service env summary:

```text
REFINERY_SERVICE_PORT=8091
PLANNER_MODE=mock
PLANNER_REFINERY_ENABLED=false
PLANNER_LLM_ENABLED=false
```

Live mock lane env summary:

```text
WORLDSIM_SCENARIO_LANE=refinery_live_mock
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/rfm-v1-refinery-live-mock-001
WORLDSIM_SCENARIO_SEEDS=101
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=4
WORLDSIM_SCENARIO_DT=0.25
WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=tick_list
WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS=2
WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS=1
WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0
WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS=30000
WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS=60000
WORLDSIM_SCENARIO_REFINERY_WAIT_MODE=block_until_settled
WORLDSIM_SCENARIO_REFINERY_CAPTURE=redacted
REFINERY_BASE_URL=http://localhost:8091
```

## Results

| Lane / check | Result | Evidence |
|---|---|---|
| Focused Java support | PASS | Gradle `BUILD SUCCESSFUL`; 5 tasks up-to-date |
| Focused C# fixture support | PASS | 2/2 `DirectorFixtureParityTests` passed |
| `refinery_fixture` | PASS | `exitCode=0`, `exitReason=ok`, `checkpointCount=1`, `refineryAppliedCount=1`, `refineryRequestFailedCount=0`, `refineryApplyFailedCount=0`, `anomalies=[]` |
| `refinery_live_mock` | PASS | `exitCode=0`, `exitReason=ok`, `checkpointCount=1`, `refineryAppliedCount=1`, `refineryRequestFailedCount=0`, `refineryApplyFailedCount=0`, `anomalies=[]` |

Fixture checkpoint evidence:

- `terminalOutcome=applied`
- `applyStatus=applied`
- `stage=directorStage:mock`
- `outputMode=both`
- `outputSource=response`
- `actionStatus` reports `patchApplied=2` and `runtimeCommands=2`

Live mock checkpoint evidence:

- `terminalOutcome=applied`
- `applyStatus=applied`
- `stage=directorStage:mock`
- `outputMode=both`
- `outputSource=response`
- `actionStatus` reports `patchApplied=2` and `runtimeCommands=2`
- Java service was run with `PLANNER_LLM_ENABLED=false`

## Proven

- The deterministic fixture lane can schedule a `SEASON_DIRECTOR_CHECKPOINT`, settle it, apply the patch, and persist refinery artifacts.
- The live mock lane can reach the Java service through HTTP, receive the deterministic mock director response, settle it, apply the patch, and persist refinery artifacts.
- Required RDIR assertions passed for both mandatory lanes.
- Both mandatory lanes produced zero request failures and zero apply failures.
- Both mandatory lanes used redacted/no-paid capture and did not use `refinery_live_paid`.
- Focused Java and C# checks support the fixture/API/adapter behavior used by the evidence pass.

## Not Proven

- Manual app smoke was not run. It is optional for RFM-V1 GREEN and remains not proven by this package.
- `refinery_live_validator` was not run. It is optional supplemental no-paid evidence, not RFM-V1 minimum acceptance.
- Paid/live LLM behavior was not run and is not proven.
- Broad solver-backed formal parity is not proven.
- Effects, biases, campaign, and causal-chain semantics are not proven solver-validated by this package.
- `directorSolverValidatedCoverage:*` was not produced in these mock lanes; this package proves behavior/application, not solver-sidecar coverage.

## Residuals / Routing

- No blocking RFM-V1 residual found.
- Any future ScenarioRunner lane/schema issue should route to Track B, not be fixed under Track D RFM-V1 scope.
- Any future formal/validator drift should route through the RFM-D1/D3/D4 fidelity artifacts or a new approved predicate-promotion/fidelity slice.
- Manual app observations can be collected later if Meta/user wants extra confidence, but they are not required for this GREEN recommendation.

## No-paid / Secret Safety

- No paid lane was run.
- No API key was used.
- Java live mock service used `PLANNER_LLM_ENABLED=false`.
- Evidence snippets include only non-secret env values.

## Formal Overclaim Guardrails

- `directorStage:refinery-validated` must not be treated as full solver-backed validation.
- `directorStage:mock` in this evidence means deterministic mock director behavior, not formal solver parity.
- `directorSolverValidatedCoverage:*`, when present in other lanes, is core-only and not broad formal parity.
- Current formal model coverage remains bounded by RFM-D1/D3/D4: cooldown and active major/epic explicit core conflict coverage are real formal slices; many other invariants remain transitional Java guards or unsupported solver-sidecar surfaces.
