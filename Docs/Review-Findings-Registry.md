# Review Findings Registry

Purpose:
- Capture reusable findings from plan reviews, step reviews, deep reviews, and post-review fix loops.
- Keep entries short and actionable so future Track agents can avoid repeated mistakes.
- Record only findings worth reusing; do not duplicate every routine review note.

Entry format:

```text
## YYYY-MM-DD - <Target> - <Severity> - <Short title>

- Track: <Track / role>
- Source: <review type, commit, PR, or doc reference>
- Finding: <what was wrong or risky>
- Impact: <why it matters>
- Resolution / guidance: <what to do next time>
- Status: open | fixed | guidance
```

Severity guide:
- `blocking`: must be fixed before commit or step closeout.
- `major`: likely bug, regression, ownership violation, or missing verification.
- `minor`: non-blocking improvement or maintainability issue.
- `guidance`: reusable process/coordination note.

Entries:

## 2026-05-21 - Wave 9 Deep Review - Major - Partial campaign rosters must not complete then churn

- Track: Track B / Runtime campaign lifecycle
- Source: Meta deep-review synthesis for Wave 9, after external Swarm GREEN review
- Finding: `IsCampaignAssemblyComplete(...)` can mark assembly complete when a non-empty roster cannot grow, even if `MemberCount < RequestedMemberCount`; marching validation then immediately returns understrength campaigns to assembly. This can create assembly-complete / march-start / returned-or-aborted churn with no real route progress for campaigns that requested more members than are eligible.
- Impact: Wave 10 siege integration could inherit misleading campaign lifecycle counters or a stuck campaign loop, especially once larger campaign rosters become common.
- Resolution / guidance: Before Wave 10 `P6-E` starts, add a focused regression with `requestedMemberCount > eligible candidates` and align the invariant: either require full requested roster before `MarkAssemblyComplete(...)`, or explicitly support partial campaigns and update marching validation/counters accordingly.
- Status: fixed and accepted in Meta P6-E preflight re-review; strict requested-strength regression added; `P6-E` proper unblocked

## 2026-05-21 - Wave 9 Deep Review - Major - Carrier/resupply evidence must not overclaim delivery semantics

- Track: Track B + SMR/Meta / ScenarioRunner Wave 9 SMR evidence semantics
- Source: Meta deep-review synthesis for Wave 9, after external Swarm GREEN review
- Finding: The deterministic `carrier_resupply` lane proves carrier assignment plus direct carried-inventory/ration-pool supply-source consumption, but the final evidence wording and counters use delivery/resupply language. Runtime AI still leaves `SupplyCarrierCanDeliver=false`, so actual carrier delivery command/path behavior is not proven by Wave 9.
- Impact: Wave 10 planning may incorrectly treat carrier delivery/resupply as proven runtime behavior and build logistics or convoy assumptions on an evidence overclaim.
- Resolution / guidance: Before Wave 10 `P6-E` starts, reword/additively clarify Wave 9 evidence and counters as carrier assignment + supply-source consumption probes, or add a focused probe that proves actual delivery semantics before claiming delivery/resupply executed.
- Status: fixed and accepted in Meta P6-E preflight re-review via additive `carrierSupplyApplications` evidence and wording cleanup; actual actor command/path delivery remains unproven/out of Wave9 scope

## 2026-05-21 - Wave 9 Deep Review - Minor - Retired hardening plan should not look like active frontier

- Track: Meta / documentation state
- Source: Meta deep-review synthesis for Wave 9
- Finding: `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md` still says the implementation frontier is `P5-G (B part)` and uses unchecked task scaffolding, while Combined and evidence docs mark Wave 9 closed.
- Impact: External reviewers or future Track agents may mistake a retired acceptance-detail plan for current sequence authority.
- Resolution / guidance: Mark the hardening plan as historical/retired or add a top-level pointer to Combined as the current source of truth during the next docs cleanup.
- Status: open; routed to Meta docs cleanup after P6-E preflight blockers are handled

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Blocking - Drilldown timeline must not stamp final Wave9 counters onto every sample

