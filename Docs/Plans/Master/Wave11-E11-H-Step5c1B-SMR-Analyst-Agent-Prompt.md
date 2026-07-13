# Wave 11 E11-H Step 5c1-B - SMR Analyst Agent Prompt

**Session type:** SMR Analyst implementation/evidence
**Status:** REVIEW-FIX IMPLEMENTED - one step-review pending; no staging or commit
**Target:** E11-H Step 5c1-B initial ecology artifact consumer
**Prerequisite:** Meta-accepted Track B Step 5c1-A Runtime contract handoff

Implementation is complete. Use this prompt as the review contract; do not rerun or expand
the implementation before Meta disposition.

## Turn Gate

Before starting, read `ops/PROJECT_STATE.md` and the Combined Step 5c1-B row.

Step 5c1-A is accepted GREEN. Re-check the canonical state before editing; if it no longer
shows that acceptance, report `NOT READY`.

## Required Pre-Read After Gate Opens

1. `Docs/Plans/Master/Wave11-E11-H-Step5c-Habitat-Aware-Ecology-Seeding-And-SMR-Calibration-Plan.md`
2. `Docs/Plans/Master/Wave11-E11-H-Step5c1B-SMR-Initial-Ecology-Artifact-Plan.md`
3. Track B Step 5c1-A handoff and review evidence
4. `Docs/Evidence/Manual/Wave11-E11-H-Step5c-Manual-Observation-001.md`

## Instruction

Consume the accepted Runtime initial ecology contract without recomputing habitat truth.

The field means `constructor_initial / pre_runner_setup`: it is the immutable Runtime
world-construction snapshot captured before ScenarioRunner post-construction fixtures and the
first tick. It is not generic post-fixture run-start state.

Allowed files:

- `WorldSim.ScenarioRunner/Program.cs`
- `WorldSim.ScenarioRunner.Tests/EcologyTelemetryArtifactTests.cs`
- `Docs/Evidence/SMR/wave11-e11-h-step5c1-initial-observability/README.md`

Do not touch Runtime, AI, App, Graphics, scenario balance, or `ECO-*` assertion semantics.

Add optional `initialEcology` run/summary artifact consumption, compact first-event timeline fields, deterministic serialization tests, and old-baseline compatibility. Positive deterministic proof must exit `0`.

Direct-World core, custom non-lifecycle, Wave9 companion-main, and non-lifecycle Wave10
companion-main runs populate the constructor snapshot. The following three Runtime-backed
lifecycle modes must serialize a present property with exact JSON `null` in summary and run
artifacts:

- `organic_campaign_lifecycle`;
- `organic_hostile_campaign_lifecycle`;
- `manual_operator_campaign_lifecycle`.

Wave9/Wave10 side probes have no main-run initial ecology claim. Refinery lanes are
`N/A - separate artifact family`.

Explicitly classify direct-`World` main-world runs versus `SimulationRuntime`-backed special
run families. Do not claim that every mode emits `initialEcology` unless the contract is
actually reachable and tested. If a required runtime-backed mode needs a new forwarding
surface, stop and request a Track B handoff; Runtime remains out of scope.

Implementation is complete; do not edit source in the review-fix or step-review route. Review
the exact five-source anchor and locked 19-case ledger in the evidence README, and verify that
existing assertion, drilldown, lifecycle-routing, and E11-G supply hunks remain outside
Step 5c1-B ownership. Do not run Git commands; Git-backed attribution belongs only to a later
authorized `/closeout-commit` after final GREEN.

Required focused additions include:

- current-format populated baseline compare;
- immediate previous baseline without `initialEcology`;
- older baseline without any ecology blocks;
- same-seed supply-fixture invariance for constructor snapshot provenance;
- exact-null `[Theory]` for all three Runtime-backed lifecycle modes;
- all seven nullable first-event timeline fields without repeating `initialEcology`;
- deterministic helper defaults including `WORLDSIM_SCENARIO_DT=0.25`;
- inherited non-core lane/foreign-config isolation;
- controlled nullable-empty distance JSON compatibility without claiming a production empty
  branch;
- process and manifest exit `0`, manifest assertion failure `0`, and one-run identity.

Use a test-local temp root and best-effort `finally` cleanup for new multi-process tests. The
local evidence lane is explicit `core`, `standard`, seed `101`, planner `simple`, ticks `8`,
`dt=0.25`, Headless, rescue disabled. Raw `.artifacts/**` stay local-only.

## Required Verification

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter "EcologyTelemetryArtifactTests" --no-restore -m:1 -p:UseSharedCompilation=false
```

Run only the small local legacy initialization evidence lane specified by the plan. Do not run expanded/full E11-H matrices. Do not track raw `.artifacts/**`.

After verification, mandatory focused deep-review and Meta step-review are required. A
test-heavy diff above 250 meaningful lines triggers deep-review rather than automatic stop;
Runtime scope, a fourth implementation/evidence file, assertion/balance changes, or
unisolatable dirty hunks remain hard stops.

## Required Handoff

Return:

- exact changed files;
- consumed Runtime contract version/fields;
- focused test result;
- local artifact identity and exit code;
- review-ready evidence README path and explicit statement that repository durability is
  pending `/closeout-commit` after final GREEN;
- old-baseline compatibility result;
- run-family population matrix for direct-`World` and runtime-backed modes;
- explicit confirmation that no Runtime behavior or assertions changed;
- state continuity update.
- constructor-initial/pre-runner-setup provenance;
- current plus two legacy compare results;
- supply-fixture invariance result;
- manifest proof and pre/post dirty-hunk ledger;
- focused deep-review verdict.

Do not stage, commit, or push.
