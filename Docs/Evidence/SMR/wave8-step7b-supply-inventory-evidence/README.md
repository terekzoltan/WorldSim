# Wave 8 Step 7B Supply Inventory Evidence

Status: accepted by SMR Analyst; recommend Wave 8 closeout
Owner: SMR Analyst
Date: 2026-04-29
Tested commit: `7b7a2ce Wave 12 sentence`

## Run Config

Primary peaceful all-around package:

- Profile: `all-around-smoke-wave8-001`
- Artifact dir: `.artifacts/smr/all-around-smoke-wave8-001/`
- Mode: `assert`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `small-default` (`64x40`, `InitialPop=24`, `Ticks=1200`), `medium-default` (`128x72`, `InitialPop=48`, `Ticks=1200`), `standard-default` (`192x108`, `InitialPop=72`, `Ticks=1200`)
- Total runs: `27`
- Exit: `0` / `ok`
- Drilldown: enabled, `topN=6`, `sampleEvery=25`
- Baseline path: none; no exact matching broad Wave 8 baseline was confirmed

Supply-focused package:

- Profile: `wave8-step7b-supply-focused-001`
- Artifact dir: `.artifacts/smr/wave8-step7b-supply-focused-001/`
- Mode: `standard`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Config: `supply-storehouse-refill-consumption` (`32x20`, `InitialPop=12`, `Ticks=8`, `Dt=0.25`, `SupplyScenario=storehouse_refill_consumption`)
- Total runs: `9`
- Exit: `0` / `ok`
- Drilldown: enabled, `topN=9`, `sampleEvery=1`
- Baseline path: none; this deterministic lane is new Step 7A/7B evidence, not a compare lane

Environment hardening applied for both packages:

- Explicit visual lane: `WORLDSIM_VISUAL_PROFILE=Headless`
- Compare, perf, anomaly-fail, delta-fail, and perf-fail gates were disabled unless explicitly part of the package
- Perf mode remained out of scope for Step 7B

## Healthy Signals

- Primary package completed `27/27` runs with `exitCode=0`, `assertionFailures=0`, and `effectiveVisualLane=Headless`.
- Primary package exported a `supply` block for all `27/27` runs.
- Primary package survival/economy remained healthy: minimum living colonies `4`, minimum people `24`, minimum food `894`, minimum average food per person `37.25`, starvation deaths `0`, and starvation-with-food deaths `0`.
- Supply-focused package completed `9/9` runs with `exitCode=0`, `anomalyCount=0`, and `effectiveVisualLane=Headless`.
- Supply-focused package exercised the Wave 8 supply path in every planner/seed run: `inventoryFoodConsumed=2`, `carriersWithFood=1`, `totalCarriedFood=3`, `coloniesWithBackpacks=1`, and `coloniesWithRationing=1` in all `9/9` runs.
- Supply-focused drilldown timelines contain compact `supply` fields in all selected runs and prove transient carried-food visibility: first samples show `inventoryFoodConsumed=1` and `totalCarriedFood=4`; final samples show `inventoryFoodConsumed=2` and `totalCarriedFood=3`.
- No starvation deaths, no starvation-with-food deaths, no AI no-plan anomalies, and no supply-specific blockers appeared in either package.

## Suspicious Signals

- Primary all-around package produced `12` warning anomalies, all `ANOM-CLUSTER-HIGH-BACKOFF`.
- The clustering warnings are concentrated in `standard-default` (`9`) plus `medium-default` seed `101` (`3`).
- These warnings do not block Wave 8 closeout under the Step 7B policy because exit/assertions are green, survival/economy are healthy, and supply-specific evidence is clean.
- Primary package is peaceful/supply-oriented all-around evidence. It is not combat/campaign coverage because `EnableCombatPrimitives=false` and `EnableDiplomacy=false`.
- Primary package default configs are not expected to organically exercise non-zero supply behavior; they prove supply block presence. Non-zero inventory/carry/backpack/rationing behavior is proven by the dedicated supply-focused package.
- Supply-focused lane intentionally includes low-food samples (`averageFoodPerPerson` minimum `0.4167`) as a deterministic supply exercise. It produced no starvation or starvation-with-food deaths.

## Worst Runs Ranked

1. `standard-default | Simple | seed 101`
- `ANOM-CLUSTER-HIGH-BACKOFF`, backoff `1111` vs threshold `<=600`; still `people=83`, `food=3984`, `avgFoodPerPerson=48`, `starvationWithFood=0`.

2. `standard-default | Htn | seed 101`
- `ANOM-CLUSTER-HIGH-BACKOFF`, backoff `1078` vs threshold `<=600`; still `people=79`, `food=3845`, `avgFoodPerPerson=48.67`, `starvationWithFood=0`.

3. `standard-default | Simple | seed 303`
- `ANOM-CLUSTER-HIGH-BACKOFF`, backoff `1008` vs threshold `<=600`; still `people=81`, `food=4573`, `avgFoodPerPerson=56.46`, `starvationWithFood=0`.

4. `standard-default | Goap | seed 303`
- `ANOM-CLUSTER-HIGH-BACKOFF`, backoff `1008` vs threshold `<=600`; still `people=81`, `food=4573`, `avgFoodPerPerson=56.46`, `starvationWithFood=0`.

5. `standard-default | Htn | seed 202`
- `ANOM-CLUSTER-HIGH-BACKOFF`, backoff `957` vs threshold `<=600`; still `people=74`, `food=4301`, `avgFoodPerPerson=58.12`, `starvationWithFood=0`.

Supply-focused worst runs are the low-food seed `101` cases across planners (`score=5.833`, reason `low_food_per_person`), but all still pass the supply acceptance checks with zero anomalies and zero starvation deaths.

## Step 7B Supply Answers

- Supply behavior exercised: yes, in `9/9` supply-focused runs.
- Inventory food consumed: yes, `inventoryFoodConsumed=2` in `9/9` supply-focused runs.
- Carried food visible: yes, final `totalCarriedFood=3` and timeline max `totalCarriedFood=4` in `9/9` supply-focused runs.
- Backpack state visible: yes, `coloniesWithBackpacks=1` in `9/9` supply-focused runs.
- Rationing state visible: yes, `coloniesWithRationing=1` in `9/9` supply-focused runs.
- Wave 8 closeout recommended: yes.

## Unknowns

- No exact matching broad Wave 8 baseline was confirmed, so baseline compare was not used as a Step 7B gate.
- Perf mode was intentionally out of scope for Step 7B.
- Combat/campaign behavior is not covered by this peaceful all-around package.
- Clustering warning pressure remains visible in larger peaceful configs and should stay on the later tuning/observability radar, but it is not a Wave 8 supply closeout blocker.

## Suggested Next Run

- If Meta Coordinator accepts this evidence, close Wave 8 and unblock the next sequenced supply/campaign work.
- If clustering pressure becomes a blocker for later campaign/supply movement, run a dedicated `clustering-deep` or standard-topology follow-up instead of reopening Wave 8 supply evidence.
- Do not promote a new canonical broad baseline from this package without a separate baseline decision.

## Decision Recommendation

`Wave 8 SMR evidence accepted by SMR Analyst; recommend Wave 8 closeout.`

The completed evidence package satisfies Step 7B: all-around peaceful health is assertion-green, the supply artifact surface is present in all runs, the dedicated supply lane exercises inventory consumption and carried food across all seed/planner combinations, backpack/rationing state is visible, and no survival/economy/supply blocker was found.
