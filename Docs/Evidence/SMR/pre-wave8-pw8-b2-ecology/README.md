# Pre-Wave8 PW8-B2 Ecology Evidence

Status: accepted by SMR Analyst; Meta Coordinator closeout pending
Owner: SMR Analyst
Date: 2026-04-25
Tested commit: `463aeb0 feat(pre-wave8): stabilize ecology balance`

## Run Config

Primary compare package:

- Profile: `compare-baseline-pw8-b2-ecology-001`
- Artifact dir: `.artifacts/smr/compare-baseline-pw8-b2-ecology-001/`
- Baseline path: `.artifacts/smr/baseline-candidate-pw8-b1-ecology-001/summary.json`
- Mode: `standard` with `WORLDSIM_SCENARIO_COMPARE=true`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `pw8-b1-default` (`64x40`, `InitialPop=24`, `Ticks=1200`) and `pw8-b1-medium-stress` (`128x72`, `InitialPop=48`, `Ticks=1800`)
- Total runs: `18`
- Exit: `0` / `ok`
- Compare identity: `matchedRunCount=18`, `currentOnlyRunKeys=[]`, `baselineOnlyRunKeys=[]`
- Drilldown: enabled, `topN=4`, `sampleEvery=25`

Stress-focus compare package:

- Profile: `compare-baseline-pw8-b2-ecology-stress-focus-001`
- Artifact dir: `.artifacts/smr/compare-baseline-pw8-b2-ecology-stress-focus-001/`
- Baseline path: `.artifacts/smr/baseline-candidate-pw8-b1-ecology-stress-focus-001/summary.json`
- Mode: `standard` with `WORLDSIM_SCENARIO_COMPARE=true`
- Output: `json`
- Visual lane: `Headless` (`visualLaneSource=env`)
- Seeds: `101,202,303`
- Planners: `simple,goap,htn`
- Configs: `pw8-b1-medium-stress` only (`128x72`, `InitialPop=48`, `Ticks=1800`)
- Total runs: `9`
- Exit: `0` / `ok`
- Compare identity: `matchedRunCount=9`, `currentOnlyRunKeys=[]`, `baselineOnlyRunKeys=[]`
- Drilldown: enabled, `topN=4`, `sampleEvery=25`

Environment hardening applied for both runs:

- Explicit false reset: `WORLDSIM_SCENARIO_ASSERT`, `WORLDSIM_SCENARIO_PERF`, `WORLDSIM_SCENARIO_ANOMALY_FAIL`, `WORLDSIM_SCENARIO_DELTA_FAIL`, `WORLDSIM_SCENARIO_PERF_FAIL`
- Explicit visual lane: `WORLDSIM_VISUAL_PROFILE=Headless`
- Explicit predator-human policy: `EnablePredatorHumanAttacks=false`

## Healthy Signals

- Both evidence packages completed with `exitCode=0` and `exitReason=ok`.
- Compare identity is clean in both packages: no current-only keys and no baseline-only keys.
- Compare produced `0` pass-to-fail regressions and `0` threshold breaches in both packages.
- Assertions had `0` failures in both packages; combat assertions were skipped because combat primitives are disabled for this ecology baseline lane.
- Effective `ecologyBalance` is consistent across current PW8-B2 runs: `0.04` animal replenishment chance per second, `1.0` predator replenishment chance, `18` food regrowth minimum seconds, `18` food regrowth jitter seconds.
- Default lane predator collapse materially improved: final predators moved `0 -> 14`, zero-predator ticks moved `4704 -> 67`, and predator replenishment moved `0 -> 20` across the 9 default-lane runs.
- Default lane herbivore pressure improved: zero-herbivore ticks moved `984 -> 0`; final herbivores moved `42 -> 39`, which is a small end-count decrease but without extinction windows.
- Default lane economy improved: average food per person moved `36.33 -> 41.99`, food remained positive, and depleted food nodes moved `283 -> 260`.
- Medium-stress lane predator collapse materially improved: final predators moved `0 -> 9`, zero-predator ticks moved `10554 -> 24`, and predator replenishment moved `0 -> 24` across the 9 stress runs.
- Medium-stress lane herbivore pressure stayed safe: zero-herbivore ticks remained `0 -> 0`, final herbivores moved `68 -> 137`, and people moved `506 -> 527`.
- Stress-focus drilldown confirms selected stress runs end with living predator populations and only `2..3` zero-predator ticks, rather than sustained predator extinction.

## Suspicious Signals

