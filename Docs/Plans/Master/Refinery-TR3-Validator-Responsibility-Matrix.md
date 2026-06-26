# Refinery TR3 Validator Responsibility Matrix

Status: TR3-A implementation artifact
Owner: Track D
Scope: Wave 10.5 TR3-A / Task 2 only

## Purpose

TR3-A classifies the current director imperative validator surface so later TR3 work can shrink it safely. This matrix is an audit and handoff artifact: it does not retire rules, migrate `.problem` constraints, change bridge/runtime contracts, or close the rest of the Wave10.5 audit-gates plan.

TR3-A implements only the validator responsibility matrix slice from `Docs/Plans/Master/Wave10.5-Refinery-TR3-Audit-Gates-Plan.md`. Task 1, Task 3, Task 4, Task 5, and Task 6 remain later TR3 work unless Meta opens them separately.

Later formal-fidelity inventory: `Docs/Plans/Master/Refinery-Formal-Coverage-And-Fidelity-Matrix.md` deepens this historical ownership matrix with current formal-strength classifications. This note is a pointer only; it does not rewrite TR3-A classifications.

## Scope Guardrails

- No C# bridge/client/adapter/ScenarioRunner changes in TR3-A.
- No Runtime, AI, App, or Graphics changes in TR3-A.
- No paid/live guardrail cleanup in TR3-A; that belongs to TR3-B.
- No `.problem` semantic migration in TR3-A without separate Meta/formal-model review.
- No validator rule deletion or retirement in TR3-A without existing replacement evidence and focused tests.
- Marker-only `ModelConstraint(...)` declarations in `.problem` files are not counted as formal coverage.

## Category Definitions

`Owner category` is the final TR3 gate category and must be one of:

- `formal artifact`: target owner is a real Refinery model assertion/predicate/error predicate or generated formal artifact.
- `bridge guard`: target owner is Java/C# wire parsing, contract, DTO, vocabulary, or bridge mapping validation.
- `runtime adapter guard`: target owner is C# adapter/runtime command application safety.
- `retired`: rule should be removed only with explicit dead-rule evidence and focused regression coverage.

`Current holding layer` records where the rule lives today. `transitional Java guard` means the Java validator currently owns the behavior while the final owner is not fully implemented yet.

## Validator-Owned Rule Matrix

