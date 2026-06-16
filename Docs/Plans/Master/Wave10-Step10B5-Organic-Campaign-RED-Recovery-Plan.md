# Wave 10 Step10B.5 - Organic Campaign RED Recovery Plan

Status: planned
Owner: Meta Coordinator for sequencing; Track B primary implementation
Source evidence: `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`
Companion execution checklist: `Docs/Plans/Master/Wave10-Step10B5-Track-B-Implementation-Checklist.md`

F-slice detail files:
- F0: `Docs/Plans/Master/Wave10-Step10B5-F0-Evidence-Acceptance-And-Prep-Disposition.md`
- F1: `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`
- F2: `Docs/Plans/Master/Wave10-Step10B5-F2-Target-Knowledge-Policy-Fix.md`
- F3: `Docs/Plans/Master/Wave10-Step10B5-F3-Hostile-Organic-Pilot-And-Confirm.md`
- F4: `Docs/Plans/Master/Wave10-Step10B5-F4-Manual-Downstream-Diagnostics.md`
- F5: `Docs/Plans/Master/Wave10-Step10B5-F5-Stress-Seed606-Survival-Repro-Fix.md`
- F6: `Docs/Plans/Master/Wave10-Step10B5-F6-Full-Recovery-Rerun-And-Closeout.md`

## Purpose

Step10B.2 produced runtime-backed main-run lifecycle evidence and returned RED. The failure is not a generic smoke issue: hostile organic campaigns never launched, pure organic campaigns never launched, the stress hostile matrix never launched, and three small-topology seed-606 stress runs failed survival assertions.

Step10B.5 is the recovery gate that turns that RED evidence into a sequenced fix plan. The goal is not to make one scripted lane green. The goal is to make the organic campaign lifecycle explainable, launchable under hostile/war pressure, and stable enough that Step10B.2 can be rerun with meaningful main-run evidence before Step10C residual routing or Wave10.5 readiness.

## Source Facts

Accepted Step10B.2 facts:
- `wave10-organic-hostile-soak-001`: `0/90` launches, no encounter, no siege, no resolution, no convoy, no scout, no siege-unit signal.
- `wave10-organic-pure-soak-001`: `0/90` launches; `evidenceStatus=positive` must not be read as launch proof when counters are zero.
- `wave10-organic-lifecycle-stress-001`: `0/240` launches and `exitCode=2` due to `SURV-01/02/04` failures in three small-topology seed-606 runs.
- `wave10-manual-operator-lifecycle-001`: `90/90` manual launches succeeded, `44/90` reached encounter/resolution, `11/90` reached siege, and `71/90` established forward bases.
- Manual lifecycle did not naturally activate convoys, scouts, or dedicated siege units.
- Existing suppression counters stayed zero in organic no-launch packages, so the current telemetry does not explain whether the blocker is cadence, precondition, target visibility, strategy, or validation.

Current code-level hypothesis from the evidence review:
- `SimulationRuntime.AdvanceTick(...)` calls `EvaluateOrganicCampaignLaunches(...)` before world update.
- `DefaultCampaignStrategist` only launches when a target is viable.
- `CampaignTargetOption.IsKnown` is currently tied to fresh actionable scout intel.
- Step10B.2 hostile lifecycle setup declares war but does not create scout intel.
- With no known target, the strategist likely returns `HoldDefensivePosture / NoViableTarget` before runtime application is attempted.
- Because runtime application is not attempted, cap/home-defense/route suppression counters remain zero.

This is a hypothesis to validate in Step10B.5-F1, not a license for blind tuning.

## Required Policy Decision

Default policy for implementation unless Meta overrides it:
- A `War` relation makes an enemy colony baseline-known enough for a minimal organic campaign launch.
- A `Hostile` relation may become baseline-known only under explicit runtime policy, or may still require fresh scout intel depending on diagnostics.
- Fresh scout intel remains a quality/confidence/target-choice signal, not the only possible first-launch knowledge source under `War`.
- Neutral or Tense targets remain non-launchable without future explicit design work.

If Meta rejects this default, Step10B.5-F2 must instead implement natural scout availability/observation as the first organic launch unlock path. Do not mix both policies in one unreviewed change.

## Non-Goals

- Do not weaken Step10B.2 acceptance criteria to make RED evidence disappear.
- Do not use `wave10-probes.json` as organic lifecycle proof.
- Do not directly create campaigns in organic lifecycle configs.
- Do not globally tune campaign score thresholds before decision-trail telemetry proves the score is the blocker.
- Do not add Track C strategist behavior unless runtime diagnostics prove a strategist-only contract gap.
- Do not add Track A/UI/manual smoke fixes before runtime lifecycle health is restored.
- Do not commit raw `.artifacts/smr/...` bundles.

