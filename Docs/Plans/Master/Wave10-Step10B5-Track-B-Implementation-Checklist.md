# Wave 10 Step10B.5 - Track B Implementation Checklist

Status: ready for F1 diagnostics
Primary owner: Track B
Parent plan: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`
Evidence source: `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`

Detailed F-slice files:
- F0: `Docs/Plans/Master/Wave10-Step10B5-F0-Evidence-Acceptance-And-Prep-Disposition.md`
- F1: `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`
- F2: `Docs/Plans/Master/Wave10-Step10B5-F2-Target-Knowledge-Policy-Fix.md`
- F3: `Docs/Plans/Master/Wave10-Step10B5-F3-Hostile-Organic-Pilot-And-Confirm.md`
- F4: `Docs/Plans/Master/Wave10-Step10B5-F4-Manual-Downstream-Diagnostics.md`
- F5: `Docs/Plans/Master/Wave10-Step10B5-F5-Stress-Seed606-Survival-Repro-Fix.md`
- F6: `Docs/Plans/Master/Wave10-Step10B5-F6-Full-Recovery-Rerun-And-Closeout.md`

## Mission

Recover the Step10B.2 RED evidence by making organic campaign launch behavior observable, then minimally fixing the confirmed runtime blocker. Do not jump straight to large SMR reruns. Do not tune broadly before diagnostics prove the blocker.

## Active Hypothesis To Validate

Hostile organic no-launch likely occurs because the strategist requires `CampaignTargetOption.IsKnown`, and `IsKnown` currently requires fresh scout intel. Step10B.2 hostile setup declares war but does not create scout intel, so the strategist likely returns `NoViableTarget` before runtime application and suppression counters remain zero.

This is a hypothesis. The first implementation slice must prove or disprove it.

## Guardrails

- Preserve `SimulationRuntime.AdvanceTick(...)` as the only campaign orchestration path for lifecycle SMR.
- Preserve `runs[].wave10` as main-run truth.
- Preserve `wave10-probes.json` as side-probe evidence only.
- Do not directly create campaigns in organic lifecycle configs.
- Do not weaken `SURV-*` assertions.
- Do not change Track C strategy internals unless Meta opens Track C from diagnostic evidence.
- Do not change App/Graphics.
- Do not commit raw `.artifacts/smr/...`.

## Suggested File Scope

Expected Track B files:
- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.Runtime/Diagnostics/ScenarioWave10Telemetry.cs`
- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`
- `WorldSim.Runtime.Tests/*Wave10*.cs` if focused runtime coverage is cleaner there

Only touch `WorldSim.AI/CampaignStrategy.cs` or `WorldSim.AI.Tests/CampaignStrategyTests.cs` if F1 diagnostics prove a strategy-only gap and Meta explicitly opens Track C/strategy work.

## Execution Order

### 1. Preflight

Read first:
- `ops/PROJECT_STATE.md`
- `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`
- `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`
- `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`

Then inspect:
- current worktree status,
- accepted Step10B.2-A prep slice commit `e4bb0a1 feat(wave10): add step10b2 lifecycle evidence surface`,
- whether any new local changes exist beyond the accepted prep slice.

Stop condition:
- If new local changes make prep-slice ownership/status unclear again, ask Meta before mixing them with behavior fixes.

### 2. F1 Diagnostics Only

Implement no behavior change.

Add organic launch decision-trail telemetry that can answer:
- organic evaluation ticks,
- owner factions evaluated,
- eligible members,
- available warriors after home reserve,
- target options count,
- target options by stance,
- known target count,
- missing-scout unknown target count,
- last/best strategist decision kind,
- last/best strategist reason code,
- best pressure/advantage/distance/score,
- launch apply attempt count,
- launch apply success/failure by status.

Tests required:
- hostile lifecycle without scout reports a concrete no-launch reason,
- scout-prepped deterministic organic setup reports known target and launch attempt,
- default/non-lifecycle runs remain default-safe,
- side-probe provenance remains separate.

Verification:
- focused runtime tests if added,
- `dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore`,
- `dotnet build WorldSim.sln --no-restore`.

Exit criteria:
- If target knowledge/scout hard gate is not the blocker, stop and update the plan with the proven blocker.
- If target knowledge is the blocker, continue to F2.

### 3. F2 Target Knowledge Policy Fix

Implement only after F1 confirms the blocker or Meta explicitly confirms the default policy.

Recommended policy:
- `War` target colony is baseline-known for minimal organic launch.
- `Hostile` target remains scout-gated unless F1 evidence and Meta policy say otherwise.
- Fresh scout intel remains a quality signal and should still populate scout fields.
- Neutral/Tense are not launchable.

Implementation checklist:
- Add a resolver for organic target knowledge.
- Add optional target knowledge source telemetry.
- Preserve cap/home-defense/pair/route validation.
- Preserve same-faction rejection.

Tests required:
- War target without scout can be known.
- Neutral/Tense target without scout is not known.
- Fresh scout target still reports scout data.
- No-warrior, cap, pair-cap, and route failures still block correctly.
- Hostile lifecycle ScenarioRunner pilot uses `runtimeSource=main_world_run`.

Verification:
- runtime focused tests,
- ScenarioRunner focused tests,
- full solution build.

### 4. F3 Hostile Organic Pilot

Do not run the full 90-run package first.

Run staged evidence:
- 1 seed x 1 planner x medium hostile x 1000-2000 ticks.
- 3 seeds x 3 planners x medium hostile x 4000-6000 ticks.
- 3 seeds x 3 planners x standard hostile x 4000-6000 ticks.

Review:
- launch count,
- first launch tick,
- known target count,
- last decision reason,
- survival assertions,
- perf/anomaly noise.

Stop condition:
- If launches are still zero, do not run full package. Return RED with diagnostics.

Continue condition:
- Multiple seed/planner combinations launch or remaining no-launch runs are clearly explained.

### 5. F4 Manual Downstream Diagnostics

Add or fix reason counters before changing behavior.

Convoy questions:
- Did any campaign become low supply?
- Did the strategist request a convoy?
- Was convoy spawn blocked by cap/throttle/home-defense/route?
- Did convoy spawn but not deliver/fail?

Scout questions:
- Were scout actors present?
- Were observations attempted?
- Were targets out of radius?
- Were observations skipped by relation or freshness?

Siege-unit questions:
- Was `siege_craft` unlocked?
- Did the campaign enter an encounter/siege-relevant state?
- Was there a valid target structure?
- Was spawn skipped because a unit already existed or resolver was disabled?

Allowed behavior fixes:
- Fix unreachable runtime lifecycle maintenance.
- Fix missing reason counters.
- Add a dedicated tech-enabled evidence config only if Meta agrees it is needed for siege-unit lifecycle proof.

Do not:
- globally unlock `siege_craft` in all lifecycle runs,
- claim no-tech runs prove dedicated siege-unit failure,
- overclaim convoy request as delivery proof.

### 6. F5 Stress Seed-606 Survival Repro And Fix

Reproduce only the failing lanes first:
- `small-highpop | Htn | seed 606`,
- `small-lowpop | Simple | seed 606`,
- `small-lowpop | Goap | seed 606`.

Capture first bad tick for:
- living colony count,
- people count,
- average food per person,
- starvation deaths,
- combat deaths,
- predator deaths,
- food nodes,
- no-progress/backoff,
- dense neighborhood.

Fix policy:
- If hostile precondition is too early/aggressive on small maps, add a warmup or scoped stress setup policy.
- If ecology collapses, fix narrow runtime balance with focused tests.
- If movement/clustering collapses, route with evidence.
- Never weaken `SURV-*`.

Exit criteria:
- The three failing lanes pass, or Meta accepts a separate known limitation route.

### 7. F6 Full Recovery Rerun Handoff

Only after F3-F5 pass.

SMR Analyst should run staged packages, not the full historical 5-hour shape by default:
- `wave10-organic-hostile-soak-002`,
- `wave10-manual-operator-lifecycle-002`,
- `wave10-organic-pure-soak-002` only if hostile/manual are healthy enough that pure rarity context matters,
- `wave10-organic-lifecycle-stress-002` only after targeted stress sentinel lanes pass or are explicitly routed.

Runtime-cost policy:
- hostile + manual are the decision core,
- pure starts as small context matrix, not full 90-run default,
- stress starts as seed-606 sentinel, not full 240-run default,
- perf is a separate lane after lifecycle works,
- drilldown should be conservative unless a package is non-green.

The final report must answer:
- Did hostile organic campaigns launch?
- Did pure organic campaigns launch or remain acceptably rare?
- Did manual lifecycle remain healthy?
- Which downstream lifecycle systems activated?
- Are convoy/scout/siege-unit gaps fixed, explained, or routed?
- Did stress survival recover?
- Is Track C needed?

## Minimal Command Set

Use focused gates first:

```powershell
dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj --filter Wave10 --no-restore
dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore
dotnet build WorldSim.sln --no-restore
```

Only run `WorldSim.AI.Tests` if strategy code is touched:

```powershell
dotnet test WorldSim.AI.Tests/WorldSim.AI.Tests.csproj --filter CampaignStrategyTests --no-restore
```

## Handoff Message Required At Close

Track B must end with:
- exact files changed,
- exact tests run,
- whether F1 confirmed the scout/known-target hypothesis,
- launch incidence from pilot/confirm runs,
- any survival repro/fix result,
- whether Track C is still closed or explicitly needed,
- whether SMR Analyst can start F6.
