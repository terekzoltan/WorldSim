# Wave 10 Step10B.5-F6 Full Recovery Closeout Evidence

Status: accepted YELLOW evidence
Date: 2026-06-20
Owner: SMR Analyst evidence, Meta Coordinator closeout

## Scope

F6 was executed as an evidence-only staged recovery rerun. No source, config, or ScenarioRunner behavior changes were made in this step.

Raw artifacts remain local and are not committed:

- `.artifacts/smr/wave10-organic-hostile-soak-002/`
- `.artifacts/smr/wave10-manual-operator-lifecycle-002/`

## Verification Gates

Reported pre-SMR gates:

- Runtime Wave10 tests: 131/131 passed.
- ScenarioRunner Wave10 evidence tests: 10/10 passed.
- Full solution build: 0 warnings, 0 errors.

Artifact gate summary:

| Package | Runs | Exit | Assertions | Anomalies | Provenance |
|---|---:|---:|---:|---:|---|
| `wave10-organic-hostile-soak-002` | 18 | 0 | 0 | 0 | 18/18 `main_world_run|organic|tick_sampled` |
| `wave10-manual-operator-lifecycle-002` | 18 | 0 | 0 | 0 | 18/18 `main_world_run|manual_operator|tick_sampled` |

No `wave10-probes.json` artifact was produced or used as proof for either package.

## Package Results

### Hostile Organic Core

Matrix:

- 3 seeds x 3 planners x 2 configs (`medium-hostile`, `standard-hostile`)
- `Ticks=6000`
- assert + drilldown enabled
- perf off

Result:

- Launch runs: 18/18
- Total launches: 277
- March: 17/18
- Encounter: 17/18
- Siege: 10/18
- Resolution: 17/18
- Convoy: 2 runs
- Scout: 0 runs
- Siege units: 0 runs

Decision: hostile organic is recovered/healthy for this staged pass.

### Manual Runtime-Command Control

Matrix:

- 3 seeds x 3 planners x 2 configs (`medium-manual`, `standard-manual`)
- `Ticks=6000`
- manual launch tick 500
- assert + drilldown enabled
- perf off

Result:

- Manual attempted: 18/18
- Manual command created: 16/18
- Manual residual: 2/18 `CampaignRuntimeUnavailable`
- Total launches: 191
- March: 18/18
- Encounter: 18/18
- Siege: 12/18
- Resolution: 18/18
- Convoy: 3 runs
- Scout: 0 runs
- Siege units: 0 runs

Residual runs:

- `medium-manual | Simple | seed 101`: `CampaignRuntimeUnavailable`, while lifecycle still had launches, march, and resolution.
- `standard-manual | Htn | seed 202`: `CampaignRuntimeUnavailable`, while lifecycle still had launches, march, and resolution.

Decision: manual lifecycle remains meaningful, but runtime-command availability is not clean enough for GREEN.

## Skipped Packages

Pure organic, broad stress, and perf packages were not run.

Rationale:

- F6 is staged and does not repeat the historical broad matrices by default.
- Hostile organic recovered strongly enough for the decision core.
- Manual control exposed a narrower residual that should be routed before spending runtime on broad expansion.
- F5 already covered the targeted seed-606 survival sentinel as no-fix GREEN evidence.

## Final Verdict

Step10B.5-F6 is accepted as YELLOW evidence.

Step10B can close with the following limitation:

- Organic hostile campaign launch/recovery is now proven healthy in staged SMR evidence.
- Manual runtime-command lifecycle remains meaningful, but command creation has a 2/18 `CampaignRuntimeUnavailable` residual.
- Scout and dedicated siege-unit counters remain sparse and must not be overclaimed as fixed.

## Routing

Meta accepts the F6 manual residual as a known YELLOW limitation for Step10B closeout and routes it into Step10C residual/manual gap triage.

Step10C should classify the manual-command residual as `in-scope now`, `not-yet-in-scope`, or `already resolved` before claiming Wave10.5 readiness.
