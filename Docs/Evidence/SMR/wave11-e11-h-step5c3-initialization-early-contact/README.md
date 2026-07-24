# Wave 11 E11-H Step 5c3 Initialization And Early-Contact Evidence

Date: 2026-07-18
Owner: SMR Analyst
Status: Review-ready after bounded identity hardening; Step 5c3 calibration acceptance not claimed

## Scope

This packet compares the Runtime-owned `legacy_random` and `habitat_aware` initial-animal
policies under the Meta-locked Step 5c3 profiles. It proves ScenarioRunner policy selection,
effective input identity, deterministic artifact generation, and early-event observability.

It does not tune Runtime behavior, change lifecycle constants, promote emergency rescue,
restore the focused five-case lifecycle sentinel, run expanded/full E11-H matrices, or prove
E11-H lifecycle acceptance.

Base before the Step 5c3 diff:

```text
a84168014d777203b50bca4184344e860954523e
```

The README is an untracked review-ready candidate intended for the authorized closeout commit.
Repository-durable closeout identity does not exist until that commit is created and verified.
Raw `.artifacts/**` remains local-only.

## Implementation Contract

The ScenarioRunner now accepts these nullable core-lane config fields:

```text
InitialAnimalPolicy
InitialAnimalAreaTilesPerAnimal
InitialAnimalPreferredPersonOrColonyDistance
InitialAnimalPreferredHerbivoreFoodRadius
InitialAnimalPreferredPredatorPreyRadius
```

`initialAnimalConfig` separates input identity from Runtime outcome truth:

| Field | Authority |
|---|---|
| `requestedPolicy` | ScenarioRunner validated config |
| `effectivePolicy` | Runtime `initialEcology.initialAnimalPolicy` |
| `effectivePolicySource` | Runtime `initialEcology.initialAnimalPolicySource` |
| density/radius fields | Exact validated/resolved effective input identity; explicit-policy paths pass these resolved options to Runtime, while the omitted-policy path preserves `ConstructorOptions=null` and reports the Runtime-default effective input identity |
| initialization outcome | Runtime-owned `initialEcology` |

All initial-animal overrides are core-only. Non-core/refinery lanes and Runtime-backed Wave10
lifecycle scenarios reject them deterministically with `config_error` instead of silently
ignoring them.

### Core Run Identity Hardening

The core ScenarioRunner lane now fails closed before simulation or generated artifact writes when
effective config names repeat under ordinal equality or parsed numeric seeds repeat. It also builds
the complete planned run-key set using the same byte-compatible formatter used by run files,
drilldown, assertions, and compare. Completed core runs receive a second uniqueness check before
envelope and artifact consumption.

Baseline envelopes are validated separately after deserialization. A duplicate semantic run
identity in a baseline produces a controlled `config_error` bundle for the valid current run:
`summary.json`, one unique run file, and `manifest.json` are present, while `compare.json` is absent.
The path no longer reaches the duplicate-key `ToDictionary` failure.

Current-input rejection does not delete or rewrite caller-owned artifact-directory content. The
focused duplicate-seed regression pre-populates a sentinel file and proves that it remains the only
entry after the rejected run. Diagnostics follow deterministic precedence (config name, seed,
planned key), describe semantic components only, and do not expose stable hashes or full config
fingerprints.

These guards execute only after the non-core/refinery return. Shared config/seed parsing,
`RefineryScenarioRunnerRequest`, refinery behavior, and the baseline compare semantic key remain
unchanged. Successful Runtime-backed lifecycle runs now explicitly lock `initialAnimalConfig:null`
in both summary and run-file output.

## Verification

All commands ran serially on Windows.

| Gate | Result |
|---|---|
| Clean-base Wave10 diagnostic | 1/1 PASS |
| Identity-hardening `EcologyInitializationCalibrationTests` | 17/17 PASS |
| `EcologyTelemetryArtifactTests`, including Runtime-backed null contract | 19/19 PASS |
| ScenarioRunner build after identity hardening | 0 warnings, 0 errors |
| ScenarioRunner.Tests build after identity hardening | 0 warnings, 0 errors |
| Prior review-fix Runtime read-only seeding/telemetry regression | 36/36 PASS; not rerun in this bounded cycle |
| Prior review-fix full ScenarioRunner suite | 126/126 PASS; not rerun in this bounded cycle |
| Prior review-fix full solution build | 0 warnings, 0 errors; not rerun in this bounded cycle |

