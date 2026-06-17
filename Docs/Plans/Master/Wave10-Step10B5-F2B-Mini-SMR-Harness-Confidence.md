# Wave 10 Step10B.5-F2-B - Mini-SMR / Harness Confidence

Status: accepted / closed
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F2-B is a small Track-owned evidence pass for the F2-A runtime fix. It is not a full SMR Analyst closeout.

Latest local evidence: `.artifacts/smr/wave10-step10b5-f2b-hostile-lifecycle-mini-001/`.
This is mini-SMR confidence only, not a final GREEN/YELLOW/RED SMR recommendation.

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
- every run-level `runs[].wave10.organicLaunchDiagnostics.dominantNoLaunchReason` moved past `no_available_warriors_after_home_defense`,
- raw `.artifacts` remain uncommitted,
- docs reference the artifact path as local evidence.

## Local Evidence - 2026-06-17

Artifact path: `.artifacts/smr/wave10-step10b5-f2b-hostile-lifecycle-mini-001/`.

Scope:
- seeds: `101,202`,
- planners: `simple,goap`,
- config: `hostile-lifecycle-f2b`,
- ticks: `80`,
- map/population: `40x40`, `InitialPop=80`,
- lane: `organic-hostile-campaign-lifecycle` normalized to `organic_hostile_campaign_lifecycle`.

Artifact checks:
- `manifest.json`: `exitCode=0`, `totalRuns=4`, `wave10RunCount=4`, `anomalyCount=0`.
- `anomalies.json`: empty array.
- `wave10-probes.json`: absent; lifecycle evidence remains main-run truth.
- `summary.json` `runs[]`: all runs report `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`, `campaignLaunches=0`, and `evaluationTickCount=3`.

Per-run routing evidence:

| Planner | Seed | Dominant no-launch reason | Campaign launches |
|---------|------|---------------------------|-------------------|
| Simple | 101 | `missing_scout_intel` | 0 |
| Simple | 202 | `missing_scout_intel` | 0 |
| Goap | 101 | `missing_scout_intel` | 0 |
| Goap | 202 | `missing_scout_intel` | 0 |

F2-B conclusion:
- The F2-A runtime fix moved the main-run hostile lifecycle past `no_available_warriors_after_home_defense` in all four mini-SMR runs.
- The next blocker is consistently `missing_scout_intel`.
- Note: run-level final `lastDecisionReasonCode` remains `HomeDefenseBelowMinimum`, but the accepted F2-B routing signal is the per-run `dominantNoLaunchReason`, which consistently moved past `no_available_warriors_after_home_defense` to `missing_scout_intel`.
- F2-C target-knowledge / scout gate policy is now in scope for Meta review/routing.
- F3 should not proceed before F2-C unless Meta explicitly accepts skipping F2-C.

## Handoff

The F2-B handoff must state:
- artifact path,
- seed/planner/config/tick scope,
- dominant reason after F2-A,
- whether F2-C target-knowledge policy is now in-scope,
- whether F3 can proceed without F2-C.