- Track: Track B / ScenarioRunner Wave 9 SMR evidence surface
- Source: Meta + Swarm step-review synthesis for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: `Program.cs` captures drilldown timeline samples during the normal run, builds Wave9 telemetry only after the run, then applies one final `ScenarioWave9TimelineSnapshot` to every sample. The focused test asserts positive carrier evidence in the first timeline sample, locking in retroactive final-counter stamping.
- Impact: Drilldown artifacts appear temporal but can report future/final Wave9 evidence at tick 1 or every tick, creating false-green SMR validation risk for the exact evidence surface Step 12A is supposed to prepare.
- Resolution / guidance: Before Step 12A closeout, either capture tick-accurate Wave9 telemetry at sample time or keep Wave9 evidence run-level only and leave timeline `wave9` empty/default unless values are tick-accurate. Update tests so early timeline samples cannot validate retroactively stamped final counters.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review; SMR Analyst validation remains the next active gate

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Major - Synthetic Wave9 probes must not be mislabeled as run telemetry

- Track: Track B / ScenarioRunner Wave 9 SMR evidence semantics
- Source: Meta internal step-review for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: Several Wave9 deterministic lanes build telemetry from separate mini-worlds or a separate `SimulationRuntime` after the normal ScenarioRunner run and attach the result to the run artifact.
- Impact: If these outputs are presented as ordinary run telemetry, SMR Analyst may believe the actual reported run executed the mechanics. This is acceptable only if clearly separated or explicitly documented as deterministic prep/probe evidence, not final organic run proof.
- Resolution / guidance: Before Step 12A closeout, either execute Wave9 lanes inside the reported run/runtime state or label/route them as deterministic probe evidence with clear docs/tests and SMR Analyst validation caveat. Do not use sidecar probes as final Wave 9 acceptance.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review via explicit `deterministic_probe` / `not_tick_sampled` metadata and docs; SMR Analyst validation remains the next active gate

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Minor - Wave9Scenario alias matrix should be explicit

- Track: Track B / ScenarioRunner config surface
- Source: Swarm step-review for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: The closeout-plan alias `foraging-extension` normalizes to `campaign_foraging`, while `campaign-foraging` returns config error. This may be intentional, but the implementation brief phrase "hyphen aliases" can be read as accepting `campaign-foraging`.
- Impact: Operator confusion or inconsistent evidence recipe usage can create avoidable config errors during SMR prep/validation.
- Resolution / guidance: Before Step 12A closeout, either explicitly document that `campaign-foraging` is intentionally invalid or accept it as an additional harmless alias. Add alias matrix coverage for all canonical lanes.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review by accepting `campaign-foraging` as an alias and adding alias matrix coverage; SMR Analyst validation remains the next active gate

## 2026-05-18 - Wave 9 P6-D(A) Campaign Overlay - Blocking - Invalid army anchor sentinel must not render as map coordinate

- Track: Track A / Graphics campaign overlay and panel consume
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(A-part)`
- Finding: `CampaignOverlayPass.DrawArmyMarker(...)` renders `ArmyRenderData.AnchorX/AnchorY` unconditionally, and `CampaignPanelRenderer` displays `anchor(x,y)` unconditionally. P6-D(B) read-model can validly export `AnchorActorId=-1`, `AnchorX=-1`, `AnchorY=-1` when no assigned/living anchor exists.
- Impact: Pending/memberless campaigns can draw a bogus off-map/top-left marker and show misleading `anchor(-1,-1)` text, violating safe snapshot consume for default/sentinel campaign states.
- Resolution / guidance: Before P6-D(A) closeout, guard `AnchorActorId < 0`; skip the army marker or use an explicit valid fallback such as route origin/rally with distinct pending styling, and display `anchor:none` in the panel. Include this case in focused review/manual smoke.
- Status: fixed in P6-D(A) targeted Track A follow-up; manual smoke remains a separate closeout gate

## 2026-05-18 - Wave 9 P6-D(A) Campaign Overlay - Minor - Low-cost wording should distinguish shared pixel texture from content textures

- Track: Track A / Graphics campaign overlay documentation and handoff wording
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(A-part)`
- Finding: The handoff wording says the overlay uses "no textures", but `CampaignOverlayPass` correctly draws simple primitives with the shared `context.Textures.Pixel` texture.
- Impact: The implementation remains low-cost, but the literal claim is imprecise and can confuse future reviewers about allowed primitive rendering.
- Resolution / guidance: Use precise wording: "no custom/content texture assets; uses only the shared pixel texture for primitive drawing."
- Status: fixed in P6-D(A) targeted Track A follow-up; wording now distinguishes shared pixel primitive drawing from custom/content texture assets

