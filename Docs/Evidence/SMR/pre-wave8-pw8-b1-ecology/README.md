# Pre-Wave8 PW8-B1 Ecology Evidence

Status: accepted for PW8-B2 handoff
Owner: SMR Analyst
Date: 2026-04-22

## Run Config

Primary baseline package:

- Profile: `baseline-candidate-pw8-b1-ecology-001`
- Artifact dir: `.artifacts/smr/baseline-candidate-pw8-b1-ecology-001/`
- Mode: `standard`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=default`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `pw8-b1-default` (`64x40`, `InitialPop=24`, `Ticks=1200`) and `pw8-b1-medium-stress` (`128x72`, `InitialPop=48`, `Ticks=1800`)
- Total runs: `18`
- Exit: `0` / `ok`
- Drilldown: enabled, `topN=4`, `sampleEvery=25`
- Baseline path: none

Supplementary stress-focus package:

- Profile: `baseline-candidate-pw8-b1-ecology-stress-focus-001`
- Artifact dir: `.artifacts/smr/baseline-candidate-pw8-b1-ecology-stress-focus-001/`
- Mode: `standard`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=default`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `pw8-b1-medium-stress` only (`128x72`, `InitialPop=48`, `Ticks=1800`)
- Total runs: `9`
- Exit: `0` / `ok`
- Drilldown: enabled, `topN=4`, `sampleEvery=25`
- Baseline path: none

## Healthy Signals

- Both artifact bundles completed with `exitCode=0` and `exitReason=ok`.
- Run-level `summary.json` includes the new `ecology` block for all reviewed runs.
- Drilldown `timeline.json` includes compact ecology samples, including herbivore/predator counts, food node state, replenishment counters, and zero-species counters.
- The artifact surface is sufficient to identify species disappearance timing without opening runtime code.
- The supplementary stress-focus package covers the `pw8-b1-medium-stress` lane directly, because the primary package's generic drilldown ranking selected only default-lane runs due to all selected scores being `0`.

## Suspicious Signals

- All `18/18` primary runs ended with `predators=0` and `predatorReplenishmentSpawns=0`.
- Herbivore zero-species pressure appeared in `2/18` primary runs, both on `pw8-b1-default` seed `101`.
- Medium-stress runs did not show herbivore extinction, but showed sustained predator extinction with `ticksWithZeroPredators` in the `1041..1244` range.
- The only anomalies in both packages were clustering warnings: `ANOM-CLUSTER-HIGH-BACKOFF` on `pw8-b1-medium-stress` seed `101` for `simple`, `goap`, and `htn`.
- These clustering warnings should be tracked separately from the ecology surface sufficiency decision.

## Worst Runs Ranked

1. `pw8-b1-default | Simple | seed 101`
- `ticksWithZeroHerbivores=492`, `firstZeroHerbivoreTick=456`
- `ticksWithZeroPredators=422`, `firstZeroPredatorTick=779`
- End state: `herbivores=2`, `predators=0`, `depletedFoodNodes=24`

2. `pw8-b1-default | Goap | seed 101`
- `ticksWithZeroHerbivores=492`, `firstZeroHerbivoreTick=456`
- `ticksWithZeroPredators=422`, `firstZeroPredatorTick=779`
- End state: `herbivores=2`, `predators=0`, `depletedFoodNodes=24`

3. `pw8-b1-medium-stress | Htn | seed 202`
- `ticksWithZeroHerbivores=0`
- `ticksWithZeroPredators=1244`, `firstZeroPredatorTick=557`
- End state: `herbivores=5`, `predators=0`, `depletedFoodNodes=77`

4. `pw8-b1-medium-stress | Simple | seed 101`
- `ticksWithZeroHerbivores=0`
- `ticksWithZeroPredators=1113`, `firstZeroPredatorTick=688`
- End state: `herbivores=13`, `predators=0`, `depletedFoodNodes=106`

5. `pw8-b1-medium-stress | Goap | seed 101`
- `ticksWithZeroHerbivores=0`
- `ticksWithZeroPredators=1113`, `firstZeroPredatorTick=688`
- End state: `herbivores=13`, `predators=0`, `depletedFoodNodes=106`

## Unknowns

- `predatorHumanHits` should not be interpreted as a healthy signal in these baseline packages.
- At the time these artifacts were captured, the baseline matrix did not enable the predator-human attack path, so `predatorHumanHits=0` is expected and structural for this run profile.
- Commit `bccc23d` later added per-config `EnablePredatorHumanAttacks` support to `WORLDSIM_SCENARIO_CONFIGS_JSON`; that is a useful follow-up capability for future predator-pressure stress lanes, not a replacement for this baseline matrix.
- Ecology-specific compare/assert thresholds do not exist yet. This evidence review is therefore a manual artifact sufficiency review, not an automated ecology gate.
- Generic drilldown scoring is not ecology-aware. If a future run must inspect a specific ecology lane, use a lane-focused package or update drilldown scoring later.

## Suggested Next Run

- During `PW8-B2`, run the same primary matrix against the first accepted tuning candidate and compare manually against this evidence note.
- If the tuned state needs stronger pressure separation, use the same `pw8-b1-medium-stress` lane with `Ticks=2400` as the next focused run.
- Use `EnablePredatorHumanAttacks=true` only as an optional, lane-specific predator-pressure holdout/stress lane, not as the default stabilization baseline.

## Decision

`PW8-B1 evidence sufficient for PW8-B2`.

The ecology artifact surface is sufficient for current-model stabilization work: run-level and timeline artifacts expose species counts, zero-species timing, food-node pressure, and replenishment dynamics well enough to start `PW8-B2` without tuning blind.
