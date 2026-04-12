# Refinery Integration Guide

This folder hosts the runtime glue between `WorldSim` and `refinery-service-java`.
Use `F6` in-game to trigger requests and validate director behavior from HUD + status line.

## Quick Profiles

### 1) Local fixture smoke (no Java service)
- Use when: fast adapter/runtime smoke.
```powershell
$env:REFINERY_INTEGRATION_MODE="fixture"
$env:REFINERY_GOAL="SEASON_DIRECTOR_CHECKPOINT"
$env:REFINERY_APPLY_TO_WORLD="true"
$env:REFINERY_OPERATOR_PRESET="fixture_smoke"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```

### 2) Local live mock (Java running, LLM off)
- Use when: HTTP + marker wiring check.
- Java:
```powershell
$env:PLANNER_MODE="mock"
$env:PLANNER_REFINERY_ENABLED="false"
$env:PLANNER_LLM_ENABLED="false"
./gradlew bootRun
```
- C#:
```powershell
$env:REFINERY_INTEGRATION_MODE="live"
$env:REFINERY_BASE_URL="http://localhost:8091"
$env:REFINERY_GOAL="SEASON_DIRECTOR_CHECKPOINT"
$env:REFINERY_APPLY_TO_WORLD="true"
$env:REFINERY_OPERATOR_PRESET="live_mock"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```

### 3) Local live director profile (D6.1 baseline)
- Use when: real `SEASON_DIRECTOR_CHECKPOINT` manual smoke.
- Java:
```powershell
$env:PLANNER_MODE="pipeline"
$env:PLANNER_REFINERY_ENABLED="true"
$env:PLANNER_LLM_ENABLED="true"
$env:PLANNER_LLM_API_KEY="<your-openrouter-key>"
./gradlew bootRun
```
- C#:
```powershell
$env:REFINERY_INTEGRATION_MODE="live"
$env:REFINERY_BASE_URL="http://localhost:8091"
$env:REFINERY_GOAL="SEASON_DIRECTOR_CHECKPOINT"
$env:REFINERY_APPLY_TO_WORLD="true"
$env:REFINERY_OPERATOR_PRESET="live_director"
$env:REFINERY_DIRECTOR_OUTPUT_MODE="auto"
$env:REFINERY_TIMEOUT_MS="12000"
$env:REFINERY_RETRY_COUNT="0"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```
- Why `12000` + retry `0`: one Java request may internally call OpenRouter `1..(maxRetries+1)` times.

### 4) Parity profile
- Use when: fixture/live drift guard.
```powershell
$env:REFINERY_PARITY_TEST="true"
dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj
```

## Environment Variables (C# side)

| Variable | Default | Purpose |
| --- | --- | --- |
| `REFINERY_INTEGRATION_MODE` | `off` | `off` / `fixture` / `live` runtime path |
| `REFINERY_GOAL` | `TECH_TREE_PATCH` | Goal routed to Java (`SEASON_DIRECTOR_CHECKPOINT` for director smoke) |
| `REFINERY_BASE_URL` | `http://localhost:8091` | Java service URL in live mode |
| `REFINERY_FIXTURE_RESPONSE` | goal-based default fixture | Custom fixture response path |
| `REFINERY_TIMEOUT_MS` | `1200` | HTTP timeout per attempt |
| `REFINERY_RETRY_COUNT` | `1` | C# HTTP retry count for retryable failures |
| `REFINERY_BREAKER_SECONDS` | `10` | Circuit-breaker cooldown after repeated failures |
| `REFINERY_MIN_TRIGGER_MS` | `500` | Manual trigger anti-spam interval |
| `REFINERY_REQUEST_SEED` | `123` | Deterministic request seed |
| `REFINERY_APPLY_TO_WORLD` | `false` | Apply translated runtime commands |
| `REFINERY_DIRECTOR_OUTPUT_MODE` | `auto` | `auto|both|story_only|nudge_only|off` |
| `REFINERY_OPERATOR_PROFILE` | auto-resolved | Optional in-app operator profile label shown in Settings/HUD |
| `REFINERY_OPERATOR_PRESET` | unset | Optional startup preset: `fixture_smoke|live_mock|live_director` |
| `REFINERY_DIRECTOR_MAX_BUDGET` | `5` | Runtime-side director checkpoint budget cap |
| `REFINERY_LENIENT` | `false` | Relax extra JSON field checks |
| `REFINERY_PARITY_TEST` | `false` | Enables fixture-vs-live parity test |

## Environment Variables (Java side)

