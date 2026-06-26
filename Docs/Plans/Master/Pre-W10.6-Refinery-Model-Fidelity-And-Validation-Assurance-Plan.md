# Pre-W10.6 Refinery Model Fidelity And Validation Assurance Plan

Status: Meta-reviewed, accepted for Combined insertion
Owner: Meta Coordinator primary, Track D primary implementation owner, Track B consult on runtime-fact authority
Placement: Hard governance interrupt gate after W10.6-M1 and before W10.6-Q1/Q2 coverage implementation

## Why This Exists

WorldSim wants the LLM-driven Season Director to be constrained by a formal Refinery model.

That means the correctness of the Refinery model is not a side concern. It is nearly as important as the correctness of the rest of the project, because it will decide whether an LLM-proposed intervention is formally accepted, rejected, or repaired.

If the model is too weak, marker-only, stale, or semantically drifted from the runtime truth, then a "validated" director output may still be wrong in the only place that matters: the formal gate itself.

This plan exists to make that risk first-class before the project moves deeper into Wave10.6 coverage work or Wave11 ecology runtime expansion.

## User Decisions Locked For This Plan

- Placement: new hard governance gate before Wave10.6.
- Scope: director-first only, but future combat/campaign family placement must be reserved.
- Gate strength: hard governance gate.
- Current manual confidence note: Wave10.5 has not yet been user-smoked manually, so current refinery behavior still needs a behavior-proof pass in addition to static review and test evidence.

## Problem Statement

Today, WorldSim has a meaningful Refinery transition slice, but not a mature formal-model truth surface yet.

Current reality:

- the Java service can load and solve Refinery artifacts,
- the project has `design/model/runtime/output.problem` artifacts for the director family,
- the LLM proposal path is already bounded by a designated output area concept,
- Java fallback is explicitly separated,
- shared vocabulary has been cleaned up,
- but most semantic truth still lives in transitional Java validator logic.

This creates four major risk classes:

1. **Marker-only formalism risk**

The repo contains `.problem` artifacts, but many constraints are still marker rows (`ModelConstraint(...)`) instead of real predicates or error predicates.

2. **Validator-truth drift risk**

`INV-*` semantics are still mostly enforced by Java guards. If the Java validator changes but the Refinery artifacts do not, the project can quietly misrepresent what is formally validated.

3. **Runtime-fact authority drift risk**

If `DirectorSnapshotMapper -> DirectorRuntimeFacts` loses parity with the intended formal runtime layer, then even a formally correct model may be validating the wrong world facts.

4. **False confidence risk**

If docs, review language, or operator diagnostics overstate the solver path, the team may believe the director is formally safer than it really is.

## Current State Snapshot

### What already exists and is good

- Real director artifact family layout under `refinery-service-java/src/main/resources/refinery/director/`.
- Real solver/library integration exists.
- Structured assertion-style designated output path exists.
- Runtime facts are mapped into a Refinery runtime layer.
- Deterministic fallback is isolated and documented as transitional.
- Shared Java/C# symbolic vocabulary now exists for the scoped bridge surface.
- Director output path already distinguishes solver markers such as `directorSolver*` from regular runtime markers.

### What is still transitional and high-risk

- `DirectorModelValidator` and associated Java guards still hold the majority of effective semantic truth.
- The TR3-A validator matrix explicitly says all `INV-*` rules still remain Java-held transitional guards.
- `director/model.problem` currently contains mostly `ModelConstraint(...)` rows rather than formal enforcement.
- The validator matrix explicitly notes that the only currently observed real error predicate coverage is `storyBeatDuringCooldown(...)`.
- Effects, biases, causal fields, and campaign fields are not yet covered by a broad solver-backed formal truth path.
- Current core solver coverage is limited compared to the full validator surface.

## Non-Goals

- Do not rewrite the whole director subsystem before Wave10.6.
- Do not jump straight to combat/campaign family semantics.
- Do not add paid/live requirements.
- Do not let the fallback grow into a second planner.
- Do not block on an arbitrary coverage percentage.
- Do not claim formal parity that the repo cannot prove.

## Hard Governance Rule Introduced By This Plan

Until this plan reaches its closeout gate, no new director/model/refinery/output semantic expansion may be accepted as normal feature work unless one of the following is true:

- it is explicitly transitional and documented as such,
- it adds no new formal truth claims,
- or it comes with explicit formal-model coverage evidence and a reviewed boundary note.

In practice this means:

- no new director validator semantics should land casually,
- no new `.problem` artifact claim should be treated as real enforcement without proof,
- and no future plan may describe the current Java validator as the intended end-state formal authority.
- W10.6-Q1 and W10.6-Q2 are blocked until RFM-M2 closes this interrupt gate.

## Required Truth Vocabulary For This Plan

Future work under this plan must use these terms precisely:

- design layer
- model layer
- runtime layer
- designated output area
- formal predicate
- error predicate
- transitional Java guard
- runtime-fact authority
- bridge contract
- solver-backed validation
- unsupported / marker-only / not-yet-formalized

