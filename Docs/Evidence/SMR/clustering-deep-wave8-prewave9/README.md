# Clustering Deep Wave8 Pre-Wave9 Advisory Evidence

Status: advisory YELLOW; does not block Wave 8.5
Owner: SMR Analyst
Date: 2026-04-30
Tested commit: `ed5bcb0 docs(smr): plan Wave 9 and 10 closeout gates`

## Run Config

- Profile: `clustering-deep-wave8-prewave9-001`
- Artifact dir: `.artifacts/smr/clustering-deep-wave8-prewave9-001/`
- Evidence timing: early advisory before Wave 8.5, not a hard pre-Wave9 gate by default
- Mode: `assert`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `medium-default` (`128x72`, `InitialPop=48`, `Ticks=2400`) and `standard-default` (`192x108`, `InitialPop=72`, `Ticks=2400`)
- Total runs: `18`
- Exit: `0` / `ok`
- Drilldown: enabled, `topN=10`, `sampleEvery=25`
- Baseline path: none; this is an advisory clustering investigation, not a formal compare-baseline run

Command/env summary:

- `WORLDSIM_SCENARIO_MODE=assert`
- `WORLDSIM_SCENARIO_OUTPUT=json`
- `WORLDSIM_VISUAL_PROFILE=Headless`
- `WORLDSIM_SCENARIO_SEEDS=101,202,303`
- `WORLDSIM_SCENARIO_PLANNERS=simple,goap,htn`
- `WORLDSIM_SCENARIO_DRILLDOWN=true`
- `WORLDSIM_SCENARIO_DRILLDOWN_TOP=10`
- `WORLDSIM_SCENARIO_SAMPLE_EVERY=25`
- `WORLDSIM_SCENARIO_COMPARE=false`, `WORLDSIM_SCENARIO_PERF=false`, `WORLDSIM_SCENARIO_ANOMALY_FAIL=false`, `WORLDSIM_SCENARIO_DELTA_FAIL=false`, `WORLDSIM_SCENARIO_PERF_FAIL=false`
- Direct CLI run: `dotnet run --no-build --project "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj"`

## Healthy Signals

- ScenarioRunner build passed before the run.
- The package completed `18/18` runs with `exitCode=0` and `assertionFailures=0`.
- All survival/economy assertions passed; combat assertions were skipped because the lane is peaceful by design.
- Survival/economy stayed healthy across the package: minimum people `51`, minimum food `5720`, minimum average food per person `80.544`, starvation deaths `0`, and starvation-with-food deaths `0`.
- No AI no-plan, starvation-with-food, perf, compare, or supply anomalies were emitted.
- The run is suitable as early advisory evidence for Wave 9 risk, but not a Wave 8.5 blocker.

## Suspicious Signals

- All `18/18` runs emitted `ANOM-CLUSTER-HIGH-BACKOFF` warnings.
- Normalized warning pressure increased compared with Step 7B's 1200-tick all-around medium/standard subset:
- Step 7B relevant subset: `12/18` warnings, average threshold ratio `1.250`, max threshold ratio `1.852`, average dense-neighborhood tick rate `0.235`.
- This run: `18/18` warnings, average threshold ratio `1.652`, max threshold ratio `2.362`, average dense-neighborhood tick rate `0.261`.
- Standard topology is consistently above threshold: standard average threshold ratio `2.069`, max `2.362`.
- Medium topology is now above threshold in all seeds/planners too, though less severe: medium average threshold ratio `1.303`, max `1.544`.
- Dense-neighborhood rate did not explode, but remains material: standard average dense rate roughly `0.350`, medium average dense rate roughly `0.172`.
- The signal is a movement/occupancy/clustering risk for later army supply/campaign work, not a current survival/economy failure.

## Worst Runs Ranked

1. `standard-default | Htn | seed 101`
- Backoff `2834`, backoff/tick `1.181`, threshold ratio `2.362`, dense rate `0.374`; still `people=79`, `food=7370`, `avgFoodPerPerson=93.291`, `starvationWithFood=0`.

2. `standard-default | Simple | seed 101`
- Backoff `2793`, backoff/tick `1.164`, threshold ratio `2.328`, dense rate `0.398`; still `people=84`, `food=7825`, `avgFoodPerPerson=93.155`, `starvationWithFood=0`.

3. `standard-default | Simple | seed 202`
- Backoff `2509`, backoff/tick `1.045`, threshold ratio `2.091`, dense rate `0.360`; still `people=84`, `food=8231`, `avgFoodPerPerson=97.988`, `starvationWithFood=0`.

4. `standard-default | Goap | seed 202`
- Backoff `2509`, backoff/tick `1.045`, threshold ratio `2.091`, dense rate `0.360`; still `people=84`, `food=8231`, `avgFoodPerPerson=97.988`, `starvationWithFood=0`.

5. `standard-default | Simple | seed 303`
- Backoff `2491`, backoff/tick `1.038`, threshold ratio `2.076`, dense rate `0.310`; still `people=86`, `food=8745`, `avgFoodPerPerson=101.686`, `starvationWithFood=0`.

## Decision

Advisory status: `YELLOW`.

This does not block Wave 8.5. Wave 8.5 is a Director sidecar and can proceed independently.

For Wave 9: GREEN/YELLOW policy allows proceeding with notes, but this evidence should be treated as a visible risk input for `P5-F` and later movement/logistics work. If Wave 8.5 touches `Runtime`, `ScenarioRunner`, `AI movement`, occupancy, or clustering telemetry, this evidence should be rerun or revalidated against the post-Wave8.5 HEAD before Wave 9 starts.

RED was not assigned because there were no assertion failures, no non-zero exit, no survival/economy collapse, and no starvation-with-food anomaly. Track B fix-planning is not mandatory now, but the clustering/backoff trend is strong enough that a later Wave 9-specific rerun is recommended if intervening changes affect movement or telemetry.

## Unknowns

- This is peaceful-only evidence; it does not cover combat/campaign movement behavior.
- Baseline compare was not run because no exact matching baseline was confirmed.
- The package is early advisory evidence before Wave 8.5. It is not final pre-Wave9 evidence if relevant movement/telemetry code changes land before Wave 9.
- The current anomaly model uses a coarse threshold (`backoff > ticks/2`), so review depends on normalized ratios plus outcome health rather than raw counts alone.

## Suggested Next Run

- If Wave 8.5 does not touch relevant movement/occupancy/telemetry paths, use this as advisory YELLOW input and proceed to Wave 9 with caution.
- If Wave 8.5 touches those paths, rerun the same `clustering-deep-wave8-prewave9` matrix against the new HEAD before `P5-F` starts.
- If Wave 9 introduces army/campaign movement, add a later targeted campaign/supply clustering lane rather than treating this peaceful lane as campaign proof.