| Variable | Default | Purpose |
| --- | --- | --- |
| `REFINERY_SERVICE_PORT` | `8091` | HTTP port |
| `PLANNER_MODE` | `mock` | `mock` or `pipeline` |
| `PLANNER_REFINERY_ENABLED` | `false` | Enables formal validator stage in pipeline mode |
| `PLANNER_LLM_ENABLED` | `false` | Enables OpenRouter proposal stage |
| `PLANNER_LLM_API_KEY` | empty | OpenRouter API key |
| `PLANNER_LLM_BASE_URL` | `https://openrouter.ai/api/v1` | OpenRouter base URL |
| `PLANNER_LLM_MODEL` | `openai/gpt-5.4-mini` | LLM model |
| `PLANNER_LLM_TIMEOUT_MS` | `3000` | Single completion timeout |
| `PLANNER_LLM_HTTP_REFERER` | `https://worldsim.local` | OpenRouter referer header |
| `PLANNER_LLM_APP_TITLE` | `WorldSim` | OpenRouter app title header |
| `PLANNER_LLM_TEMPERATURE` | `0.4` | Sampling temperature |
| `PLANNER_LLM_MAX_TOKENS` | `500` | Completion token cap |
| `PLANNER_DIRECTOR_OUTPUT_MODE` | `both` | Java-side output mode selection |
| `PLANNER_DIRECTOR_MAX_RETRIES` | `2` | Max iterative correction retries |
| `PLANNER_DIRECTOR_BUDGET` | `5.0` | Director checkpoint budget limit |

## HUD + Status Semantics (D6.1)

Director HUD line:
```text
Director: stage=<directorStage:*> apply=<not_triggered|applied|apply_failed|request_failed> mode=<...> src=<...> cd=<...> budget=<...>
```

Top status line (`GameHost`):
```text
Refinery applied: ...
Refinery apply failed: outcome=<apply_failed|request_failed>, stage=..., mode=..., source=..., budget=..., error=...
```

`request_failed` error taxonomy (top status line `error=...`):
- `kind=timeout`
- `kind=connection_refused`
- `kind=http_<status>`
- `kind=request_error`

Interpretation rules:
- `stage`: Java response-level stage marker for season director (`directorStage:*`).
- `apply`: C# local outcome.
- `mode/src`: effective output mode and decision source (`response|env|fallback|...`).
- `budget`: director budget marker mirrored from Java explain if present.
- Java explain also includes `llmStage`, `llmCompletionCount`, `llmRetryRounds`, and `llmCandidateSanitized` markers for director retry/repair observability.
- S7-A causal-chain wire/request observability remains marker-based:
  - `causalChainOps:<n>`
  - `causalChainMaxTriggers:1`
  - `causalChainMetrics:food_reserves_pct,morale_avg,population,economy_output`
  - `causalChainEqPolicy:population_exact;floating_tolerance=0.0001`

## In-game operator controls (S7-B A-part)

- `F6`: trigger refinery request.
- `Ctrl+F6`: cycle requested director output mode (`auto -> both -> story_only -> nudge_only -> off -> auto`) without restart.
- `Ctrl+Shift+F6`: cycle operator preset (`fixture_smoke -> live_mock -> live_director -> ...`) without restart.
- `Ctrl+F12`: settings overlay now includes director profile/mode/source/stage/apply status.

Operational seam lock for Track A consume:
- stable profile label: `fixture_smoke|live_mock|live_director`
- stable requested mode vocabulary: `auto|both|story_only|nudge_only|off`
- stable requested mode source labels: `env|profile|operator`
- stable integration lane label: `off|fixture|live`

Smoke lane note:
- `java_planner_smoke` is the PowerShell helper lane (`run-smoke.ps1` / `check-markers.ps1`) and validates Java response markers only.
- `full_stack_smoke` is a manual app/runtime lane: run the app, use `F6`, and verify HUD/settings/apply state end-to-end.

## Failure Playbook

1. **`apply=apply_failed`**
   - Java response arrived and stage/mode/source are valid.
   - Failure happened during C# translate/apply path.
   - Check `LastDirectorActionStatus` in HUD status line for exact runtime exception.

2. **`apply=request_failed`**
   - Request failed before a usable Java response reached apply.
   - Check Java service health, URL, timeout, or API key/network issues.
   - Status line now includes failure kind (`timeout`, `connection_refused`, `http_<status>`, `request_error`) and the actual attempt count performed.
   - Budget policy: request failure does not reset or consume checkpoint budget; HUD keeps last committed checkpoint budget values.

3. **`circuit open` / throttled triggers**
   - Wait `REFINERY_BREAKER_SECONDS`, or reduce repeated failures.
   - Keep manual trigger cadence above `REFINERY_MIN_TRIGGER_MS`.

4. **Director contract mismatch suspicion**
   - Wave 6.1 contract freeze keeps story effect duration equal to parent beat duration.
   - If mismatch appears, capture response JSON and raise as regression.

## Notes

- One manual `F6` may cause multiple OpenRouter completions (`1..(PLANNER_DIRECTOR_MAX_RETRIES+1)`) inside one `/v1/patch` request.
- Use Java telemetry endpoint for quick counters during live smoke: `GET /v1/director/telemetry`.
- Final Wave 6.1.1 manual regression matrix and pass/fail checklist: `Docs/Wave3-Director-Smoke-Checklist.md`.
