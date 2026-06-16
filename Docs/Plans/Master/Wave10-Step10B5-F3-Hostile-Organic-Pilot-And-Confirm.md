# Wave 10 Step10B.5-F3 - Hostile Organic Pilot And Confirm

Status: planned
Owner: Track B for focused runs; SMR Analyst may assist with artifact review
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
