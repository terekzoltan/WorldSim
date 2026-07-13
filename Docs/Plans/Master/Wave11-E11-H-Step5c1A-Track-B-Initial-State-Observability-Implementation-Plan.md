# Wave 11 E11-H Step 5c1-A - Track B Initial-State Observability Implementation Plan

Status: ACCEPTED GREEN - Step 5c1-B unlocked
Owner: Track B
Reviewer: Meta Coordinator
Downstream consumer: SMR Analyst in Step 5c1-B
Last updated: 2026-07-11

## Target

Implement the Runtime-owned initial ecology telemetry producer and first-event tick instrumentation without changing initial spawn behavior.

Canonical parent: `Docs/Plans/Master/Wave11-E11-H-Step5c-Habitat-Aware-Ecology-Seeding-And-SMR-Calibration-Plan.md`.

Manual evidence input: `Docs/Evidence/Manual/Wave11-E11-H-Step5c-Manual-Observation-001.md`.

## Readiness

- Step 5b4 diagnostic fallback is accepted.
- Failed replacement-headroom and survival/reproduction candidates are reverted/not retained.
- Step 5c is canonical.
- This step is observability-only and must preserve legacy random initialization exactly.
- Meta review approved the exact nullable distance/ratio types, first-event semantics,
  E11-G wrapper exceptions, downstream compile gate, and mandatory deep-review below.
- No further user or cross-Track decision is required before implementation.

## Pre-Edit Dirty-Hunk Gate

All three scoped files already contain accepted-prior E11-G supply telemetry hunks. Before
editing source, capture their exact `git diff --numstat` and unified diff. Preserve every
existing E11-G field, `Empty` value, builder mapping, counter, harvest path, and test
assertion.

Two explicit additive exceptions are permitted:

- `ReportPlantFoodConsumedByAnimals(int amount)` may add positive-amount-only,
  first-write-only grazing tick assignment after the existing forwarding call;
- `ReportMeatFromHunt(int amount)` may add positive-amount-only, first-write-only hunt tick
  assignment after the existing forwarding call.

For both wrappers, `_ecologyState.Report...(amount)` must remain the first operation, receive
the same amount, and execute exactly once. No early return, extra counter, or RNG call is
allowed. Every other E11-G hunk remains textually and semantically unchanged.

## Legacy Initialization Characterization Gate

Before any production edit, add test-only characterization for:

- `World(16, 16, 8, randomSeed: 42)`;
- the literal ordered initial `(AnimalKind, X, Y)` roster;
- the literal next post-construction `world.CreateEntityRng().Next()` value.

Extract and hard-code both literals while production code is still unchanged. The roster
guards initial count/order/species/coordinates; the RNG sentinel guards against telemetry
consuming world RNG after animal creation. If seed `42` has no positive water/blocked
placement, select and hard-code a separate fixed seed before production edits. Final tests
must not search dynamically for a suitable seed.

## File Scope

Allowed production files:

- `WorldSim.Runtime/Diagnostics/ScenarioEcologyTelemetry.cs`
- `WorldSim.Runtime/Simulation/World.cs`

Allowed test file:

- `WorldSim.Runtime.Tests/ScenarioEcologyTelemetryTests.cs`

No other source file is in scope without a stop-and-return Meta handoff.

## Locked Runtime Contract

Add Runtime diagnostics records under `WorldSim.Runtime.Diagnostics`.

### Distance Summary

`ScenarioEcologyDistanceSummarySnapshot`:

- `SampleCount: int`
- `Minimum: int?`
- `Maximum: int?`
- `Average: double?`

Empty samples use `SampleCount=0` with nullable distance values. Do not use magic numeric sentinels.
Only a source actor with at least one eligible target contributes a nearest-target sample.

### Per-Region Initial State

`ScenarioInitialEcologyRegionSnapshot`:

- `RegionId`
- `LandTileCount`
- `PlantCapacityTotal`
- `ActiveFoodNodes`
- `Herbivores`
- `Predators`

