# Wave 8.6 W8.6-D1 - Track D Policy Lock Handoff

Status: GREEN

Owner: Track D

Audience: Meta Coordinator, Track B, SMR Analyst

## Scope Decision

W8.6-D1 is a policy lock with one explicit Java config mapping. No Java source, bridge patch, or director pipeline behavior change is needed for this slice because the current Java director path already exposes the required marker and telemetry surface:

- `llmStage:*`
- `llmCompletionCount:*`
- `llmRetryRounds:*`
- `llmCandidateSanitized:*`
- optional `llmCandidateSanitizeTags:*`
- `budgetUsed:*`
- `causalChainOps:*`
- optional `directorSolver*` markers when solver observability is enabled
- `/v1/director/telemetry` aggregate snapshot

Track D did not enable paid behavior and did not change bridge patch semantics. The only Java-side implementation change is the explicit `application.yml` mapping for `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED`.

## Java Paid Profile Policy

Java defaults remain safe:

- `PLANNER_LLM_ENABLED=false`
- `PLANNER_LLM_MODEL=openai/gpt-5.4-mini`
- `PLANNER_DIRECTOR_MAX_RETRIES=2`
- `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=false` maps explicitly to `planner.director.solverObservabilityEnabled` in `refinery-service-java/src/main/resources/application.yml`

The Java default `PLANNER_DIRECTOR_MAX_RETRIES=2` is not the paid preset default. Wave 8.6 paid presets must set retry policy explicitly:

| Preset | Java retry setting | Expected completion cap | Notes |
|--------|--------------------|-------------------------|-------|
| `paid_micro_total2` | `PLANNER_DIRECTOR_MAX_RETRIES=0` | `2` | First blocking paid micro evidence target. |
| `paid_probe_2x2x2` | `PLANNER_DIRECTOR_MAX_RETRIES=1` | `8` | Guarded optional probe; not an automatic closeout blocker unless Meta promotes it after micro evidence. |

Paid Java runs require all of the following:

- `PLANNER_MODE=pipeline`
- `PLANNER_LLM_ENABLED=true`
- local `PLANNER_LLM_API_KEY` outside the repo
- explicit paid ScenarioRunner confirmation from Track B tooling
- no-cost rehearsal proof before paid execution

Recommended for Wave 8.6 rehearsal and paid pilots:

- `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED=true` to enable `directorSolver*` markers through the explicit Java property mapping

## Completion And Retry Semantics

Use these meanings consistently in Track B artifacts and SMR review:

| Term / marker | Meaning |
|---------------|---------|
| completion | One OpenRouter response request made by Java. |
| Java correction retry | An additional completion inside one `/v1/patch` request after validator feedback. |
| C# request retry | An additional runtime/adapter HTTP request attempt after request failure. |
| `llmCompletionCount` | Observed completion count for one `/v1/patch` request. |
| `llmRetryRounds` | Java validator feedback/correction rounds used inside that request. |
| `llmRetries` | Legacy alias for validator retry rounds; do not use as the primary Wave 8.6 artifact contract. |
| `retryAttemptsTotal` telemetry | Aggregate extra completions beyond the first completion in each director request. |
| `validationRetryRoundsTotal` telemetry | Aggregate Java validator retry rounds. |

Paid cost estimation belongs to Track B tooling and should include both retry dimensions:

```text
run_count * checkpoints_per_run * (REFINERY_RETRY_COUNT + 1) * (PLANNER_DIRECTOR_MAX_RETRIES + 1)
```

For Wave 8.6 paid presets, `REFINERY_RETRY_COUNT=0` and concurrency is `1`.

## Marker Contract For Track B

Track B may parse and summarize these Java markers, but must not redefine their meaning:

| Marker family | Meaning |
|---------------|---------|
| `directorStage:*` | Pipeline truth. |
| `directorOutputMode:*` | Java-side output-mode marker. |
| `llmStage:*` | Java LLM stage classification. |
| `llmCompletionCount:*` | Per-request observed completion count. |
| `llmRetryRounds:*` | Per-request Java validator correction rounds. |
| `llmCandidateSanitized:*` | Whether Java-side normalization repaired the LLM candidate. |
| `llmCandidateSanitizeTags:*` | Optional stable sanitize tag list when sanitization occurred. |
| `budgetUsed:*` | Java-computed director influence budget used by the emitted patch. |
| `causalChainOps:*` | Number of causal-chain ops in the emitted patch. |
| `directorSolverPath:*` | Solver-sidecar path truth. |
| `directorSolverStatus:*` | Solver-sidecar status truth. |
| `directorSolverGeneratorResult:*` | Solver generator result or `none`. |
| `directorSolverExtraction:*` | Solver output extraction status. |
| `directorSolverValidatedCoverage:*` | Repeated core-only coverage values. |
| `directorSolverUnsupported:*` | Repeated unsupported feature values. |
| `directorSolverDiagnostic:*` | Optional stable diagnostic code only. |

Truth-in-labeling rules:

- `directorStage:refinery-validated` does not imply solver-backed validation.
- `directorSolver*` is the only solver-sidecar truth family.
- `directorSolverValidatedCoverage:story_core` covers only `beatId`, `text`, `durationTicks`, and `severity`.
- `directorSolverValidatedCoverage:directive_core` covers only `colonyId`, `directive`, and `durationTicks`.
- `effects`, `biases`, `campaign`, and `causalChain` are not solver-validated in Wave 8.6.

## Telemetry Endpoint Policy

`/v1/director/telemetry` is recommended when Track B enables `WORLDSIM_SCENARIO_REFINERY_CAPTURE_TELEMETRY=true` or an equivalent telemetry-capture setting.

It is not hard-required for every Wave 8.6 artifact if per-checkpoint explain markers already provide the required evidence. When captured, telemetry should be treated as aggregate context, while checkpoint markers remain the per-checkpoint truth source.

## Formal / Refinery Scorecard Terms

D1 does not require a complete `INV-*` catalog. Track B and SMR evidence should expose stable categories and known marker families.

Formal/refinery quality categories:

| Category | Required interpretation |
|----------|-------------------------|
| Pipeline validity | Classify `directorStage:*` as validated, fallback, mock/pass-through, or request/apply failure context. |
| Solver-sidecar status | Classify `directorSolverPath`, `directorSolverStatus`, `directorSolverExtraction`, and coverage markers separately from pipeline stage. |
| Warnings and invariant IDs | Preserve stable warning IDs and `INV-*` style codes when present; do not persist raw exception text as schema. |
| Budget markers | Record whether budget markers such as `budgetUsed:*` are present for director responses. |
| Unsupported claims | Surface non-`none` `directorSolverUnsupported:*` values. |
| Stable diagnostics | Surface `directorSolverDiagnostic:*` stable codes when present. |

SMR Analyst owns the final scorecard verdict. Track D only defines formal/refinery quality semantics.

## Secret And Capture Policy

Wave 8.6 paid evidence is local-only and advisory.

Do not commit or persist:

- API keys
- raw auth headers
- raw paid response payloads
- full paid request/response capture

Paid capture default is `hash`. `full` capture is not allowed for Wave 8.6 paid presets. Director creativity review under hash capture should rely on hashes, frequencies, directive distributions, and operator notes rather than raw paid text.

## Track B Handoff

Track B owns implementation of:

- `refinery_live_validator`
- `refinery_live_paid`
- paid preset parsing
- explicit paid confirmation
- rehearsal artifact validation
- completion cap estimation and enforcement
- artifact shape and scorecard persistence
- no-paid regression tests for `core`, generic `all`, and CI-safe paths

Track D constraints for B1:

- do not redefine Java marker meanings,
- do not treat `directorStage:*` as solver-backed validation,
- do not require full paid capture,
- keep paid excluded from default `core`, generic `all`, and CI,
- keep `paid_probe_2x2x2` guarded and optional unless Meta later promotes it.

W8.6-B1 may start: YES.