## Plan Structure

### RFM-M1 - Governance Lock And Language Correction

Owner: Meta Coordinator

Goal:

- Make the model-fidelity concern explicit in the project workflow and docs.

Tasks:

- Create this plan.
- Insert a hard governance interrupt gate before W10.6-Q1/Q2 in Combined.
- Update mandatory refinery reference docs with a priority note.
- Update `ops/PROJECT_STATE.md` so the next step reflects this new gate.

Acceptance:

- Combined no longer treats Wave10.6 as the immediate next active gate.
- Combined explicitly shows W10.6-M1 as complete, but W10.6-Q1/Q2 as blocked by this gate.
- The mandatory pre-read/refinery alignment docs mention that model fidelity is first-class.
- Next action clearly points to the new fidelity gate.

### RFM-D1 - Formal Coverage Inventory

Owner: Track D

Goal:

- Produce the first truth inventory that distinguishes what is actually formalized vs what is still Java-held.

Tasks:

- Audit the current director artifacts and classify each relevant semantic rule as one of:
  - real formal predicate/error predicate,
  - marker-only artifact row,
  - transitional Java guard,
  - bridge/runtime guard,
  - unsupported by solver path.
- Start from the existing TR3-A validator matrix, but deepen it from rule ownership into formal-strength classification.
- Cover at least:
  - story beat cardinality,
  - directive cardinality,
  - cooldown,
  - severity/effect alignment,
  - effect/bias type validity,
  - budget cap,
  - domain stack cap,
  - contradictory modifiers,
  - active major/epic exclusivity,
  - colony/faction reference bounds,
  - campaign op constraints,
  - causal-chain constraints.

Deliverable:

- New checked-in matrix doc, likely:
  - `Docs/Plans/Master/Refinery-Formal-Coverage-And-Fidelity-Matrix.md`

Acceptance:

- Every currently important semantic family is labeled with real formal status.
- No marker-only rule is described as equivalent to real formal coverage.
- The matrix names the highest-risk gaps explicitly.

### RFM-D2 - Runtime-Fact Authority And Fixture Corpus

Owner: Track D primary, Track B consult

Goal:

- Prove that the Refinery runtime layer is validating the right world facts.

Tasks:

- Lock the authoritative mapping contract for `DirectorSnapshotMapper -> DirectorRuntimeFacts`.
- Require representative fixture payloads or snapshot samples sourced from the C# refinery snapshot/runtime path, not Java-only synthetic assumptions.
- Define a fixture corpus of representative runtime inputs, including:
  - empty/minimal checkpoint,
  - active cooldown,
  - active major/epic beat,
  - active directive,
  - budget edge/boundary,
  - multiple-colony world,
  - campaign-enabled vs disabled paths,
  - causal-chain-relevant metric facts.
- Add parity tests showing the runtime layer fields consumed by the model are intentional, stable, and not silently defaulted away.

Required Track B consult deliverable:

- a short handoff note or fixture contract confirming which C# snapshot/runtime fields are the authority for the Java runtime layer,
- explicit identification of any known transitional fallback/default fields,
- and agreement on which drift cases must fail loudly.

Acceptance:

- Runtime-fact authority is documented and test-backed.
- A future Track B snapshot change can fail clearly if it drifts from the expected formal runtime layer contract.
- The gate cannot pass on Java-only self-confirmation; at least one C#-originated fixture/sample path must be part of the proof surface.

### RFM-D3 - Director-First Predicate Promotion Pack

Owner: Track D

Goal:

- Convert the highest-value director rules from marker-only/transitional status into real formal predicates or error predicates.

Priority order for first promotion wave:

1. story beat at most one
2. one directive per colony
3. effect duration aligned to parent story duration
4. contradictory same-domain modifiers forbidden
5. budget cap
6. active major/epic exclusivity

Tasks:

- Choose a minimal first subset that can be made formally real without destabilizing the bridge.
- The chosen subset must come from RFM-D1's highest-risk director rules, not only from easy/low-value candidates.
- Add actual predicates/error predicates instead of `ModelConstraint(...)` placeholders.
- Add focused solver-path tests for each promoted rule.
- Keep Java guards until replacement evidence exists and parity is proven.

Acceptance:

- At least one multi-rule family beyond cooldown is genuinely promoted into real formal enforcement.
- The promoted subset includes negative solver tests and explicit parity/mismatch classification against the transitional validator path.
- The matrix from RFM-D1 shows improved formal coverage, not just better comments.
- Java guards are only downgraded or retained with explicit reason.

### RFM-D4 - Differential Solver-vs-Validator Harness

Owner: Track D

Goal:

- Detect semantic drift between the formal model, the Java validator, and the final emitted/validated outputs.

Tasks:

- Build a deterministic differential test harness that feeds the same checkpoint/candidate into:
  - current Java validator path,
  - current solver-backed path,
  - bridge extraction/output path.