Regions must be emitted in ascending `RegionId` order.

### Initial Ecology Snapshot

`ScenarioInitialEcologyTelemetrySnapshot`:

- `InitialAnimalPolicy`: `legacy_random` in Step 5c1-A
- `InitialAnimalPolicySource`: `runtime_default` in Step 5c1-A
- `TotalAnimals`
- `Herbivores`
- `Predators`
- `PredatorHerbivoreRatio: double?`, nullable when no herbivore denominator exists
- `AnimalsOnWater`
- `AnimalsOnMovementBlockedTiles`
- `ViableRegions`
- `ViableRegionsWithoutHerbivores`
- `PredatorsInPreyEmptyRegions`
- `HerbivoresWithFoodInVision`
- `PredatorsWithPreyInVision`
- `PredatorsWithinHumanHarassRadius`
- `PredatorsWithinEarlyHumanContactRadius`
- `FoodVisionRadius`
- `PreyVisionRadius`
- `HumanHarassRadius`
- `EarlyHumanContactRadius`
- `HerbivoreToNearestFoodDistance`
- `PredatorToNearestPreyDistance`
- `PredatorToNearestPersonDistance`
- `Regions`

Only actors alive at capture are included. Person liveness uses `Health > 0f`. `Regions`
must be an ascending, deeply read-only copy using `Array.AsReadOnly(regions.ToArray())`; do
not expose a mutable list or source-array alias. The public builder returns the same cached
snapshot instance on every call.

Observation radii must be named constants in the diagnostic builder and exported in the snapshot. Use current behavior horizons where they exist:

- herbivore food vision: `5`;
- predator prey vision: `6`;
- predator human harass radius: `2`;
- early human contact diagnostic radius: `6`.

These values are observability horizons in Step 5c1-A, not accepted Step 5c2 seeding policy thresholds.

### First-Event Fields

Extend `ScenarioEcologyTelemetrySnapshot` and its compact timeline projection with nullable first-event ticks:

- `FirstPredatorHumanContactTick`
- `FirstPredatorHuntTick`
- `FirstHerbivoreGrazingTick`
- `FirstPredatorDeathTick`
- `FirstHerbivoreDeathTick`
- `FirstPredatorBirthTick`
- `FirstHerbivoreBirthTick`

Only the first event sets each value. Later events must not overwrite it.
Tick `0` is valid for a direct pre-update event; missing events remain `null`. Birth fields
mean successful queue-acceptance ticks. Herbivore death means the tick on which the World
update loop observes and removes the dead herbivore.

Append new positional fields after all existing fields. Do not interleave them with the
accepted-prior E11-G supply fields.

## Implementation Steps

### 1. Add Failing Contract Tests

Add tests before production code for:

- initial counts equal the actual initial `_animals` collection;
- `TotalAnimals == Herbivores + Predators`;
- animals on water and movement-blocked tiles match the actual map state;
- per-region species totals sum to global totals;
- regions are sorted by id;
- same seed/config yields exactly equal initial snapshots;
- empty distance samples serialize as nullable values rather than sentinels;
- first-event ticks are null before events and preserve the first tick after repeated reports;
- `ToTimelineSnapshot()` maps all first-event fields.
- the literal initial-animal roster and post-construction RNG sentinel remain unchanged;
- repeated initial-snapshot builder calls return the same cached reference;
- post-construction animal, food/ecology, and tick mutations do not alter initial telemetry;
- region collection mutation is rejected and no mutable alias escapes;
- distance summaries cover no target, same tile, exact radius, beyond radius, nearest of
  multiple targets, and valid-target-only sample counting;
- zero/negative grazing and hunt reports neither change supply counters nor set first-event
  ticks; positive reports forward exactly once and preserve the first tick.

Tests may inspect existing internal Runtime state through the current test assembly access. Do not add public mutable test hooks.

