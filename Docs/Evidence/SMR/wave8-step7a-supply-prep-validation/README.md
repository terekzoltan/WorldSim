# Wave 8 Step 7A Supply Prep Validation

Status: accepted by SMR Analyst for Step 7B handoff
Owner: SMR Analyst
Date: 2026-04-28
Track B commit: `fd0e457 feat(wave8): add supply SMR telemetry`

## Run Config

- Profile: `wave8-step7a-supply-prep-validation-001`
- Artifact dir: `.artifacts/smr/wave8-step7a-supply-prep-validation-001/`
- Mode: `standard`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101`
- Planners: `simple,goap,htn`
- Config: `supply-storehouse-refill-consumption`
- Supply scenario: `storehouse_refill_consumption`
- Scenario shape: `32x20`, `InitialPop=12`, `Ticks=8`, `Dt=0.25`, `BirthRateMultiplier=0.0`
- Drilldown: enabled, `topN=3`, `sampleEvery=1`
- Exit: `0` / `ok`

Environment hardening applied:

- Explicit false reset: `WORLDSIM_SCENARIO_ASSERT`, `WORLDSIM_SCENARIO_PERF`, `WORLDSIM_SCENARIO_ANOMALY_FAIL`, `WORLDSIM_SCENARIO_DELTA_FAIL`, `WORLDSIM_SCENARIO_PERF_FAIL`
- Explicit visual lane: `WORLDSIM_VISUAL_PROFILE=Headless`

## Required Field Results

| Requirement | Result |
|-------------|--------|
| Run-level `supply` block exists | PASS |
| `inventoryFoodConsumed` visible | PASS: `2` in all 3 planner runs |
| Carried-food evidence visible | PASS: final `carriersWithFood=1`, `totalCarriedFood=3` in all 3 planner runs |
| Backpack state visible | PASS: `coloniesWithBackpacks=1` in all 3 planner runs |
| Rationing state visible | PASS: `coloniesWithRationing=1` in all 3 planner runs |
| Compact timeline `supply` fields visible | PASS: all selected drilldown timelines include `supply` |
| Transient carried-food proof | PASS: timeline max `totalCarriedFood=4`, final `3` |
| Old-baseline compatibility | PASS: `SupplyTelemetryArtifactTests.Compare_OldBaselineWithoutSupplyBlock_StillParses` |

## Healthy Signals

- Validation package completed with `exitCode=0` and `anomalyCount=0`.
- All three planner modes produced identical required supply evidence: `inventoryFoodConsumed=2`, `carriersWithFood=1`, `totalCarriedFood=3`, `coloniesWithBackpacks=1`, `coloniesWithRationing=1`.
- Drilldown timelines prove the carried-food state is observable over time: first sample shows `inventoryFoodConsumed=1`, `totalCarriedFood=4`; final sample shows `inventoryFoodConsumed=2`, `totalCarriedFood=3`.
- No starvation deaths, no starvation-with-food deaths, no no-progress backoff, and no anomalies were reported in the validation package.
- Old summaries without the new `supply` block remain compare-parse compatible via the focused test.

## Suspicious Signals

- The supply-focused validation lane intentionally has low average food per person (`0.4167`) because it is a short deterministic supply exercise, not a balance baseline.
- Combat assertions are skipped because combat primitives are disabled for this supply prep lane.
- The first rebuild-included focused test invocation timed out before a final test summary; the no-build rerun of the same supply test filter passed `4/4`.

## Decision

`supply prep sufficient for Step 7B`.

The Track B export/config surface is sufficient for final Wave 8 SMR evidence: artifacts can answer whether carried food exists, whether inventory food was consumed, whether backpack/rationing state is visible, and whether compact drilldown timelines expose supply behavior.

## Suggested Next Run

- Proceed to Step 7B with the full Wave 8 SMR package.
- Include this supply-focused lane alongside the required `101,202,303 x simple,goap,htn` evidence target unless Meta Coordinator approves a narrower exception.
- Keep using explicit env reset and `WORLDSIM_VISUAL_PROFILE=Headless` for comparable SMR evidence.
