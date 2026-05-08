# Wave 8.6 Paid Live Director Pilot Evidence

Status: YELLOW accepted
Owner: SMR Analyst
Date: 2026-05-05

## Decision

Wave 8.6 SMR1 is accepted as YELLOW evidence. The no-cost validator rehearsal was GREEN, and the paid `paid_micro_total2` pilot stayed within all paid guardrails. The only caveat is formal/refinery observability: both paid checkpoints reported solver-sidecar `load_failure` with validated solver coverage `none`.

Recommendation: Wave 9 may start with this W8.6 YELLOW caveat. Do not run `paid_probe_2x2x2` for this closeout.

## Artifact Paths

- Validator rehearsal: `.artifacts/smr/wave8.6-validator-rehearsal-001/`
- Paid micro: `.artifacts/smr/wave8.6-paid-micro-total2-001/`

Raw `.artifacts` bundles are local evidence only and should not be committed.

## Validator Rehearsal

Run purpose:

- Prove the real runtime/adapter/Java path can execute without paid LLM calls.
- Gate any later paid run with a no-cost artifact.

Command/env summary, secrets omitted:

```text
Java service:
PLANNER_MODE=pipeline
PLANNER_REFINERY_ENABLED=true
PLANNER_LLM_ENABLED=false
PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true
REFINERY_SERVICE_PORT=8091

ScenarioRunner:
WORLDSIM_SCENARIO_LANE=refinery_live_validator
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/wave8.6-validator-rehearsal-001
WORLDSIM_SCENARIO_SEEDS=101,202
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=4
WORLDSIM_SCENARIO_DT=0.25
WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=tick_list
WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS=2
WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS=1
WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0
WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS=15000
WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS=30000
WORLDSIM_SCENARIO_REFINERY_WAIT_MODE=block_until_settled
WORLDSIM_SCENARIO_REFINERY_CAPTURE=redacted
REFINERY_BASE_URL=http://localhost:8091
dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj
```

Result:

- `exitCode=0`, `exitReason=ok`
- `refineryProfile=validator`
- `checkpointCount=2`
- `refineryAppliedCount=2`
- `refineryRequestFailedCount=0`
- `refineryApplyFailedCount=0`
- `refineryFallbackCount=0`
- `refineryValidatedCount=2`
- `observedCompletionCount=0`
- `llmStageHistogram`: `disabled=2`
- `directorSolverStatusHistogram`: `success=2`
- `directorSolverValidatedCoverageCheckpointCounts`: `story_core=2`, `directive_core=2`
- `anomalies.json`: empty
- Secret scan: `0` findings

Verdict: GREEN. This artifact was used as the paid rehearsal gate input.

Note: an earlier no-cost attempt used a shorter request timeout and produced one cold-path timeout. The accepted artifact above is the rerun with explicit longer timeouts and clean request/apply results.

## Paid Micro

Run purpose:

- Execute the smallest approved paid LLM Director pilot through the real runtime/adapter/Java path.
- Verify paid guardrails, cap accounting, artifact shape, and no-secret capture before Wave 9.

Model:

- Expected Java default: `openai/gpt-5.4-mini`
- The artifact did not expose a model field.

Command/env summary, secrets omitted:

```text
Java service:
PLANNER_MODE=pipeline
PLANNER_REFINERY_ENABLED=true
PLANNER_LLM_ENABLED=true
OpenRouter API key set only in the local Java service shell; key name/value omitted
PLANNER_DIRECTOR_MAX_RETRIES=0
PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true
REFINERY_SERVICE_PORT=8091

ScenarioRunner:
WORLDSIM_SCENARIO_LANE=refinery_live_paid
WORLDSIM_SCENARIO_MODE=assert
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/wave8.6-paid-micro-total2-001
WORLDSIM_SCENARIO_SEEDS=101,202
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=4
WORLDSIM_SCENARIO_DT=0.25
WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=tick_list
WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS=2
WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS=1
WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0
WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES=0
WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS=60000
WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS=90000
WORLDSIM_SCENARIO_REFINERY_WAIT_MODE=block_until_settled
WORLDSIM_SCENARIO_REFINERY_CAPTURE=hash
WORLDSIM_SCENARIO_REFINERY_PAID_PRESET=paid_micro_total2
WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS=2
WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM=I_UNDERSTAND_OPENROUTER_COSTS
WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT=.artifacts/smr/wave8.6-validator-rehearsal-001/manifest.json
REFINERY_BASE_URL=http://localhost:8091
dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj
```

