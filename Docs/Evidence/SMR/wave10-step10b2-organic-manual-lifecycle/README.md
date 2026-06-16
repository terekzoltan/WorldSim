# Wave 10 Step10B.2 Organic/Manual Lifecycle Evidence

Status: RED evidence; recommend Track B fix-planning before Wave 10.5
Owner: SMR Analyst
Date: 2026-06-15
Tested workspace: `eb5e7b6` base commit plus local uncommitted Step10B.2-A prep slice

## Decision

Step10B.2 is not closeout-ready.

The full Step10B.2-B package run answered the main lifecycle questions with runtime-backed main-run telemetry and counter-based interpretation, not side-probe inference:

- hostile organic lifecycle did not launch a single campaign in any of the 90 hostile-organic runs,
- pure organic lifecycle also did not launch a single campaign in any of the 90 pure-organic runs,
- manual/operator lifecycle launched successfully in all 90 manual runs and reached meaningful downstream stages in a material subset of them,
- stress coverage found survival assertion failures in three small-topology seed-606 runs,
- therefore the overall gate is RED even though three of the four package manifests exited `0`.

This is a counter-based RED, not a cosmetic one:

- `campaignLaunches=0` across both organic packages,
- `firstEncounterTick/firstSiegeTick/firstResolutionTick=null` across both organic packages,
- `campaignLaunchBlockedByCap/PairCap/HomeDefense/RouteBudget=0` across both organic packages,
- stress package `exitCode=2` with failed `SURV-01`, `SURV-02`, and `SURV-04`.

## Artifact Paths

- Prep/pilot context only:
  - `.artifacts/smr/wave10-step10b2-lifecycle-pilot-001/`
- Package B hostile organic:
  - `.artifacts/smr/wave10-organic-hostile-soak-001/`
- Package C manual operator lifecycle:
  - `.artifacts/smr/wave10-manual-operator-lifecycle-001/`
- Package A pure organic:
  - `.artifacts/smr/wave10-organic-pure-soak-001/`
- Package D stress matrix:
  - `.artifacts/smr/wave10-organic-lifecycle-stress-001/`

Raw `.artifacts` bundles are local evidence only and should not be committed.

## Shared Interpretation Rules

These rules were used for the final readout and must remain the source of truth if this note is referenced later:

- Use runtime-backed main-run `runs[].wave10` fields as truth.
- Do not use `wave10-probes.json` for Step10B.2 lifecycle claims.
- Do not treat `evidenceStatus=positive` as launch proof unless the counters also show launch/lifecycle activity.
- Pure-organic `positive` with `campaignLaunches=0` is rarity/context evidence only.
- Hostile-organic no-launch in the long package is a real blocker, not a pilot-only caveat.
- Route to Track C only if the evidence later proves a strategist/advisory-only blocker. This run set did not prove that.

## Run Config

### Shared package settings

- Mode: `assert`
- Perf: enabled
- Output: `text`
- Visual lane: `Headless`
- Seeds: `101,202,303,404,505,606,707,808,909,1001`
- Planners: `simple,goap,htn`
- Drilldown: enabled
- Artifact family: `.artifacts/smr/<package-name>/`

### Package B: `wave10-organic-hostile-soak-001`

- Artifact dir: `.artifacts/smr/wave10-organic-hostile-soak-001/`
- Configs:
  - `medium-hostile`: `40x40`, `InitialPop=80`, `Ticks=10000`, `Dt=0.25`
  - `standard-hostile`: `64x40`, `InitialPop=96`, `Ticks=10000`, `Dt=0.25`
  - `large-hostile`: `96x64`, `InitialPop=128`, `Ticks=10000`, `Dt=0.25`
- Scenario: `organic_hostile_campaign_lifecycle`

### Package C: `wave10-manual-operator-lifecycle-001`