## 2026-05-18 - Wave 9 P6-D(B) Snapshot Handoff - Blocking - Interpolated snapshots must preserve Campaigns

- Track: Track A / Graphics snapshot consume, discovered during Track B P6-D(B) handoff review
- Source: Meta internal correctness lane + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: `WorldSnapshotInterpolator.Interpolate(...)` constructs `WorldRenderSnapshot` through the compatibility constructor, which defaults `Campaigns` to `Array.Empty<CampaignRenderData>()`, so interpolated snapshots drop populated campaign read-model data.
- Impact: Track A can receive an apparently stable P6-D(B) campaign snapshot from runtime but lose it in the existing Graphics interpolation path before overlay consume.
- Resolution / guidance: Before P6-D handoff closeout / Track A overlay consume, preserve `current.Campaigns` in `WorldSnapshotInterpolator.Interpolate(...)` and add a focused regression proving interpolation keeps non-interpolated campaign collections. This requires explicit Graphics/Track A scope approval if fixed before Step 11.
- Status: fixed in P6-D(B) after authorized first-gate Graphics handoff patch and Meta re-review

## 2026-05-18 - Wave 9 P6-D(B) Read Model - Blocking - Public strings must not leak simulation enum names

- Track: Track B / Runtime campaign read-model contract
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: `SimulationRuntime.BuildCampaignRenderData(...)` exports public read-model strings via simulation enum `.ToString()` for campaign phase, supply source, forage status, and forage failure reason, and tests assert those PascalCase enum names.
- Impact: Track A would couple to C# enum member names/casing before the UI consume step, making the snapshot contract brittle and violating the P6-D acceptance that phase/status/source/failure fields be read-model-safe.
- Resolution / guidance: Before P6-D(B) closeout, replace enum `.ToString()` exports with explicit stable read-model mapping helpers (prefer lower_snake_case strings or dedicated read-model enums) and update tests to assert those stable contract literals.
- Status: fixed in P6-D(B) after Meta re-review

## 2026-05-18 - Wave 9 P6-D(B) Read Model - Major - Prove route fields and snapshot no-mutation contract

- Track: Track B / Runtime campaign read-model tests
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: Current tests cover campaign presence, detachment, supply, counters, waypoint count, and encounter marker, but do not directly assert route intent coordinates, resolved objective coordinates, `NextWaypointIndex` to `IsNext` consistency, or before/after `GetSnapshot()` counter/cache immutability.
- Impact: P6-D(B) is the Track A handoff contract; without these checks, later consumers may rely on unpinned fields or miss accidental side effects in snapshot mapping.
- Resolution / guidance: Before P6-D(B) closeout, add focused assertions for route intent/resolved objective/next waypoint consistency and a before/after `GetSnapshot()` no-mutation check for route counters/cache state. Keep mapping side-effect free.
- Status: fixed in P6-D(B) after Meta re-review

## 2026-05-17 - Wave 9 P6-C March Supply - Blocking - Revalidate roster after supply-induced routing