## Ownership

Track B owns:
- Runtime organic launch decision-trail telemetry.
- Target knowledge policy and runtime validation/application.
- ScenarioRunner lifecycle evidence surface and focused tests.
- Manual lifecycle downstream diagnostics for convoy/scout/siege-unit non-activation.
- Stress seed-606 small-topology survival investigation and fix.

SMR Analyst owns:
- Pilot and full package reruns after Track B gates are green.
- Counter-based evidence review and GREEN/YELLOW/RED recommendation.

Meta Coordinator owns:
- Policy decisions.
- Cross-track routing.
- Closeout/defer decisions for Step10B.5, Step10C, and Wave10.5 readiness.

Track C stays closed unless:
- Track B diagnostics show valid known targets, valid warriors, valid scores, and valid runtime application opportunity, but the strategist still emits a wrong advisory decision.
- Or Track B proves natural scout role/behavior requires AI/planner ownership rather than runtime state policy.

Track A stays deferred unless:
- Runtime lifecycle evidence becomes healthy and the remaining issue is visual/manual/operator readability.

## Sequential Plan

### Step10B.5-F0 - Evidence Acceptance And Prep-Slice Disposition

Owner: Meta Coordinator

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F0-Evidence-Acceptance-And-Prep-Disposition.md`

Prereq:
- Step10B accepted GREEN.
- Step10B.2 evidence note exists and is reviewed.

Actions:
- Accept Step10B.2 as RED evidence.
- Record that Step10C residual routing and Wave10.5 readiness remain blocked.
- Review the local Step10B.2-A prep slice separately from gameplay fixes.
- Decide whether to commit the prep slice as an evidence-surface commit before Track B begins behavior work.
- Preserve local raw artifacts as local evidence only.

Acceptance:
- Combined plan references Step10B.5 as the next recovery gate.
- `ops/PROJECT_STATE.md` points to Track B Step10B.5-F1/F2 as the next role/action.
- No Track C or Track A work is opened.

Unlocks:
- Step10B.5-F1.

### Step10B.5-F1 - Organic Launch Decision-Trail Diagnostics

Owner: Track B

Scope: instrumentation only; no behavior change.

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`

Problem:
- Current no-launch evidence is ambiguous because zero suppression counters can mean the runtime never tried to apply a launch.

Implementation targets:
- `WorldSim.Runtime/SimulationRuntime.cs`
- `WorldSim.Runtime/Diagnostics/ScenarioWave10Telemetry.cs`
- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`
- Runtime focused tests if a separate runtime diagnostics surface is added.

Additive diagnostics should answer:
- Was organic evaluation called?
- Which factions were evaluated?
- How many eligible members and available warriors existed per owner faction?
- How many target options existed?
- How many target options were War, Hostile, Tense, Neutral?
- How many target options were known vs unknown?
- How many targets were unknown only because fresh scout intel was missing?
- What strategist decision kind and reason code were emitted?
- What was the best launch score, pressure score, advantage score, and distance penalty observed?
- Did runtime application get attempted?
- If attempted, what `CampaignCreationStatus` resulted?

Recommended artifact additions:
- Add nullable/default-safe fields under `runs[].wave10` or a nested `organicLaunchDiagnostics` block.
- Add compact drilldown timeline fields for last/best organic decision state.
- Keep old baseline parsing safe.

Focused tests:
- Hostile lifecycle with `DeclareWar` but no scout intel reports evaluation and `NoViableTarget` or equivalent target-knowledge blocker rather than unexplained zero launch.
- Deterministic scout-prepped organic setup reports known target and launch attempt.
- Non-lifecycle/default runs remain `not_configured` / `not_sampled`.
- `wave10-probes.json` remains side-probe evidence and is not merged into main-run lifecycle claims.

Acceptance:
- A no-launch run has a concrete reason trail.
- Hostile organic no-launch can be classified as target knowledge, warriors, score, caps, route, cadence, or strategist behavior.
- No gameplay behavior changes are included in F1.

Unlocks:
- Step10B.5-F2 if target knowledge is confirmed as the primary blocker.
- Track C routing only if F1 proves a strategist-only blocker.

### Step10B.5-F2 - Organic Target Knowledge Policy Fix

Owner: Track B

Scope: minimal runtime behavior fix; no broad balancing.

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F2-Target-Knowledge-Policy-Fix.md`

Prereq:
- F1 diagnostics identify target knowledge / scout hard gate as the main no-launch blocker, or Meta explicitly accepts the default war-known policy.

