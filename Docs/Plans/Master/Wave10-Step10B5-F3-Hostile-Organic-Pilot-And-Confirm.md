# Wave 10 Step10B.5-F3 - Hostile Organic Pilot And Confirm

Status: SMR Analyst routing accepted / Route C selected
Owner: Track B for staged run execution and artifact handoff; SMR Analyst reviews artifacts after Track B handoff
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F3 is verification, not a new fix. It proves whether F2 moved hostile organic lifecycle from zero-launch to meaningful launch behavior before expensive packages are rerun.

## Purpose

Avoid wasting another long SMR run if the organic launch pipeline is still blocked. Use staged pilots to confirm launch incidence, first launch timing, and no-launch explanations.

F3 is also the main runtime-cost control point. A hostile organic zero-launch result is enough to stop the expensive recovery rerun path until diagnostics/fixes improve it.

## Run Order

Start small:

- 1 seed,
- 1 planner,
- medium hostile config,
- 1000-2000 ticks,
- drilldown optional but useful,
- perf optional for speed.

Then medium confirm:

- 3 seeds,
- `simple,goap,htn`,
- medium hostile config,
- 4000-6000 ticks.

Then standard confirm:

- 3 seeds,
- `simple,goap,htn`,
- standard hostile config,
- 4000-6000 ticks.

Do not run the full 90-run hostile package until these confirms show useful signals.

## Runtime-Cost Rules

Use cheap runs to answer decision questions first:

- keep `perf=false` unless a perf regression is the explicit hypothesis,
- keep drilldown small, for example top 3 or only failing/non-green runs,
- prefer medium/standard before large,
- prefer 3 seeds before 10 seeds,
- stop before pure/stress if hostile organic is still zero-launch.

Do not use more matrix size to compensate for missing diagnostics. If no-launch is unexplained, return to F1/F2 instead.

## Metrics To Review

- `campaignLaunches`,
- `firstCampaignLaunchTick`,
- `activeCampaigns`,
- `resolvedCampaigns`,
- known target count,
- unknown target count,
- last/best decision reason,
- launch apply attempts,
- launch apply failures by status,
- first assembly/march/encounter if present,
- survival assertions,
- anomaly/perf counts.

## Stop Conditions

Stop and return RED if:

- hostile organic remains zero-launch across the staged confirm,
- diagnostics still cannot explain no-launch,
- F2 caused hard survival/economy failures,
- F2 caused manual/operator launch regression.

If any of these stop conditions apply, do not run:

- full pure organic,
- broad stress,
- perf-long,
- large-map expansion.

## Continue Conditions

Proceed to F4/F5 if:

- hostile organic launches in multiple seed/planner combinations, or
- launch incidence is low but no-launch runs have explicit non-bug explanations accepted by Meta.

## Acceptance

F3 is accepted when Meta/Track B can state one of:

- GREEN for continuing: hostile organic launch is restored enough to diagnose downstream lifecycle.
- YELLOW for continuing: launches happen but incidence is low and needs final SMR review.
- RED stop: launch remains blocked and F2/F1 must be revised.

## Handoff To F4 And F5

The F3 handoff must include:

- artifact paths for pilot/confirm runs,
- launch incidence by config/planner/seed,
- representative no-launch reason summary,
- whether manual downstream diagnostics should start,
- whether stress survival repro should start,
- whether full SMR package is still blocked,
- whether pure organic should stay deferred or proceed as small context matrix.

## Track B Execution Handoff - 2026-06-18

Track B ran the staged hostile organic pilot/confirm sequence. No source/test code was changed in F3. Raw artifacts remain local-only under `.artifacts/smr/`.

### Package 1 - Tiny pilot

