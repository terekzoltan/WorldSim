# Wave 11 E11-H Step 5c - Habitat-Aware Ecology Seeding And SMR Calibration Plan

Status: Canonical - Step 5c2 accepted GREEN; Step 5c3 ready
Owner: Meta Coordinator
Runtime owner: Track B
Evidence owner: SMR Analyst
Manual observation owner: User / Manual QA
Last updated: 2026-07-16

## Purpose

Replace the current globally random initial animal placement with a deterministic, habitat-aware ecology seeding policy, and extend SMR so initial-condition quality can be measured separately from lifecycle behavior.

This is an E11-H recovery program. It does not weaken hard ecology invariants and does not treat better initialization as proof that the closed-loop lifecycle is correct.

## Trigger And Evidence

Step 5b1-5b4 established that E11-H is not blocked by an assertion/export defect:

- the hard ecology invariant scaffold detects real species-continuity failures;
- broad RED tuning was cleaned rather than packaged;
- bounded predator replacement headroom failed the focused production gate and was reverted;
- instance-local survival/reproduction bandwidth failed the focused production gate and was reverted;
- seed `202` remains predator-birthless in focused rows;
- seed `101` exposes herbivore/prey continuity failure;
- emergency rescue remains zero in the relevant failures.

Code inspection identified a foundational initial-condition gap: initial animals are placed on globally random map coordinates, including possible water or movement-invalid tiles, and independently assigned as roughly 70% herbivore / 30% predator. Initial placement does not account for land safety, plant food, fertility, prey, colony distance, regional capacity, or viable local food-web structure.

## Locked Decisions

- Initial ecology state is part of the runtime model, not test-only setup.
- `WorldSim.Runtime` owns seeding policy and deterministic selection.
- `WorldSim.ScenarioRunner` owns config, artifact export, matrix execution, and invariant evaluation only.
- `WorldSim.Graphics` may visualize exported initialization state but must not calculate habitat suitability.
- The user/manual QA lane is qualitative diagnostic evidence, not an acceptance gate.
- Emergency rescue remains disabled in normal acceptance lanes.
- Existing `ECO-*` assertions remain hard and are not weakened for this program.
- Lifecycle constants, predator-human combat behavior, and initial seeding must not be tuned in the same implementation slice.
- The legacy random policy may remain only as an explicit compare/regression mode; it must not silently remain the normal accepted default after habitat-aware seeding is promoted.

## Non-Goals

- No domestication, farms, pens, milk, eggs, or livestock economy.
- No renderer-side ecology computation.
- No `Person.cs` predator retaliation changes.
- No Track C AI policy changes unless a later evidence-backed handoff identifies a separate contract gap.
- No emergency rescue promotion.
- No broad predator/herbivore lifecycle constant tuning.
- No expanded 9-run or full 45-run E11-H matrix before the focused gate order permits it.
- No package/commit of the current interleaved dirty worktree as part of this planning step.

## Target Runtime Policy

### Initial Herbivore Seeding

Candidate tiles are ranked deterministically by:

1. valid land and movement safety;
2. active plant food / plant biomass proximity;
3. fertile, non-overloaded ecology region;
4. minimum distance from colony origins and dense human clusters;
5. regional carrying-capacity budget;
6. deterministic bounded fallback with an explicit counter.

### Initial Predator Seeding

Candidate tiles are ranked deterministically by:

1. valid land and movement safety;
2. prey-bearing region and bounded distance to live herbivores;
3. minimum distance from colony origins and dense human clusters;
4. prey-linked regional predator budget;
5. distribution across viable regions instead of local concentration;
6. deterministic bounded fallback with an explicit counter.

### Initial Population Budget

- Derive the herbivore budget from viable land, plant capacity, and bounded density policy.
- Derive the predator budget from the accepted herbivore budget and prey-linked capacity.
- Do not assign species independently with a global random ratio.
- Preserve exact same-seed/config determinism.
- Keep the first implementation greedy and bounded; no global optimization solver is required.

## SMR Contract Extensions

Initial-state artifact fields must include:

- initial herbivore count;
- initial predator count;
- initial predator/herbivore ratio;
- animals by ecology region;
- viable regions with no herbivores;
- predators in regions with no prey;
- predators within configured distance of people/colonies;
- herbivores within configured distance of active food;
- predator-to-nearest-prey distance summary;
- herbivore-to-nearest-food distance summary;
- initial seeding fallback counts and reasons;
- effective initial animal policy and policy source;
- deterministic initial ecology viability score or explicit component scores.

Early timeline markers must include:

- first predator-human contact tick;
- first predator hunt tick;
- first herbivore grazing tick;
- first predator death tick;
- first herbivore death tick;
- first predator birth tick;
- first herbivore birth tick.

## Scenario Configuration

Additive configuration surface:

- `InitialAnimalPolicy`: `legacy_random | habitat_aware`;
- bounded initial density or named density preset;
- minimum colony/human distance;
- preferred herbivore-to-food radius;
- preferred predator-to-prey radius.

