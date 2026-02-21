# Refinery Integration Guide

This folder hosts the runtime glue between `WorldSim` and the Java `refinery-service-java` planner service.
Use `F6` in-game to trigger patch calls and observe the HUD status line.

## Quick Profiles

### 1) Local fixture smoke (no Java service)
- Use when: fast UI/apply checks.
- PowerShell:
```powershell
$env:REFINERY_INTEGRATION_MODE="fixture"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```

### 2) Local live mock (Java running, pipeline off)
- Use when: HTTP wiring check only.
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
dotnet run --project WorldSim.App/WorldSim.App.csproj
```

### 3) Local live pipeline + refinery slice
- Use when: Track D smoke for `TECH_TREE_PATCH + addTech`.
- Java:
```powershell
$env:PLANNER_MODE="pipeline"
$env:PLANNER_REFINERY_ENABLED="true"
$env:PLANNER_LLM_ENABLED="false"
./gradlew bootRun
```
- C#:
```powershell
$env:REFINERY_INTEGRATION_MODE="live"
$env:REFINERY_BASE_URL="http://localhost:8091"
$env:REFINERY_APPLY_TO_WORLD="true"
dotnet run --project WorldSim.App/WorldSim.App.csproj
```
- Expected HUD clue: `stage=refineryStage:enabled`.

### 4) Parity test profile
- Use when: fixture/live drift guard in CI or local.
- Requires Java service running.
```powershell
$env:REFINERY_PARITY_TEST="true"
dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj
```

## Environment Variables (C# side)

| Variable | Default | Purpose | Typical use |
| --- | --- | --- | --- |
| `REFINERY_INTEGRATION_MODE` | `off` | `off` / `fixture` / `live` runtime path | Local debug/smoke |
| `REFINERY_BASE_URL` | `http://localhost:8091` | Java service URL in live mode | Live integration |
| `REFINERY_FIXTURE_RESPONSE` | examples tech fixture | Custom fixture response path | Fixture experimentation |
| `REFINERY_TIMEOUT_MS` | `1200` | HTTP timeout per attempt | Slow machine/network tuning |
| `REFINERY_RETRY_COUNT` | `1` | Retry count for retryable errors | Flaky local service |
| `REFINERY_BREAKER_SECONDS` | `10` | Cooldown after repeated failures | Avoid spam retry loops |
| `REFINERY_MIN_TRIGGER_MS` | `500` | Anti-spam trigger interval | Input throttling |
| `REFINERY_REQUEST_SEED` | `123` | Deterministic request seed | Reproducible tests |
| `REFINERY_APPLY_TO_WORLD` | `false` | Apply translated runtime commands (`addTech`) | Domain-side smoke |
| `REFINERY_LENIENT` | `false` | Relax extra JSON field checks | Forward-compat probing |
| `REFINERY_PARITY_TEST` | `false` | Enables fixture-vs-live parity test | CI/local drift check |

## Environment Variables (Java side)

| Variable | Default | Purpose |
| --- | --- | --- |
| `REFINERY_SERVICE_PORT` | `8091` | HTTP port |
| `PLANNER_MODE` | `mock` | `mock` or `pipeline` |
| `PLANNER_REFINERY_ENABLED` | `false` | Enables minimal Refinery stage in pipeline mode |
| `PLANNER_LLM_ENABLED` | `false` | Enables LLM proposal stage |

## HUD Status Field Guide

Example:
```text
Refinery applied: applied=0, deduped=0, noop=1, techs=2, events=0, hash=482033CD->482033CD, stage=refineryStage:enabled, warn=...
```

- `applied`: operations that changed tracked state.
- `deduped`: same `opId` seen before.
- `noop`: valid op but no effective state change.
- `hash`: canonical state hash before/after apply.
- `stage`: refinery stage marker from Java response (`enabled` / `disabled`).
- `warn`: first warning from Java response if present.

## Failure Playbook

1. **`unknown techId`**
   - Confirm `Tech/technologies.json` contains the ID.
   - Confirm runtime loaded techs (message includes `loadedTechCount=`).
   - Mapping layer is intentionally postponed (see TODO).

2. **`stage=refineryStage:disabled` when expected enabled**
   - Check Java envs: `PLANNER_MODE=pipeline`, `PLANNER_REFINERY_ENABLED=true`.

3. **`circuit open` / throttled triggers**
   - Wait `REFINERY_BREAKER_SECONDS`, or reduce request failures.
   - For frequent manual tests, increase `REFINERY_MIN_TRIGGER_MS` only if needed.

4. **Parity fails**
   - Ensure live request seed/tick/goals match fixture assumptions.
   - Check Java planner mode and marker in response.

## Future alternative to many env vars

To reduce env noise in day-to-day development, preferred next options:

- **In-game debug settings panel** (toggle mode, URL, applyToWorld, timeout/retry).
- **Local config file** (for example `worldsim.refinery.local.json`) loaded at startup in dev mode.
- Keep env vars primarily for CI and scripted runs.

## Todo
- Add a dedicated tech ID mapping file so Java planner IDs can differ safely from `Tech/technologies.json`.
- Persist patch dedupe state (`AppliedOpIds`) once save/load lifecycle is implemented.

## Season Director roadmap (Track D)

The next major Track D feature is a checkpoint-based Season Director that can emit two independent outputs from one snapshot:

- `story beat`
- `planner nudge`

Both outputs are planned to be independently switchable (`both`, `story_only`, `nudge_only`, `off`).

Detailed 3-sprint implementation plan:

- `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md`
