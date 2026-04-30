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

## Wave 10.5 Gate Impact

Wave 10.5 remains blocked until Wave 8.5 and Wave 10 closeout are both complete.

After this plan, `Wave 10 closeout` means:

- all Wave 10 implementation steps are complete
- Wave 10 SMR prep is complete
- Wave 10 SMR evidence is accepted
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
