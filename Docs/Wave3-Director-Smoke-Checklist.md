# Wave 3 Director Smoke Checklist

Status: Active
Owner: Track D
Last updated: 2026-03-06

Purpose:
- Manual smoke checklist for `S3-D` parity + runtime verification.
- Focus on `SEASON_DIRECTOR_CHECKPOINT` behavior via fixture/live trigger path.

## Preconditions

- Java service running on `http://localhost:8091`.
- App runs with refinery trigger enabled (`F6` path).
- `REFINERY_GOAL=SEASON_DIRECTOR_CHECKPOINT`.

Recommended baseline env:

```powershell
$env:REFINERY_INTEGRATION_MODE="fixture"
$env:REFINERY_GOAL="SEASON_DIRECTOR_CHECKPOINT"
$env:REFINERY_APPLY_TO_WORLD="true"
$env:REFINERY_DIRECTOR_OUTPUT_MODE="auto"
$env:REFINERY_DIRECTOR_DAMPENING="1.0"
```

## Marker Smoke (Java)

- Run mode matrix marker checks:

```powershell
./scripts/run-smoke.ps1 -ExpectedMode both
./scripts/run-smoke.ps1 -ExpectedMode story_only
./scripts/run-smoke.ps1 -ExpectedMode nudge_only
./scripts/run-smoke.ps1 -ExpectedMode off
```

- Expect `PASS` each run.

## App Smoke (F6)

- Trigger once with `F6`.
- Verify event feed contains a `[Director]` beat entry (story mode enabled).
- Verify active directive appears (nudge mode enabled).
- Verify no rapid retrigger when cooldown/throttle is active.

## Dampening Smoke

1. Set:

```powershell
$env:REFINERY_DIRECTOR_DAMPENING="0.0"
```

2. Trigger `F6` with `REFINERY_APPLY_TO_WORLD=true`.
3. Verify director status and markers are present, but gameplay multipliers stay neutral.
4. Restore:

```powershell
$env:REFINERY_DIRECTOR_DAMPENING="1.0"
```

## Parity Gate

- C# fixture replay test passes for director expected state.
- Optional live parity test (`REFINERY_PARITY_TEST=true`) shows same canonical hash for fixture vs live for season director.
