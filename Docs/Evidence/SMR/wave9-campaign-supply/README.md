# Wave 9 Campaign/Supply SMR Prep

Status: Track B prep surface implemented locally; SMR Analyst validation still required.

## Purpose

This note documents the ScenarioRunner evidence surface for Wave 9 army supply and campaign skeleton validation. It is a prep/run-recipe note only and does not claim final Wave 9 evidence acceptance.

## Config Surface

Use `Wave9Scenario` in `WORLDSIM_SCENARIO_CONFIGS_JSON`.

Canonical values:
- `army_supply_depletion`
- `carrier_resupply`
- `campaign_foraging`
- `campaign_assembly_march_encounter`

Accepted aliases from the closeout plan:
- `army-supply-depletion` -> `army_supply_depletion`
- `carrier-resupply` -> `carrier_resupply`
- `foraging-extension` -> `campaign_foraging`
- `campaign-assembly-march-encounter` -> `campaign_assembly_march_encounter`

Additional accepted alias:
- `campaign-foraging` -> `campaign_foraging`

Artifact output always reports the normalized underscore value in `wave9.wave9Scenario`.

## Run-Level Deterministic Probe Evidence

Run-level and per-run JSON include a nullable/default-safe `wave9` block. When `Wave9Scenario` is configured, this block is deterministic prep/probe evidence, not organic tick-sampled runtime telemetry:

- `wave9.evidenceKind = "deterministic_probe"`
- `wave9.timelineSemantics = "not_tick_sampled"`

The run-level block has dedicated counters for:
- army supply depletion and source-specific consumption
- carrier assignments and Wave 9 carrier delivery semantics
- campaign foraging attempts/success/food gained/cap evidence
- campaign launch, assembly, march, route progress, and encounter evidence

Without a configured Wave 9 scenario, the default block remains safe to parse and reports `wave9.evidenceKind = "not_configured"` and `wave9.timelineSemantics = "not_sampled"` with counters at `0`.

## Timeline Tick-Sampled Evidence

Drilldown timeline samples include compact `wave9` fields for schema stability, but deterministic probe counters are not tick-sampled values. Until true tick-accurate Wave 9 sampling exists, timeline `wave9` counters remain empty/default and must not be interpreted as temporal proof.

In particular, final run-level counters must not be retroactively stamped onto each timeline sample.

## SMR Analyst Validation

This prep surface makes deterministic probes inspectable. It does not validate final Wave 9 behavior, organic AI endurance/foraging behavior, or full campaign closeout. SMR Analyst validation remains the required gate before any final Wave 9 acceptance claim.

## Semantics

- `carrierDeliveries` means successful Wave 9 army supply application through the current carried-inventory/ration-pool model. It is not a Wave 10 convoy or supply-line delivery metric.
- `supplySourceMode` is the last/effective source mode; source-specific counters (`memberInventoryConsumed`, `rationPoolConsumed`, `carriedInventorySupplyTicks`, `rationPoolSupplyTicks`) are the stronger multi-tick proof.
- `campaign_foraging` is a deterministic model/runtime evidence hook. It is not a claim that organic AI foraging or campaign endurance extension is validated; that belongs to the SMR Analyst validation matrix.
- `campaign_assembly_march_encounter` uses the `SimulationRuntime` campaign path (`TryCreateCampaign`, `AdvanceTick`, `Campaigns`, `GetSnapshot`) and avoids raw `World.Update` as campaign proof.

## Suggested Prep Smoke

Use a focused local smoke before SMR Analyst validation:

```powershell
$env:WORLDSIM_SCENARIO_ARTIFACT_DIR = ".artifacts/smr/wave9-campaign-supply-focused-001"
$env:WORLDSIM_SCENARIO_OUTPUT = "json"
$env:WORLDSIM_SCENARIO_SEEDS = "101,202,303"
$env:WORLDSIM_SCENARIO_PLANNERS = "simple,goap,htn"
$env:WORLDSIM_SCENARIO_DRILLDOWN = "true"
$env:WORLDSIM_SCENARIO_DRILLDOWN_TOP = "3"
$env:WORLDSIM_SCENARIO_SAMPLE_EVERY = "25"
$env:WORLDSIM_SCENARIO_CONFIGS_JSON = '[{"Name":"army-supply","Width":32,"Height":32,"InitialPop":24,"Ticks":160,"Dt":1.0,"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave9Scenario":"army_supply_depletion"},{"Name":"carrier-resupply","Width":32,"Height":32,"InitialPop":24,"Ticks":160,"Dt":1.0,"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave9Scenario":"carrier_resupply"},{"Name":"campaign-foraging","Width":32,"Height":32,"InitialPop":24,"Ticks":160,"Dt":1.0,"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave9Scenario":"campaign_foraging"},{"Name":"campaign-lifecycle","Width":32,"Height":32,"InitialPop":24,"Ticks":160,"Dt":1.0,"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave9Scenario":"campaign_assembly_march_encounter"}]'
dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj -c Release
```

Inspect at minimum:
- `manifest.json`
- `summary.json`
- `anomalies.json`
- one `runs/*.json`
- `drilldown/index.json`
- selected `drilldown/<runKey>/timeline.json`

Expected inspection result:
- run-level `wave9.evidenceKind` is `deterministic_probe`
- run-level `wave9.timelineSemantics` is `not_tick_sampled`
- selected timeline `wave9` samples keep deterministic probe counters at default values unless a future tick-accurate sampler is implemented

## Closeout Boundary

Track B prep evidence is not final Wave 9 acceptance. Final closeout requires SMR Analyst review per `Docs/Plans/Master/Wave9-10-SMR-Closeout-Plan.md` and Meta Coordinator acceptance.
