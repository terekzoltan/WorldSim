# Director Live Smoke Checklist (Wave 6.1)

Status: Active
Owner: Track D
Last updated: 2026-03-24

Purpose:
- Manual smoke checklist for the live `SEASON_DIRECTOR_CHECKPOINT` path after Wave 6.1 hardening.
- Validate contract freeze (D6.1-A), apply observability split (D6.1-B), and operator-facing status semantics.

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

- HUD director line includes: `stage=<...> apply=<...> mode=<...> src=<...> budget=<...>`.
- Top status line starts with either:
  - `Refinery applied: ...`
  - `Refinery apply failed: outcome=<apply_failed|request_failed>, ...`

`apply` meanings:
- `applied`: response arrived and C# apply completed.
- `apply_failed`: response arrived, but C# translate/apply path failed.
- `request_failed`: no usable response reached apply phase.
- `not_triggered`: no completed checkpoint yet.

## Core Manual Smoke

1. Press `F6` once.
2. Verify director HUD updates (`stage`, `apply`, `mode`, `src`, `budget`).
3. Verify status line updates with `Refinery applied:` or `Refinery apply failed:` outcome payload.
4. If `apply=applied`, verify at least one gameplay-visible director effect (story beat feed or directive).
5. Verify trigger throttling/cooldown still prevents rapid accidental retriggers.

## Failure Matrix Smoke

- **Case A — response + apply success**
  - HUD: `apply=applied`.
  - Status: `Refinery applied: ...`.
  - `stage/mode/src` are non-empty and consistent.

- **Case B — response + apply failure**
  - HUD: `apply=apply_failed`.
  - Status: `Refinery apply failed: outcome=apply_failed, ...`.
  - `stage/mode/src/budget` still reflect response-level truth.

- **Case C — request failure before response**
  - HUD: `apply=request_failed`.
  - Status: `Refinery apply failed: outcome=request_failed, ...`.
  - `stage` may remain `not_triggered`/unknown, which is expected.

## Wave 6.1 Contract Guardrails

- Story beat effect duration follows parent beat duration in the Java pipeline.
- If a live run appears to fail with a duration mismatch, treat it as regression and capture raw response for triage.

## Retry / OpenRouter Note

- One manual `F6` may trigger `1..(PLANNER_DIRECTOR_MAX_RETRIES+1)` OpenRouter completions within a single `/v1/patch` request.
- This is expected behavior of the iterative correction loop, not duplicate user input.

## Optional Checks

- Java telemetry snapshot:
  - `curl http://localhost:8091/v1/director/telemetry`
- Optional parity lane:
  - `$env:REFINERY_PARITY_TEST="true"`
  - `dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj`
