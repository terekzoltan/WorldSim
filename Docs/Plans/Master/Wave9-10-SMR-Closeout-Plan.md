# Wave 9-10 SMR Closeout Plan

Status: coordinator-planned, pending implementation
Owner: Meta Coordinator
Last updated: 2026-04-30

## Purpose

Add explicit SMR closeout gates to Wave 9 and Wave 10, following the Wave 8 pattern:

- First build a targeted ScenarioRunner/SMR evidence surface.
- Then let SMR Analyst run and review wave-specific evidence packages.
- Do not allow a generic all-around smoke to stand in for feature-specific proof.

This plan is the detailed source of truth for the Wave 9 and Wave 10 SMR prep/evidence steps. The Combined plan records only sequencing, ownership, and high-level gate placement.

## Core Rule

Every major wave closeout SMR package must be able to answer whether the newly implemented mechanics actually executed.

If the runner artifacts cannot answer that question, Track B must add the missing evidence surface before SMR Analyst runs the final closeout package.

## Non-Goals

- Do not redesign the whole SMR system.
- Do not make paid/live Refinery behavior part of these combat/campaign closeout gates.
- Do not add UI-only proof as a substitute for runtime/runner evidence.
- Do not promote a canonical baseline from these runs unless Meta explicitly makes a separate baseline decision.

## Shared SMR Prep Pattern

Each wave gets two final steps:

- `SMR prep - export/config`: Track B owned. Adds runner artifact blocks, drilldown fields, deterministic scenario lanes, and focused tests.
- `SMR evidence`: SMR Analyst owned. Runs and reviews the closeout packages after prep is accepted.

Shared prep acceptance:

- New artifact blocks are nullable/default-safe so old baselines still parse.
- Drilldown timeline includes compact fields for the new behavior.
- At least one deterministic lane proves the target behavior actually happens.
- Focused ScenarioRunner tests cover artifact shape, old-baseline compatibility where relevant, drilldown, and one deterministic behavior lane.
- Prep does not change gameplay rules except for small deterministic scenario setup hooks explicitly scoped to ScenarioRunner config.

Shared evidence acceptance:

- Use `Docs/Plans/Master/SMR-Minimum-Ops-Checklist.md`.
- Inspect `manifest.json`, `summary.json`, `anomalies.json`, and relevant drilldown artifacts.
- Use seeds `101,202,303` and planners `simple,goap,htn` unless SMR Analyst records a narrower exception with a follow-up recommendation.
- Record healthy signals, suspicious signals, unknowns, worst runs, and the recommended next run/baseline decision.
- State explicitly whether any finding blocks wave closeout.

## Wave 9 SMR Prep

Wave 9 scope:

- Army supply model.
- Supply carrier runtime/AI behavior.
- Foraging behavior.
- Fallback supply budget.
- Campaign skeleton through assembly, march, encounters, snapshot, and overlays.

Track B prep should add runner-visible evidence for these domains:

### Army Supply

Run-level and timeline fields should include enough of:

- active armies
- total army members
- total army ration pool or carried army supply
- supply consumed
- low-supply ticks
- out-of-supply ticks
- supply attrition events
- retreats/routes caused by low supply

### Campaign Skeleton

Run-level and timeline fields should include enough of:

- active campaigns
- campaign launches
- assembly started/completed
- march started/completed
- encounters triggered
- campaigns returned/aborted
- average or peak march distance

### Carrier And Resupply

Run-level and timeline fields should include enough of:

- carrier assignments
- resupply trips
- resupply delivered
- carrier failures or losses, if modeled

### Foraging

Run-level and timeline fields should include enough of:

- forage attempts
- forage successes
- food gained from foraging
- bounded-foraging cap evidence

### Wave 9 Deterministic Lanes

Recommended lane names:

- `army-supply-depletion`
- `carrier-resupply`
- `foraging-extension`
- `campaign-assembly-march-encounter`

Wave 9 prep is accepted only if deterministic lanes prove at least:

- supply consumption and low-supply behavior
- carrier/resupply behavior if the carrier feature is implemented
- foraging extends campaign endurance but does not replace supply
- campaign assembly/march/encounter lifecycle executes without relying on teleport-only placeholder behavior

## Wave 9 SMR Evidence

Recommended package names:

- `all-around-smoke-wave9-001`
- `wave9-campaign-supply-focused-001`