Result:

- `exitCode=0`, `exitReason=ok`
- `refineryProfile=live_paid`
- `paidPreset=paid_micro_total2`
- `checkpointCount=2`
- `estimatedCompletions=2`
- `maxCompletions=2`
- `observedCompletionCount=2`
- `expectedJavaDirectorMaxRetries=0`
- `refineryAppliedCount=2`
- `refineryRequestFailedCount=0`
- `refineryApplyFailedCount=0`
- `refineryFallbackCount=0`
- `refineryValidatedCount=0`
- `llmStageHistogram`: `candidate=2`
- `observedRetryRounds=0`
- `llmCandidateSanitizedHistogram`: `true=2`
- `llmCandidateSanitizeTagCounts`: `story_effect_modifier_clamped,directive_bias_weight_clamped=2`
- `totalBudgetUsed=7.8`, `maxBudgetUsed=4.8`
- `anomalies.json`: empty
- Secret scan: `0` findings
- Regex scan for API key/auth/header patterns: `0` matches

## Scorecard

| Block | Status | Evidence | Verdict |
|-------|--------|----------|---------|
| Balance stability | No runner-level regression signal in this tiny refinery lane | `exitCode=0`, no anomalies, request/apply/fallback clean | GREEN for guardrail smoke; not a balance baseline |
| Director creativity | Limited by hash/no-raw capture and B1 artifact shape | `llmStage=candidate=2`, budget used `3.0` and `4.8`, sanitization happened in both checkpoints | YELLOW informational; enough for micro, not enough for qualitative creativity proof |
| Failure hardening | Clean | request failed `0`, apply failed `0`, fallback `0`, hard anomaly `0`, cap respected | GREEN |
| Formal/refinery quality | Solver-sidecar caveat | `directorStage:refinery-validated=2`, but `directorSolverStatus=load_failure=2` and coverage `none=2` | YELLOW |

## Track D Diagnostic Summary

Track D consult classified the paid micro solver-sidecar issue as likely extraction-side solver observability bug, not as a paid-run guardrail failure:

- Major/epic severity node lookup can throw when `severity_minor`, `severity_major`, and `severity_epic` nodes are not all present.
- The exception is mapped to `LOAD_FAILURE` in `DirectorRefinerySolver`.
- This is not likely a classpath/resource loading issue.
- No paid rerun is needed for W8.6-SMR1.
- This is not a Wave 9 blocker if accepted as a YELLOW caveat.

The paid candidate still passed Java formal validation and applied through the runtime/adapter path. The caveat is specifically that solver-sidecar observability did not validate story/directive core coverage during the paid run.

## No-Paid-Probe Decision

`paid_probe_2x2x2` was not run.

Reason:

- Meta accepted W8.6-SMR1 as YELLOW.
- Paid micro already consumed the approved two-completion envelope.
- The remaining issue is diagnostic/formal-refinery quality, not something that should be explored with an additional eight-completion paid probe in this closeout.

## Final Recommendation

Wave 9 may start with the W8.6 YELLOW caveat recorded above.

Recommended follow-up:

- Track D should fix or narrow the solver-sidecar severity-node lookup/load-failure path before relying on paid-run `directorSolverValidatedCoverage` as a quality signal.
- Future paid evidence should continue to use hash/no-full capture and explicit completion caps.
- Do not promote this paid pilot to a deterministic baseline.