- Clustering warnings increased from `3` in the PW8-B1 stress baseline to `5` in the PW8-B2 stress evidence packages.
- The warning type is unchanged: `ANOM-CLUSTER-HIGH-BACKOFF`, category `clustering`, severity `warning`.
- Existing stress seed `101` warnings are broadly comparable but not fully cleaner: `simple` and `goap` moved `1238 -> 1235`, while `htn` moved `1181 -> 1222`.
- Two new stress warnings appeared for `htn` seed `202` (`952`) and `htn` seed `303` (`968`), both just above the `<=900` threshold.
- Medium-stress depleted food nodes increased `674 -> 783`, and average food per person moved `86.91 -> 85.60`; this is not a threshold breach, but it should remain visible in later balance reviews.
- The primary package's generic drilldown ranking still is not ecology-aware and selected default-lane zero-score runs, so the stress-focus package remains necessary for stress-lane inspection.

## Worst Runs Ranked

1. `pw8-b1-default | Goap | seed 303`
- Zero-predator ticks improved `585 -> 30`; zero-herbivore ticks stayed `0 -> 0`.
- End state: `herbivores=5`, `predators=1`, `predatorReplenishmentSpawns=1`, `depletedFoodNodes=25`, `avgFoodPerPerson=43.43`.

2. `pw8-b1-default | Simple | seed 303`
- Zero-predator ticks improved `585 -> 30`; zero-herbivore ticks stayed `0 -> 0`.
- End state: `herbivores=5`, `predators=1`, `predatorReplenishmentSpawns=1`, `depletedFoodNodes=25`, `avgFoodPerPerson=43.43`.

3. `pw8-b1-medium-stress | Htn | seed 303`
- Zero-predator ticks improved `1185 -> 3`; zero-herbivore ticks stayed `0 -> 0`.
- End state: `herbivores=22`, `predators=1`, `predatorReplenishmentSpawns=3`, `depletedFoodNodes=106`, `avgFoodPerPerson=92.78`.

4. `pw8-b1-medium-stress | Goap | seed 202`
- Zero-predator ticks improved `1244 -> 3`; zero-herbivore ticks stayed `0 -> 0`.
- End state: `herbivores=15`, `predators=1`, `predatorReplenishmentSpawns=3`, `depletedFoodNodes=105`, `avgFoodPerPerson=80.74`.

5. `pw8-b1-medium-stress | Simple | seed 202`
- Zero-predator ticks improved `1244 -> 3`; zero-herbivore ticks stayed `0 -> 0`.
- End state: `herbivores=15`, `predators=1`, `predatorReplenishmentSpawns=3`, `depletedFoodNodes=105`, `avgFoodPerPerson=80.74`.

## Unknowns

- Ecology-specific automated compare thresholds still do not exist; ecology improvement is a manual SMR review based on `summary.json` and drilldown timelines.
- `predatorHumanHits=0` remains structural for these baseline lanes because `EnablePredatorHumanAttacks=false`.
- This evidence does not test the optional predator-pressure holdout lane with predator-human attacks enabled.
- This evidence does not validate the later closed-loop ecology redesign, plant propagation, herbivore lifecycle, predator reproduction, or generalized carrying-capacity system.
- The clustering warning increase is not an ecology collapse, but it is real enough to track as a separate no-progress/backoff follow-up if it blocks later Wave 8 readability or balance work.

## Suggested Next Run

- If Meta Coordinator accepts this evidence, close the Pre-Wave8 addendum and allow Wave 8 inventory work to start on this tuned ecology state.
- Do not promote a canonical ecology baseline automatically; if a baseline refresh is desired, use this same matrix as the candidate and make that a separate Meta Coordinator decision.
- If follow-up capacity exists before or during Wave 8, run a narrow clustering/no-progress check for `pw8-b1-medium-stress | htn | seeds 101,202,303` rather than retuning ecology immediately.
- Keep `EnablePredatorHumanAttacks=true` as a future dedicated predator-pressure holdout lane, not as part of the default acceptance baseline.

## Decision Recommendation

`PW8-B2 evidence accepted by SMR Analyst; recommend Meta Coordinator close Pre-Wave8 and unblock Wave 8.`

The tuned current-model ecology state is materially better than PW8-B1 for predator persistence, no longer shows trivial default predator collapse as the expected outcome, does not worsen herbivore extinction pressure, and does not introduce survival/economy compare regressions. The remaining clustering warnings are non-ecology caveats and should be tracked separately.
