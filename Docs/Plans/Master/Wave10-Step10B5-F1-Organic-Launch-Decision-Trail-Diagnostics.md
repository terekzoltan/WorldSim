# Wave 10 Step10B.5-F1 - Organic Launch Decision-Trail Diagnostics

Status: accepted / committed in `cf34de6`
Owner: Track B
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F1 is diagnostics-only. It must make no-launch explainable before any behavior fix is attempted.

F0 handoff is closed as of 2026-06-16. Track B should build on the accepted Step10B.2-A evidence-surface prep commit `e4bb0a1 feat(wave10): add step10b2 lifecycle evidence surface` and keep F1 strictly diagnostics-only.

## Purpose

The Step10B.2 RED evidence showed zero organic launches and zero suppression counters. That is not enough to distinguish between missing cadence, missing eligible warriors, missing known targets, low launch score, strategy hold, or failed runtime application.

F1 adds a runtime-owned decision trail so every no-launch lifecycle run has a concrete reason.

## Implementation Notes

Implemented on 2026-06-16 as diagnostics-only runtime instrumentation. A review fix pass on 2026-06-16
completed the score/count semantics before commit. The exported run-level block is named `organicLaunchDiagnostics`
and is default-safe for non-lifecycle runs.

Field semantics are intentionally narrow:

- The uncommitted `evaluationCount` field was replaced, not preserved as an alias.
- `evaluationTickCount`: cumulative organic launch cadence evaluation tick count.
- `ownerEvaluationCount`: cumulative owner-loop evaluations run during cadence ticks.
- `lastEvaluationTick`: last cadence tick observed, or `null` before evaluation.
- `evaluatedFactionIds`: deterministic sorted set of owner faction IDs evaluated by the runtime loop.
- `last*` count fields: last evaluated owner/faction snapshot fields, not aggregate matrix fields.
- `hasLastBestCandidateScore`: `true` only after at least one target option has been observed.
- `lastBest*Score`: diagnostic best-candidate score components; default zero when `hasLastBestCandidateScore=false`.
- `lastBestLaunchScore`: diagnostic composite `(pressure * 0.55) + (advantage * 0.45) + (visibleEnemyPressure * 0.15) - distancePenalty`, clamped to `0..1`; it is not a new launch gate.
- `launchApply*`: cumulative runtime apply-attempt counters.
- `launchApplyFailureStatuses`: stable list of `{ status, count }` entries.
- `dominantNoLaunchReason`: compact classified reason using the F1 vocabulary.

No launch policy, target-knowledge policy, AI strategist behavior, App code, or Graphics code changed in F1.

Observed diagnostics:

- Small runtime-controlled war-without-scout setup with prepared warriors reports the scout/known-target blocker path.
- Main-run hostile lifecycle smoke `.artifacts/smr/wave10-step10b5-f1-hostile-diagnostics-smoke-003/` reports
  `dominantNoLaunchReason=no_available_warriors_after_home_defense`, `evaluationTickCount=3`, `ownerEvaluationCount=12`,
  `runtimeSource=main_world_run`, `campaignLaunches=0`, current score/count schema, and no `wave10-probes.json`.

Meta routing decision after F1: proceed to F2-A runtime war mobilization / launchable warrior availability first. Target-knowledge policy is conditional F2-C after F2-A/F2-B move the main-run blocker past `no_available_warriors_after_home_defense`.

## Primary Hypothesis

Hostile organic no-launch likely happens because organic target viability requires `CampaignTargetOption.IsKnown`, and `IsKnown` currently depends on fresh scout intel. The hostile lifecycle setup declares war but does not create scout intel, so the strategist likely returns `NoViableTarget` before runtime application and before suppression counters can increment.

F1 must prove or disprove this.

## Suggested Scope

Likely files:

- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.Runtime/Diagnostics/ScenarioWave10Telemetry.cs`
- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`
- focused runtime tests if cleaner than runner-only coverage.

Do not touch `WorldSim.AI` unless diagnostics prove a strategy-only issue and Meta explicitly approves.

## Diagnostics To Add

Add additive/default-safe run-level fields or a nested block for organic launch diagnostics.

The diagnostics should answer:

- how many organic evaluation ticks ran,
- which owner factions were evaluated,
- eligible campaign members per owner,
- available warriors after home-defense reserve,
- active campaign count per owner,
- target option count,
- target option count by stance,
- known target count,
- unknown target count,
- targets unknown due to missing scout intel,
- best pressure score,
- best advantage score,
- best distance penalty,
- best launch score,
- last strategist decision kind,
- last strategist reason code,
- launch apply attempt count,
- launch apply success count,
- launch apply failure count by `CampaignCreationStatus`.

Add compact timeline fields only if useful and cheap:

- last decision reason,
- known target count,
- available warriors,
- best score,
- launch attempts.

## Expected Implementation Shape

Prefer a small internal diagnostics accumulator in `SimulationRuntime` that is updated during `EvaluateOrganicCampaignLaunches(...)` and exported through `BuildScenarioWave10TelemetrySnapshot(...)`.

Do not expose mutable world internals.

Do not make diagnostics depend on ScenarioRunner-only code.

Do not change the actual launch decision in F1.

## Tests

Add focused coverage for:

- hostile lifecycle with war but no scout intel reports evaluated factions and a concrete no-launch reason,
- the expected missing-scout or no-known-target diagnostic appears if that is the blocker,
- scout-prepped deterministic setup reports known targets and launch attempt path,
- default/non-lifecycle runs stay default-safe,
- `runs[].wave10.runtimeSource` remains `main_world_run` for lifecycle configs,
- `wave10-probes.json` remains side-probe evidence.

## Verification

Run at minimum:

```powershell
dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore
dotnet build WorldSim.sln --no-restore
```

If runtime tests are added:

```powershell
dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj --filter Wave10 --no-restore
```

## Exit Conditions

F1 is GREEN if no-launch is classified by evidence rather than guessed.

If diagnostics prove target knowledge/scout hard gate is the primary blocker, proceed to F2.

If diagnostics prove another blocker, stop and update F2 before implementing behavior.

If diagnostics prove a strategy-only advisory bug, stop and ask Meta whether Track C opens.

## Handoff To F2

The F1 handoff must include:

- files changed,
- tests run,
- whether behavior changed (`no` expected),
- the dominant no-launch reason,
- evidence for or against the scout/known-target hypothesis,
- whether F2 should implement war-known target policy or a different fix.
