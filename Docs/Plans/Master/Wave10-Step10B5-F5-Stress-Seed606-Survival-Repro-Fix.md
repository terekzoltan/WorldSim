# Wave 10 Step10B.5-F5 - Stress Seed-606 Survival Repro Fix

Status: accepted - no-fix `no_longer_reproducible` evidence
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F5 handles the hard survival failures found by the Step10B.2 stress package. It should not be merged conceptually with organic launch target policy.

Meta routing note after F4:
- F5 is now explicitly open as the next Track B step.
- Scope is limited to the three known Step10B.2 stress seed-606 survival lanes below.
- Do not use the old F3 standard `movementSpeedMultiplier=0` collapse as F5 evidence.
- Do not use F5 for manual downstream positive scout-role or tech-enabled siege-unit proof.
- Do not run full hostile/pure/stress/perf broad packages unless a later Meta/SMR decision opens them.
- The first implementation action must be targeted repro only. Do not change code before root-cause classification exists.
- If the targeted repro no longer fails after prior accepted fixes, close F5 as `no_longer_reproducible_after_prior accepted fixes + targeted evidence`; do not make runtime behavior changes.

## Purpose

Three small-topology seed-606 hostile lifecycle lanes failed formal survival assertions. Step10B.5 cannot close cleanly if the stress package still collapses colonies to zero people/living colonies.

## Failing Lanes

- `small-highpop | Htn | seed 606`
- `small-lowpop | Simple | seed 606`
- `small-lowpop | Goap | seed 606`

Failures:

- `SURV-01`: `LivingColonies >= 1`
- `SURV-02`: `People > 0`
- `SURV-04`: `AverageFoodPerPerson >= 1.0`

## Repro First

Before fixing, reproduce the three lanes with focused drilldown.

Use three separate single-lane commands, not a cartesian config/planner matrix, so each artifact maps unambiguously to one original failing lane. The repro artifact root should be:

```text
.artifacts/smr/wave10-step10b5-f5-seed606-repro-001/
```

Each command must run assert mode, JSON output, drilldown enabled, `MovementSpeedMultiplier=1`, and `Wave10Scenario=organic_hostile_campaign_lifecycle`. Use lane names that normalize identity explicitly:

- `small-highpop__htn__seed606`
- `small-lowpop__simple__seed606`
- `small-lowpop__goap__seed606`

Mandatory lanes:

- `small-highpop | Htn | seed 606`
- `small-lowpop | Simple | seed 606`
- `small-lowpop | Goap | seed 606`

Capture first-bad-tick information for:

- first living-colony drop,
- first severe population drop,
- first food-per-person collapse,
- starvation deaths,
- combat deaths,
- predator deaths,
- deaths other,
- food node depletion,
- no-progress/backoff,
- dense-neighborhood pressure,
- active battles/combat groups,
- routing people,
- campaign launch state if any.

## Classification

Classify the root cause as one of:

- `hostile_setup_too_early`,
- `economy_ecology_starvation`,
- `combat_annihilation`,
- `predator_pressure`,
- `movement_clustering_starvation`,
- `map_resource_generation`,
- `instrumentation_assertion_mismatch`,
- `no_longer_reproducible`.

Do not fix until classification is written in the handoff or commit message. The classification must include one `primary cause` and may include optional `secondary contributors`. Normalize lane identity in the handoff with the exact config/planner/seed tuple.

## Fix Policy

Allowed fixes:

- narrow ScenarioRunner evidence-config warmup/default-neutral field before hostile precondition if evidence proves immediate war pressure causes collapse; default lifecycle behavior must remain unchanged,
- narrow small-topology resource/economy guard if evidence proves ecology collapse,
- narrow runtime correction for movement/clustering if evidence proves no-progress collapse,
- focused test coverage for the reproduced failure.

Not allowed:

- weakening `SURV-*` assertions,
- hiding population zero by artifact post-processing,
- changing `BirthRateMultiplier=0` silently; it is an intentional stress parameter,
- broad ecology rebalance without evidence,
- changing unrelated planners to mask stress failure,
- Track C strategist/planner changes unless Meta routes a proven strategy-only finding.

Before any runtime behavior change, answer both self-checks in the handoff:

- Can this be solved as ScenarioRunner evidence config instead?
- Does this affect non-stress gameplay?

If runtime survival/combat/ecology/movement behavior changes are needed, treat the result as a deep-review candidate and add relevant focused runtime tests.

## Tests

Add focused tests or scenario-runner coverage that proves:

- the three seed-606 lanes no longer fail, or
- the targeted repro is no longer reproducible after prior accepted fixes, or
- the limitation is explicitly accepted and separated from Step10B.5 closeout.

