# Post-Wave7 Large-Topology Combat-Realization Fix Plan

Status: ready after baseline reconcile
Owner: SMR Analyst
Last updated: 2026-04-15

## 1. Purpose

This plan defines the next narrow post-Wave7 runtime slice.

Primary question:

- how can the large-topology `standard` lane improve `contact -> pairing -> damage` realization without regressing the `medium` control lane?

This is explicitly a non-wave follow-up.

It is:

- runtime-centered,
- narrow in scope,
- evidence-gated,
- and constrained by the already-landed contact-funnel observability layer.

## 2. Hard Blocker: Baseline Reconcile

Implementation must not start until one canonical baseline is selected and verified across:

- code,
- summary/handoff docs,
- and evidence artifacts.

This blocker is now resolved for the current workspace.

### 2.1 Canonical baseline selected

Canonical source-of-truth baseline:

- retained large-topology person-level contact fix in runtime code,
- refreshed handoff summary:
  - `Docs/Plans/Master/Post-Wave7-Contact-Funnel-And-Retreat-Dominance-Implementation-Summary.md`
- post-fix artifacts:
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-005/`
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-003/`
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-full-planner-confirm-001/`

### 2.2 Reconcile proof checklist

Completed checks:

1. Code truth
- retained fix present in:
  - `WorldSim.Runtime/Simulation/World.cs`
  - `WorldSim.Runtime/Simulation/Person.cs`

2. Summary truth
- retained-fix and full-confirm state merged into:
  - `Docs/Plans/Master/Post-Wave7-Contact-Funnel-And-Retreat-Dominance-Implementation-Summary.md`

3. Artifact truth
- canonical post-fix artifacts treated as future comparison baseline

4. Reproduction proof
- current workspace re-ran canonical HTN standard/medium lanes into:
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-reconcile-001/`
  - `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-reconcile-001/`
- headline metrics matched the canonical retained baseline

## 3. Explicit Inputs

Implementation input docs:

- `Docs/Plans/Master/Post-Wave7-Contact-Funnel-And-Retreat-Dominance-Implementation-Summary.md`
- this plan

Implementation input artifacts:

- `.artifacts/smr/planner-compare-wave7-retreat-dominance-standard-005/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-medium-control-003/`
- `.artifacts/smr/planner-compare-wave7-retreat-dominance-full-planner-confirm-001/`

## 4. Scope

## 4.1 In scope

- narrow runtime contact-realization tuning for large topology,
- runtime-owned logic only,
- local pursue-to-contact / contact-to-pairing / damage-realization improvement,
- targeted follow-through behavior under existing combat semantics,
- validation using the existing `contact` telemetry as source-of-truth.

## 4.2 Out of scope

- threat/arbitration rewrite,
- planner rebalance,
- broad world-side topology expansion,
- global combat-group eligibility/clustering/pairing rewrite,
- new observability slice,
- process-spawn test harness infra work.

## 5. Guardrails

1. Do not reopen broad pairing-window / eligibility / clustering rewrite.

Reason:

- this was already tried and rejected during retained-fix work.

2. Do not reopen threat/arbitration logic.

Reason:

- current residual is downstream of that layer and cross-planner.

3. Do not do planner-specific tuning.

Reason:

- the residual `standard` vs `medium` gap remains cross-planner after the retained fix and full-planner confirm matrix.

4. Do not couple gameplay to telemetry events.

Required wording and implementation rule:

- fixes may react to underlying hostile-contact / pursuit / direct-combat conditions,
- fixes must not key behavior directly off `hostileSensed`, `adjacentContacts`, `FactionCombatDamageEvents`, or any `ReportContact*` hook.

5. Keep the contact-funnel telemetry as source-of-truth for validation.

## 6. Topology Gate

The topology gate must be explicit and runtime-owned.

Canonical gate:

- `Width * Height >= 18000`

Implication:

- `standard (192x108 = 20736)` -> gated path active
- `medium (128x72 = 9216)` -> gated path inactive

This slice must include at least one negative test proving the medium/default path remains unaffected.

## 7. Runtime Seams

Primary seam candidates:

- `World.ResolveGroupCombatPhase()`
- `World.GetCombatPairingDistance()` remains unchanged in the first pass
- World-owned combat-core frontier helper logic used only for pairing eligibility and pair selection in the gated path

Preferred character of the fix:

- local,
- World-owned,
- pairing-realization oriented,
- topology-gated,
- no global rewrite.

Person-local chase / memory follow-up is not the first-pass seam for this slice.

## 7.1 Battle Geometry Alignment Decision

Battle geometry stays unchanged in this slice.

Chosen rule for this slice:

- do not introduce a new anchor model,
- do not modify battle center / radius,
- do not modify routing origin,
- do not modify snapshot/read-model geometry.

The new signal is allowed to influence pairing only when a conservative geometry guard is satisfied.

## 7.2 Architecture Note

Combat-core membership is decided in World-owned helper logic.

Explicit rule:

- `RuntimeCombatGroup` remains a passive runtime state container,
- it must not gain direct `World` query responsibilities,
- it must not gain frontier-query or pairing-core responsibilities.

The relevant context stays in `World`:

- `CurrentTick`
- combat intent / follow-through checks
- any topology-gated combat-core frontier helper logic

## 7.3 Frontier Support Guard

Frontier unlock must not rely on a single outlier combat-core member.

Chosen support rule for this slice:

- both groups must have at least `2` pairing-core members,
- there must be at least `2` qualifying combat-core member pairs whose distance is `<= pairingThreshold`,
- and the frontier signal is the average of the two closest qualifying pair distances.

This is intentionally stricter than raw minimum-distance unlocking.

## 7.4 Geometry Guard

Frontier-based pairing remains allowed only when anchor distance stays within a fixed conservative geometry guard.

Chosen guard for this slice:

- `anchorDistance <= pairingThreshold + 2`

This guard is frozen in the plan before implementation and must be covered by a targeted runtime test.

## 8. Success Criteria

For the `standard` lane, at least one of these should improve materially without `medium-default` regression:

- `adjacentContacts`
- `factionCombatDamageEvents`
- `battlePairings`
- `combatEngagements`

Must also hold:

- `routingBeforeDamage` stays `0`
- `medium-default` remains materially stable
- no broad side effects that mimic the rejected world-side topology expansion

## 9. Verification Gate

Step 1: targeted runtime tests

- existing contact-follow-through suite
- existing contact-funnel telemetry suite
- at least one positive large-topology test
- at least one negative medium/default unchanged test

Step 2: targeted SMR rerun

Minimum required configs:

- `standard-default`
- `standard-fastmove`
- `medium-default`

Step 3: conditional broader rerun

Only if Step 2 shows improvement without control-lane regression:

- rerun the full-planner confirm matrix

## 10. Exit Conditions

This slice can be closed when all of the following are true:

- runtime change is small and localized,
- targeted tests pass,
- targeted SMR rerun shows `standard` improvement,
- `medium-default` does not regress,
- and the result is merged back into the summary handoff document.