- Track: Track B / Runtime campaign march lifecycle
- Source: Meta + Swarm re-review synthesis for Wave 9 `P6-C` targeted fix pass
- Finding: March supply ticks before route/path/movement work, and `ArmySupplyModel` can route members via `BeginRouting(...)`; the current march loop can then keep using the pre-supply member list for same-tick movement/encounter checks.
- Impact: A member made invalid by supply attrition can still march or trigger encounter in the same tick, violating the P6-C lifecycle invariant that routing/invalid members must not march.
- Resolution / guidance: Before P6-C closeout, revalidate/recompute march-capable members immediately after supply ticking and before pathing, movement, or encounter transition. If supply causes understrength/invalid roster, return to assembly or skip movement for that tick. Add carried-inventory/no-supply regression and either add ration-pool/multi-member variants or document why focused coverage is sufficient.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C March Supply - Blocking - No-progress marching ticks skip logistics cost

- Track: Track B / Runtime campaign march lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: Current march supply ticking happens only after successful march progress; valid `Marching` campaigns that hit no-target/no-path/no-move branches record `NoProgress` and skip `TickCampaignMarchSupply(...)`.
- Impact: A blocked or stuck marching army can avoid supply consumption indefinitely, contradicting the submitted P6-C contract that march supply uses exactly one source per campaign tick.
- Resolution / guidance: Before P6-C closeout, either tick exactly one supply source after roster validation on every eligible positive-dt marching tick, independent of movement success, or explicitly change the contract/docs/tests to progress-only logistics. Add a deterministic no-progress supply regression.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C Route Cache - Blocking - Cache validity must use persistent world topology

- Track: Track B / Runtime campaign route lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: Campaign route cache validation is based on a newly constructed `NavigationGrid` per call, whose topology version is instance-local rather than tied to persistent world topology.
- Impact: Cached routes can remain apparently valid across topology changes until the immediate next-step blocked check catches them, weakening deterministic recompute/cache-hit semantics and delayed blocked-route coverage.
- Resolution / guidance: Before P6-C closeout, tie campaign route cache validity to persistent world topology or an equivalent persisted topology signature, and add delayed blocked cached-step coverage beyond `PeekNext`.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C Encounter Target - Major - Fallback route target can diverge from encounter trigger

- Track: Track B / Runtime campaign encounter lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: `TryGetCampaignMarchTarget(...)` can choose a fallback passable tile around the target colony, while encounter detection still checks only original route target proximity.
- Impact: If fallback radius exceeds encounter proximity, a campaign can reach the resolved route target but never enter `Encounter`, then stall as path-to-current-target returns no usable path.
- Resolution / guidance: Before P6-C closeout, either store/use the resolved march objective for encounter proximity or constrain fallback selection to encounter-valid proximity. Add a regression for blocked target/proximity with fallback radius greater than one.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-B Lifecycle Fix - Minor - Add adversarial roster lifecycle coverage before march semantics harden

- Track: Track B / Runtime campaign lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B` lifecycle fix
- Finding: Shared predicates cover assigned-member invalidation broadly, but explicit regressions are still sparse for individual edge states such as `Health <= 0`, isolated `IsInCombat`, invalidation caused during `_world.Update`, and max-one replacement after pruning with multiple candidates.
- Impact: P6-C will add march semantics on top of assembled rosters; without adversarial coverage, later refactors could assume roster permanence or miss a specific invalidation path even though the shared helper currently handles it.
- Resolution / guidance: Before or during P6-C march start, add focused guard tests for health-zero assigned members, isolated in-combat invalidation, world-update-induced invalidation, multi-candidate max-one replacement after pruning, and roster revalidation before first march step.
- Status: fixed in P6-C

## 2026-05-17 - Wave 9 P6-C March Handoff - Guidance - Revalidate roster before first march step

- Track: Track B / Runtime campaign march handoff
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B` lifecycle fix
- Finding: P6-B now assembles rosters and hands off with `CampaignPhase.Marching`, but P6-C must not assume roster permanence between assembly completion and first march tick.
- Impact: Actors can die, route, enter combat, or otherwise become unavailable after assembly; starting march without revalidation could reintroduce the same lifecycle class of bugs fixed in P6-B.
- Resolution / guidance: P6-C must revalidate campaign roster before the first march movement/counter update and define deterministic prune/replacement/non-complete behavior for newly invalid members.
- Status: fixed in P6-C

