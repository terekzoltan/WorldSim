# Wave 11 E11-H Step 5c1-B - SMR Initial Ecology Artifact Plan

Status: REVIEW-FIX IMPLEMENTED - focused contract GREEN; one step-review pending; no commit
Owner: SMR Analyst
Upstream owner: Track B
Reviewer: Meta Coordinator
Last updated: 2026-07-11

## Target

Consume the accepted Step 5c1-A Runtime initial ecology contract in ScenarioRunner artifacts without adding seeding behavior or changing hard ecology assertions.

Canonical parent: `Docs/Plans/Master/Wave11-E11-H-Step5c-Habitat-Aware-Ecology-Seeding-And-SMR-Calibration-Plan.md`.

Upstream plan: `Docs/Plans/Master/Wave11-E11-H-Step5c1A-Track-B-Initial-State-Observability-Implementation-Plan.md`.

## Dependency Gate

This lane is execution-dependent on the accepted Runtime DTO and builder handoff from Step 5c1-A. It is intentionally not parallel with Runtime implementation.

Meta accepted Step 5c1-A GREEN on 2026-07-11. This dependency gate is satisfied.

## Meta Review Contract Lock

The artifact meaning is exact:

> `initialEcology` is the immutable Runtime world-construction snapshot captured in the
> `World` constructor before ScenarioRunner post-construction setup and before the first
> tick. ScenarioRunner object-initializer flags, ecology-balance setup, supply fixtures,
> side-probe setup, and later mutations do not alter it.

Use `constructor_initial / pre_runner_setup` in evidence and handoffs. Do not describe the
field as generic post-fixture run-start state.

Run-family contract:

| Run family | Route | `initialEcology` contract |
|---|---|---|
| Default/core main run | Direct `World` | Populated `constructor_initial / pre_runner_setup` |
| Custom non-lifecycle core run | Direct `World` | Populated `constructor_initial / pre_runner_setup` |
| Wave9 companion main run | Direct `World` | Main run populated; side probe N/A |
| Non-lifecycle Wave10 companion main run | Direct `World` | Main run populated; side probe N/A |
| `organic_campaign_lifecycle` | `SimulationRuntime` | Property present, exact JSON `null` |
| `organic_hostile_campaign_lifecycle` | `SimulationRuntime` | Property present, exact JSON `null` |
| `manual_operator_campaign_lifecycle` | `SimulationRuntime` | Property present, exact JSON `null` |
| Wave9/Wave10 side probes | Separate proof payload | N/A; no main-run attribution |
| Non-core refinery lanes | Separate `RefineryScenarioRunner` envelope | N/A; separate artifact family |

This is an additive nullable `smr/v1` field and does not require a schema-version bump.
Missing legacy data maps to `null`; do not use `ScenarioInitialEcologyTelemetrySnapshot.Empty`
as a missing-data sentinel. No Runtime forwarding is currently required. If any
Runtime-backed mode becomes required to populate the field, stop and open a separate Track B
handoff.

## File Scope

Allowed production file:

- `WorldSim.ScenarioRunner/Program.cs`

Allowed test file:

- `WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs`

Allowed evidence output after verification:

- `Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md`

No Runtime, AI, App, Graphics, assertion-threshold, or scenario-balance file is in scope.

## Contract Consumption

Consume Track B's accepted:

- `ScenarioInitialEcologyTelemetrySnapshot`;
- `ScenarioInitialEcologyRegionSnapshot`;
- `ScenarioEcologyDistanceSummarySnapshot`;
- first-event tick additions on `ScenarioEcologyTelemetrySnapshot` and its timeline projection;
- `World.BuildScenarioInitialEcologyTelemetrySnapshot()` builder.

Do not duplicate habitat or distance calculations in ScenarioRunner.

Before adding artifact fields, inventory the run families that own a direct `World` versus
those that only own `SimulationRuntime`. Main-world direct-`World` runs must populate
`initialEcology`. Runtime-backed special modes may leave it absent/null only when the exact
run-family list and reason are documented and tested. Do not claim all-mode population from
main-world evidence. If all run families are required to populate the field, stop and request
a separate Track B Runtime forwarding handoff instead of editing Runtime in this lane.

## Implementation Steps

### 1. Add Optional Run Artifact Field