Also check that the fix does not break:

- hostile organic launch diagnostics,
- manual lifecycle launch,
- existing Wave10 evidence tests.

## Verification

Minimum:

```powershell
dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj --filter Wave10 --no-restore
dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore
dotnet build WorldSim.sln --no-restore
```

Then run a focused stress repro package for only the seed-606 lanes before the broad stress matrix.

Post-fix or no-fix sentinel artifact root:

```text
.artifacts/smr/wave10-step10b5-f5-seed606-postfix-001/
```

The sentinel must rerun the same three lane identities from the repro step. Required result: `exitCode=0`, `assertionFailures=0`, no `SURV-01`, `SURV-02`, or `SURV-04`, and no `wave10-probes.json` proof claim.

## Acceptance

F5 is accepted when:

- the three failing lanes are fixed, or
- the three failing lanes are no longer reproducible after prior accepted fixes and targeted evidence, or
- Meta accepts a documented small-topology limitation route,
- no `SURV-*` assertions were weakened,
- organic launch diagnostics are still valid,
- no manual lifecycle regression is introduced.

## Implementation Outcome - 2026-06-20

Classification: `no_longer_reproducible`

Primary cause: `no_longer_reproducible_after_prior accepted fixes + targeted evidence`

Secondary contributors: none classified. The current post-F4/Route-C runtime and ScenarioRunner state no longer reproduces the original Step10B.2 seed-606 survival failures when the three lanes are rerun with explicit/effective `MovementSpeedMultiplier=1` and the same stress lifecycle config shape.

No code fix was applied. Per the F5 guardrail, runtime behavior was not changed because all targeted repro lanes passed before any fix.

Targeted repro artifacts:

- `.artifacts/smr/wave10-step10b5-f5-seed606-repro-001/small-highpop__htn__seed606/`
- `.artifacts/smr/wave10-step10b5-f5-seed606-repro-001/small-lowpop__simple__seed606/`
- `.artifacts/smr/wave10-step10b5-f5-seed606-repro-001/small-lowpop__goap__seed606/`

No-fix sentinel artifacts:

- `.artifacts/smr/wave10-step10b5-f5-seed606-postfix-001/small-highpop__htn__seed606/`
- `.artifacts/smr/wave10-step10b5-f5-seed606-postfix-001/small-lowpop__simple__seed606/`
- `.artifacts/smr/wave10-step10b5-f5-seed606-postfix-001/small-lowpop__goap__seed606/`

Sentinel result summary:

| Lane | Living colonies | People | Food | Avg food/person | Assertions | Anomalies |
|---|---:|---:|---:|---:|---|---|
| `small-highpop | Htn | seed 606` | 1 | 1 | 98 | 98 | 0 failures | 0 |
| `small-lowpop | Simple | seed 606` | 1 | 5 | 411 | 82.2 | 0 failures | 0 |
| `small-lowpop | Goap | seed 606` | 1 | 5 | 448 | 89.6 | 0 failures | 0 |

Verification run:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter Wave10 --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter Wave10CampaignEvidenceTests --no-restore -m:1 /p:UseSharedCompilation=false
dotnet build "WorldSim.sln" --no-restore -m:1 /p:UseSharedCompilation=false
```

Results:

- Runtime Wave10 tests: 131 passed, 0 failed.
- ScenarioRunner Wave10 evidence tests: 10 passed, 0 failed.
- Full solution build: 0 warnings, 0 errors.
- `wave10-probes.json`: not produced for these runs; proof remains main-world `runs[].wave10` plus assertions/manifest artifacts.

F6 handoff recommendation: F5 is accepted as no-fix GREEN evidence for the three targeted seed-606 lanes only. Broad stress/full recovery rerun should still remain an SMR Analyst / Meta decision under F6, not an automatic Track B expansion from this step.

## Review Outcome - 2026-06-20

Meta + Swarm review verdict: GREEN / APPROVE.

No findings discovered. Swarm review independently confirmed the six scoped artifact manifests, hard `SURV-01`, `SURV-02`, and `SURV-04` pass status, absence of `wave10-probes.json`, and postfix summary values documented above.

Residual risk: this acceptance only covers the three targeted seed-606 lanes. It is not a broad stress-package GREEN and does not open full hostile/pure/stress/perf packages without a separate F6 Meta/SMR decision.

## Handoff To F6

The F5 handoff must include:

- root-cause classification,
- exact fix or limitation route,
- repro artifact paths,
- post-fix/no-fix sentinel artifact paths,
- tests run,
- whether broad stress rerun can proceed.