- Artifact dir: `.artifacts/smr/wave10-manual-operator-lifecycle-001/`
- Configs:
  - `medium-manual`: `40x40`, `InitialPop=80`, `Ticks=6000`, `Dt=0.25`, `Wave10ManualLaunchTick=500`
  - `standard-manual`: `64x40`, `InitialPop=96`, `Ticks=6000`, `Dt=0.25`, `Wave10ManualLaunchTick=500`
  - `large-manual`: `96x64`, `InitialPop=128`, `Ticks=6000`, `Dt=0.25`, `Wave10ManualLaunchTick=500`
- Scenario: `manual_operator_campaign_lifecycle`

### Package A: `wave10-organic-pure-soak-001`

- Artifact dir: `.artifacts/smr/wave10-organic-pure-soak-001/`
- Configs:
  - `medium-pure`: `40x40`, `InitialPop=80`, `Ticks=10000`, `Dt=0.25`
  - `standard-pure`: `64x40`, `InitialPop=96`, `Ticks=10000`, `Dt=0.25`
  - `large-pure`: `96x64`, `InitialPop=128`, `Ticks=10000`, `Dt=0.25`
- Scenario: `organic_campaign_lifecycle`

### Package D: `wave10-organic-lifecycle-stress-001`

- Artifact dir: `.artifacts/smr/wave10-organic-lifecycle-stress-001/`
- Configs:
  - `small-lowpop`: `32x32`, `InitialPop=48`, `Ticks=4000`, `Dt=0.25`
  - `small-highpop`: `32x32`, `InitialPop=80`, `Ticks=4000`, `Dt=0.25`
  - `medium-default`: `40x40`, `InitialPop=80`, `Ticks=4000`, `Dt=0.25`
  - `medium-fastmove`: `40x40`, `InitialPop=80`, `Ticks=4000`, `Dt=0.25`, `MovementSpeedMultiplier=1.5`
  - `standard-default`: `64x40`, `InitialPop=96`, `Ticks=4000`, `Dt=0.25`
  - `standard-fastmove`: `64x40`, `InitialPop=96`, `Ticks=4000`, `Dt=0.25`, `MovementSpeedMultiplier=1.5`
  - `large-lowpop`: `96x64`, `InitialPop=96`, `Ticks=4000`, `Dt=0.25`
  - `large-highpop`: `96x64`, `InitialPop=128`, `Ticks=4000`, `Dt=0.25`
- Scenario: `organic_hostile_campaign_lifecycle`

## Counter-Based Summary

| Package | Exit | Runs | Evidence status mix | Launch runs | Encounter runs | Siege runs | Resolved runs | Convoy runs | Forward-base runs | Scout runs | Siege-unit runs |
|---|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Hostile organic | `0 / ok` | `90` | `proof_unavailable=90` | `0` | `0` | `0` | `0` | `0` | `0` | `0` | `0` |
| Manual lifecycle | `0 / ok` | `90` | `positive=90` | `90` | `44` | `11` | `44` | `0` | `71` | `0` | `0` |
| Pure organic | `0 / ok` | `90` | `positive=90` | `0` | `0` | `0` | `0` | `0` | `0` | `0` | `0` |
| Stress hostile | `2 / assert_fail` | `240` | `proof_unavailable=240` | `0` | `0` | `0` | `0` | `0` | `0` | `0` | `0` |

## Package B: Hostile Organic Soak

### Result

- `exitCode=0`, `exitReason=ok`
- `assertionFailures=0`
- `anomalyCount=70`
- `perfRedCount=70`
- `wave10` status mix: `proof_unavailable=90`

### Counter facts

- `campaignLaunches=0` in all `90/90` runs
- `firstCampaignLaunchTick=null` in all runs
- `firstEncounterTick=null` in all runs
- `firstSiegeTick=null` in all runs
- `firstResolutionTick=null` in all runs
- `convoysSpawned=0` in all runs
- `forwardBasesEstablished=0` in all runs
- `scoutIntelObserved=0` in all runs
- `siegeUnitsSpawned=0` in all runs

### Suppression / blocker counters

All hostile runs also reported zero on the obvious runtime-side blockers:

- `campaignLaunchBlockedByCap=0`
- `campaignLaunchBlockedByPairCap=0`
- `campaignLaunchBlockedByHomeDefense=0`
- `campaignLaunchRouteBudgetExhausted=0`
- `freshScoutIntel=0`
- `campaignTargetsWithScoutIntel=0`

### Interpretation

This is not merely “rare organic launch.” The package never entered the launch pipeline at all. The absence of both launches and suppression counters suggests the next owner should be Track B first:

- runtime cadence,
- runtime precondition construction,
- lifecycle surface correctness,
- or a missing runtime-visible strategist/output signal.

This evidence does **not** yet prove a Track C-only strategist/advisory bug, because no strategist/suppression signal is visible in the counters.

## Package C: Manual Operator Lifecycle

### Result

- `exitCode=0`, `exitReason=ok`
- `assertionFailures=0`
- `anomalyCount=68`
- `perfRedCount=68`
- `wave10` status mix: `positive=90`

### Counter facts

- `manualLaunchAttempted=true` in `90/90` runs
- `manualLaunchSucceeded=true` in `90/90` runs
- `manualLaunchStatus="Created"` in `90/90` runs
- `campaignLaunches>0` in `90/90` runs
- unique launch combinations: `30/30` planner+seed combinations
- `firstAssemblyTick` present in `90/90`, fixed at `499`
- `firstMarchTick` present in `46/90`
- `firstEncounterTick` present in `44/90`
- `firstSiegeTick` present in `11/90`
- `firstResolutionTick` present in `44/90`
- `forwardBasesEstablished>0` in `71/90`
- maximum `longestUnresolvedCampaignAgeTicks=5501`

### Stage timing ranges

- `firstAssemblyTick`: `499 .. 499`
- `firstMarchTick`: `499 .. 5990`
- `firstEncounterTick`: `502 .. 3598`
- `firstSiegeTick`: `562 .. 3599`
- `firstResolutionTick`: `503 .. 3718`

### Per-config stage summary

- `medium-manual`
  - launch `30/30`
  - encounter `14/30`
  - siege `6/30`
  - resolution `14/30`
  - forward base `19/30`
- `standard-manual`
  - launch `30/30`
  - encounter `18/30`
  - siege `2/30`
  - resolution `18/30`
  - forward base `27/30`
- `large-manual`
  - launch `30/30`
  - encounter `12/30`
  - siege `3/30`
  - resolution `12/30`
  - forward base `25/30`

### What did not activate naturally

Across all `90` manual runs:

- `convoysSpawned=0`
- `scoutIntelObserved=0`
- `siegeUnitsSpawned=0`

### Interpretation

The runtime-owned manual launch path is healthy enough to prove:

- launch itself,
- assembly,
- some march progression,
- some encounter,
- some resolution,
- and frequent forward-base creation.

It does **not** prove that supply convoys, scout intel, or dedicated siege units participate naturally under the tested manual lifecycle package.

## Package A: Pure Organic Soak

### Result

- `exitCode=0`, `exitReason=ok`
- `assertionFailures=0`
- `anomalyCount=60`
- `perfRedCount=60`
- `wave10` status mix: `positive=90`

### Counter facts

- `campaignLaunches=0` in `90/90` runs
- `firstCampaignLaunchTick=null` in `90/90`
- `firstEncounterTick=null` in `90/90`
- `firstSiegeTick=null` in `90/90`
- `firstResolutionTick=null` in `90/90`
- no convoys, no forward bases, no scouts, no siege units

### Interpretation

This package must **not** be read as positive organic launch proof.

The `positive` status only means:

- the runtime-backed main-run surface executed,
- the artifact shape is valid,
- and a no-launch result is acceptable as rarity/context evidence in isolation.

Because Package B also had `0/90` launches, Package A strengthens the overall RED result rather than softening it.

## Package D: Stress Matrix

### Result

- `exitCode=2`, `exitReason=assert_fail`
- `assertionFailures=9`
- `anomalyCount=170`
- `perfRedCount=170`
- `wave10` status mix: `proof_unavailable=240`

### Launch/lifecycle facts

