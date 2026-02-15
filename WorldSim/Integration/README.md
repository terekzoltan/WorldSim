# Refinery Integration Guide

This folder hosts the runtime glue between `WorldSim` and the Java `refinery-service-java` patch planner. It helps you trigger deterministic patches (`F6`) and observe the status string rendered in-game.

## Modes

Env variable: `REFINERY_INTEGRATION_MODE`

| Value | Behavior |
| --- | --- |
| `off` (default) | No integration, F6 only toggles the help status. Safe for normal gameplay.
| `fixture` | Applies `refinery-service-java/examples/responses/patch-tech-tree-v1.expected.json` locally. Fast path that doesn't require the Java service.
| `live` | Calls `POST /v1/patch` on the configured Java service. Timeout/retry/circuit breaker guard keeps the loop responsive.

## Common env tweaks

- `REFINERY_BASE_URL` – base URL for live mode (default `http://localhost:8091`).
- `REFINERY_TIMEOUT_MS` – live HTTP timeout in milliseconds (default 1200).
- `REFINERY_RETRY_COUNT` – additional retry attempts for retryable errors (default 1).
- `REFINERY_BREAKER_SECONDS` – seconds to wait after two consecutive live failures (default 10).
- `REFINERY_MIN_TRIGGER_MS` – minimum time between F6 triggers (default 500ms). 
- `REFINERY_REQUEST_SEED` – seed passed to the patch planner, useful for deterministic fixture/livel parity.
- `REFINERY_APPLY_TO_WORLD` – `true` if you want the integration runtime to invoke `TechTree.Unlock` when `addTech` arrives (currently only `agriculture`).
- `REFINERY_PARITY_TEST` – turning this on runs the parity unit test that compares fixture vs live hash (requires live service). Only meant for dev/CI.
- `REFINERY_LENIENT` – set `true` to relax strict parsing (extra JSON fields allowed, unknown ops still fail).

## Status bar meaning

In `Game1.Draw` the status line shows:

```
Refinery applied: applied=1, deduped=0, noop=0, techs=2, events=0, hash=32EA7108->1712AD13
```

- `applied` – number of ops that mutated the tracked patch state.
- `deduped` – ops dropped because their `opId` already appeared.
- `noop` – ops that were validated but left the state unchanged (common when live rolling the same patch repeatedly).
- `techs/events` – counts of tracked techs/world events.
- `hash` – canonical SHA256 of the tracked state before/after apply.
- If throttled or in circuit breaker, the status reports that instead.

## Debug playbook

1. **Styled test** (fixture path only):
   - `set REFINERY_INTEGRATION_MODE=fixture` + `dotnet run --project WorldSim/WorldSim.csproj`
   - Press `F6` once → expect `applied=1`. Press again quickly → expect `noop=1` or `throttled`.

2. **Live test (with Java service)**:
   - In `refinery-service-java`: `./gradlew bootRun` (port 8091 by default).
   - `set REFINERY_INTEGRATION_MODE=live`, optionally `REFINERY_BASE_URL=http://localhost:8091`, `REFINERY_APPLY_TO_WORLD=true`.
   - `dotnet run --project WorldSim/WorldSim.csproj` → press `F6`. Observe HTTP success in Java logs and updated status line.
   - If you get `Refinery trigger throttled` or `circuit open`, wait for `REFINERY_MIN_TRIGGER_MS` / `REFINERY_BREAKER_SECONDS` before retrying.

3. **Parity sanity check (CI/local)**:
   - Requires live service running.
   - `set REFINERY_PARITY_TEST=true` and run `dotnet test WorldSim.RefineryClient.Tests/WorldSim.RefineryClient.Tests.csproj`.
   - The fixture hash must match the live patch hash; otherwise service and fixture outputs have diverged.

4. **Failure handling**:
   - If you see `Cannot apply addTech: unknown techId 'X'`, add the missing tech to `Tech/technologies.json` or extend the Java -> C# mapping (see `Integration/TODO.md`).
   - For random HTTP errors, check `refinery-service-java/logs` and make sure you don’t exceed `REFINERY_TIMEOUT_MS`.

## Todo
- Add a dedicated tech ID mapping file so the Java planner can emit techs that differ from what's present in `Tech/technologies.json`.
- Persist `SimulationPatchState` (`AppliedOpIds`) when the game saves/loads so deduping survives restarts.
