# Refinery Formal Coverage And Fidelity Matrix

Status: RFM-D1 implementation artifact
Owner: Track D
Scope: Pre-W10.6 RFM-D1 formal coverage inventory

## Purpose

RFM-D1 records the current director Refinery truth surface as a current repo snapshot. It distinguishes real Refinery predicates/error predicates from marker-only artifacts, transitional Java guards, bridge/extractor guards, runtime application guards, observability-only markers, and unsupported solver-sidecar regions.

This matrix is artifact/inventory evidence only. It does not promote any `.problem` rule, retire any Java validator guard, change any bridge/runtime contract, or prove behavior.

## Current Repo Snapshot Sources

Formal artifact sources:
- `refinery-service-java/src/main/resources/refinery/director/design.problem`
- `refinery-service-java/src/main/resources/refinery/director/model.problem`
- `refinery-service-java/src/main/resources/refinery/director/runtime.problem`
- `refinery-service-java/src/main/resources/refinery/director/output.problem`

Java validator and design sources:
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorModelValidator.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorDesign.java`

Solver-sidecar, extraction, and observability sources:
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorRuntimeAssertionsMapper.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorOutputAssertionsProblemMapper.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorCorePatchAssertionsMapper.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorRefinerySolver.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorValidatedOutputExtractor.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/refinery/DirectorSolverObservability.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/ComposedPatchPlanner.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorPipelineTelemetry.java`