## 2026-05-17 - Wave 9 P6-B Campaign Assembly - Blocking - Revalidate assigned members before movement and completion

- Track: Track B / Runtime campaign assembly
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B`
- Finding: Initial campaign roster eligibility can exclude routing, combat, and hard-job actors, but already assigned army members still need lifecycle revalidation before later rally movement and assembly completion.
- Impact: An actor can become routing, in-combat, active in a battle/group, or hard-combat-job-owned after recruitment; moving or completing that actor as campaign-owned can violate combat/routing ownership and hand P6-C an invalid roster.
- Resolution / guidance: Before moving rostered members or counting them for completion, revalidate current actor state or define explicit campaign ownership semantics. Add regressions where an assigned member becomes routing, active combat/battle/group, or hard-job-owned after assignment.
- Status: fixed

## 2026-05-17 - Wave 9 P6-B Campaign Assembly - Blocking - Handle dead or missing assigned members deterministically

- Track: Track B / Runtime campaign assembly
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B`
- Finding: A campaign roster that stores only actor IDs can become permanently stuck if an assigned actor dies or is removed before rally completion and no prune/replacement/failure policy exists.
- Impact: `MemberCount >= RequestedMemberCount` can prevent replacement while completion can never succeed because a roster ID no longer resolves to a living person, leaving assembly blocked forever and making P6-C handoff unsafe.
- Resolution / guidance: During assembly, prune dead/missing members and allow deterministic replacement, or introduce an explicit failed/disbanding/recruiting fallback state. Add tests for assigned-member death/removal before rally completion and full-roster recovery/failure behavior.
- Status: fixed

## 2026-05-15 - Wave 9 P6-A1 Campaign Query Boundary - Minor - Prove detached roster snapshots once rosters become non-empty

- Track: Track B / Runtime campaign query boundary
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-A1`
- Finding: Empty-roster snapshot tests can prove the public query seam no longer exposes the live roster collection, but they do not prove copied roster contents remain stable after future assembly/rally mutation fills `MemberActorIds`.
- Impact: P6-B will introduce real roster assignment/mutation; without a non-empty retained-snapshot regression, a later mapper refactor could accidentally re-expose live roster state while existing empty-roster tests still pass.
- Resolution / guidance: When P6-B adds roster mutation, add a focused non-empty roster regression that captures a `CampaignRuntimeSnapshot`, mutates internal campaign/army roster state through runtime methods, and asserts the retained snapshot keeps the original copied member IDs.
- Status: fixed

## 2026-05-15 - Wave 9 P6-A Campaign Entities - Guidance - Keep runtime state query seams internal until read-model export

- Track: Track B / Runtime campaign entities
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-A`
- Finding: Returning a copied collection of live runtime entity objects is useful for runtime tests, but it is not an immutable read-model contract for UI, ScenarioRunner, or cross-track consumption.
- Impact: Later P6-B/P6-C progression may add mutability to campaign/army state; if downstream tracks consume live state directly, snapshot boundary and ownership can drift.
- Resolution / guidance: Treat `SimulationRuntime.Campaigns` as a runtime-internal/test seam for P6-A. Before Track A/SMR consumption in P6-D or Wave 9 SMR prep, add explicit immutable read-model/export DTOs rather than exposing live runtime objects.
- Status: guidance

## 2026-05-13 - Wave 9 P5-H Foraging AI - Blocking - Zero-score support goals must not become selected trace goals