| Rule ID | Current behavior | Current implementation | Current test evidence | Behavior mode | Owner category | Current holding layer | Desired TR3 endpoint | Retirement condition | Notes / risk |
|---|---|---|---|---|---|---|---|---|---|
| INV-01 | Reject unsupported director op types; reject campaign ops when disabled; normalize severity when explicit severity does not match effect count. | `DirectorModelValidator.validateAndRepair`, `normalizeOptionalSeverity`, op dispatch; `DirectorDesign.INV_01`. | `DirectorModelValidatorTest.validateAndRepair_NormalizesStorySeverityToEffectCount`, `validateAndRepair_RejectsCampaignOpsWhenGateDisabled`; planner gate-off fallback tests. | reject / normalize | bridge guard | transitional Java guard | Supported op and campaign gate policy should move toward bridge/config guard; severity vocabulary should move toward shared vocabulary/formal artifact coverage. | None in TR3-A. | This rule mixes multiple meanings; keep matrix rows explicit during later split work. |
| INV-02 | Enforce at most one story beat; reject unsupported/null effect entries. | `storyBeatSeen`, `sanitizeEffects`. | Extra story beat currently repaired by dropping, covered indirectly by validator/planner tests; effect validation covered by `DirectorModelValidatorTest.validateAndRepair_RejectsUnknownEffectDomain`. | reject / drop | formal artifact | transitional Java guard; marker-only `inv_storybeat_at_most_one` exists | Real designated-output-area cardinality/error predicate plus extraction test coverage. | None in TR3-A. | Marker-only model rows are not enough to retire Java guard. |
| INV-03 | Reject story beat during cooldown; reject effect modifier outside bounds. | cooldown check; `sanitizeEffects`; `model.problem` has `error storyBeatDuringCooldown(...)`. | Cooldown behavior covered at planner/fallback level; modifier bounds covered by fuzz and domain/effect tests. | reject | formal artifact | partial formal coverage plus transitional Java guard | Expand formal coverage beyond cooldown and prove parity; keep Java guard until candidate paths are fully covered. | None in TR3-A. | `storyBeatDuringCooldown` is the only observed real error predicate coverage in current `.problem` files. |
| INV-04 | Require story beat identity fields. | `beatId` blank check. | Covered through valid/invalid story tests and planner fallback behavior. | reject | bridge guard | transitional Java guard | Bridge/DTO guard for required wire fields, with formal output-area required-field coverage when solver path is authoritative. | None in TR3-A. | Required identity is both bridge safety and formal output-area shape. |
| INV-05 | Require story text and enforce max text length; reject excessive effect count. | story text checks; `sanitizeEffects` effect count cap. | Covered by existing validator tests and fuzz no-crash coverage. | reject | bridge guard | transitional Java guard | Bridge guard for payload bounds; formal artifact can model text presence, not practical text length semantics alone. | None in TR3-A. | Text length is pragmatic runtime/ops safety, not purely formal model truth. |
| INV-06 | Clamp story duration; align effect durations to parent beat duration. | duration clamp; `hasMismatchedEffectDuration`; `sanitizeEffects`. | `DirectorModelValidatorTest.validateAndRepair_AlignsStoryEffectDurationToBeatDuration`; budget-after-alignment test. | repair / normalize | formal artifact | transitional Java guard; marker-only `inv_effect_duration_aligned_to_story` exists | Formal output constraint for aligned duration plus bridge/runtime-safe repair policy decision. | None in TR3-A. | Repair behavior may remain orchestration-owned even if formal constraint is added. |
| INV-07 | Enforce directive vocabulary and directive name presence. | directive blank/allowlist check. | Valid/invalid directive planner tests; fallback drops invalid directive. | reject | bridge guard | transitional Java guard | Shared vocabulary/bridge guard in TR3-C; formal directive kind coverage after vocabulary source is explicit. | None in TR3-A. | Do not expand vocabulary in TR3-A. |
| INV-08 | Reject new major beat when an active major beat exists. | `hasActiveSeverity(facts, "major")`. | `DirectorModelValidatorTest.validateAndRepair_RejectsMajorWhenMajorAlreadyActive`, allows-major counterpart. | reject | formal artifact | transitional Java guard | Formal runtime-fact predicate using active beat facts and severity. | None in TR3-A. | Requires reliable runtime assertions for active beats. |
| INV-09 | Reject new epic beat when an active epic beat exists. | `hasActiveSeverity(facts, "epic")`. | `DirectorModelValidatorTest.validateAndRepair_RejectsEpicWhenEpicAlreadyActive`, allows-epic counterpart. | reject | formal artifact | transitional Java guard | Formal runtime-fact predicate using active beat facts and severity. | None in TR3-A. | Same migration dependency as INV-08. |
| INV-10 | Enforce domain stack cap; currently also used for directive duration clamp warning. | `validateDomainStackCap`; directive duration clamp warning. | `DirectorModelValidatorTest.validateAndRepair_AllowsSameSignSameDomainModifiers`; domain stack behavior also covered by fuzz. | reject / repair | formal artifact | transitional Java guard; marker-only `inv_domain_stack_cap` exists | Split mixed use: domain stack to formal artifact, directive duration warning to bridge/repair policy. | None in TR3-A. | Mixed meaning needs later cleanup before any deletion. |
| INV-11 | Enforce opId, colony/faction references, campaign self-target, treaty kind required; duplicate opId also reports this code. | opId checks, `seenOpIds`, colony bounds, faction range, self-target checks, treaty required. | `validateAndRepair_RejectsDuplicateOpId`, campaign range/self-target/treaty tests. | reject | bridge guard | transitional Java guard | Bridge/contract guard for IDs and references; some runtime adapter checks may remain for apply-time truth. | None in TR3-A. | Broad catch-all code; later TR3 should split if diagnostics need sharper ownership. |
| INV-12 | Enforce max ops, conflicting/duplicate directives, bias validation, one campaign op per checkpoint. | max size check, `directivesPerColony`, `sanitizeBiases`, `campaignSeen`. | duplicate/conflicting directives covered through validator/planner tests; multiple campaign op test; fuzz. | reject / drop | formal artifact | transitional Java guard; marker-only `inv_one_directive_per_colony` exists | Formal cardinality/conflict predicates where possible; bridge guard for bias payload shape. | None in TR3-A. | Contains both model cardinality and bridge payload concerns. |
| INV-13 | Normalize operation ordering deterministically. | `repaired.sort(...)`, `sortKey`, `stableSecondaryKey`. | `DirectorModelValidatorTest.validateAndRepair_NormalizesOrderDeterministically`. | sort / normalize | bridge guard | transitional Java guard | Bridge/output canonicalization policy; may remain Java orchestration even after formal migration. | None in TR3-A. | Not a formal invariant by itself; it is deterministic output policy. |
| INV-14 | Conservative retry never adds new ops. | `conservativeRetryPatch` filters only from candidate patch. | `DirectorModelValidatorTest.conservativeRetryPatch_DropsInvalidOpsWithoutAddingNewOnes`. | fallback / drop | bridge guard | planner/fallback orchestration guard | Keep as retry/fallback orchestration policy; TR3-B should own cleanup. | None in TR3-A. | Included for audit completeness but not a pure validator semantic rule. |
| INV-15 | Enforce total checkpoint influence budget. | `validateInfluenceBudget`, `DirectorInfluenceBudget.calculateBudgetUsed`. | budget exceeded/boundary tests in `DirectorModelValidatorTest`; sidecar budget markers elsewhere. | reject | formal artifact | transitional Java guard; marker-only `inv_budget_cap` exists | Formal budget predicate after cost vocabulary is modeled; Java guard remains until parity proof exists. | None in TR3-A. | Budget is both formal policy and operator safety guard. |
| INV-16 | Reject causal chain loop and missing follow-up identity/text. | `validateAndSanitizeCausalChain`. | `DirectorCausalChainValidationTest.validateAndRepair_RejectsCausalChainLoop_INV16`. | reject | formal artifact | transitional Java guard | Formal causal-chain predicates when causal fields are promoted into solver coverage. | None in TR3-A. | Causal chain remains unsupported by core sidecar coverage; do not promote in TR3-A. |
| INV-17 | Enforce combined parent+follow-up causal chain budget. | `validateAndSanitizeCausalChain` budget calculation. | `DirectorCausalChainValidationTest.validateAndRepair_RejectsCombinedBudget_INV17`. | reject | formal artifact | transitional Java guard | Formal combined-budget predicate after causal chain formal support exists. | None in TR3-A. | Depends on future causal chain solver promotion. |
| INV-18 | Validate causal condition type, metric, operator, threshold semantics. | `validateAndSanitizeCausalChain`. | `DirectorCausalChainValidationTest` unknown metric and population eq tests. | reject | formal artifact | transitional Java guard | Formal causal condition vocabulary/predicate coverage. | None in TR3-A. | Causal fields are currently unsupported in solver core coverage. |
| INV-19 | Validate causal chain window and maxTriggers bounds. | `validateAndSanitizeCausalChain`. | `DirectorCausalChainValidationTest` window and maxTriggers tests. | reject | formal artifact | transitional Java guard | Formal causal chain bounds after causal support is opened. | None in TR3-A. | Keep Java guard until causal model support exists. |
| INV-20 | Reject contradictory same-domain modifiers in one checkpoint. | `validateNoContradictoryModifiers`. | `DirectorModelValidatorTest.validateAndRepair_RejectsContradictorySameDomainModifiers`; opposite-domain and same-sign tests. | reject | formal artifact | transitional Java guard; marker-only `inv_no_contradictory_same_domain_modifiers` exists | Real formal predicate/error predicate with parity tests. | None in TR3-A. | Marker-only row is not a replacement. |