### 2. Capture Initial State Once

- Build the initial snapshot only after map, colonies, people, animals, and `EcologyState` exist.
- Capture before the first `World.Update(...)` mutation.
- Store an immutable snapshot on `World` and return it from `BuildScenarioInitialEcologyTelemetrySnapshot()`.
- Do not recompute "initial" metrics from current mutable state later.

Important current defect to expose, not fix in this step:

- `RandomFreePos()` currently returns any map coordinate.
- Initial animals may therefore start on water or other invalid positions.
- Step 5c1-A must count this truth; Step 5c2 owns the behavior fix.

### 3. Compute Deterministic Initial Metrics

- Use Manhattan distance, matching current animal sensing semantics.
- Food candidates are active food nodes only.
- Prey candidates are living initial herbivores.
- Human candidates are living initial people.
- `AnimalsOnWater` checks actual tile ground.
- `AnimalsOnMovementBlockedTiles` uses the existing movement-blocked authority and must not replace it with duplicate terrain logic.
- Region viability in Step 5c1-A is descriptive: land exists and plant capacity is positive. Do not invent a final habitat score.
- `ViableRegionsWithoutHerbivores` and `PredatorsInPreyEmptyRegions` must be calculated from the same ordered region snapshot list.

Avoid per-tick cost: all initial distance scans run once during construction only.

### 4. Instrument First Events Without Behavior Changes

Set first-event ticks at existing authoritative report/mutation seams:

- predator-human contact: `ReportPredatorHumanHit()`;
- predator hunt: positive `ReportMeatFromHunt(...)` call, currently predator capture-owned;
- herbivore grazing: positive `ReportPlantFoodConsumedByAnimals(...)` call;
- predator death: `ReportPredatorDeath()`;
- herbivore death: the existing animal removal path when a dead herbivore is removed;
- predator birth: successful `QueuePredatorBirth(...)` commit;
- herbivore birth: successful `QueueHerbivoreBirth(...)` commit.

For grazing and hunt wrappers, preserve E11-G forwarding in this exact order:

```csharp
_ecologyState.Report...(amount);
if (amount > 0)
    _first...Tick ??= _tickCounter;
```

Do not add event-order side effects, RNG calls, movement changes, or extra lifecycle decisions.

### 5. Preserve Existing Telemetry Compatibility

- Existing ecology fields retain their names and meanings.
- `Empty` records include null first-event fields and a deterministic empty initial snapshot if one is provided.
- Timeline remains compact; do not repeat the full per-region initial snapshot in each timeline sample.
- The separate initial snapshot is consumed by ScenarioRunner in Step 5c1-B.

### 6. Diff Self-Review

Explicitly verify:

- no changes to initial animal count, position, or species selection;
- no changes to `Animal.Spawn(...)`;
- no changes to `RandomFreePos()`;
- no lifecycle constant changes;
- no `Person.cs`, AI, ScenarioRunner, App, or Graphics changes;
- no new RNG consumption;
- no mutable collection exposed from the initial snapshot.
- the literal roster and post-construction RNG sentinel remain unchanged;
- all protected E11-G hunks remain unchanged except the two approved additive wrappers;
- no Step 5c1-A hunk appears in `Animal.cs`, `EcologyState.cs`, `Person.cs`, ScenarioRunner,
  AI, App, or Graphics.

### 7. Known Downstream Compile Compatibility

The existing ScenarioRunner structurally serializes `ScenarioEcologyTelemetrySnapshot` and
`ScenarioEcologyTimelineSnapshot`. New first-event fields may therefore become additively
visible in artifacts without a `Program.cs` edit. Step 5c1-A proves compile compatibility
only. Artifact naming, JSON acceptance, old-baseline compatibility, and durable evidence
remain Step 5c1-B SMR ownership.

If ScenarioRunner compilation requires a ScenarioRunner source change, stop and return to
Meta; Track B does not gain ScenarioRunner edit ownership in this step.