- Track: Track C / AI foraging behavior
- Source: Meta + Swarm step-review synthesis for Wave 9 `P5-H (C part)`
- Finding: `GoalSelector` can select the first non-cooldown goal even when its score is `0f`. For trace-only support goals such as `ForageArmySupply`, command-level no-demand guards can return `Idle` while `Trace.SelectedGoal` still reports the support goal after cooldown rotation.
- Impact: A runtime-created no-demand context can still pollute AI traces with a support goal that acceptance says must never be selected without explicit demand. Focused command/preview tests can pass while the selected-goal invariant remains unproven.
- Resolution / guidance: Prevent zero-score support goals from becoming selected goals, either with a positive-score eligibility guard in goal selection or an explicit selector policy for trace-only support goals. Add runtime multi-planner/no-demand regressions that assert `Trace.SelectedGoal`, command, and preview together.
- Status: fixed

## 2026-05-13 - Wave 9 P5-G Supply Carrier AI - Blocking - Do not let trace-only commands dominate default utility selection

- Track: Track C / AI supply-carrier behavior
- Source: Meta + Swarm step-review synthesis for Wave 9 `P5-G (C part)`
- Finding: A trace-only AI command that maps to `Job.Idle` can become the highest-scoring default utility goal if it is not gated by explicit demand or execution readiness. In `P5-G (C part)`, `MaintainArmySupply -> AssignSupplyCarrier` could be selected in safe no-carrier runtime context while no runtime carrier state is created.
- Impact: The actor can repeatedly choose a no-effect idle command, suppressing normal productive goals and creating a no-progress loop despite focused one-tick command trace tests passing.
- Resolution / guidance: Either execute the command by mutating the intended runtime state, or keep it trace-only but gate it behind explicit demand/readiness so it cannot dominate default runtime utility selection. Add a multi-tick regression proving carrier commands do not repeat indefinitely as no-op idle work and that useful fallback/progress resumes.
- Status: fixed

## 2026-05-12 - Review Process - Guidance - Document accepted residual findings before step closeout

- Track: Meta Coordinator / all Tracks
- Source: User policy after Wave 9 `P5-H (B part)` review synthesis
- Finding: Minor, nit, or residual-risk findings that are accepted rather than fixed can be lost if they remain only in chat review output.
- Impact: Future agents may repeat the issue or assume the risk was resolved instead of consciously deferred.
- Resolution / guidance: Any non-fixed review finding that remains after final synthesis must be recorded in a durable artifact, preferably `Docs/Review-Findings-Registry.md` and, when tied to future implementation, the relevant master/sequence plan as a deferred follow-up.
- Status: guidance

## 2026-05-12 - Wave 9 P5-H Foraging - Guidance - Treat HarvestFailed as defensive until harvest seam changes

- Track: Track B / Runtime foraging model
- Source: Step review synthesis for Wave 9 `P5-H (B part)`
- Finding: `ArmyForageFailureReason.HarvestFailed` is a defensive branch after prevalidation; current `World.TryHarvest(...)` / `Tile.Harvest(...)` behavior makes it effectively unreachable without introducing a mockable harvest seam or concurrent mutation path.
- Impact: Adding test-only seams just to force this branch would be unnecessary churn now, but future harvest refactors could make the branch reachable without coverage.
- Resolution / guidance: Keep the branch as defensive. If a mockable harvest seam, concurrent harvest path, or changed `World.TryHarvest(...)` contract is introduced, add a focused `HarvestFailed` regression test at that time.
- Status: guidance

## 2026-05-12 - Wave 9 P5-H Foraging - Minor - Cover no-yield and no-capacity branches explicitly

- Track: Track B / Runtime foraging model
- Source: Step review synthesis for Wave 9 `P5-H (B part)`
- Finding: A foraging model can implement safe no-yield/no-capacity handling while focused tests only cover successful saturation near capacity, leaving the explicit failure branch unprotected.
- Impact: Future cap or pool-capacity changes could silently convert a zero-capacity attempt into a mutation, overflow, or misleading success.
- Resolution / guidance: Add a focused test for full ration-pool or zero-yield conditions that asserts `NoYield`, unchanged source node, unchanged pool, and failure counter updates.
- Status: fixed