## Planner / Fallback Orchestration Matrix

| Responsibility | Current implementation | Current test evidence | Behavior mode | Owner category | Current holding layer | Desired TR3 endpoint | Notes / risk |
|---|---|---|---|---|---|---|---|
| Iterative correction loop | `DirectorRefineryPlanner.validateAndRepair(...)` retry loop. | `DirectorRefineryPlannerIterativeCorrectionTest`; `DirectorRefineryPlannerTest`. | retry / fallback | bridge guard | planner orchestration | TR3-B fallback boundary cleanup. | Do not mix with validator-rule retirement in TR3-A. |
| Deterministic fallback after failed validation | `DirectorRefineryPlanner` fallback construction after retries. | `DirectorRefineryPlannerTest.validateAndRepair_UsesDeterministicFallbackAfterRetries`. | fallback | bridge guard | planner orchestration | TR3-B should document solver path vs deterministic fallback responsibilities. | Fallback must stay conservative and explicit. |
| Rejected command accounting | `DirectorPipelineTelemetry` via planner validation failure path. | duplicate opId planner telemetry tests. | telemetry | bridge guard | planner telemetry | TR3-B/SMR evidence policy. | Evidence semantics matter for paid/live guardrails but are not TR3-A behavior. |
| Campaign fallback gate | `DirectorRefineryPlanner` constructor/config path and fallback behavior. | `DirectorRefineryPlannerTest.validateAndRepair_FallbackCanEmitCampaignWhenEnabled`, gate-off fallback test. | fallback / config gate | bridge guard | planner orchestration | TR3-B fallback cleanup; TR3-C vocabulary/family prep. | Keep separate from validator `campaignEnabled` rejection. |
| Solver-sidecar coverage honesty | `DirectorRefinerySolver`, `DirectorSolverObservability`, ScenarioRunner consume paths. | W8.7 sidecar tests and validator artifact evidence. | evidence / diagnostics | bridge guard | solver-sidecar orchestration | Later Wave10.5 Task 4, not TR3-A. | TR3-A does not expand coverage fields. |

