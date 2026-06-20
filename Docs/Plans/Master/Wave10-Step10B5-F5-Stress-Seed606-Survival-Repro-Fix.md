# Wave 10 Step10B.5-F5 - Stress Seed-606 Survival Repro Fix

Status: unblocked next - stress seed-606 scope only
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F5 handles the hard survival failures found by the Step10B.2 stress package. It should not be merged conceptually with organic launch target policy.

Meta routing note after F4:
- F5 is now explicitly open as the next Track B step.
- Scope is limited to the three known Step10B.2 stress seed-606 survival lanes below.
- Do not use the old F3 standard `movementSpeedMultiplier=0` collapse as F5 evidence.
- Do not use F5 for manual downstream positive scout-role or tech-enabled siege-unit proof.
- Do not run full hostile/pure/stress/perf broad packages unless a later Meta/SMR decision opens them.

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

- hostile setup too early/aggressive on compact topology,
- economy/ecology starvation collapse,
- combat annihilation,
- predator pressure,
- movement/clustering starvation,
- map/resource generation issue,
- instrumentation/assertion mismatch.

Do not fix until classification is written in the handoff or commit message.

## Fix Policy

Allowed fixes:

- narrow warmup before hostile precondition in stress lifecycle if evidence proves immediate war pressure causes collapse,
- narrow small-topology resource/economy guard if evidence proves ecology collapse,
- narrow runtime correction for movement/clustering if evidence proves no-progress collapse,
- focused test coverage for the reproduced failure.

Not allowed:

- weakening `SURV-*` assertions,
- hiding population zero by artifact post-processing,
- broad ecology rebalance without evidence,
- changing unrelated planners to mask stress failure.

## Tests

Add focused tests or scenario-runner coverage that proves:

- the three seed-606 lanes no longer fail, or
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

## Acceptance

F5 is accepted when:

- the three failing lanes are fixed, or
- Meta accepts a documented small-topology limitation route,
- no `SURV-*` assertions were weakened,
- organic launch diagnostics are still valid,
- no manual lifecycle regression is introduced.

## Handoff To F6

The F5 handoff must include:

- root-cause classification,
- exact fix or limitation route,
- repro artifact paths,
- tests run,
- whether broad stress rerun can proceed.