## 2026-05-12 - Wave 9 P5-G Supply Carrier - Major - Keep singular assignment state and actor role flags synchronized

- Track: Track B / Runtime supply carrier hook
- Source: Step review synthesis for Wave 9 `P5-G (B part)`
- Finding: A singular carrier state such as `AssignedCarrierActorId` can drift from per-actor role flags if reassignment does not clear the previous carrier role, or if clear operations accept an actor that is not the assigned carrier.
- Impact: Later runtime, AI, snapshot, or UI consumers can observe multiple `SupplyCarrier` actors, or an empty carrier state while an actor remains snapshot-visible as a carrier.
- Resolution / guidance: Lock the lifecycle with focused tests for assign A -> assign B, wrong-actor clear, assigned-actor clear, and snapshot consistency. Reassignment should either reject until explicit clear or atomically transfer the role; wrong-actor clear should reject/no-op without mutating state.
- Status: fixed

## 2026-05-11 - Wave 9 P5-I Ration Pool - Minor - Prove lifecycle conservation before integration

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-I`
- Finding: Reservation-pool supply models can pass isolated reserve, consume, and return tests while still lacking one end-to-end conservation proof across the whole lifecycle.
- Impact: Later carrier/campaign wiring could accidentally duplicate or lose food when combining reservation, consumption, and return paths.
- Resolution / guidance: Add a focused reserve -> consume -> return test proving final colony food equals original food minus consumed food, and pool/member inventory state remains consistent.
- Status: fixed

## 2026-05-11 - Wave 9 P5-I Ration Pool - Minor - Saturate all reservation arithmetic consistently

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-I`
- Finding: If only some reservation calculations use saturating arithmetic, extreme caller/config values can overflow an unguarded intermediate such as home-reserve computation.
- Impact: Overflow can violate the home-reserve contract and allow reservation from food that should remain protected.
- Resolution / guidance: Use the same saturating/normalized arithmetic policy for home reserve, desired budget, pool add, and return paths, and cover boundary inputs with focused tests.
- Status: fixed