All effective values must be exported in run/summary artifacts. Invalid values must fail deterministically or clamp with explicit effective-value evidence; silent fallback is not accepted.

## Execution Steps

### Step 5c1-A - Runtime Initial-State Observability Producer

Prerequisite: Step 5b4 diagnostic fallback accepted; both failed runtime candidates reverted/not retained.

Owner: Track B.

Implementation plan: `Docs/Plans/Master/Wave11-E11-H-Step5c1A-Track-B-Initial-State-Observability-Implementation-Plan.md`.

Work:

- define runtime-owned initial ecology telemetry snapshot;
- measure water/invalid initial placement, initial distribution, region/species/food/prey/human-distance truth, and early-event ticks;
- add same-seed determinism tests for the current legacy policy before behavior changes.

Expected handoff: stable initial-state telemetry/config contract plus focused tests.

Acceptance/evidence gate:

- current legacy initialization is observable without changing behavior;
- same seed/config produces identical initial telemetry;
- no lifecycle constants, combat policy, `Person.cs`, AI, ScenarioRunner, App, or Graphics behavior changes.

Unlocks: Step 5c1-B after Meta/step-review acceptance.

### Step 5c1-B - SMR Initial Ecology Artifact Consumer

Prerequisite: Step 5c1-A accepted GREEN and Runtime contract handoff available.

Owner: SMR Analyst.

Implementation plan: `Docs/Plans/Master/Wave11-E11-H-Step5c1B-SMR-Initial-Ecology-Artifact-Plan.md`.

Work:

- consume Runtime-owned initial telemetry in run/summary artifacts;
- expose Runtime-owned first-event fields in timeline artifacts;
- preserve old-baseline compatibility when `initialEcology` is absent;
- add positive deterministic artifact tests requiring exit code `0`;
- produce a short local legacy initialization evidence lane and a review-ready README that
  becomes repository-durable only through `/closeout-commit` after final GREEN.

Expected handoff: accepted ScenarioRunner artifact contract and Step 5c1 evidence packet.

Acceptance/evidence gate:

- ScenarioRunner does not recompute habitat or distance truth;
- old artifacts remain parseable;
- direct-`World` main-world and `SimulationRuntime`-backed special run families are explicitly classified; an all-mode claim requires reachable, tested Runtime data, otherwise the mode remains documented optional-null;
- a required runtime-backed forwarding gap opens a separate Track B handoff and cannot be implemented by the SMR Analyst;
- no `ECO-*` assertion or scenario balance change;
- no Runtime, AI, App, or Graphics change;
- raw artifacts remain local-only.
- the README is explicitly included in the review-ready package and does not claim checked-in
  durability before closeout.

Unlocks: Step 5c2 only after the final Meta/step-review synthesis is GREEN and the authorized
`/closeout-commit` has completed and been verified.

### Step 5c2 - Deterministic Habitat-Aware Runtime Seeding

Prerequisite: Step 5c1-B accepted GREEN.

Owner: Track B.

Work:

- implement bounded region-aware herbivore and predator seeding;
- preserve legacy random policy behind an explicit compare-only config;
- add deterministic fallback reasons/counters;
- enforce land-safe, food-aware, prey-aware, colony-distance, and regional budget rules;
- keep lifecycle, capacity, and predator-human behavior constants unchanged.

Expected handoff: runtime policy implementation and focused unit/runtime tests.

Acceptance/evidence gate:

- deterministic same-seed placement;
- no predator starts in prey-empty regions unless explicit fallback is counted;
- initial herbivores have bounded food access;
- initial predator-human proximity is bounded by configured policy;
- species budgets remain bounded by runtime capacity rules;
- no rescue/replenishment is used;
- no global random 70/30 assignment remains in the promoted habitat-aware path.

Unlocks: Step 5c3.

Step 5c2 outcome:

- accepted GREEN and integrated into `master` as commit `cdeee3d5512d5e88c18f01f3bece696cff8801e3`;
- committed tree `3203115f42f2cd6925c4a7ea4f74c51a149e1216` preserves the reviewed five-file candidate identity;
- focused committed-tree gates passed: initial seeding 19/19, telemetry 17/17, supply 24/24, Runtime analyzer/build 0/0, solution build 0/0, format verification, and diff hygiene;
- no lifecycle, predator-human, rescue/replenishment, AI, App, Graphics, or ScenarioRunner behavior was promoted in this slice;
- Step 5c3 is ready, while lifecycle viability, expanded/full matrices, E11-I, and E11-J remain blocked by their later gates.

### Step 5c3 - SMR Initialization And Early-Contact Calibration

Prerequisite: Step 5c2 accepted GREEN.

Owner: SMR Analyst.

Required lanes:

- `ecology_initialization`: 1-10 ticks, initial distribution only;
- `ecology_early_contact`: 100-300 ticks, first contact/hunt/grazing/death timing;
- existing focused predator-human 5-case gate remains the lifecycle acceptance sentinel.