The clean-base Wave10 diagnostic passed, so no Wave10 failure was classified as pre-existing.
This identity-hardening cycle intentionally reran only the two directly affected process-test
classes and their scoped builds. The wider Runtime, full ScenarioRunner, and full-solution rows are
retained evidence from the preceding review-fix cycle, not claims that those gates were rerun here.
The passed/failed/skipped totals from each named execution remain authoritative.

Exact focused commands:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EcologyInitializationCalibrationTests"
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EcologyTelemetryArtifactTests"
dotnet build "WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj" --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --no-restore -m:1 -p:UseSharedCompilation=false
```

## Canonical Reproduction Recipes

The commands below create fresh local-only artifacts. They intentionally fail if their generated
target already exists, and they guard the reviewed `...-001` paths from overwrite. Reproduction
means semantic equivalence of the documented profiles and evidence fields, not byte-for-byte
artifact identity.

Run both recipes from the WorldSim repository root, where `WorldSim.sln` and the
`WorldSim.ScenarioRunner` project directory are present. Their relative project and artifact paths
are intentionally repository-root-relative.

### Initialization Recipe

```powershell
[Environment]::GetEnvironmentVariables("Process").Keys |
    Where-Object { $_ -like "WORLDSIM_SCENARIO_*" } |
    ForEach-Object { [Environment]::SetEnvironmentVariable($_, $null, "Process") }
[Environment]::SetEnvironmentVariable("WORLDSIM_VISUAL_PROFILE", $null, "Process")

$runStamp = Get-Date -Format "yyyyMMdd-HHmmssfff"
$artifactDir = Join-Path (Get-Location) ".artifacts\smr\wave11-e11-h-step5c3-initialization-repro-$runStamp"
$reviewedArtifactDir = Join-Path (Get-Location) ".artifacts\smr\wave11-e11-h-step5c3-initialization-001"
if ([IO.Path]::GetFullPath($artifactDir) -eq [IO.Path]::GetFullPath($reviewedArtifactDir)) {
    throw "Refusing to overwrite the reviewed initialization artifact."
}
if (Test-Path -LiteralPath $artifactDir) {
    throw "Artifact directory already exists: $artifactDir"
}

$configsJson = @'
[
  {
    "Name": "ecology_initialization__legacy_random",
    "Width": 64,
    "Height": 40,
    "InitialPop": 24,
    "Ticks": 1,
    "Dt": 0.25,
    "EnableCombatPrimitives": false,
    "EnableDiplomacy": false,
    "EnableSiege": true,
    "StoneBuildingsEnabled": false,
    "BirthRateMultiplier": 1.0,
    "MovementSpeedMultiplier": 1.0,
    "EnablePredatorHumanAttacks": false,
    "EmergencyRescuePolicy": "disabled",
    "InitialAnimalPolicy": "legacy_random",
    "InitialAnimalAreaTilesPerAnimal": 256
  },
  {
    "Name": "ecology_initialization__habitat_aware",
    "Width": 64,
    "Height": 40,
    "InitialPop": 24,
    "Ticks": 1,
    "Dt": 0.25,
    "EnableCombatPrimitives": false,
    "EnableDiplomacy": false,
    "EnableSiege": true,
    "StoneBuildingsEnabled": false,
    "BirthRateMultiplier": 1.0,
    "MovementSpeedMultiplier": 1.0,
    "EnablePredatorHumanAttacks": false,
    "EmergencyRescuePolicy": "disabled",
    "InitialAnimalPolicy": "habitat_aware",
    "InitialAnimalAreaTilesPerAnimal": 256
  }
]
'@

$env:WORLDSIM_SCENARIO_LANE = "core"
$env:WORLDSIM_SCENARIO_MODE = "standard"
$env:WORLDSIM_SCENARIO_OUTPUT = "json"
$env:WORLDSIM_VISUAL_PROFILE = "Headless"
$env:WORLDSIM_SCENARIO_SEEDS = "101,202,303"
$env:WORLDSIM_SCENARIO_PLANNERS = "simple"
$env:WORLDSIM_SCENARIO_CONFIGS_JSON = $configsJson
$env:WORLDSIM_SCENARIO_ARTIFACT_DIR = $artifactDir