- `campaignLaunches=0` in all `240/240` runs
- `encounter=0`
- `siege=0`
- `resolved=0`
- no convoys, no forward bases, no scouts, no siege units

### Failed assertions

Failed invariants:

- `SURV-01` x3: `LivingColonies >= 1`
- `SURV-02` x3: `People > 0`
- `SURV-04` x3: `AverageFoodPerPerson >= 1.0`

Failing runs:

1. `small-highpop | Htn | seed 606`
   - `livingColonies=0`
   - `people=0`
   - `food=169`
   - `avgFoodPerPerson=0`
2. `small-lowpop | Simple | seed 606`
   - `livingColonies=0`
   - `people=0`
   - `food=262`
   - `avgFoodPerPerson=0`
3. `small-lowpop | Goap | seed 606`
   - `livingColonies=0`
   - `people=0`
   - `food=262`
   - `avgFoodPerPerson=0`

### Drilldown top offenders

The highest-ranked stress drilldown runs were the three assertion-failing seed-606 small-topology lanes:

1. `small-highpop | Htn | seed 606`
   - reasons: `assert_fail:3`, `perf_red:1`, `low_food_per_person`, `colony_extinction`
2. `small-lowpop | Goap | seed 606`
   - reasons: `assert_fail:3`, `low_food_per_person`, `colony_extinction`
3. `small-lowpop | Simple | seed 606`
   - reasons: `assert_fail:3`, `low_food_per_person`, `colony_extinction`

### Interpretation

The stress matrix does two things:

- confirms the hostile organic no-launch problem persists across a broader parameter matrix,
- and reveals that some small-topology seed-606 lanes collapse hard enough to trip formal survival assertions.

That means the current Step10B.2 issue is not only “organic campaigns do not emerge”; it also includes a compact-topology survivability failure mode under hostile-organic coverage.

## Healthy Signals

- All four packages produced valid runtime-backed main-run artifacts with clean provenance.
- No Step10B.2 package used `wave10-probes.json` for lifecycle claims.
- Manual/operator lifecycle launch path is strongly proven: `90/90` successful launches.
- Manual lifecycle can progress beyond launch in a material subset of runs:
  - encounter `44/90`
  - siege `11/90`
  - resolution `44/90`
- Forward-base behavior does activate naturally in manual lifecycle runs: `71/90`.

## Suspicious Signals

- Hostile organic never launches across `90/90` runs.
- Pure organic never launches across `90/90` runs.
- Stress hostile never launches across `240/240` runs.
- Hostile organic suppression counters remain zero, so the failure is not currently explained by cap/home-defense/route-budget gating.
- Scout-intel counters remain zero in all organic packages, so the launch pipeline is not even reaching that part of the lifecycle.
- Dedicated siege units never activate in any Step10B.2 package.
- Convoys never activate in any Step10B.2 package.
- Perf anomalies are widespread across all packages.
- Stress package contains hard survival assertion failures on seed `606` small-topology runs.

## Worst Runs Ranked

1. `small-highpop | Htn | seed 606` from stress package
- `exit path`: assert fail
- failed: `SURV-01`, `SURV-02`, `SURV-04`
- reasons: `assert_fail:3`, `perf_red:1`, `low_food_per_person`, `colony_extinction`

2. `small-lowpop | Goap | seed 606` from stress package
- `exit path`: assert fail
- failed: `SURV-01`, `SURV-02`, `SURV-04`
- reasons: `assert_fail:3`, `low_food_per_person`, `colony_extinction`

3. `small-lowpop | Simple | seed 606` from stress package
- `exit path`: assert fail
- failed: `SURV-01`, `SURV-02`, `SURV-04`
- reasons: `assert_fail:3`, `low_food_per_person`, `colony_extinction`

4. `large-hostile | Goap | seed 101` from hostile package
- no assert fail, but top hostile drilldown offender due to `perf_red:2`
- still `0` launches and no lifecycle activation

5. `large-manual | Goap | seed 101` from manual package
- no assert fail, but top manual drilldown offender due to `perf_red:2`
- still useful because it belongs to the package that actually demonstrated encounter/resolution behavior

