# Wave 10 Step10B.5-Route C - Scenario Config Diagnostic

Status: accepted / closed - F4 unlocked
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

Route C is a narrow Track B diagnostic opened by the accepted F3 SMR Analyst routing. It verifies whether the F3 standard-confirm collapse is gameplay-valid evidence or is materially confounded by ScenarioRunner config/default handling.

## Purpose

Resolve the `movementSpeedMultiplier=0` validity question before treating the standard confirm as a valid F5 survival/economy bug or running any full 90-run hostile package.

F3 proved hostile organic zero-launch is fixed. Route C must not reopen that question. It only checks whether standard topology failure is valid evidence.

## Scope

In scope:
- Compare medium vs standard effective config fields from F3 artifacts.
- Inspect ScenarioRunner config/default handling for `MovementSpeedMultiplier` in lifecycle and non-lifecycle paths.
- Identify whether standard F3 config intentionally set, accidentally omitted, or incorrectly preserved `movementSpeedMultiplier=0`.
- Apply the Route C default policy: lifecycle evidence configs must not silently preserve omitted/non-positive `MovementSpeedMultiplier`; `MovementSpeedMultiplier <= 0` is a safe-normalized evidence config bug unless the lane is explicitly documented as a no-movement stressor.
- If approved by the diagnostic result, run one targeted standard sentinel with effective `MovementSpeedMultiplier=1`.
- Update docs/state with a routing recommendation.

Out of scope:
- No broad gameplay tuning.
- No full 90-run hostile package.
- No pure/stress/perf package expansion.
- No Track C strategist changes.
- No App/Graphics work.
- No F4/F5 implementation.
- No normalization of `BirthRateMultiplier=0`; it may be intentional in lifecycle configs.

## Inputs

- F3 handoff: `Docs/Plans/Master/Wave10-Step10B5-F3-Hostile-Organic-Pilot-And-Confirm.md`
- Standard artifact: `.artifacts/smr/wave10-step10b5-f3-hostile-organic-standard-confirm-001/`
- Medium artifact: `.artifacts/smr/wave10-step10b5-f3-hostile-organic-medium-confirm-001/`
- ScenarioRunner config code: `WorldSim.ScenarioRunner/Program.cs`

## Required Diagnostic Questions

1. Does the standard F3 artifact really have effective `movementSpeedMultiplier=0` in all runs?
2. Does the medium F3 artifact use effective `movementSpeedMultiplier=1`?
3. Is `movementSpeedMultiplier=0` intentional for `hostile-standard-f3-confirm`, or caused by config omission/default handling?
4. Does the lifecycle path consume raw `MovementSpeedMultiplier` while another path normalizes `<=0` to `1f`?
5. If standard is rerun with effective movement speed `1`, does the hard survival/economy collapse and no-march behavior persist?

## Suggested Implementation Shape

Start with read-only analysis:
- Parse medium and standard `summary.json` effective config fields.
- Inspect `Program.cs` config creation, JSON override binding, and lifecycle execution path.
- Record exact source lines or config entries responsible for movement speed behavior.

Then choose the minimal action:
- Treat the current F3 standard as artifact-validity questionable, not as a documented stressor lane.
- If raw config cannot prove omitted vs explicit zero, artifact effective value plus non-nullable `ScenarioConfig` default behavior is sufficient diagnostic evidence to align lifecycle handling with existing non-lifecycle assert normalization.
- If the standard config omitted movement speed and lifecycle preserved default `0`, fix the default/normalization at the narrow ScenarioRunner config boundary and add focused coverage.
- If no code fix is needed, run only a targeted sentinel with explicit `MovementSpeedMultiplier=1` to validate the interpretation.

## Targeted Sentinel

Only after the read-only diagnostic identifies the right path, run a single narrow standard sentinel.

Suggested artifact path:
- `.artifacts/smr/wave10-step10b5-routec-standard-movement-sentinel-001/`

Suggested matrix:
- one seed first: `101`
- one planner first: `simple`
- standard hostile config: `Width=64`, `Height=40`, `InitialPop=120`, `Ticks=5000`, `Dt=0.25`
- explicit `MovementSpeedMultiplier=1`
- `Wave10Scenario=organic-hostile-campaign-lifecycle`
- combat/diplomacy/siege enabled
- `WORLDSIM_SCENARIO_MODE=assert`

Escalate to 3 seeds x 3 planners only if the one-run sentinel is valid but inconclusive and Meta approves the cost.

## Acceptance

Route C is accepted when Track B can state one of:

- `config_bug_confirmed`: standard F3 was materially invalid because movement speed default/normalization was wrong; fix/coverage and rerun sentinel are complete.
- `standard_stressor_confirmed`: movement speed `0` was intentional; Meta must decide whether this stressor is valid F5 evidence.
- `standard_valid_failure`: movement speed `1` sentinel still shows survival/economy collapse or lifecycle stall, making F5 the likely next route.
- `standard_recovered`: movement speed `1` sentinel has launch plus movement/march evidence and no hard assertion collapse; Track B may recommend F4/F5 routing, but Meta/SMR Analyst owns the final next-step unlock.