dotnet run --project "WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj" --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Initialization recipe failed with exit code $LASTEXITCODE."
}
$artifactDir
```

### Early-Contact Recipe

```powershell
[Environment]::GetEnvironmentVariables("Process").Keys |
    Where-Object { $_ -like "WORLDSIM_SCENARIO_*" } |
    ForEach-Object { [Environment]::SetEnvironmentVariable($_, $null, "Process") }
[Environment]::SetEnvironmentVariable("WORLDSIM_VISUAL_PROFILE", $null, "Process")

$runStamp = Get-Date -Format "yyyyMMdd-HHmmssfff"
$artifactDir = Join-Path (Get-Location) ".artifacts\smr\wave11-e11-h-step5c3-early-contact-repro-$runStamp"
$reviewedArtifactDir = Join-Path (Get-Location) ".artifacts\smr\wave11-e11-h-step5c3-early-contact-001"
if ([IO.Path]::GetFullPath($artifactDir) -eq [IO.Path]::GetFullPath($reviewedArtifactDir)) {
    throw "Refusing to overwrite the reviewed early-contact artifact."
}
if (Test-Path -LiteralPath $artifactDir) {
    throw "Artifact directory already exists: $artifactDir"
}

$configsJson = @'
[
  {
    "Name": "ecology_early_contact__legacy_random",
    "Width": 64,
    "Height": 40,
    "InitialPop": 24,
    "Ticks": 300,
    "Dt": 0.25,
    "EnableCombatPrimitives": false,
    "EnableDiplomacy": false,
    "EnableSiege": true,
    "StoneBuildingsEnabled": false,
    "BirthRateMultiplier": 1.0,
    "MovementSpeedMultiplier": 1.0,
    "EnablePredatorHumanAttacks": true,
    "EmergencyRescuePolicy": "disabled",
    "InitialAnimalPolicy": "legacy_random",
    "InitialAnimalAreaTilesPerAnimal": 256
  },
  {
    "Name": "ecology_early_contact__habitat_aware",
    "Width": 64,
    "Height": 40,
    "InitialPop": 24,
    "Ticks": 300,
    "Dt": 0.25,
    "EnableCombatPrimitives": false,
    "EnableDiplomacy": false,
    "EnableSiege": true,
    "StoneBuildingsEnabled": false,
    "BirthRateMultiplier": 1.0,
    "MovementSpeedMultiplier": 1.0,
    "EnablePredatorHumanAttacks": true,
    "EmergencyRescuePolicy": "disabled",
    "InitialAnimalPolicy": "habitat_aware",
    "InitialAnimalAreaTilesPerAnimal": 256
  }
]
'@

$env:WORLDSIM_SCENARIO_LANE = "core"
$env:WORLDSIM_SCENARIO_MODE = "standard"
$env:WORLDSIM_SCENARIO_OUTPUT = "json"
$env:WORLDSIM_VISUAL_PROFILE = "Headless"
$env:WORLDSIM_SCENARIO_SEEDS = "101,202,303"
$env:WORLDSIM_SCENARIO_PLANNERS = "simple"
$env:WORLDSIM_SCENARIO_CONFIGS_JSON = $configsJson
$env:WORLDSIM_SCENARIO_ARTIFACT_DIR = $artifactDir
$env:WORLDSIM_SCENARIO_DRILLDOWN = "true"
$env:WORLDSIM_SCENARIO_DRILLDOWN_TOP = "6"
$env:WORLDSIM_SCENARIO_SAMPLE_EVERY = "25"