## Acceptance Criteria

- Current legacy random initialization is observable without behavior drift.
- Water/invalid initial placement is measured rather than hidden.
- Same seed/config gives identical initial telemetry.
- Initial species and per-region totals are internally consistent.
- Distance summaries and observation radii are explicit.
- First event ticks are production-path and first-write-only.
- No existing ecology counter semantics change.
- Focused tests pass.
- Step 5c1-B receives a stable compile-time Runtime contract.

## Verification

Run focused tests only:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "ScenarioEcologyTelemetryTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Record the matched, passed, failed, and skipped test counts. Zero matched tests is not GREEN.

Then compile the known downstream consumer without editing it:

```powershell
dotnet build "WorldSim.ScenarioRunner\WorldSim.ScenarioRunner.csproj" --no-restore -m:1 -p:UseSharedCompilation=false
```

Then hygiene:

```powershell
git diff --check
```

Do not run expanded or full E11-H matrices in this step.

## Required Handoff To SMR Analyst

Provide:

- exact DTO names and fields;
- exact builder method name;
- focused test result;
- confirmation that behavior and RNG cadence are unchanged;
- list of nullable/empty semantics;
- diff summary limited to the declared files.
- literal roster and post-construction RNG sentinel results;
- exact matched test count and downstream ScenarioRunner build result;
- protected E11-G hunk comparison and forbidden-path audit;
- mandatory deep-review verdict.

## Stop Conditions

- Stop if initial metrics require changing spawn behavior.
- Stop if a public mutable test hook appears necessary.
- Stop if ScenarioRunner or App changes appear necessary.
- Stop if a metric would require per-tick full-map recomputation.
- Stop if any unrelated dirty hunk must be modified.
- Stop if ScenarioRunner compile compatibility requires a ScenarioRunner source edit.
- Stop if the literal roster or post-construction RNG sentinel changes.

## Implementation Evidence

- Scoped implementation files: `ScenarioEcologyTelemetry.cs`, `World.cs`, and
  `ScenarioEcologyTelemetryTests.cs` only.
- Added Runtime contracts: `ScenarioEcologyDistanceSummarySnapshot`,
  `ScenarioInitialEcologyRegionSnapshot`, and `ScenarioInitialEcologyTelemetrySnapshot`.
- Added cached builder: `World.BuildScenarioInitialEcologyTelemetrySnapshot()`.
- Legacy seed `42` ordered animal roster remained unchanged; post-construction RNG sentinel
  remained `313862347`.
- Fixed seed `0` exposes the legacy placement defect with `AnimalsOnWater=3` and
  `AnimalsOnMovementBlockedTiles=3`.
- Focused Runtime verification: 17 matched, 17 passed, 0 failed, 0 skipped.
- ScenarioRunner downstream compile: succeeded with 0 warnings and 0 errors; no
  ScenarioRunner source edit was required.
- `git diff --check` reported no whitespace errors; only existing LF/CRLF conversion
  warnings were emitted.
- Independent reviewer: APPROVED, no findings.
- Test engineer: PASS after closing all-seven first-write and mixed-liveness proof gaps.
- Final critic: APPROVED with high confidence, no findings.
- Meta high-risk step-review plus external Swarm synthesis: GREEN; no blocking or major findings.
- Non-blocking natural-caller timestamp regression coverage is routed to the Combined Step 5c5/package gate and does not reopen this implementation.
- Security pre-check found no secrets in the three scoped files. SAST/quality/placeholder
  tools could not persist evidence because a parent workspace `.swarm` directory conflicts
  with this project root; focused compile/test/deep-review evidence remains authoritative.
- No expanded/full E11-H matrix, package, staging, commit, or push was performed.

## Build/Review Readiness

Implementation, mandatory deep-review, Meta step-review, and external Swarm synthesis are GREEN.
Step 5c1-A is accepted and Step 5c1-B is unlocked for the SMR Analyst.