Add `InitialEcology` to `ScenarioRunResult` as an optional additive field with a safe default for old baselines.

- Direct-World main runs read the cached Runtime builder immediately after the `World` object
  initializer and before `ApplyEcologyBalanceConfig(...)`, `ApplySupplyScenarioConfig(...)`,
  or the first tick.
- Run JSON and `summary.json` expose the same Runtime-owned values.
- Do not repeat the per-region initial snapshot in every timeline sample.
- Early-event tick fields remain in the compact ecology timeline projection supplied by Runtime.

### 2. Preserve Old Artifact Compatibility

- Baselines without `initialEcology` must continue to parse.
- Existing baselines without ecology blocks must continue to parse.
- Compare mode must not treat missing legacy `initialEcology` as a regression in Step 5c1-B.
- No new hard invariant or threshold is introduced yet.

Compatibility must cover three distinct cases:

- an unmodified current-format baseline with populated `initialEcology`;
- the immediate previous format with only `initialEcology` removed;
- an older format with `initialEcology`, `ecology`, and `ecologyBalance` removed.

Every compare proof uses identical config/planner/seed/visual-lane identity and requires
`matchedRunCount=1`, empty current-only/baseline-only keys, `totalFailureCount=0`, and process
exit code `0`.

Step 5c1-B is observability only. Step 5c3 owns calibration and acceptance interpretation.

### 3. Add Focused Artifact Tests

Add tests for:

- run/summary JSON contains `initialEcology`;
- all locked top-level initial fields are present;
- region array is present and ordered by `regionId`;
- distance summaries preserve nullable empty semantics;
- initial totals are consistent in serialized output;
- timeline ecology contains all nullable first-event ticks;
- repeated same-seed runs emit byte-equivalent `initialEcology` JSON;
- old baseline without `initialEcology` still parses and compare executes;
- existing old-baseline-without-ecology compatibility remains GREEN.
- direct-`World` main-world runs populate `initialEcology`;
- runtime-backed special run families are explicitly classified as populated or optional-null,
  with no unsupported all-mode claim.
- all three Runtime-backed lifecycle modes serialize a present property with exact JSON
  `null` in both summary and run artifacts;
- a post-construction `storehouse_refill_consumption` fixture does not alter the same-seed
  constructor snapshot;
- current-format baseline compare with populated `initialEcology` exits `0`;
- deterministic subprocess defaults include `lane=core`, `mode=standard`, `ticks=8`,
  `dt=0.25`, `planner=simple`, `seed=101`, and `visual profile=Headless`;
- inherited non-core lane and foreign config values cannot reroute the focused helper;
- manifest evidence records `schemaVersion=smr/v1`, `exitCode=0`,
  `assertionFailures=0`, and one run identity.

Runtime tests own empty-distance production semantics. Step 5c1-B proves preservation of the
explicit nullable JSON shape (`sampleCount=0` with null minimum/maximum/average) through a
controlled compatibility fixture. Do not claim that a random production artifact necessarily
executes the empty-distance branch.

Use exit code `0` for positive deterministic artifact proof. Failure-tolerant exit-code assertions must not be used to claim positive determinism.

### 4. Produce A Small Evidence Packet

After tests pass, run one short legacy initialization artifact lane:

- seed `101`;
- planner `simple`;
- 1-10 ticks;
- emergency rescue disabled;
- core lane, standard mode, `dt=0.25`, and Headless visual profile explicit;
- no assertions added or weakened;
- artifact remains local-only under `.artifacts/**`.

The review-ready evidence README records the following and becomes checked-in/durable only
through `/closeout-commit` after a GREEN final step-review synthesis:

- command/env;
- artifact path as local-only;
- effective world/config identity;
- initial water/invalid counts;
- initial region/species distribution;
- initial distance summaries;
- first-event fields observed in the short window;
- explicit non-claim that Step 5c1-B proves habitat-aware seeding or E11-H viability.
- explicit constructor-initial/pre-runner-setup provenance and run-family matrix;
- process and manifest exit/assertion identity proof;
- explicit non-claim that production evidence exercised an empty-distance branch.

New multi-process tests use a test-local temp root and best-effort `finally` cleanup after all
`JsonDocument` instances are disposed. Cleanup failure must not hide the original assertion
failure.