Recommended implementation:
- Introduce a runtime-owned target knowledge resolver, for example `ResolveOrganicCampaignTargetKnowledge(...)`.
- Treat `Stance.War` target colonies as baseline-known for minimal organic launch.
- Treat `Stance.Hostile` conservatively: either require scout intel or allow baseline-known only if F1 evidence shows hostile pressure should be enough. If uncertain, start with War-only baseline-known and keep Hostile scout-gated.
- Preserve scout fields as additional quality data:
  - `HasScoutIntel`
  - `ScoutIntelTicksSinceRefresh`
  - `ScoutIntelConfidence`
  - optional `TargetKnowledgeSource` such as `fresh_scout_intel`, `war_relation`, `hostile_relation`, `unknown`.
- Do not make Neutral/Tense targets launchable.
- Do not bypass cap, home-defense, pair-cap, route preflight, or same-faction validation.

Focused tests:
- War target without scout intel can become viable if warriors and route are valid.
- Neutral/Tense targets without scout intel stay non-viable.
- Fresh scout intel still marks target with scout fields and remains preferred/visible in telemetry.
- No available warriors still prevents launch.
- Pair cap / owner cap / route preflight still block launch and increment the right counters.
- ScenarioRunner lifecycle pilot returns `runs[].wave10.runtimeSource = main_world_run` and no side-probe overclaim.

Acceptance:
- A controlled hostile/war lifecycle pilot produces at least one organic launch or a new explicit blocker reason.
- Diagnostics show target known count is no longer zero under War preconditions.
- Existing deterministic Step10B probe lanes are unchanged.

Unlocks:
- Step10B.5-F3.

### Step10B.5-F3 - Hostile Organic Pilot And Medium Confirm

Owner: Track B for targeted rerun; SMR Analyst may assist with artifact review

Scope: verification only after F2.

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F3-Hostile-Organic-Pilot-And-Confirm.md`

Run order:
- Tiny pilot: one planner, one seed, one medium hostile config, 1000-2000 ticks.
- Medium confirm: 3 seeds x 3 planners x medium hostile, 4000-6000 ticks.
- Standard confirm: 3 seeds x 3 planners x standard hostile, 4000-6000 ticks.

Evidence requirements:
- `campaignLaunches > 0` in multiple seed/planner combinations, or a classified remaining blocker.
- `firstCampaignLaunchTick` present where launch occurs.
- Decision-trail fields explain any no-launch run.
- No hard survival assertion failures in pilot/confirm lanes.

Acceptance:
- If hostile organic remains `0` launches after F2, stop and return RED with the new diagnostic reason. Do not run full expensive packages.
- If medium/standard confirm launches, proceed to F4/F5 before full SMR rerun.

Unlocks:
- Step10B.5-F4 and Step10B.5-F5.

### Step10B.5-F4 - Manual Lifecycle Downstream Diagnostics

Owner: Track B

Scope: explain convoy, scout, and siege-unit non-activation under manual lifecycle.

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F4-Manual-Downstream-Diagnostics.md`

Problems from Step10B.2:
- Manual launch succeeded `90/90`.
- Forward bases established in `71/90`.
- Convoys, scouts, and siege units stayed zero.

Convoy diagnostics:
- Active campaigns evaluated for convoy.
- Min/avg campaign supply readiness.
- Sustained out-of-supply ticks max.
- Convoy eligible campaign count.
- Convoy request decision count.
- Spawn blocks by cap, throttle, home-defense, route.

Scout diagnostics:
- Active scout actors by faction.
- Scout-capable population.
- Observation attempts.
- Observations skipped by relation.
- Observations skipped by radius.
- Nearest hostile target distance vs scout radius.

Siege-unit diagnostics:
- Attacker `siege_craft` unlocked state.
- Siege-unit eligibility checks.
- No-spawn reason: tech locked, no encounter/siege relevance, no target structure, already spawned, resolver disabled, invalid campaign.

Allowed fixes:
- Fix clear runtime reason-counter gaps.
- Fix lifecycle maintenance if convoy/scout/siege-unit logic is unreachable despite valid conditions.
- Do not unlock tech globally in all lifecycle configs unless a dedicated tech-enabled package is explicitly added.

Acceptance:
- Manual lifecycle no-convoy/no-scout/no-siege-unit is explained by counters.
- Any behavior fix has focused tests and does not regress manual launch success.
- If dedicated siege units require `siege_craft`, the final evidence wording must say so and not treat default no-tech runs as siege-unit failure.

Unlocks:
- Step10B.5-F6 after F5.

### Step10B.5-F5 - Stress Seed-606 Small-Topology Survival Fix

Owner: Track B