## Step10B.2 Questions Answered

### Does pure organic campaign emergence happen in long runs?

No, not in the executed matrix.

- `0/90` pure-organic runs launched campaigns.
- This is context evidence only, not proof that pure organic is healthy-but-rare.

### Does hostile organic emergence happen when the world has plausible campaign pressure?

No.

- `0/90` hostile-organic runs launched campaigns.
- `0/240` hostile-organic stress runs launched campaigns.
- No downstream lifecycle stage activated.

### What happens after manual/operator launch over a long runtime-backed run?

The runtime command path works reliably:

- `90/90` launches succeeded,
- encounter/resolution happened in `44/90`,
- siege in `11/90`,
- forward bases in `71/90`.

But the deeper campaign ecosystem remains partial:

- no convoys,
- no scouts,
- no dedicated siege units.

### Which lifecycle stages are actually observed?

- Organic hostile: none beyond preconditioned world setup.
- Pure organic: none.
- Manual lifecycle: launch, assembly, some march, some encounter, some siege, some resolution.

### Do supply lines, forward bases, scouts, siege units, war score, loot, and peace/resolution activate naturally?

- Forward bases: yes, under manual lifecycle.
- Resolution/war score/loot/peace: partial yes, under the subset of manual runs that resolved.
- Supply convoys: no evidence in Step10B.2-B.
- Scout intel: no evidence in Step10B.2-B.
- Dedicated siege units: no evidence in Step10B.2-B.

### Are failures design limitations, evidence gaps, or implementation bugs?

Current recommendation:

- hostile/pure organic no-launch is a real implementation/runtime-evidence gap, not just a documentation gap,
- small-topology seed-606 extinction in the stress matrix is a real runtime stability issue,
- the current evidence does **not** yet isolate a Track C-only strategist/advisory fault.

## Routing Recommendation

### Primary route: Track B

Reason:

- hostile organic lifecycle never launches,
- suppression counters stay zero,
- no scout-intel or target-with-intel counters activate,
- stress matrix shows survivability failures in addition to no-launch,
- manual lifecycle proves the runtime can create and advance campaigns when explicitly injected.

Recommended Track B focus areas for fix-planning:

- why runtime-backed hostile-organic lifecycle never enters launch,
- whether war/tension preconditions are too weak or not visible to the launch cadence,
- whether required organic launch inputs are absent from the runtime-backed lifecycle surface,
- why compact topology seed `606` can collapse to colony extinction during hostile-organic stress coverage.

### Track C

Still closed for now.

Reason:

- current counters do not show a strategist/advisory decision trail proving “the strategist decided no launch despite valid runtime target/precondition state.”

### Track A

Still deferred.

Reason:

- runtime lifecycle health is not yet good enough to make visual/manual consume the primary issue.

## Unknowns

- Why exactly hostile-organic launch never activates:
  - runtime cadence issue,
  - runtime-visible precondition issue,
  - strategist-output issue,
  - or a combination of these.
- Why scout-intel counters remain zero in all Step10B.2-B packages.
- Why no convoy behavior appears in the manual lifecycle package despite forward-base activity.
- Why dedicated siege units never activate in these lifecycle packages.
- Whether the seed-606 small-topology extinction is specific to hostile-organic preconditions or a broader small-topology combat pressure problem.

## Suggested Next Step

- Meta should accept this as RED Step10B.2 evidence.
- Immediate next owner should be Track B fix-planning.
- Do not reopen Track C yet.
- Do not commit prep/evidence changes yet.
- Do not promote any Step10B.2 artifact to a baseline.

## Decision Recommendation

`Wave 10 Step10B.2 evidence is RED. Recommend Track B fix-planning before Wave 10.5 readiness or Step10C residual disposition.`

The decisive reasons are:

- hostile organic lifecycle `0/90` launches,
- hostile-organic stress lifecycle `0/240` launches,
- pure organic lifecycle `0/90` launches and must not be misread as positive proof,
- stress matrix contains hard survival assertion failures,
- only the manually injected runtime path demonstrates meaningful campaign lifecycle.