dotnet run --project "WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj" --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Early-contact recipe failed with exit code $LASTEXITCODE."
}
$artifactDir
```

The checked-in `EcologyEarlyContact_PolicyPairExportsEventsAndAllSixDrilldowns` process contract
runs 100 ticks to verify paired identity, field shape, and drilldown completeness at bounded test
cost. The reviewed domain conclusion comes from the 300-tick recipe and artifacts above. The
100-tick contract does not prove tick-300 population outcomes, and the 300-tick calibration run
does not freeze the current prey collapse as desired product behavior.

## Evidence Authority Ledger

| Source | Authority | Not authority for |
|---|---|---|
| `manifest.json` | Package cardinality, exit/assertion/anomaly state, visual lane, compare state, and drilldown metadata | Individual config/seed/planner run identity or ecology outcomes |
| `summary.json:runs[]` and `runs/*.json` | Individual `configName`, `plannerMode`, `seed`, profile, `initialAnimalConfig`, `initialEcology`, and final `ecology` | Sampled intermediate timeline state |
| `drilldown/index.json:runs[]` | Selected `runKey` values and `timelineSamples` cardinality | Final ecology or individual sampled values |
| `drilldown/<runKey>/timeline.json` | Sample ticks and sampled `ecology` state/first-event fields | Package cardinality or final run identity |

Initial viability and distance table source fields are individual run fields:

```text
seed
initialAnimalConfig.effectivePolicy
initialEcology.initialHerbivoresSpawned
initialEcology.initialPredatorsSpawned
initialEcology.animalsOnWater
initialEcology.animalsOnMovementBlockedTiles
initialEcology.viableRegionsWithoutHerbivores
initialEcology.predatorsInPreyEmptyRegions
initialEcology.herbivoresWithFoodInVision
initialEcology.predatorsWithPreyInVision
initialEcology.predatorsWithinEarlyHumanContactRadius
initialEcology.initialSeedingFallbackCount
initialEcology.herbivoreToNearestFoodDistance.average
initialEcology.predatorToNearestPreyDistance.average
initialEcology.predatorToNearestPersonDistance.average
```

Early-event and continuity table source fields are individual run fields:

```text
ecology.herbivores
ecology.predators
ecology.ticksWithZeroHerbivores
ecology.firstZeroHerbivoreTick
ecology.firstPredatorHumanContactTick
ecology.firstPredatorHuntTick
ecology.firstHerbivoreGrazingTick
ecology.firstHerbivoreDeathTick
ecology.firstPredatorBirthTick
ecology.firstHerbivoreBirthTick
ecology.predatorHumanHits
ecology.emergencyRescues
ecology.herbivoreReplenishmentSpawns
ecology.predatorReplenishmentSpawns
```

The immediate-prey-collapse conclusion requires the combined individual-run evidence
`initialHerbivoresSpawned=5`, `initialPredatorsSpawned=5`,
`predatorToNearestPreyDistance.average=1`, `firstPredatorHuntTick=1`,
`firstZeroHerbivoreTick` equal to 2 or 3, final `ecology.herbivores=0`, and zero rescue and
replenishment counters. The legacy control conclusion requires `firstZeroHerbivoreTick=null`
and a positive final `ecology.herbivores` count at tick 300. `drilldown/index.json` proves all six
run keys were selected with 13 timeline samples each; the corresponding timeline files expose
the sampled `tick` and `ecology.*` progression. Generic drilldown score is not the domain-health
authority.

### Review-Fix Recipe Smoke

The documented recipes were run serially without modifying the reviewed `...-001` artifacts.
Fresh local-only outputs:

```text
.artifacts/smr/wave11-e11-h-step5c3-initialization-repro-20260718-164722703/
.artifacts/smr/wave11-e11-h-step5c3-early-contact-repro-20260718-164727548/
```

| Check | Initialization repro | Early-contact repro |
|---|---:|---:|
| process / manifest exit | `0` / `0` | `0` / `0` |
| seeds / planners / configs / runs | `3 / 1 / 2 / 6` | `3 / 1 / 2 / 6` |
| assertion failures / skipped / anomalies | `0 / 18 / 0` | `0 / 18 / 0` |
| effective visual lane | `Headless` | `Headless` |
| selected drilldowns | `0` | `6` |
| timeline samples per selected run | n/a | `13` |

Individual run inspection confirmed all six requested/effective/source identities. The fresh
early-contact repro also reproduced the reviewed domain result exactly: habitat-aware seeds
`101/202/303` reached zero herbivores first on ticks `2/2/3`, retained final predator count `5`,
and recorded zero rescue and replenishment; legacy controls retained positive herbivore counts
through tick 300. This is semantic recipe verification and an explicit non-GREEN domain finding,
not a product acceptance assertion.

The eighteen skipped assertions are expected in both standard-mode packages because the profiles
intentionally disable combat primitives. They are disclosed separately from the zero assertion
failures and do not turn structural artifact health into ecology acceptance.

## Initialization Lane

Local-only artifact:

```text
.artifacts/smr/wave11-e11-h-step5c3-initialization-001/
```

Profile:

```text
lane=core
mode=standard
visualProfile=Headless
planner=simple
seeds=101,202,303
world=64x40
initialPopulation=24
ticks=1
dt=0.25
combat=false
diplomacy=false
siege=true
predatorHumanAttacks=false
emergencyRescuePolicy=disabled
areaTilesPerAnimal=256
preferred overrides=null
replenishment/regrowth overrides=null
```

Manifest:

| Field | Value |
|---|---:|
| `schemaVersion` | `smr/v1` |
| `exitCode` | `0` |
| `exitReason` | `ok` |
| `seedCount` | `3` |
| `plannerCount` | `1` |
| `configCount` | `2` |
| `totalRuns` | `6` |
| `assertionFailures` | `0` |
| `anomalyCount` | `0` |
| `effectiveVisualLane` | `Headless` |
| `compareEnabled` | `false` |

The policy pair differs only in config name and `InitialAnimalPolicy`; all shared world and
numeric inputs are identical.

## Initial Viability Comparison

Abbreviations: `H/P` = initial herbivores/predators, `W/B` = animals on water/blocked tiles,
`PE` = predators in prey-empty regions, `HF/PV` = herbivores with food / predators with prey
in vision, `EH` = predators in early-human-contact radius, `FB` = fallback count.

| Seed | Policy | H/P | W/B | Viable regions without H | PE | HF/PV | EH | FB |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 101 | `habitat_aware` | 5/5 | 0/0 | 7 | 0 | 5/5 | 0 | 0 |
| 101 | `legacy_random` | 7/3 | 2/2 | 8 | 2 | 6/1 | 0 | 0 |
| 202 | `habitat_aware` | 5/5 | 0/0 | 7 | 0 | 5/5 | 1 | 1 |
| 202 | `legacy_random` | 9/1 | 1/1 | 7 | 0 | 7/0 | 0 | 0 |
| 303 | `habitat_aware` | 5/5 | 0/0 | 7 | 0 | 5/5 | 0 | 0 |
| 303 | `legacy_random` | 7/3 | 0/0 | 6 | 2 | 7/0 | 1 | 0 |

Distance averages:

| Seed | Policy | Herbivore-food | Predator-prey | Predator-person |
|---:|---|---:|---:|---:|
| 101 | `habitat_aware` | 0.000 | 1.000 | 17.800 |
| 101 | `legacy_random` | 3.286 | 12.333 | 22.667 |
| 202 | `habitat_aware` | 0.400 | 1.000 | 13.000 |
| 202 | `legacy_random` | 3.444 | 13.000 | 8.000 |
| 303 | `habitat_aware` | 0.000 | 1.000 | 14.200 |
| 303 | `legacy_random` | 2.714 | 11.667 | 9.333 |

The habitat-aware policy removes water/blocked placement, places every predator in a prey-bearing
region, and puts all herbivores within food vision. Seed 202 records one explicit
`predator_person_or_colony_distance_relaxed` fallback, matching its single preferred-distance
outlier. No fallback or outlier was hidden.

The same evidence also exposes a high 1:1 predator/herbivore budget and one-tile predator-prey
distance in every habitat-aware seed. The early-contact lane shows this is not a benign metric.

## Early-Contact Lane

Local-only artifact:

```text
.artifacts/smr/wave11-e11-h-step5c3-early-contact-001/
```

Profile differences from initialization:

```text
ticks=300
predatorHumanAttacks=true
drilldown=true
drilldownTop=6
sampleEvery=25
```

Manifest:

| Field | Value |
|---|---:|
| `exitCode` | `0` |
| `totalRuns` | `6` |
| `assertionFailures` | `0` |
| `anomalyCount` | `0` |
| `drilldownEnabled` | `true` |
| `drilldownSelectedRuns` | `6` |
| `drilldownTopN` | `6` |
| timeline samples per run | `13` |

All six expected run keys occur exactly once and have `timeline.json` plus `replay.json`.

## Early-Event And Continuity Results

`C/H/G/HD/PB/HB` means first predator-human contact, predator hunt, herbivore grazing,
herbivore death, predator birth, and herbivore birth ticks. Predator death is null in every run.

| Seed | Policy | Final H/P | Zero-H ticks / first | C/H/G/HD/PB/HB | Human hits | Rescue | Replenish H/P |
|---:|---|---:|---:|---|---:|---:|---:|
| 101 | `habitat_aware` | 0/5 | 299 / 2 | 35/1/null/1/null/null | 115 | 0 | 0/0 |
| 101 | `legacy_random` | 18/7 | 0 / null | 32/1/2/1/81/2 | 39 | 0 | 0/0 |
| 202 | `habitat_aware` | 0/5 | 299 / 2 | 11/1/null/1/null/null | 143 | 0 | 0/0 |
| 202 | `legacy_random` | 47/4 | 0 / null | 122/53/1/1/125/1 | 41 | 0 | 0/0 |
| 303 | `habitat_aware` | 0/5 | 298 / 3 | 24/1/null/1/null/null | 130 | 0 | 0/0 |
| 303 | `legacy_random` | 3/10 | 0 / null | 1/16/1/16/81/1 | 91 | 0 | 0/0 |

## Finding

The initialization metrics improve local food/prey proximity, but the promoted habitat-aware
allocation starts five predators adjacent to five herbivores. Predators hunt on tick 1 and all
herbivores are extinct by tick 2 or 3 in every tested seed. No habitat-aware run records grazing,
herbivore birth, predator birth, rescue, or replenishment before the prey collapse.

The legacy controls retain herbivores through tick 300 and show grazing and both birth paths.

This is a material Step 5c3 calibration failure. It must not be hidden behind the structurally
green artifact manifests, and it must not be fixed by stacking lifecycle tuning inside this SMR
slice. The evidence should be routed to Meta for a narrow seeding-policy calibration decision.

## Non-Claims

- `CombatPrimitives=false` means the early-contact lane is not human-retaliation acceptance proof.
- `EnablePredatorHumanAttacks=true` is identical across each policy pair and exposes predator
  harass/contact only.
- Default replenishment and regrowth settings remained active. Their counters are recorded;
  all replenishment counters were zero. Any future default replenishment event must not be
  described as a seeding-policy result.
- Cross-policy Runtime RNG consumption differs. Early-contact deltas represent the full
  initialization-policy trajectory, not an isolated position-only causal estimate.
- Standard-mode exit code `0` proves execution/config/artifact health, not ecological health.
- Baseline comparison uses `configName + planner + visual lane + seed` as its semantic run key and
  does not validate `initialAnimalConfig` policy/options equality. This package does not use compare
  mode for its cross-policy conclusion. Reusing one config name after changing policy/options must
  not be cited as no-config-drift evidence until a separate ScenarioRunner compare-identity
  hardening gate defines that contract.
- The clean `master` does not contain a runnable focused five-case `ECO-SPECIES-01/02` lifecycle
  sentinel. Step 5c3 neither restores nor claims it.
- Expanded 9-run and full 45-run E11-H matrices were not run and remain blocked.

## Optional Step 5c4 Manual Targets

Recommended qualitative targets only if Meta requests manual runtime validation during Step 5c4:

| Priority | Run | Observation target |
|---:|---|---|
| 1 | `ecology_early_contact__habitat_aware`, seed 202, Simple | One relaxed human-distance predator plus immediate one-tile prey collapse |
| 2 | `ecology_early_contact__legacy_random`, seed 202, Simple | Control topology with delayed first hunt at tick 53 and sustained herbivore growth |

Manual evidence remains qualitative and cannot override deterministic SMR results.

## Downstream Gates

- Step 5c4 may start only after Step 5c3 final GREEN and verified closeout commit. Manual targets are optional, not a default prerequisite.
- Step 5c4 must classify the immediate prey-collapse mechanism before authorizing a follow-up.
- The focused five-case lifecycle sentinel must be separately restored or redefined and run GREEN
  before the expanded 9-run gate can open.
- The historical `DEFER_STEP5C5` finding ID still requires a seven-distinct-value timeline mapping
  fixture or an explicit evidence-backed waiver/reclassification at Step 5c4 package closeout.
- Natural production-caller timestamp regressions remain a Step 5c4/E11-H package-closeout gate.
- E11-I and E11-J remain blocked until E11-H is accepted GREEN.