The closeout report must answer:

- Did armies consume supply?
- Did low supply affect morale, stamina, retreat, route, or campaign behavior?
- Did carrier/resupply behavior execute?
- Did foraging extend campaigns without becoming infinite food?
- Did campaigns assemble, march, and encounter targets?
- Did campaign state remain deterministic and non-stuck?
- Did new mechanics introduce starvation-with-food, clustering/backoff, no-progress, AI no-plan, or economy regressions?

Wave 9 is closeout-ready only after the SMR evidence report recommends acceptance and Meta accepts it.

## Wave 10 SMR Prep

Wave 10 scope:

- Campaign siege integration.
- Campaign resolution, loot, war score, peace.
- Strategic campaign AI.
- Campaign UI polish.
- Supply lines, convoys, forward bases, scouts.
- Dedicated siege units and bounded multi-front war.

Track B prep should add runner-visible evidence for these domains:

### Campaign Resolution

Run-level and timeline fields should include enough of:

- sieges entered from campaign flow
- breaches during campaigns
- victories and defeats
- loot transferred
- war score deltas
- forced peace or capitulation events
- campaigns stuck unresolved beyond threshold

### Supply Lines And Forward Bases

Run-level and timeline fields should include enough of:

- active supply lines
- convoys spawned
- convoys delivered
- convoys intercepted or lost
- forward base count
- forward base rest/resupply events

### Scouting

Run-level and timeline fields should include enough of:

- scout assignments
- intel discoveries
- campaigns using scout intel
- blind target launches avoided, if modeled

### Siege Units

Run-level and timeline fields should include enough of:

- siege units spawned or attached
- siege unit damage dealt
- siege unit losses
- wall or breach contribution

### Multi-Front Constraints

Run-level and timeline fields should include enough of:

- active campaigns per faction
- home garrison minimum violations
- multi-front launches blocked by constraints
- war score balancing signals

### Wave 10 Deterministic Lanes

Recommended lane names:

- `campaign-siege-resolution`
- `supply-line-convoy-intercept`
- `forward-base-long-campaign`
- `scout-intel-campaign-choice`
- `siege-unit-breach`
- `multi-front-bounded`

Wave 10 prep is accepted only if deterministic lanes prove at least:

- campaign siege reaches resolution and does not remain endless
- loot/war score/peace state changes when outcomes happen
- supply lines or forward bases affect campaign sustainment
- scouts affect campaign choice or intel if implemented
- siege units have measurable impact distinct from normal fighters
- multi-front constraints prevent runaway empty-colony behavior

## Wave 10 SMR Evidence

Recommended package names:

- `all-around-smoke-wave10-001`
- `wave10-campaign-resolution-focused-001`
- `wave10-logistics-siege-focused-001`
- optional `combat-smoke-wave10-001`

The closeout report must answer:

- Did campaigns resolve instead of running forever?
- Did war score, loot, and peace/capitulation change state deterministically?
- Did supply lines and forward bases matter?
- Did scouts affect campaign behavior or intel?
- Did siege units alter siege outcomes?
- Did multi-front constraints prevent runaway campaign count or empty home colonies?
- Did new mechanics introduce perf, clustering, no-progress, combat deadlock, or AI planning regressions?

Wave 10 is closeout-ready only after the SMR evidence report recommends acceptance and Meta accepts it.

### Wave 10 Step10B.2 Organic/Manual Lifecycle Follow-up

Step10B closed the deterministic/probe-based Wave 10 evidence gate, but the user requested an additional long-run organic/manual lifecycle evidence gate before Step10C residual decisions and Wave10.5 readiness.

Source plan:
- `Docs/Plans/Master/Wave10-Step10B2-Organic-Campaign-Lifecycle-SMR-Plan.md`

Step10B.2 must answer:
- Do campaigns emerge organically in long main-world runtime-backed runs?
- If conflict/hostile preconditions exist, does the organic campaign cadence/strategist path launch campaigns without manual creation?
- After runtime-owned manual/operator launch, does the campaign assemble, march, encounter, siege, resolve, or stall?
- Do supply lines, forward bases, scout intel, siege units, war score, loot, and peace/resolution participate naturally in long runs?
- Do long campaign runs introduce no-progress, clustering, perf, survival/economy, combat, or AI-planning regressions?