### 5. Review Scope

Explicitly verify:

- no Runtime or behavior changes;
- no `ECO-*` assertion additions, removals, or threshold changes;
- no scenario config tuning;
- no fallback habitat calculations in ScenarioRunner;
- no raw `.artifacts/**` tracked;
- no unrelated Program.cs hunk modified.

The current review-fix/step-review route performs no Git operations. Use the exact five-source
anchor and locked 19-case ledger in the evidence README to review the owner-safe boundary. The
handoff must state that pre-existing assertion/drilldown, lifecycle-routing, and E11-G supply
hunks remain outside Step 5c1-B ownership. Git-backed diff identity is established only by a
later authorized `/closeout-commit` after final GREEN.

## Acceptance Criteria

- ScenarioRunner consumes Runtime-owned initial telemetry without recomputation.
- Run and summary artifacts expose deterministic initial ecology evidence.
- Timeline exposes first-event ticks through the compact Runtime projection.
- Old baselines remain compatible.
- Positive deterministic proof requires exit code `0`.
- No hard invariant behavior changes.
- Evidence README is explicitly included and truthful about pending durability; raw artifacts
  stay local-only. Repository durability is a closeout result, not a review-fix precondition.
- Run-family reachability is explicit: direct-`World` and runtime-backed modes are not conflated.
- Direct-world provenance is explicitly constructor-initial/pre-runner-setup.
- Current-format and both legacy baseline forms compare cleanly.
- The three Runtime-backed lifecycle modes emit exact JSON `null`.
- The supply fixture leaves the constructor snapshot unchanged.
- Deterministic `DT` and inherited-environment isolation are proven.

## Verification

Run focused tests:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter "EcologyTelemetryArtifactTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Do not run expanded 9-run or full 45-run E11-H matrices.

The focused test implementation may exceed 250 meaningful lines when the excess is
table-driven verification and evidence code. This triggers mandatory focused deep-review,
not an automatic stop. Stop for a fourth implementation/evidence file, Runtime forwarding,
new source abstraction, assertion/balance changes, or unrelated hunk overlap.

## Required Handoff To Meta

Provide:

- Track B contract version/field confirmation;
- focused test result;
- local artifact run identity and exit code;
- review-ready evidence README path and explicit pending-durability status;
- compatibility result for baseline without `initialEcology`;
- run-family population matrix for direct-`World` and runtime-backed modes;
- explicit statement that no assertions or runtime behavior changed.
- constructor-initial/pre-runner-setup provenance;
- current plus two legacy compare results;
- manifest exit/assertion/run-identity result;
- pre/post dirty-hunk ledger;
- focused deep-review verdict.

## Stop Conditions

- Stop if Track B contract differs from the accepted handoff.
- Stop if ScenarioRunner would need to inspect mutable Runtime internals.
- Stop if habitat suitability must be recomputed in ScenarioRunner.
- Stop if positive proof cannot achieve exit code `0` without changing assertions or balance.
- Stop if unrelated dirty Program.cs/test hunks cannot be isolated.
- Stop if a required runtime-backed mode cannot expose initial ecology without a new Runtime
  forwarding contract; open a Track B handoff rather than editing Runtime.
- Stop if durable plan/prompt synchronization is missing before source edits.
- Stop if exact JSON-null lifecycle, current baseline, supply-invariance, deterministic-DT,
  or nullable-distance compatibility proof cannot be implemented within the declared scope.

## Build/Review Readiness

Implementation evidence:

- direct-World constructor snapshot is exposed in run and summary artifacts;
- all three Runtime-backed lifecycle modes serialize exact JSON `null`;
- focused `EcologyTelemetryArtifactTests` gate passed 19/19;
- current and both legacy baseline forms compare cleanly;
- short local artifact lane exited `0` with manifest assertion failures `0` and anomalies `0`;
- review-ready evidence: `Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md`;
- independent reviewer APPROVED, test engineer PASS, and final critic APPROVE with no
  remaining findings;
- no Runtime, AI, App, Graphics, `ECO-*` assertion, or scenario-balance change was made;
- raw artifacts remain local-only.

GREEN for one Meta step-review. Step 5c2 remains blocked until that final synthesis is GREEN
and `/closeout-commit` completes.
