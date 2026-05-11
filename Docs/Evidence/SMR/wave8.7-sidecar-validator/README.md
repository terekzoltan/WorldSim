# Wave 8.7 Sidecar Validator Evidence

Status: GREEN accepted
Owner: SMR Analyst
Date: 2026-05-11

## Decision

W8.7-D3 no-paid `refinery_live_validator` artifact check passed after `dfef555 fix(track-d): stabilize director sidecar extraction`.

Meta closeout decision: Wave 9 P5-F may proceed. No paid probe was run.

## Artifact Path

- Validator artifact: `.artifacts/smr/wave8.7-sidecar-validator-001/`

Raw `.artifacts` bundles are local evidence only and should not be committed.

## Command / Env Summary

Java service:

```text
PLANNER_MODE=pipeline
PLANNER_REFINERY_ENABLED=true
PLANNER_LLM_ENABLED=false
PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true
PLANNER_DIRECTOR_MAX_RETRIES=0
REFINERY_SERVICE_PORT=8091
```

ScenarioRunner:

```text
WORLDSIM_SCENARIO_LANE=refinery_live_validator
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/wave8.7-sidecar-validator-001
WORLDSIM_SCENARIO_SEEDS=101,202
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
dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj
```

Service lifecycle: SMR Analyst started the no-paid Java service for this run and stopped it after verification.

## Result

Runner-level verdict: GREEN.

- `exitCode=0`, `exitReason=ok`
- `refineryProfile=validator`
- `checkpointCount=2`
- `appliedCount=2`
- `requestFailedCount=0`
- `applyFailedCount=0`
- `fallbackCount=0`
- `validatedCount=2`
- `anomalies.json`: empty

Sidecar coverage verdict: GREEN.

- `directorSolverPathHistogram`: `validated_core=2`
- `directorSolverStatusHistogram`: `success=2`
- `directorSolverGeneratorResultHistogram`: `success=2`
- `directorSolverExtractionHistogram`: `success=2`
- `directorSolverValidatedCoverageCheckpointCounts`: `story_core=2`, `directive_core=2`
- `directorSolverUnsupportedCheckpointCounts`: `none=2`
- No `directorSolverStatus=load_failure` observed.

Secret/no-paid verdict: GREEN.

- Telemetry baseline before run: `llmCompletionCountTotal=0`
- Telemetry after run: `llmCompletionCountTotal=0`
- `llmStageHistogram`: `disabled=2`
- `observedCompletionCount=0`
- `observedRetryRounds=0`
- `secretscan` findings: `0`
- Artifact-local auth/token regex scan findings: `0`

## Notes

- Fixed timeout values were used: request `30000ms`, wait `60000ms`.
- Artifact collision policy did not require suffix rollover because `wave8.7-sidecar-validator-001` did not exist before the run.
- Java tests were not rerun during D3 because Meta already accepted the W8.7-D1/D2 Java gates; the D3 artifact showed no sidecar regression.
- Combined execution plan marker updates were handled by Meta closeout.
