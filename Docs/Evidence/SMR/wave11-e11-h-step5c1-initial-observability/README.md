# Wave 11 E11-H Step 5c1-B Initial Ecology Observability Evidence

Date: 2026-07-13
Owner: SMR Analyst
Status: accepted GREEN; repository-durable through closeout ID `wave11-e11-h-step5c1b-closeout-20260713`

## Scope

This packet proves additive ScenarioRunner consumption of the accepted Step 5c1-A Runtime
initial ecology contract. It does not change Runtime behavior, seeding, lifecycle, ecology
balance, or `ECO-*` assertions.

`initialEcology` is an additive nullable field under `smr/v1`. Its exact provenance is
`constructor_initial / pre_runner_setup`: the immutable Runtime world-construction snapshot
captured before ScenarioRunner post-construction fixtures and before the first tick.

This README is an explicit member of the Step 5c1-B closeout package. Closeout ID
`wave11-e11-h-step5c1b-closeout-20260713` establishes its repository-durable package identity;
the verified Git commit SHA and tree SHA are recorded in `ops/PROJECT_STATE.md`. Raw
`.artifacts/**` output remains local-only.

## Focused Verification

Command:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter "EcologyTelemetryArtifactTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Result:

- matched: 19
- passed: 19
- failed: 0
- skipped: 0
- duration: 43 seconds

The focused class proves:

- direct-world run and summary artifact population;
- semantic equality between run and summary `initialEcology` values;
- same-seed/config deterministic summary and run-file subtrees;
- all three Runtime-backed lifecycle modes emit an explicit JSON `null`;
- all seven nullable first-event timeline fields are serialized without repeating the full
  initial snapshot in timeline samples;
- current-format baseline compare;
- immediate previous baseline compatibility without `initialEcology`;
- older baseline compatibility without any ecology blocks;
- controlled nullable empty-distance JSON compatibility;
- post-construction supply setup does not alter the constructor snapshot;
- inherited non-core lane and foreign config do not reroute the focused helper.
- Wave9 and non-lifecycle Wave10 companion main runs retain direct-World populated snapshots
  while their side-probe evidence remains separately attributed.

## Local Artifact Lane

Raw artifact path, local-only:

```text
.artifacts/smr/wave11-e11-h-step5c1-initial-observability-001/
```

Effective command profile:

```text
WORLDSIM_SCENARIO_LANE=core
WORLDSIM_SCENARIO_MODE=standard
WORLDSIM_SCENARIO_SEEDS=101
WORLDSIM_SCENARIO_PLANNERS=simple
WORLDSIM_SCENARIO_TICKS=8
WORLDSIM_SCENARIO_DT=0.25
WORLDSIM_SCENARIO_OUTPUT=json
WORLDSIM_VISUAL_PROFILE=Headless
WORLDSIM_SCENARIO_ARTIFACT_DIR=.artifacts/smr/wave11-e11-h-step5c1-initial-observability-001
```

The config JSON and optional assert/compare/perf/anomaly/drilldown variables were cleared.
Emergency rescue remained disabled through the Runtime default/effective balance contract.

Manifest result:

| Field | Value |
|---|---:|
| `schemaVersion` | `smr/v1` |
| `exitCode` | `0` |
| `exitReason` | `ok` |
| `assertionFailures` | `0` |
| `assertionSkipped` | `3` |
| `anomalyCount` | `0` |
| `seedCount` | `1` |
| `plannerCount` | `1` |
| `configCount` | `1` |
| `totalRuns` | `1` |
| `effectiveVisualLane` | `Headless` |
| `visualLaneSource` | `env` |

## Constructor Initial Snapshot

Run identity:

| Field | Value |
|---|---|
| config | `default` |
| seed | `101` |
| planner | `Simple` |
| world | `64x40` |
| initial human population | `24` |
| ticks / dt | `8 / 0.25` |
| initial animal policy | `legacy_random` |
| policy source | `runtime_default` |

Initial animal summary:

| Metric | Value |
|---|---:|
| total animals | `10` |
| herbivores | `7` |
| predators | `3` |
| predator/herbivore ratio | `0.42857142857142855` |
| animals on water | `2` |
| animals on movement-blocked tiles | `2` |
| viable regions | `12` |
| viable regions without herbivores | `8` |
| predators in prey-empty regions | `2` |
| herbivores with food in vision | `6` |
| predators with prey in vision | `1` |
| predators within human harass radius | `0` |
| predators within early human contact radius | `0` |

Distance summaries:

| Summary | Samples | Min | Max | Average |
|---|---:|---:|---:|---:|
| herbivore to nearest food | `7` | `1` | `8` | `3.2857142857142856` |
| predator to nearest prey | `3` | `2` | `25` | `12.333333333333334` |
| predator to nearest person | `3` | `19` | `26` | `22.666666666666668` |

Region species distribution, ordered by `regionId`:

```text
0:H4/P0, 1:H1/P0, 2:H0/P0, 3:H0/P0,
4:H1/P1, 5:H0/P0, 6:H0/P0, 7:H1/P0,
8:H0/P0, 9:H0/P0, 10:H0/P1, 11:H0/P1
```

## First-Event Fields

The eight-tick final ecology snapshot reported:

| Field | Value |
|---|---:|
| `firstPredatorHumanContactTick` | `null` |
| `firstPredatorHuntTick` | `1` |
| `firstHerbivoreGrazingTick` | `1` |
| `firstPredatorDeathTick` | `null` |
| `firstHerbivoreDeathTick` | `1` |
| `firstPredatorBirthTick` | `null` |
| `firstHerbivoreBirthTick` | `1` |

Focused drilldown tests separately prove that these seven properties remain nullable
number-or-null fields in compact timeline artifacts and that `initialEcology` is not copied
into timeline samples.

## Run-Family Availability

| Run family | Contract |
|---|---|
| default/core direct-world main run | populated `constructor_initial / pre_runner_setup` |
| custom non-lifecycle direct-world main run | populated `constructor_initial / pre_runner_setup` |
| Wave9 companion main run | main run populated; side probe N/A |
| non-lifecycle Wave10 companion main run | main run populated; side probe N/A |
| `organic_campaign_lifecycle` | property present, exact JSON `null` |
| `organic_hostile_campaign_lifecycle` | property present, exact JSON `null` |
| `manual_operator_campaign_lifecycle` | property present, exact JSON `null` |
| Wave9/Wave10 side probes | N/A; separate proof payload, no main-run attribution |
| non-core refinery lanes | N/A; separate artifact family |

No Runtime forwarding contract is required for this matrix.

## Reproducible Attribution Ledger

The reconstruction reference is
`fee461152349d4ceefc1d44c89ecdc45173cb2c8`. The accepted Track B prerequisite boundary and
the P0 material-equivalence confirmation are pinned under `.opencode-router/artifacts/` and
are package inputs, not authority to execute the superseded P0-P4/three-commit workflow.

This ledger deliberately replaces the earlier narrative claim that a pre-edit diff alone was
sufficient. A reviewer can reproduce the Step 5c1-B source boundary by locating the following
five unique anchor-level changes in `WorldSim.ScenarioRunner/Program.cs`:

| Hunk | Required anchor | Required placement |
|---|---|---|
| Constructor snapshot read | `var initialEcology = world.BuildScenarioInitialEcologyTelemetrySnapshot();` | Immediately after direct `World` construction and before `ApplyEcologyBalanceConfig(...)` and `ApplySupplyScenarioConfig(...)`. |
| Result-builder argument | `initialEcology,` | Second argument of the direct-world `BuildRunResult(...)` call, between `world` and `mainRunConfig`. |
| Result-builder parameter | `ScenarioInitialEcologyTelemetrySnapshot initialEcology,` | Second parameter of `BuildRunResult(...)`, between `World world` and `ScenarioConfig config`. |
| Direct result assignment | `InitialEcology: initialEcology` | Final named argument of the direct-world `ScenarioRunResult` construction. |
| Backward-compatible record field | `ScenarioInitialEcologyTelemetrySnapshot? InitialEcology = null` | Final defaulted field of `ScenarioRunResult`. |

No other `Program.cs` hunk is Step 5c1-B-owned. In particular, ecology assertions,
drilldown scoring, lane helpers, lifecycle execution, balance/config policy, and `ECO-*`
semantics are excluded.

