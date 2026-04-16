# Director Live Smoke Checklist (Wave 6.1.1)

Status: Active
Owner: Track D
Last updated: 2026-03-24

Purpose:
- Final Wave 6.1.1 operator checklist for live `SEASON_DIRECTOR_CHECKPOINT` smoke.
- Freeze manual regression verification across atomicity, status semantics, request diagnostics, and Java retry/repair observability.

## Preconditions

- Java service reachable at `http://localhost:8091`.
- App running with refinery trigger (`F6`).
- Goal is `SEASON_DIRECTOR_CHECKPOINT`.

## Recommended Live Profile

Java:

```powershell
$env:PLANNER_MODE="pipeline"
$env:PLANNER_REFINERY_ENABLED="true"
$env:PLANNER_LLM_ENABLED="true"
$env:PLANNER_LLM_API_KEY="<your-openrouter-key>"
./gradlew bootRun
```

C#:

```powershell
$env:REFINERY_INTEGRATION_MODE="live"
$env:REFINERY_BASE_URL="http://localhost:8091"
$env:REFINERY_GOAL="SEASON_DIRECTOR_CHECKPOINT"
$env:REFINERY_APPLY_TO_WORLD="true"
$env:REFINERY_DIRECTOR_OUTPUT_MODE="auto"
$env:REFINERY_TIMEOUT_MS="12000"
$env:REFINERY_RETRY_COUNT="0"
$env:REFINERY_DIRECTOR_DAMPENING="1.0"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```

## Expected Marker Semantics

Terminology lock (TU1-D1):
- `preset` = named control action bundle (hotkey cycle)
- `profile` = currently active operator-facing label
- `lane` = integration transport lane (`off|fixture|live`)
- `requested` = operator requested output mode state
- `mode` = effective output mode on response/apply status lines

- Always-visible operator summary should read like: `Dir: eff=<mode> <apply> | requested=<requested> | profile=<profile> | lane=<lane>`.
- Debug-visible operator detail should expose: `stage`, `effSrc`, `reqSrc`, `profileSrc`.
- HUD director detail block includes: `mode=<...> apply=<...>` plus debug-only `stage/src/cd` detail.
- For Season Director live path, `stage` should be `directorStage:*` (not the legacy `refineryStage:*` marker family).
- Raw adapter status text is no longer the default green-path HUD line.
- On failure, diagnostics should surface `Refinery apply failed: outcome=<apply_failed|request_failed>, ...` and preserve request-failure kind taxonomy.
- Trigger-start status line uses disambiguated control-state wording:
  - `Refinery request started: ... lane=<...>, ... requested=<...>(<...>)`

`apply` meanings:
- `applied`: response arrived and C# apply completed.
- `apply_failed`: response arrived, but C# translate/apply path failed.
- `request_failed`: no usable response reached apply phase.
- `not_triggered`: no completed checkpoint yet.

## Core Manual Smoke

1. Press `F6` once.
2. Verify always-visible operator summary updates (`eff`, `apply`, `req`, `profile`, `lane`).
3. Verify debug-visible director detail updates (`stage`, `src`, `cd`, budget/detail block when applicable).
4. On failure, verify diagnostics preserve `Refinery apply failed: outcome=...` payload and request-failure kind detail.
5. If `apply=applied`, verify at least one gameplay-visible director effect (story beat feed or directive).
6. Verify trigger throttling/cooldown still prevents rapid accidental retriggers.

## Regression Matrix

### Case A - Success
- Trigger: valid response, valid apply path.
- Expected:
  - Summary: `eff=<mode> applied | requested=<...> | profile=<...> | lane=<...>`
  - Debug detail: `stage=directorStage:*`, `mode/src` consistent
  - budget reflects current checkpoint commit

### Case B - Apply failure after response
- Trigger: response arrives but C# apply fails (e.g. invalid runtime command target).
- Expected:
  - Summary/HUD: `apply=apply_failed`
  - Failure diagnostics: `Refinery apply failed: outcome=apply_failed, ...`
  - `stage/mode/src/budget` still reflect response-level truth
  - budget remains last committed checkpoint state (no new commit on apply failure)

### Case C - Request failure before response
- Trigger: timeout / refused connection / HTTP error before usable response.
- Expected:
  - Summary/HUD: `apply=request_failed`
  - Failure diagnostics: `Refinery apply failed: outcome=request_failed, ...`
  - `error` detail includes `kind=timeout|connection_refused|http_<status>|request_error` and attempts
  - stage may remain `not_triggered/unknown`
  - budget remains last committed checkpoint state (no reset/consume)

### Case D - Deterministic fallback
- Trigger: validation retries exhausted.
- Expected:
  - explain includes `directorStage:fallback-deterministic`
  - warning contains fallback marker
  - output is still contract-valid and applyable

### Case E - Retry-heavy validated output
- Trigger: initial candidate invalid, later retry validates.
- Expected:
  - explain includes `directorStage:refinery-validated`
  - `llmCompletionCount > 1`
  - `llmRetryRounds > 0` (legacy alias: `llmRetries > 0`)
  - if planner sanitize happened: `llmCandidateSanitized:true` (+ optional tags)

## Evidence Capture

For each matrix case collect:
- HUD director line snapshot (`stage/apply/mode/src/budget`)
- Top status line (`Refinery applied` or `Refinery apply failed` with outcome)
- Java explain markers from response (`directorStage`, `llmCompletionCount`, `llmRetryRounds`, `llmCandidateSanitized`, `budgetUsed`)
- If present, also capture `llmStage` to distinguish disabled config from parse/request failures.
- Optional Java telemetry snapshot: `curl http://localhost:8091/v1/director/telemetry`

## Pass/Fail Rules

- Pass only if all five regression matrix cases are reproducible and observed semantics match expected output.
- Any mismatch between response-level stage markers and local apply outcome is fail.
- Any budget reset/consume on request/apply failure is fail.
- Any inability to answer completion count or sanitize occurrence from markers/logs is fail.

## Wave 6.1 Contract Guardrails

- Story beat effect duration follows parent beat duration in the Java pipeline.
- If a live run appears to fail with a duration mismatch, treat it as regression and capture raw response for triage.

## Retry / OpenRouter Note

- One manual `F6` may trigger `1..(PLANNER_DIRECTOR_MAX_RETRIES+1)` OpenRouter completions within a single `/v1/patch` request.
- This is expected behavior of the iterative correction loop, not duplicate user input.
- Use Java explain markers to disambiguate usage:
  - `llmStage:<...>` = disabled vs missing config vs candidate vs parse/request failure state
  - `llmCompletionCount:<n>` = actual completion calls
  - `llmRetryRounds:<n>` = validator retry rounds
  - `llmCandidateSanitized:<true|false>` (+ optional tags) = planner-side repair happened before validation

## Optional Checks

- Optional parity lane:
  - `$env:REFINERY_PARITY_TEST="true"`
  - `dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj`