- Compare outcomes using a controlled classification, for example:
  - both accept same normalized result,
  - formal rejects while validator accepts,
  - validator rejects while formal accepts,
  - unsupported-by-formal path,
  - transitional repair-only case.
- Emit a mismatch report that is useful for planning, not just pass/fail.

Acceptance:

- The project can demonstrate where the formal path and transitional Java path still diverge.
- Unsupported regions are explicit instead of being mistaken for parity.

### RFM-V1 - Wave10.5 Behavior Proof Pass

Owner: Track D primary, Meta/user smoke assist optional

Goal:

- Confirm the currently accepted Wave10.5 director slice behaves as expected in practice, not only in static review.

Tasks:

- Run a focused behavior proof pass using safe lanes only:
  - fixture,
  - live_mock,
  - optional manual app smoke if available.
- Record what is proven vs not proven.
- Explicitly avoid overclaiming solver-backed fidelity where the formal model is still transitional.

Why this exists:

- The user explicitly noted Wave10.5 has not yet been manually tested.
- We should not strengthen governance around a formal gate without re-grounding what the current slice actually proves.

Acceptance:

- Short evidence note exists for the current user-facing/director-facing behavior proof.
- Any mismatch between formal expectation and observed behavior is routed back into RFM-D1/D3/D4.

### RFM-M2 - Closeout And W10.6 Unblock Decision

Owner: Meta Coordinator

Goal:

- Decide whether the most dangerous model-fidelity holes have been exposed and routed clearly enough to allow Wave10.6 to resume.

Wave10.6 may reopen only when:

- RFM-D1 is accepted,
- RFM-D2 is accepted,
- at least one real predicate-promotion slice from RFM-D3 is accepted,
- RFM-D4 can explain current drift boundaries,
- and RFM-V1 has either passed or documented non-blocking residuals.

This is a governance gate, not a demand that the ultimate formal model is finished.

## Execution Order

The intended order is:

1. `RFM-M1` governance lock
2. `RFM-D1` formal coverage inventory
3. `RFM-D2` runtime-fact authority and fixture corpus
4. `RFM-D3` director-first predicate promotion pack
5. `RFM-D4` differential solver-vs-validator harness
6. `RFM-V1` focused Wave10.5 behavior proof pass (may start earlier when safe lanes are ready, but must close before `RFM-M2`)
7. `RFM-M2` closeout and W10.6 unblock decision

If ownership and evidence are clear, `RFM-V1` may run in parallel with `RFM-D2`/`RFM-D3`, but it must not overclaim beyond fixture/live_mock/manual-safe proof.

## Verification Strategy

The new plan is only credible if verification distinguishes these proof types:

1. **Artifact existence proof**
- file exists, parses, loads

2. **Formal enforcement proof**
- predicate/error predicate actually influences solver validity

3. **Parity proof**
- formal path and validator path agree for a covered rule family

4. **Bridge proof**
- validated facts map correctly to bridge DTOs/runtime commands

5. **Behavior proof**
- fixture/live_mock/manual behavior matches what the project claims publicly

Every reviewed step under this plan should state explicitly which proof type it is providing.

## Future Family Placement

This plan is director-first only.

Future family fidelity work should not begin before Wave11 closes unless a major blocker appears.

Reserved future placement:

- **Post-Wave11 / Wave11.5 candidate:** combat/campaign refinery family fidelity planning and artifact-strength audit.
- If Wave11 ecology changes runtime-fact surfaces significantly, default family-fidelity expansion should slide to **Wave12+** rather than overlapping with ecology churn.

Planned future topics for that later slice:

- combat family real predicate/error-predicate design
- campaign family real predicate/error-predicate design
- shared/common artifact truth vs family-local truth
- family-neutral solver evidence semantics
- future extraction/mapping parity beyond the current director bridge

## Risks This Plan Intends To Close

| Risk | Why it matters | Closing mechanism |
|---|---|---|
| Marker-only artifacts mistaken for real enforcement | false formal confidence | RFM-D1 + RFM-D3 |
| Validator/model drift | validated output may be semantically wrong | RFM-D1 + RFM-D4 |
| Runtime-fact mismatch | model validates wrong world snapshot | RFM-D2 |
| Unsupported regions misreported as formal | governance and ops misinformation | RFM-D1 + RFM-D4 + RFM-V1 |
| New semantics added to transitional Java casually | formal gate degrades over time | hard governance rule |

## Done Definition

This pre-W10.6 gate is done when:

- model-fidelity governance is explicit in Combined/state/docs,
- the project has a real formal coverage matrix,
- runtime-fact authority is documented and test-backed,
- at least one non-trivial rule family is promoted from marker-only/transitional into real formal enforcement,
- differential drift reporting exists,
- current Wave10.5 behavior has been re-grounded by a focused proof pass,
- and Meta can say clearly what the formal model does prove, what it does not prove, and what remains transitional.

## Recommended Immediate Next Step

Start `RFM-D1` as the first real implementation step, and block W10.6-Q1/Q2 until this pre-W10.6 interrupt gate closes through `RFM-M2`.
