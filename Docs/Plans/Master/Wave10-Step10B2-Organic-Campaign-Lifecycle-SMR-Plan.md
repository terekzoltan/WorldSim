# Wave 10 Step10B.2 - Organic Campaign Lifecycle SMR Plan

## Purpose

Step10B closed the Wave 10 evidence gate for deterministic/probe-based campaign, logistics, siege, scout, and multi-front lanes. It did not fully answer a separate player-facing question: in long normal runtime conditions, do campaigns emerge organically, and if a campaign is manually/operator-launched, what actually happens over time?

Step10B.2 is an additional SMR evidence gate before Step10C residual routing and Wave10.5 readiness. It is not a gameplay tuning step by default.

## Owner And Scope

Owner: SMR Analyst.

Allowed scope:
- ScenarioRunner evidence execution and artifact review.
- Minimal additive instrumentation/export if current artifacts cannot answer lifecycle questions.
- Runtime-backed ScenarioRunner execution path for campaign lifecycle evidence, because campaign orchestration lives under `SimulationRuntime.AdvanceTick(...)`, not raw `World.Update(...)`.
- Runtime-owned manual/operator launch invocation from ScenarioRunner using existing runtime command/API semantics.

Not allowed by default:
- Gameplay tuning to make organic campaigns more likely.
- Track C strategist/advisory changes.
- Track A/App/manual UI smoke changes.
- Global assertion weakening.
- Overclaiming side-probe evidence as main-run organic truth.
- Raw `.artifacts/smr/...` commits.

If evidence shows a real gameplay or strategist gap, Step10B.2 should return RED/YELLOW evidence plus a route to the correct Track rather than fixing it inline.

## Current Evidence Gap

Step10B accepted:
- `.artifacts/smr/all-around-smoke-wave10-001/` as broad health evidence.
- `.artifacts/smr/wave10-campaign-resolution-focused-002/` as targeted 8-lane proof evidence.
- `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/` as prep/supporting probe evidence.

Remaining gap:
- `organic_campaign_launch` in Step10B is still `simulation_runtime_probe` evidence, not a long main-world organic lifecycle run.
- In the repaired Step10B targeted package, assert-mode Wave10 companion main-world runs intentionally disable diplomacy/combat primitives for stable health checks, so they do not prove organic campaign emergence.
- Existing lane counters prove launch/setup signals, but not necessarily long lifecycle: assembly, march, encounter, siege, resolution, war score, loot, peace, supply, forward-base, scout, and siege-unit interactions under natural pressure.

## Required Instrumentation / Evidence Surface

The SMR Analyst should first inspect whether current artifacts can answer the lifecycle questions. If not, implement only the smallest additive evidence surface needed.

Expected instrumentation needs:
- A runtime-backed ScenarioRunner mode/config flag for selected runs so long lifecycle evidence uses `SimulationRuntime.AdvanceTick(...)`.
- Main-run Wave10 lifecycle telemetry where `runs[].wave10.runtimeSource = "main_world_run"` for Step10B.2 lifecycle configs.
- Manual/operator injection support: at a configured tick, call the runtime-owned manual campaign launch command/API and record attempt/success/failure.
- Timeline/drilldown samples that preserve main-run truth and do not mix side-probe counters into main-run lifecycle claims.
- Additive artifact fields only; older baseline parsing should remain default-safe.

Suggested config additions, names are illustrative:
- `Wave10Scenario = "organic_campaign_lifecycle"`
- `Wave10Scenario = "organic_hostile_campaign_lifecycle"`
- `Wave10Scenario = "manual_operator_campaign_lifecycle"`
- optional fields for manual launch tick and runtime-backed execution, if needed.

## SMR Packages

### Package A - Pure Organic Soak

Artifact: `.artifacts/smr/wave10-organic-pure-soak-001/`

Purpose:
- Check whether campaigns emerge without manual launch or deterministic campaign creation.

Suggested matrix:
- planners: `simple,goap,htn`
- seeds: `101,202,303,404,505,606,707,808,909,1001`
- configs: medium / standard / large long-run profiles
- ticks: 10k-15k minimum; increase if local runtime budget allows
- diplomacy/combat/siege: ON
- drilldown: ON with conservative sampling, e.g. every 100-250 ticks

Interpretation:
- GREEN is not required to mean pure organic always launches; pure organic may be naturally rare.
- If no pure organic launch occurs at all, report it explicitly and compare with hostile organic and manual packages.

### Package B - Hostile Organic Soak

Artifact: `.artifacts/smr/wave10-organic-hostile-soak-001/`

Purpose:
- Check whether organic campaign launch occurs when the world is put into a plausible hostile/war/tension state without directly creating a campaign.

Suggested matrix:
- planners: `simple,goap,htn`
- seeds: same 10-seed set as Package A
- configs: medium / standard / large hostile profiles
- ticks: 10k-15k minimum
- diplomacy/combat/siege: ON
- no manual campaign creation

Interpretation:
- This is the primary organic emergence package. If hostile preconditions exist and campaigns still do not launch, route to Track B/C based on observed suppress reasons and strategist outputs.

### Package C - Manual Operator Lifecycle

Artifact: `.artifacts/smr/wave10-manual-operator-lifecycle-001/`

Purpose:
- Model the player's manual/operator launch flow in headless SMR by invoking the runtime-owned operator command from ScenarioRunner, then observing the lifecycle over a long run.

Suggested matrix:
- planners: `simple,goap,htn`
- seeds: same 10-seed set
- configs: medium / standard / large manual lifecycle profiles
- ticks: 5k-10k minimum
- manual launch tick: e.g. 100 or 500
- diplomacy/combat/siege: ON