## 2026-05-11 - Wave 9 P5-F Army Supply - Minor - Lock fractional zero-supply semantics

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-F`
- Finding: Integer inventory-consumption models can leave zero carried food plus sub-unit fractional demand in an ambiguous state if tests do not explicitly define whether supply pressure starts immediately or only after a whole food unit is unmet.
- Impact: Downstream fallback budget, carrier, foraging, or campaign steps may reinterpret `IsOutOfSupply` differently and create inconsistent attrition/routing behavior.
- Resolution / guidance: Add focused regression coverage or documentation that locks the intended semantics before closing the step: either zero food with fractional demand is not out-of-supply until whole-unit demand is unmet, or change the model to apply immediate zero-supply pressure.
- Status: fixed

## 2026-04-30 - Wave 8 ScenarioRunner Tests - Minor - Bound nested runner process helpers

- Track: Track B / SMR Analyst test harness
- Source: Wave 8 deep review after Step 7B closeout
- Finding: ScenarioRunner xUnit helpers launched nested `dotnet run` processes with unbounded waits and no process-tree cleanup.
- Impact: A hung or externally timed-out test could leave orphan `dotnet.exe`/MSBuild processes and poison later verification with file locks.
- Resolution / guidance: Use a shared test-only process helper with a generous default timeout, env override (`WORLDSIM_SCENARIO_TEST_TIMEOUT_MINUTES`), concurrent stdout/stderr reads, and `Kill(entireProcessTree: true)` cleanup. Do not apply this timeout to direct/manual SMR CLI runs.
- Status: fixed

## 2026-04-30 - SMR Clustering Deep Evidence - Minor - Drilldown topN may miss clustering-worst runs

- Track: SMR Analyst / ScenarioRunner observability
- Source: Step review for `clustering-deep-wave8-prewave9-001`
- Finding: The drilldown `topN` selector can choose runs with `score=0` and omit the highest clustering/backoff anomaly runs, because the generic drilldown score is not clustering-focused.
- Impact: A clustering investigation can correctly rank worst runs from `summary.json`/`anomalies.json`, but drilldown artifacts may not include detailed timelines for the actual clustering-worst runs.
- Resolution / guidance: For clustering-focused evidence, explicitly verify whether `drilldown/index.json` covers the worst anomaly runs. If not, rank from `summary.json`/`anomalies.json` and either record that limitation or add a future clustering-aware drilldown selector.
- Status: guidance

## 2026-04-30 - TR2-B Minimal Solver Slice - Minor - Report unsupported nested output features explicitly

- Track: Track D / Tools.Refinery migration
- Source: Step review for Wave 8.5 `TR2-B`
- Finding: Minimal solver slices may intentionally model only a subset of the existing internal assertion DTO. If nested fields are omitted from the formal problem, they can be mistaken as solver-validated unless diagnostics or tests make the omission explicit.
- Impact: Later bridge-mapping work could accidentally assume fields such as effects, biases, campaign, or causal chains were formally validated when the minimal slice only proved story/directive shell consistency.
- Resolution / guidance: For every intentionally unsupported assertion field, either emit an explicit unsupported-feature diagnostic or add a test that proves the field is out of scope for the current slice. Do not silently treat omitted fields as solved/validated facts.
- Status: guidance

## 2026-05-03 - TR2-D Cross-Track Review - Guidance - Classify no-churn diffs before closeout

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.5 `TR2-D`
- Finding: A Track D no-churn gate can legitimately detect parallel Track B-owned C#/ScenarioRunner diffs during a coordinated cross-track step.
- Impact: Treating all non-empty no-churn gates as Track D bugs can incorrectly block allowed parallel work, while ignoring them can hide ownership violations.
- Resolution / guidance: Stop Track D closeout, classify the foreign diffs by owner/scope, then resume only the owning track's verification. Mark the step YELLOW until the dependent Track B consume/evidence pass and Track D final verification are both complete.
- Status: guidance

## 2026-05-03 - TR2-D Evidence Fixtures - Major - Keep marker-rich evidence payloads semantically consistent

- Track: Track B / ScenarioRunner evidence
- Source: Step review for Wave 8.5 `TR2-D` Track B B2
- Finding: Repo-local marker-rich fixture/mock responses can claim `directorSolverValidatedCoverage:story_core` while omitting a core field such as story beat `severity` from the response patch payload.
- Impact: The artifact proves parser persistence but overclaims solver-backed semantic coverage, weakening truth-in-labeling evidence for TR2-D.
- Resolution / guidance: Marker-rich evidence responses must include every core field implied by their claimed coverage. For `story_core`, include `beatId`, `text`, `durationTicks`, and `severity`; for `directive_core`, include `colonyId`, `directive`, and `durationTicks`.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Prove documented env vars map to runtime properties

- Track: Track D / Java refinery config
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: A policy handoff documented `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED` as the switch for `directorSolver*` markers before `application.yml` explicitly mapped that env var to `planner.director.solverObservabilityEnabled`.
- Impact: Track B could follow the handoff and still fail to enable solver observability, producing paid/rehearsal artifacts without the expected `directorSolver*` evidence.
- Resolution / guidance: Any documented operator env var used as a cross-track contract must be backed by code/config or the exact supported property mechanism must be documented and tested. Prefer explicit `application.yml` mappings for Java Spring properties.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Separate current enforcement from planned guardrails

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: Refinery live SMR docs described paid-live guardrails as code-enforced even though `refinery_live_validator` and `refinery_live_paid` guardrail implementation belonged to the later Track B `W8.6-B1` step.
- Impact: Future agents could assume paid confirmation, rehearsal proof, completion caps, or concurrency locks already existed and start paid or review work on a false safety premise.
- Resolution / guidance: Planning docs must distinguish currently enforced behavior from planned downstream enforcement, especially around paid/live/secret/cost guardrails.
- Status: fixed