The focused test boundary is the existing
`WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs` class with exactly 19 xUnit
cases. The locked origin count is 8 base cases, 1 accepted E11-G deterministic supply case,
and the following 10 Step 5c1-B cases:

| Method | xUnit cases |
|---|---:|
| `Compare_OldBaselineWithoutInitialEcology_MatchesAndExitsZero` | 1 |
| `Compare_OldBaselineWithoutAnyEcologyBlocks_MatchesAndExitsZero` | 1 |
| `Compare_BaselineWithNullableEmptyInitialDistanceSummary_ParsesAndExitsZero` | 1 |
| `RuntimeBackedLifecycleModes_EmitExplicitNullInitialEcology_AndExitZero` | 3 |
| `CompanionMainRuns_KeepDirectWorldConstructorInitialEcology` | 2 |
| `InitialEcology_PostConstructionSupplyFixture_DoesNotChangeConstructorSnapshot` | 1 |
| `RunnerHelper_OverridesInheritedNonCoreLaneAndForeignConfig` | 1 |
| **Step 5c1-B total** | **10** |
| **Focused class total** | **19** |

The following Step 5c1-B assertion anchors extend base-origin cases without changing their
case identity:

| Base-origin method | Step 5c1-B-owned assertion boundary |
|---|---|
| `MainWorld_RunAndSummary_ExposeMatchingConstructorInitialEcology_AndExitZero` | `summaryInitialEcology` shape, run-artifact `initialEcology`, and semantic equality only. |
| `Drilldown_TimelineContainsCompactEcologyFields` | Seven nullable first-event fields, final summary/timeline equality, and absence of full `initialEcology` in samples only. |
| `InitialEcology_SameSeedAndConfig_IsByteEquivalentAcrossIndependentRuns` | Summary/run `initialEcology` deterministic equality and semantic equality only. |
| `Compare_CurrentBaselineWithInitialEcology_MatchesAndExitsZero` | Current baseline contains an object-valued `initialEcology` before the existing compare assertion. |

The E11-G case remains `EcologySupplyBridgeFields_ArePresentAndDeterministic`. Helpers reachable
only from the listed Step 5c1-B cases/assertions are in scope; config balance, rescue policy,
E11-G supply assertions, and other existing test behavior are excluded. A different focused
count is a hard stop requiring new Meta confirmation.

The owner-safe prerequisite boundary is separately locked to exactly 17
`ScenarioEcologyTelemetryTests` cases plus a nonzero, all-passing
`Wave11EcologySupplyBridgeTests` filter. All AI, App, Graphics, refinery, Route A tuning,
expanded/full matrices, raw `.artifacts/**`, Step 5c2, E11-I, and E11-J surfaces are excluded.

The closeout verifies this anchor ledger against the allowed package paths and records the Git
commit/tree identity in `ops/PROJECT_STATE.md`. The durable package identity is
`wave11-e11-h-step5c1b-closeout-20260713`; no staging or commit belonged to the earlier
review-fix or step-review stages.

## Compatibility Policy

- Current-format populated `initialEcology` summaries compare with one matched run and zero
  compare failures.
- Missing `initialEcology` deserializes as `null` and compares without identity drift.
- Older summaries without `initialEcology`, `ecology`, or `ecologyBalance` remain parseable.
- Runtime tests own empty-distance production semantics. Step 5c1-B proves preservation and
  parsing of the explicit empty JSON shape; this packet does not claim the local production
  run exercised an empty-distance branch.

## Non-Claims

- This is not habitat-aware seeding proof.
- This is not lifecycle viability proof.
- This does not make E11-H GREEN or closeout-ready.
- This is not an all-mode population claim.
- This does not prove Wave9/Wave10 side-probe initial state.
- This does not prove refinery-lane initial ecology.
- No expanded predator-human or full 45-run ecology matrix was executed.
- Raw `.artifacts/**` files are local-only and are not part of the review-ready or future
  checked-in evidence packet.
- This README is repository-durable only as part of the verified closeout package identified by
  `wave11-e11-h-step5c1b-closeout-20260713`; raw artifacts remain local-only.
