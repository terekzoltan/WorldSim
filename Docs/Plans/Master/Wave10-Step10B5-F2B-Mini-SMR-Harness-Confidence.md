# Wave 10 Step10B.5-F2-B - Mini-SMR / Harness Confidence

Status: pending F2-A
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F2-B is a small Track-owned evidence pass for the F2-A runtime fix. It is not a full SMR Analyst closeout.

## Mini-SMR Policy

Track agents may run mini-SMR only when the run directly validates their current fix and avoids a separate handoff for a tiny proof loop.

Limits:
- 1-3 seeds,
- 1-2 planners,
- 1-2 configs,
- short/focused tick budget,
- local raw artifacts only,
- no final GREEN/YELLOW/RED recommendation for the wave.

SMR Analyst still owns:
- full package reruns,
- cross-seed/cross-planner closeout,
- baseline/compare evidence,
- final SMR recommendation.

## Harness Policy

Scenario setup may provide a war-ready minimum for harness/control purposes if needed, but it must not replace the F2-A runtime gameplay fix.

Allowed harness setup:
- hostile/war relation setup,
- enough non-campaign-created population/roles to exercise the pipeline,
- explicit evidence-lane labeling.

Not allowed:
- direct campaign creation,
- scout intel unless the lane explicitly tests scout behavior,
- claiming harness proof as organic rarity proof.

## Acceptance

F2-B is accepted when a small hostile lifecycle main-run artifact shows:
- `runs[].wave10.runtimeSource = main_world_run`,
- no `wave10-probes.json` overclaim,
- dominant reason moved past `no_available_warriors_after_home_defense`,
- raw `.artifacts` remain uncommitted,
- docs reference the artifact path as local evidence.

## Handoff

The F2-B handoff must state:
- artifact path,
- seed/planner/config/tick scope,
- dominant reason after F2-A,
- whether F2-C target-knowledge policy is now in-scope,
- whether F3 can proceed without F2-C.