## Verification

If code changes are made:
- focused ScenarioRunner tests around config/default handling,
- `dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --no-restore --filter "FullyQualifiedName~Wave10CampaignEvidenceTests" -m:1 /p:UseSharedCompilation=false`,
- `dotnet build "WorldSim.sln" --no-restore -m:1 /p:UseSharedCompilation=false`.

Always verify artifacts:
- `manifest.json` exit/assertion/anomaly counts,
- `summary.json` effective `movementSpeedMultiplier`, provenance, launch incidence, and lifecycle stage counters,
- absence of `wave10-probes.json`,
- `assertions.json` if assert mode is used.

## Handoff

The Route C handoff must include:
- exact cause of `movementSpeedMultiplier=0`,
- whether a code/config fix was made,
- targeted sentinel artifact path,
- comparison against F3 standard confirm,
- recommended next route: F4, F5, another narrow diagnostic, or stop.

## Track B Execution Handoff - 2026-06-19

Route C classified the F3 standard-confirm failure as a ScenarioRunner evidence config/default bug, not valid standard stress evidence.

Cause:
- F3 medium effective artifact config had `movementSpeedMultiplier=1` in 9/9 runs and reached march in 8/9.
- F3 standard effective artifact config had `movementSpeedMultiplier=0` in 9/9 runs and reached march in 0/9.
- `ScenarioConfig.MovementSpeedMultiplier` is non-nullable, so omitted JSON defaults to `0`.
- The non-lifecycle assert companion path already normalized `MovementSpeedMultiplier <= 0` to `1f`, while lifecycle scenarios consumed the raw config.
- The F3 standard lane was not documented as a no-movement stressor, so its collapse is artifact-validity questionable.

Code/config fix:
- `WorldSim.ScenarioRunner/Program.cs` now safe-normalizes lifecycle `MovementSpeedMultiplier <= 0` to `1f` at the ScenarioRunner main-run config boundary.
- `BirthRateMultiplier=0` remains unchanged and intentionally out of scope.
- No runtime movement/gameplay code was changed.

Focused coverage:
- `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs` now covers omitted lifecycle `MovementSpeedMultiplier` and asserts effective `movementSpeedMultiplier=1`, `birthRateMultiplier=0`, `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`, and absence of `wave10-probes.json`.

Targeted sentinel:
- Artifact: `.artifacts/smr/wave10-step10b5-routec-standard-movement-sentinel-001/`
- Matrix: 1 seed (`101`) x 1 planner (`simple`) x 1 standard hostile lifecycle config (`64x40`, `InitialPop=120`, `Ticks=5000`, `Dt=0.25`, combat/diplomacy/siege enabled, explicit `MovementSpeedMultiplier=1`).
- Result: `exitCode=0`, `assertionFailures=0`, `anomalyCount=0`, no `wave10-probes.json`.
- Provenance: `runtimeSource=main_world_run`, `proofType=organic`, `timelineSemantics=tick_sampled`.
- Lifecycle: `campaignLaunches=3`, `firstMarchTick=1835`, `firstEncounterTick=2214`, `firstResolutionTick=2215`, `resolvedCampaigns=1`, `firstSiegeTick=null`.
- Survival/economy: `livingColonies=1`, `people=7`, `food=2966`, `averageFoodPerPerson=423.7143`.

Classification:
- `config_bug_confirmed`: F3 standard was materially invalid because lifecycle movement-speed default handling produced effective `0`.
- `standard_recovered`: the explicit movement-speed sentinel recovered launch, march, encounter, resolution, and survival/economy assertions for the one-run Route C gate.

Recommended next route:
- Meta/SMR Analyst review should decide the next unlock.
- Track B recommendation: F4 may proceed from medium + Route C sentinel evidence; F5 can likely be deferred for this standard movement-speed collapse, but stress seed-606 remains a separate known issue if Meta keeps F5 in scope.
- Do not run the full hostile package until Meta/SMR Analyst accepts Route C classification.

## Meta/SMR Routing Closeout - 2026-06-20

Decision:
- Route C accepted as GREEN.
- F4 manual downstream diagnostics is explicitly unlocked as the next Track B step.
- The old F3 standard `movementSpeedMultiplier=0` collapse is not valid F5 survival/economy evidence.
- F5 remains deferred/narrowed to the separate stress seed-606 survival issue only if Meta keeps that gate active after F4 result review.
- Full hostile, pure, stress, and perf broad packages remain blocked until later Meta/SMR decision.
- Track C and Track A remain closed because Route C proved no strategist/advisory or UI blocker.

Rationale:
- Meta step-review and SMR Analyst routing review both approved `config_bug_confirmed` + one-run `standard_recovered`.
- The sentinel recovered launch/march/encounter/resolution and passed assertions, but did not enter siege; downstream convoy/scout/siege-unit non-activation remains F4 scope.