## Formal Artifact Coverage Notes

- Current director `.problem` files define a real `error storyBeatDuringCooldown(...)` predicate.
- Current `ModelConstraint(...)` rows are useful design markers but are not enforcement by themselves.
- Current sidecar validated core covers only story core and directive core fields. Effects, biases, causal fields, and campaign fields remain unsupported by core sidecar coverage.
- Java validator rules should remain as current holding-layer guards until a rule has replacement evidence in a formal artifact, bridge guard, or runtime adapter guard.

## TR3-A Classification Summary

- Formal-covered now: `INV-03` cooldown has partial real formal coverage via `storyBeatDuringCooldown(...)`, but Java transitional guard remains.
- Bridge/runtime-candidate: op types, wire identity/reference checks, vocabulary checks, deterministic ordering, retry/fallback orchestration, and apply-time safety concerns.
- Transitional Java guard: all `INV-*` rules remain held by Java for TR3-A closeout.
- Retired: none.

## Handoff To TR3-B / TR3-C

- TR3-B should use this matrix to separate fallback responsibilities from validator semantics and keep paid/live guardrails explicit.
- TR3-C should use this matrix when reducing mirrored vocabulary for factions, treaty kinds, goal categories, domains, severities, and output modes.
- Later formal migration should move one rule family at a time, with explicit replacement evidence before deleting Java guards.