Policy:
- Step10B.2 is evidence + minimal instrumentation only; do not tune gameplay inline.
- Use runtime-backed ScenarioRunner main-run evidence for lifecycle claims, not side-probe evidence.
- Return GREEN/YELLOW/RED with precise Track routing for any gap.

Step10B.2 result:
- RED evidence. Hostile organic lifecycle produced `0/90` launches, pure organic produced `0/90` launches, stress hostile produced `0/240` launches and failed `SURV-01/02/04` in three seed-606 small-topology runs, while manual/operator lifecycle proved launch (`90/90`) and partial downstream progression only.
- Source evidence: `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`.
- This opens Step10B.5 recovery before Step10C residual disposition or Wave10.5 readiness.

### Wave 10 Step10B.5 Organic Campaign RED Recovery

Source plans:
- `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`
- `Docs/Plans/Master/Wave10-Step10B5-Track-B-Implementation-Checklist.md`
- Detailed F0-F6 execution files are indexed in the parent plan and in the Track B checklist.

Purpose:
- Convert the Step10B.2 RED lifecycle evidence into a sequenced Track B recovery pass.
- First make organic campaign no-launch explainable through runtime/main-run diagnostics.
- Then minimally fix the confirmed runtime blocker.
- Only after focused pilots pass, rerun the full Step10B.2 recovery packages.

Step10B.5 sequence:
- F0 Meta: accept Step10B.2 RED evidence and decide Step10B.2-A prep-slice disposition.
- F1 Track B: add organic launch decision-trail diagnostics with no behavior change.
- F2 Track B: implement the accepted target-knowledge policy or another blocker proven by F1.
- F3 Track B / SMR Analyst: hostile organic pilot and medium/standard confirm runs.
- F4 Track B: manual lifecycle downstream diagnostics for convoy, scout, and siege-unit non-activation.
- F5 Track B: seed-606 small-topology survival repro/fix without weakening `SURV-*`.
- F6 SMR Analyst / Meta: full recovery rerun and GREEN/YELLOW/RED closeout.

Default policy unless Meta overrides:
- `War` targets are baseline-known enough for first organic campaign launch.
- Fresh scout intel remains a quality/target-choice signal, not the only possible first-launch knowledge source under `War`.
- Neutral/Tense targets remain non-launchable.

Step10B.5 must not:
- Merge side-probe evidence into main-run lifecycle claims.
- Directly create organic campaigns in lifecycle configs.
- Tune campaign scores before decision-trail diagnostics prove score is the blocker.
- Open Track C unless diagnostics prove a strategist-only contract gap.
- Open Track A before runtime lifecycle health is restored.

Runtime-cost policy:
- Hostile organic and manual lifecycle are the decision core.
- Pure organic full matrix is deferred unless hostile/manual are healthy enough that pure rarity context matters.
- Stress begins as targeted sentinel, especially seed-606 small topology, before any broad matrix.
- Perf is a separate lane after lifecycle behavior works.
- Drilldown should be conservative and focused on non-green packages.

## Wave 10.5 Gate Impact

Wave 10.5 remains blocked until Wave 8.5 and Wave 10 closeout are both complete.

After this plan, `Wave 10 closeout` means:

- all Wave 10 implementation steps are complete
- Wave 10 SMR prep is complete
- Wave 10 SMR evidence is accepted
- the user-requested Step10B.2 organic/manual lifecycle gate has been dispositioned
- Step10B.5 RED-recovery is closed GREEN/YELLOW with Meta acceptance or explicitly deferred
- Meta Coordinator records the closeout decision

## Risks

- If prep is skipped, SMR Analyst can produce a green generic smoke while new campaign/supply mechanics never execute.
- If lanes are too scripted, they may prove only harness setup and miss organic integration problems.
- If lanes are too organic, they may be flaky and fail to exercise the target behavior.
- If artifact fields are not nullable/default-safe, compare against older baselines can break.

## Recommended Meta Policy

- Prefer deterministic focused lanes for feature proof plus all-around smoke for regression pressure.
- Keep broad baseline promotion separate from wave closeout.
- Treat warning anomalies as review inputs, not automatic blockers, unless they touch the wave's acceptance criteria.
- Add targeted follow-up runs for clustering/perf/combat deadlock when evidence points there instead of reopening unrelated feature scope.