Runtime-fact and bridge sources:
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorSnapshotMapper.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorRuntimeFacts.java`
- `refinery-service-java/src/main/java/hu/zoltanterek/worldsim/refinery/planner/director/DirectorBridgeContractMapper.java`

Historical baseline source:
- `Docs/Plans/Master/Refinery-TR3-Validator-Responsibility-Matrix.md`

## Classification Vocabulary

- `real formal predicate/error predicate`: an actual `pred` or `error` in a `.problem` artifact that the Refinery solver evaluates.
- `marker-only artifact row`: a model marker such as `ModelConstraint(...)` or `OutputBoundaryRule(...)` that documents intent but does not enforce a rule by itself.
- `artifact existence only`: a file, class, reference, or resource exists, but no enforcement claim follows from existence alone.
- `transitional Java guard`: imperative Java validation, repair, retry, or fallback code currently holds the behavior.
- `bridge/parser/extractor guard`: parsing, core assertion mapping, extractor, DTO, or bridge mapping logic constrains or rejects data outside a formal predicate.
- `runtime application / adapter guard`: C# adapter or runtime application logic protects apply-time behavior. RFM-D1 records this only when it is relevant to a director semantic boundary.
- `observability-only coverage marker`: diagnostic or telemetry signal such as `directorSolverValidatedCoverage:story_core` or `directorSolverValidatedCoverage:directive_core`; this is not formal predicate proof.
- `unsupported by solver path`: the solver sidecar path explicitly omits or reports a feature as unsupported.

## Formal Artifact Inventory

| Artifact | Current content | Evidence source(s) | Current proof type | Overclaim guardrail note |
|---|---|---|---|---|
| `director/design.problem` | Director checkpoint, story slot, directive slot, severity/domain/goal/directive shape classes. | `design.problem` | artifact existence only | Classes and references define vocabulary/shape; they are not rule enforcement without predicates/error predicates. |
| `director/runtime.problem` | Runtime checkpoint context fields and active beat/directive fact classes. | `runtime.problem`; `DirectorRuntimeAssertionsMapper` | artifact existence only + mapped assertion shape | Mapped runtime facts are not the same as authoritative C# runtime facts; authority remains RFM-D2. |
| `director/output.problem` | Designated output area and marker `OutputBoundaryRule(...)` rows. | `output.problem` | artifact existence only + marker-only artifact row | Output area shape exists; `OutputBoundaryRule(...)` is not a real formal predicate. |
| `director/model.problem` marker rows | `ModelConstraint(...)` entries for most intended invariants. | `model.problem` lines with `ModelConstraint(...)` | marker-only artifact row | Marker-only rows must not be counted as real formal coverage. |
| `director/model.problem` cooldown predicate | `error storyBeatDuringCooldown(...)` over runtime cooldown and story slot. | `model.problem`; `DirectorRefinerySolver` | real formal predicate/error predicate for this narrow case | This is partial cooldown coverage only; Java cooldown guard still remains. |

## Semantic-Family Matrix

| Semantic family | Current status | Formal artifact status | Evidence source(s) | Current proof type | Holding layer | Overclaim guardrail note | RFM-D2/D3 implication |
|---|---|---|---|---|---|---|---|
| Story beat cardinality | Extra story beats are dropped/normalized before final output; extractor rejects multiple true story slots. | `ModelConstraint(inv_storybeat_at_most_one)` marker only; slot shape exists. | `DirectorModelValidator.storyBeatSeen`; `DirectorValidatedOutputExtractor` multiple story slot check; `model.problem` | transitional Java guard + bridge/parser/extractor guard + marker-only artifact row | Java validator and extractor | Extractor failure is not formal predicate proof; marker-only row is not enforcement. | Candidate RFM-D3 family, but selection depends on RFM-D2 authority and Meta review. |
| Directive cardinality | Duplicate same directive may be dropped; conflicting directive per colony rejected; extractor rejects multiple true directive slots. | `ModelConstraint(inv_one_directive_per_colony)` marker only; directive slot shape exists. | `DirectorModelValidator.directivesPerColony`; `DirectorValidatedOutputExtractor`; `model.problem` | transitional Java guard + bridge/parser/extractor guard + marker-only artifact row | Java validator and extractor | The extractor only sees core slot output, not per-colony directive semantics. | Candidate RFM-D3 family; needs careful per-colony formal design. |
| Cooldown | Story beat during active cooldown is rejected. | `error storyBeatDuringCooldown(...)` exists and is real for runtime cooldown plus story slot. | `model.problem`; `DirectorModelValidator` cooldown branch; `DirectorRuntimeAssertionsMapper` | real formal predicate/error predicate + transitional Java guard | Formal artifact and Java validator | Formal coverage is partial: it covers story beat during cooldown, not every director invariant. | Useful reference pattern for RFM-D3 predicate promotion. |
| Severity/effect alignment | Explicit severity is normalized from effect count; mismatched effect duration is aligned to story duration. | `ModelConstraint(inv_effect_duration_aligned_to_story)` marker only. | `DirectorModelValidator.normalizeOptionalSeverity`, `inferSeverity`, `hasMismatchedEffectDuration`, `sanitizeEffects`; `model.problem` | transitional Java guard + marker-only artifact row | Java validator | Normalization/repair is not formal predicate proof. Nested effects are omitted from core solver sidecar. | High-risk candidate due historical duration mismatch class. |
| Effect type/domain/modifier validity | Effects must be `domain_modifier`, known domain, and bounded modifier. | `ModelConstraint(inv_domain_modifier_bounds)` marker only. | `DirectorModelValidator.sanitizeEffects`; `DirectorDesign.VALID_DOMAINS`; `RefineryVocabulary.DOMAINS` | transitional Java guard + shared vocabulary + marker-only artifact row | Java validator and shared vocabulary | Shared vocabulary is bridge consistency, not solver-backed validation. | RFM-D3 may model a subset after budget/domain design is clear. |
| Bias type/goal/weight validity | Biases must be `goal_bias`, known goal category, bounded weight, and optional bounded duration. | `ModelConstraint(inv_goal_bias_weight_bounds)` marker only. | `DirectorModelValidator.sanitizeBiases`; `DirectorDesign.VALID_GOAL_CATEGORIES`; `RefineryVocabulary.GOAL_CATEGORIES` | transitional Java guard + shared vocabulary + marker-only artifact row | Java validator and shared vocabulary | Biases are omitted by current core sidecar mapping. | Later than core story/directive/effect promotion unless RFM-D3 scopes it explicitly. |
| Budget cap | Total influence budget is rejected if over remaining budget; causal chain budget includes follow-up cost. | `ModelConstraint(inv_budget_cap)` marker only; runtime budget field exists. | `DirectorModelValidator.validateInfluenceBudget`; `DirectorInfluenceBudget`; `DirectorRuntimeAssertionsMapper.remainingInfluenceBudget`; `model.problem` | transitional Java guard + mapped runtime fact + marker-only artifact row | Java validator | Budget field existence is not a formal budget predicate. | High-risk RFM-D3 candidate after RFM-D2 confirms budget fact authority. |
| Domain stack cap | Same-domain modifier sum cannot exceed absolute cap. | `ModelConstraint(inv_domain_stack_cap)` marker only. | `DirectorModelValidator.validateDomainStackCap`; `DirectorDesign.MAX_DOMAIN_STACK`; `model.problem` | transitional Java guard + marker-only artifact row | Java validator | No real predicate/error predicate currently sums effect modifiers. | Candidate RFM-D3 family if effect modeling is included. |
| Contradictory modifiers | Opposite-sign same-domain modifiers in one checkpoint are rejected. | `ModelConstraint(inv_no_contradictory_same_domain_modifiers)` marker only. | `DirectorModelValidator.validateNoContradictoryModifiers`; `model.problem` | transitional Java guard + marker-only artifact row | Java validator | The marker name does not enforce contradiction detection. | High-risk RFM-D3 candidate because it constrains harmful LLM effect combinations. |
| Active major/epic exclusivity | New major/epic beats rejected when matching active severity exists. | No real predicate; active beat fact shape exists. | `DirectorModelValidator.hasActiveSeverity`; `DirectorRuntimeAssertionsMapper.appendActiveBeat`; `runtime.problem` | transitional Java guard + mapped runtime fact | Java validator and runtime assertion mapper | Runtime fact mapping is not authority proof; RFM-D2 owns authority. | High-risk RFM-D3 candidate after active beat authority is locked. |
| Colony reference bounds | Directive colonyId must be within current colony count. | Runtime colony count field exists, but no real predicate. | `DirectorModelValidator` colonyId check; `DirectorSnapshotMapper.readColonyCount`; `DirectorRuntimeAssertionsMapper.colonyCount` | transitional Java guard + mapped runtime fact | Java validator and mapper | Java defaults `colonyCount` to at least 1; authority remains unresolved. | RFM-D2 must define canonical colony count source and drift cases. |
| Faction reference bounds | Campaign faction ids must be within static range and cannot self-target. | No director formal artifact coverage. | `DirectorModelValidator.validateFactionRange`; `DirectorDesign.MIN_FACTION_ID/MAX_FACTION_ID`; `DirectorBridgeContractMapper` campaign mapping | transitional Java guard + bridge/parser/extractor guard | Java validator and bridge | Campaign is outside current solver core sidecar. | Do not promote before campaign family work unless Meta opens it. |
| Campaign op constraints | Campaign ops require `campaignEnabled`, one campaign op per checkpoint, valid kind/treaty fields. | No director formal artifact coverage; campaign assertion is not mapped into core solver output. | `DirectorModelValidator`; `DirectorCorePatchAssertionsMapper` marks campaign unsupported; `DirectorOutputAssertionsProblemMapper` records `unsupportedFeaturesIgnored:campaign`; `DirectorSolverObservability` emits `directorSolverUnsupported:campaign` | transitional Java guard + unsupported by solver path + observability-only coverage marker | Java validator and solver-sidecar observability | `directorSolverUnsupported:campaign` is an honest unsupported marker, not coverage. | Future post-Wave11 family fidelity; not RFM-D3 unless separately approved. |
| Causal-chain constraints | Causal chain type, condition metric/operator/threshold, follow-up loop, window, max triggers, and combined budget are Java-held. | No director formal artifact coverage; causal chain is omitted from core sidecar. | `DirectorModelValidator.validateAndSanitizeCausalChain`; `DirectorCorePatchAssertionsMapper` records `causalChain`; `DirectorOutputAssertionsProblemMapper` records `unsupportedFeaturesIgnored:causalChain`; `DirectorSolverObservability` emits `directorSolverUnsupported:causalChain` | transitional Java guard + unsupported by solver path + observability-only coverage marker | Java validator and solver-sidecar observability | Unsupported marker must not be described as solver-backed causal validation. | Route to RFM-D4 mismatch reporting and later explicit causal promotion plan. |
| Op id uniqueness and max ops | Missing/duplicate opId rejected; max ops rejected. | No real predicate. | `DirectorModelValidator.seenOpIds`, `MAX_OPS_PER_CHECKPOINT` | transitional Java guard | Java validator | This is bridge/output hygiene, not current formal coverage. | Keep as bridge/validator guard unless formal output identity design is opened. |
| Story identity, text, and duration bounds | Required fields and text length enforced; duration clamped. | Story attributes exist; no real predicate for text/duration bounds. | `DirectorModelValidator` story field checks; `design.problem` story attributes | transitional Java guard + artifact existence only | Java validator | Attribute existence is not required-field or bounds proof. | Duration alignment may be higher priority than text bounds. |
| Directive duration bounds | Directive duration clamped into safe range. | Directive attribute exists; no real predicate. | `DirectorModelValidator` directive duration clamp; `design.problem` directive duration attribute | transitional Java guard + artifact existence only | Java validator | Clamp behavior is orchestration/repair policy, not formal proof. | Not first RFM-D3 candidate unless directive family is promoted. |
| Deterministic output ordering | Repaired ops are sorted for deterministic output. | No formal artifact coverage. | `DirectorModelValidator.sortKey`, `stableSecondaryKey` | transitional Java guard | Java validator/orchestration | Deterministic ordering is output policy, not model validity. | Keep in Java orchestration. |
| Conservative retry never adds ops | Retry filters candidate patch without adding new ops. | No formal artifact coverage. | `DirectorModelValidator.conservativeRetryPatch`; `DirectorRefineryPlanner` retry/fallback flow | transitional Java guard | Java planner orchestration | Retry behavior is not formal predicate coverage. | Keep in orchestration; RFM-D4 can compare drift outcomes. |
| Solver-sidecar story/directive core extraction | Core story/directive can be mapped, solved, extracted, and observed as story_core/directive_core. | Formal artifacts provide core story/directive shape; extraction enforces concrete fields and single slots. | `DirectorCorePatchAssertionsMapper`; `DirectorOutputAssertionsProblemMapper`; `DirectorValidatedOutputExtractor`; `DirectorSolverObservability` | bridge/parser/extractor guard + observability-only coverage marker + artifact existence only | Core assertion mapper, extractor, observability | `story_core` and `directive_core` are coverage markers for extracted core fields, not proof that all story/directive semantics are formal predicates. | RFM-D4 should compare formal/validator/bridge outcomes for these core paths. |
| Mapped runtime facts | Java maps tick, colonyCount, cooldown, remaining budget, active beats, and active directives into runtime assertions. | Runtime artifact fields exist and mapper emits assertions. | `DirectorSnapshotMapper`; `DirectorRuntimeFacts`; `DirectorRuntimeAssertionsMapper`; `runtime.problem` | mapped assertion shape + artifact existence only | Java snapshot/runtime assertion mappers | Mapped facts are not authority proof. | RFM-D2 must tie these to C#-originated fixtures. |
| Authoritative runtime facts | Not locked in RFM-D1. Some values have Java fallback/default behavior. | Not established by formal artifacts. | `DirectorSnapshotMapper` fallback/default paths; Pre-W10.6 plan RFM-D2 | unsupported by RFM-D1; future authority proof needed | RFM-D2 Track D + Track B consult | RFM-D1 must not claim runtime-fact authority closure. | Direct RFM-D2 prerequisite. |

## INV Cross-Reference

| Rule family | Primary current code | Formal-strength classification | Notes |
|---|---|---|---|
| `INV-01` | `DirectorModelValidator` op dispatch, campaign gate, severity normalization | transitional Java guard / bridge guard | Mixed rule; split before any retirement. |
| `INV-02` | story cardinality and effect null/type checks | marker-only for cardinality; Java guard for effects | No real story cardinality predicate. |
| `INV-03` | cooldown and effect modifier bounds | partial real formal predicate for cooldown only; Java guard for modifier bounds | `storyBeatDuringCooldown(...)` is narrow coverage. |
| `INV-04` | story beat identity fields | transitional Java guard / bridge guard | Attribute existence is not required-field proof. |
| `INV-05` | story text bounds and max effects | transitional Java guard | Text length is pragmatic bridge/ops safety. |
| `INV-06` | story duration clamp and effect duration alignment | marker-only + transitional Java guard | High-risk historical mismatch class. |
| `INV-07` | directive vocabulary/name | transitional Java guard + shared vocabulary | Vocabulary parity is not formal predicate proof. |
| `INV-08` | active major exclusivity | transitional Java guard + mapped runtime fact | Needs RFM-D2 authority before promotion. |
| `INV-09` | active epic exclusivity | transitional Java guard + mapped runtime fact | Needs RFM-D2 authority before promotion. |
| `INV-10` | domain stack cap and directive duration warning | marker-only + transitional Java guard | Mixed meaning; split before promotion. |
| `INV-11` | opId, colony/faction references, campaign self-target, treaty kind required | transitional Java guard / bridge guard | Broad catch-all; not formal coverage. |
| `INV-12` | max ops, directive conflicts, bias validation, one campaign op | marker-only for directive cardinality; Java guard for rest | Campaign side remains unsupported by solver sidecar. |
| `INV-13` | deterministic operation ordering | transitional Java orchestration guard | Output canonicalization, not formal validity. |
| `INV-14` | conservative retry does not add ops | planner orchestration guard | Not a formal semantic rule. |
| `INV-15` | influence budget | marker-only + transitional Java guard | High-risk RFM-D3 candidate after RFM-D2 budget authority. |
| `INV-16` | causal follow-up identity/loop | transitional Java guard + unsupported by solver path | Causal chain omitted from core sidecar. |
| `INV-17` | causal combined budget | transitional Java guard + unsupported by solver path | Depends on causal modeling and budget authority. |
| `INV-18` | causal condition metric/operator/threshold | transitional Java guard + unsupported by solver path | `directorSolverUnsupported:causalChain` is observability-only. |
| `INV-19` | causal window/maxTriggers | transitional Java guard + unsupported by solver path | Not formal coverage. |
| `INV-20` | contradictory same-domain modifiers | marker-only + transitional Java guard | High-risk RFM-D3 candidate. |

## Highest-Risk Gaps

1. Budget cap: operator-visible budget safety is Java-held, while `inv_budget_cap` is marker-only and `remainingInfluenceBudget` authority is not locked until RFM-D2.
2. Effect duration alignment: historical live alignment issues make marker-only coverage especially risky.
3. Contradictory modifiers and domain stack cap: these constrain harmful LLM intervention combinations but remain Java-held.
4. Active major/epic exclusivity: depends on active beat runtime facts, so formal promotion depends on RFM-D2 authority.
5. Campaign and causal chain: present in Java/wire paths but explicitly unsupported by the core solver-sidecar path.
6. Runtime-fact authority: Java can map runtime facts into assertions, but RFM-D1 does not prove those facts are authoritative C# truth.

## RFM-D2 Handoff

RFM-D2 should lock runtime-fact authority for:
- `tick`
- `colonyCount`
- `beatCooldownRemainingTicks`
- `remainingInfluenceBudget`
- `activeBeats`
- `activeDirectives`

RFM-D2 should explicitly document Java fallback/default fields in `DirectorSnapshotMapper`, C#-originated fixture sources, and drift cases that must fail loudly. RFM-D1 does not close runtime-fact authority.

## RFM-D3 Candidate Shortlist

RFM-D1 only provides a risk-ranked shortlist. It does not decide which rule must be promoted in RFM-D3.

Candidate families for RFM-D3 review:
- story beat at most one
- one directive per colony / directive cardinality
- effect duration aligned to parent story duration
- contradictory same-domain modifiers forbidden
- influence budget cap
- active major/epic exclusivity after RFM-D2 authority lock

Final RFM-D3 scope must come after RFM-D2 and Meta review.

## Non-Goals And Guardrails

- No `.problem` semantic migration was performed in RFM-D1.
- No Java validator, solver, bridge, C#, runtime, ScenarioRunner, AI, App, or Graphics behavior was changed in RFM-D1.
- `story_core` and `directive_core` are observability-only coverage markers for extracted core fields, not proof of full story/directive formal coverage.
- `directorSolverUnsupported:campaign` and `directorSolverUnsupported:causalChain` are unsupported-path markers, not formal coverage.
- `RFM-D1` provides artifact/inventory evidence only; it is not formal parity proof, not formal predicate promotion proof, and not behavior proof.