Scope: reproduce, diagnose, and minimally fix survival assertion failures.

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F5-Stress-Seed606-Survival-Repro-Fix.md`

Failing lanes:
- `small-highpop | Htn | seed 606`
- `small-lowpop | Simple | seed 606`
- `small-lowpop | Goap | seed 606`

Investigation should identify first bad tick for:
- living colony drop,
- people collapse,
- food-per-person collapse,
- starvation deaths,
- combat deaths,
- predator deaths,
- food node depletion,
- hostile combat pressure,
- no-progress/backoff,
- dense-neighborhood pressure.

Fix policy:
- Do not weaken `SURV-01/02/04` assertions.
- If this is hostile setup too aggressive for small topology, adjust setup sequencing or warmup policy rather than global ecology tuning.
- If this is ecology/resource collapse, prefer narrow runtime balance with focused regression coverage.
- If this is movement/clustering collapse, route to movement/occupancy only with evidence.

Acceptance:
- The three seed-606 repro lanes no longer fail `SURV-01/02/04`, or a separate Meta-accepted small-topology limitation route is documented.
- The fix does not hide organic launch diagnostics.

Unlocks:
- Step10B.5-F6.

### Step10B.5-F6 - Full Recovery Rerun And Closeout

Owner: SMR Analyst for execution/review; Meta for final closeout

Detailed file: `Docs/Plans/Master/Wave10-Step10B5-F6-Full-Recovery-Rerun-And-Closeout.md`

Prereq:
- F3 hostile organic confirm shows launch in multiple combinations or an accepted classified limitation.
- F4 explains manual downstream gaps.
- F5 handles seed-606 survival failures or routes them explicitly.

Run order:
- `wave10-organic-hostile-soak-002` first.
- `wave10-manual-operator-lifecycle-002` second.
- `wave10-organic-pure-soak-002` third.
- `wave10-organic-lifecycle-stress-002` fourth.

Full rerun rules:
- Keep runtime-backed main-run truth.
- Keep `wave10-probes.json` side-probe only.
- Use conservative drilldown sampling to avoid artifact explosion.
- Do not promote new artifacts to baseline unless Meta opens a separate baseline step.

GREEN criteria:
- Hostile organic launches in multiple seed/planner/config combinations.
- Manual lifecycle remains healthy and does not systematically stall.
- At least partial lifecycle beyond launch is observed: assembly/march/encounter and preferably siege/resolution in some lanes.
- No hard `SURV-*` assertion failures.
- Remaining convoy/scout/siege-unit gaps are either fixed or explicitly classified by reason counters.

YELLOW criteria:
- Hostile organic launches but low incidence.
- Pure organic remains rare/zero.
- Convoy/scout/siege-unit remain sparse but are explained and routed.
- No hard survival assertions.

RED criteria:
- Hostile organic still has zero launches.
- Manual lifecycle regresses.
- Survival failures persist.
- Telemetry cannot explain the remaining no-launch/no-lifecycle result.

Closeout outputs:
- Checked-in evidence note under `Docs/Evidence/SMR/...` if Meta requests persistent evidence.
- Combined plan status update.
- `ops/PROJECT_STATE.md` update.
- Track C/A routing only if evidence explicitly proves those owners.

## Verification Matrix

Minimum focused gates before SMR rerun:
- `dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj --filter Wave10 --no-restore`
- `dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore`
- `dotnet test WorldSim.AI.Tests/WorldSim.AI.Tests.csproj --filter CampaignStrategyTests --no-restore` only if Track C/strategy tests are touched.
- `dotnet build WorldSim.sln --no-restore`

SMR gates should be staged:
- Pilot before any full package.
- Medium/standard confirm before large/stress.
- Full B/C/A/D rerun only after focused gates pass.

## Evidence Interpretation Rules

- `campaignLaunches=0` overrides any positive-looking status label for organic lifecycle proof.
- `evidenceStatus=positive` means artifact shape/run completed unless counters prove lifecycle behavior.
- `manual_operator` proof is runtime command proof, not App hotkey/UI proof.
- Convoy proof should distinguish requested/spawned/delivered/failed.
- Siege-unit proof should state whether `siege_craft` was unlocked.
- Scout proof should distinguish scout actor presence, observation, freshness, and campaign target use.

## Done Definition

Step10B.5 is done when Meta can state one of:
- GREEN: hostile organic and manual lifecycle are healthy enough to unblock Wave10.5 after any Step10C disposition.
- YELLOW: organic/manual lifecycle is partially healthy with explicit routed residuals, and Meta accepts the remaining limitations.
- RED: recovery failed with a clear owner and next fix scope.

Until then, Wave10.5 remains blocked.