Required compare:

- `legacy_random` vs `habitat_aware`;
- identical seeds/configs;
- lifecycle constants unchanged;
- predator-human policy unchanged;
- emergency rescue disabled.

Acceptance/evidence gate:

- seeds `101,202,303` initialization lanes have complete identity and no config drift;
- habitat-aware policy improves or preserves initial viability components without hiding failures;
- no initial prey-empty predator regions unless explicitly justified fallback occurs;
- no immediate colony adjacency outlier beyond configured policy;
- artifacts expose sufficient evidence for manual and Meta review.

If SMR identifies a proven Runtime telemetry defect, open a separate Track B repair handoff. Do not edit Runtime files inside the SMR Analyst row.

Unlocks: Step 5c4.

### Step 5c4 - Manual Visual Observation

Prerequisite: Step 5c3 artifacts reviewed and one or two concrete manual targets selected.

Owner: User / Manual QA. Track A/App changes are not implied by this lane.

Work:

- run one or two specified app profiles;
- observe initial predator/prey/food/human topology;
- capture first predator-human contact, first hunt/grazing opportunity, clustering/pathing, and visible local depletion;
- return a short structured observation packet with screenshots or a short recording when practical.

Acceptance/evidence gate:

- manual packet records exact env/profile, duration, overlays, observations, and limitations;
- manual evidence is labelled qualitative and is not used as hard invariant proof;
- any missing visualization is routed to E11-I or a separately reviewed Track A/App diagnostic seam, not implemented inside Step 5c4.

Unlocks: Step 5c5.

### Step 5c5 - Meta Route Decision And Focused Gate

Prerequisite: Step 5c3 SMR evidence and Step 5c4 manual packet available, or manual lane explicitly waived with reason.

Owner: Meta Coordinator.

Decision outcomes:

- If initialization quality is GREEN and the focused 5-case gate is GREEN, authorize expanded 9-run predator-human evidence.
- If initialization quality is GREEN but lifecycle remains RED, accept seeding independently and open one evidence-backed lifecycle/behavior route.
- If initialization remains RED, return to a narrow seeding calibration plan without lifecycle tuning.
- If manual and SMR evidence disagree, prefer deterministic SMR for acceptance and open a separate app parity/config observability route.
- If the focused gate still combines independent seed `101` prey collapse and seed `202` early predator extinction, authorize explicit gate decomposition for diagnosis while keeping final E11-H hard closeout unchanged.

Acceptance/evidence gate:

- written route decision classifies each failure mechanism;
- no unresolved candidate is silently promoted;
- `DEFER_STEP5C5`: the SMR Analyst provides a controlled artifact-path fixture with distinct
  values for all seven first-event timeline fields and exact field-by-field mapping proof, or
  Meta explicitly waives/reclassifies the finding with evidence before E11-H package closeout;
- before E11-H package/closeout, natural production-caller timestamp regressions cover predator-human contact, predator hunt, herbivore grazing, and predator death, unless Meta records an explicit evidence-backed waiver;
- all non-retained experiments are reverted or quarantined diagnostic-only;
- focused 5-case GREEN is still required before expanded 9-run;
- expanded 9-run GREEN is still required before full 45-run.

Unlocks: either a single reviewed follow-up route or the existing expanded/full E11-H gate order.

## Verification Matrix

Track B focused verification:

```powershell
dotnet test "WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj" --filter "InitialEcology|HabitatAware|Wave11AnimalLifecycle" --no-restore -m:1 -p:UseSharedCompilation=false
```

ScenarioRunner focused verification:

```powershell
dotnet test "WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj" --filter "EcologyInitialization|EcologyEarlyContact|LowCostProfileCompatibility" --no-restore -m:1 -p:UseSharedCompilation=false
```

Hygiene:

```powershell
git diff --check
```

Exact test names are implementation-plan outputs; filters above define the intended test families, not permission to create placeholder-only tests.

## Hard Stop Conditions

- Stop if implementation requires `Person.cs`, Track C AI, or Track A/Graphics behavior without a new reviewed handoff.
- Stop if lifecycle constants are changed in the same slice as seeding.
- Stop if ScenarioRunner begins calculating or mutating habitat suitability.
- Stop if emergency rescue or replenishment is needed for acceptance.
- Stop after a focused RED candidate; do not stack another tuning patch.
- Do not run expanded/full matrices before the strict gate order permits them.
- Do not package raw `.artifacts/**`.

## Definition Of Done

- Initial ecology placement is deterministic and habitat-aware in the promoted runtime policy.
- Initial herbivore and predator budgets are derived from viable habitat/prey capacity rather than independent global random assignment.
- SMR can separate initial-condition quality from lifecycle outcome.
- Legacy random and habitat-aware policies can be compared under identical seeds/configs.
- Manual visual observations can be reconciled with exported SMR evidence.
- E11-H hard invariants and rescue-free closeout requirements remain unchanged.