- Artifact: `.artifacts/smr/wave10-step10b5-f3-hostile-organic-pilot-001/`
- Matrix: 1 seed (`101`) x 1 planner (`simple`) x 1 config (`hostile-medium-f3-pilot`)
- Config: `Width=40`, `Height=40`, `InitialPop=80`, `Ticks=1500`, `Dt=0.25`, combat/diplomacy/siege enabled, `Wave10Scenario=organic-hostile-campaign-lifecycle`
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`
- Provenance: `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`; no `wave10-probes.json`
- Launch summary: 1/1 launch runs, 6 total launches, first launch tick 40, assembly/march/encounter/siege/resolution incidence 1/1
- No-launch reason distribution: `launch_applied:1`
- Anomaly classification: none

### Package 2 - Medium confirm

- Artifact: `.artifacts/smr/wave10-step10b5-f3-hostile-organic-medium-confirm-001/`
- Matrix: 3 seeds (`101,202,303`) x 3 planners (`simple,goap,htn`) x 1 config (`hostile-medium-f3-confirm`)
- Config: `Width=40`, `Height=40`, `InitialPop=80`, `Ticks=5000`, `Dt=0.25`, combat/diplomacy/siege enabled, `Wave10Scenario=organic-hostile-campaign-lifecycle`
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`
- Provenance: 9/9 runs have `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`; no `wave10-probes.json`
- Launch summary: 9/9 launch runs, 86 total launches, first launch tick min/max 20/60, by planner `Simple:3/3`, `Goap:3/3`, `Htn:3/3`
- Lifecycle incidence: assembly 9/9, march 8/9, encounter 8/9, siege 6/9, resolution 8/9
- No-launch reason distribution: `launch_applied:9`
- Anomaly classification: none

### Package 3 - Standard confirm

- Artifact: `.artifacts/smr/wave10-step10b5-f3-hostile-organic-standard-confirm-001/`
- Matrix: 3 seeds (`101,202,303`) x 3 planners (`simple,goap,htn`) x 1 config (`hostile-standard-f3-confirm`)
- Config: `Width=64`, `Height=40`, `InitialPop=120`, `Ticks=5000`, `Dt=0.25`, combat/diplomacy/siege enabled, `Wave10Scenario=organic-hostile-campaign-lifecycle`
- Result: `exitCode=2`, `exitReason=assert_fail`, `assertionFailures=45`, `assertionSkipped=2`, `anomalyCount=0`
- Provenance: 9/9 runs have `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`; no `wave10-probes.json`
- Launch summary: 9/9 launch runs, 29 total launches, first launch tick min/max 20/40, by planner `Simple:3/3`, `Goap:3/3`, `Htn:3/3`
- Lifecycle incidence: assembly 9/9, march 0/9, encounter 0/9, siege 0/9, resolution 0/9
- No-launch reason distribution: `launch_applied:9`
- Assertion failure distribution: `SURV-01:9`, `SURV-02:9`, `SURV-03:9`, `SURV-04:9`, `ECON-01:9`; skipped `COMB-01:1`, `COMB-02:1`
- Anomaly classification: none in `anomalies.json`; assertion failures are blocking for clean GREEN and need SMR Analyst/Meta routing

### Track B assessment for review

- Hostile organic zero-launch is no longer the blocker: all staged packages launched in every run.
- Medium topology shows downstream lifecycle progress through resolution in most runs and has a clean assert/anomaly gate.
- Standard topology launches but does not progress past assembly within 5000 ticks and fails survival/economy assertions in every run.
- Full 90-run hostile package remains blocked until SMR Analyst/Meta decide whether F4/F5 should proceed immediately, whether F5 should absorb the standard survival/economy collapse, or whether another narrower Track B diagnostic is required first.

## SMR Analyst Routing Closeout - 2026-06-18

- F3 artifact review completed read-only with no source/test/doc changes in the SMR Analyst pass.
- Hostile zero-launch recovery verdict: GREEN.
- Medium confirm verdict: GREEN for medium-backed downstream diagnostics.
- Standard confirm verdict: YELLOW/RED observed survival/economy failure, but artifact validity is QUESTIONABLE because effective `movementSpeedMultiplier=0` materially affects lifecycle/survival interpretation.
- Accepted routing: Route C, a new narrow Track B scenario/config diagnostic before treating standard as a valid F5 survival bug or running any full 90-run hostile package.
- F4/F5/F6 remain blocked until Meta explicitly opens the next step from this Route C decision.