Interpretation:
- This does not prove App hotkey/UI behavior. It proves runtime behavior after the same runtime-owned launch path is invoked.

### Package D - Stress / Coverage Matrix

Artifact: `.artifacts/smr/wave10-organic-lifecycle-stress-001/`

Purpose:
- Increase coverage over map size, population, movement speed, and planner variability with shorter but broader runs.

Suggested matrix:
- planners: `simple,goap,htn`
- seeds: 10+ seeds if runtime budget permits
- configs: small / medium / standard / large; low/high population; optional normal/fast movement
- ticks: 3k-5k

Interpretation:
- Use this to identify seed-sensitive stuckness, perf, clustering, no-progress, route-budget, and launch-suppression patterns.

## Metrics To Review

Campaign lifecycle:
- campaign launches
- first campaign launch tick
- active campaigns
- resolved campaigns
- first assembly/march/encounter/siege/resolution tick if available
- campaign age / longest unresolved campaign age if available
- phase distribution or phase-tick counts if available

Organic launch and suppression:
- strategist/advisory launch attempts if available
- launch suppressed by cap, pair cap, home defense, route budget, same faction, insufficient force, missing target
- max active campaigns per faction
- max unresolved campaigns per faction pair
- home garrison violations

Resolution and outcomes:
- attacker victories
- defender-held outcomes
- war score delta
- loot food/wood/stone/gold
- peace/ceasefire applied
- unresolved campaign count at end

Logistics:
- active supply convoys
- convoys spawned/delivered/failed
- convoy throttle/cap/home-defense/route-budget blocks
- active forward bases
- forward bases established/expired/abandoned
- forward base rest ticks
- low/out-of-supply signals if available

Scout and siege:
- scout intel observed/refreshed/expired
- fresh scout intel
- campaign targets with scout intel
- campaign sieges entered
- siege pressure ticks
- breaches observed
- siege units spawned/active/inactive
- siege unit action ticks

General health:
- living colonies and population
- food/person and starvation
- combat deaths, engagements, battle ticks
- no-progress/backoff counters
- clustering/dense neighborhood counters
- AI no-plan/replan counters
- perf average/max/p99 tick time

## Acceptance Policy

Use tiered acceptance instead of a single brittle pass/fail.

GREEN:
- Packages run successfully with no hard assertion failures or severe anomalies.
- Hostile organic package produces campaign launches across multiple seed/planner combinations.
- Manual operator lifecycle package shows campaigns remain meaningful after launch and do not systematically get stuck.
- At least partial lifecycle is observed beyond launch: assembly/march/encounter and preferably siege/resolution in some lanes.
- No runaway campaign count, empty-home systemic failure, no-progress collapse, or severe perf/clustering regression.

YELLOW:
- Pure organic emergence is rare or absent, but hostile organic and manual lifecycle paths work.
- Campaigns launch and move but resolution/siege/logistics outcomes are rare.
- Evidence is enough to route focused follow-ups, but not enough to claim a mature organic lifecycle.

RED:
- Hostile organic package cannot produce campaign launches.
- Manual operator lifecycle campaigns systematically fail/stall after launch.
- Runtime-backed lifecycle telemetry cannot be produced reliably.
- Severe regressions appear in survival, economy, combat, no-progress, clustering, or perf.

## Failure Routing

- Track B route: runtime cadence, validation, cap/precondition, campaign lifecycle, ScenarioRunner evidence surface, manual/operator runtime command behavior.
- Track C route: only if evidence shows the advisory/strategist contract is the blocker rather than runtime validation/application.
- Track A/App route: only for visual/manual hotkey/readability proof after runtime lifecycle evidence is healthy.
- Meta route: classify whether a rare pure-organic launch rate is acceptable design, needs tuning, or should be deferred.

## Verification Plan For SMR Analyst

Preflight:
- `git status --short`
- Confirm only expected local artifacts/untracked architecture automation output exist.
- Confirm Step10B artifacts remain available for comparison: `all-around-smoke-wave10-001`, `wave10-campaign-resolution-focused-002`, `wave10-step10c-b-runtime-evidence-002`.

If instrumentation is added:
- focused ScenarioRunner tests for runtime-backed main-run Wave10 lifecycle config
- focused ScenarioRunner tests for manual operator launch injection
- focused tests proving `runs[].wave10.runtimeSource = "main_world_run"` for lifecycle configs
- full solution build

Evidence execution:
- run Package A, B, C, and D unless runtime cost forces an explicit staged split
- inspect `manifest.json`, `summary.json`, `runs/*.json`, `anomalies.json`, `assertions.json`, drilldown index/timelines
- rank worst runs by stuckness, no-progress, unresolved campaign age, no launch, perf, clustering, starvation/economy, and combat deadlock

Final report must answer:
- Does pure organic campaign emergence happen in long runs?
- Does hostile organic emergence happen when the world has plausible campaign pressure?
- What happens after manual/operator launch over a long runtime-backed run?
- Which lifecycle stages are actually observed?
- Do supply lines, forward bases, scouts, siege units, war score, loot, and peace/resolution activate naturally?
- Are failures design limitations, evidence gaps, or implementation bugs?
- Which Track, if any, should own the next fix?

## Handoff Summary

Step10B.2 should be executed by SMR Analyst as a large organic/manual lifecycle evidence package with minimal instrumentation if needed. The step should not tune gameplay inline. Its output is a GREEN/YELLOW/RED evidence recommendation plus precise Track routing for any discovered gaps.
