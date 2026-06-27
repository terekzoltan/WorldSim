# Pre-W10.6 RFM-M2 Closeout

Status: GREEN accepted
Owner: Meta Coordinator
Date: 2026-06-27

## Decision

RFM-M2 closes the Pre-W10.6 Refinery Model Fidelity interrupt gate.

Wave10.6-Q1 and Wave10.6-Q2 are unblocked for their own turn-gates:

- `W10.6-Q1` C# coverage infrastructure, Track B primary with Track C/D consult.
- `W10.6-Q2` Java coverage infrastructure, Track D.

This closeout does not claim the director Refinery model is solver-complete. It only confirms that the most dangerous fidelity risks identified before W10.6 have been exposed, bounded, and routed clearly enough for baseline coverage work to resume.

## Inputs Accepted

| Gate | Status | Evidence |
|---|---|---|
| RFM-D1 | GREEN | `Docs/Plans/Master/Refinery-Formal-Coverage-And-Fidelity-Matrix.md` distinguishes real formal coverage, marker-only rows, transitional Java guards, bridge/runtime guards, observability-only markers, and unsupported solver-sidecar surfaces. |
| RFM-D2 | GREEN | `Docs/Plans/Master/Refinery-RFM-D2-Runtime-Fact-Authority-And-Fixtures.md` and Java fixture tests tie `DirectorRuntimeFacts` to C#-originated PatchRequest fixtures and document canonical vs transitional fields. |
| RFM-D3 | GREEN | Active major/epic story-beat exclusivity was promoted into real formal error predicates for the scoped director core path, while Java validator guards remain transitional/backstop. |
| RFM-D4 | GREEN | `DirectorRefineryDifferentialHarnessTest` classifies validator/solver/bridge agreement, drift, repair-only transitional behavior, and unsupported formal surfaces. |
| RFM-V1 | GREEN | `Docs/Evidence/SMR/pre-w10.6-rfm-v1-behavior-proof/README.md` records no-paid `refinery_fixture` and `refinery_live_mock` behavior proof with one applied checkpoint and zero request/apply failures in both lanes. |

## Formal / Transitional Boundary Statement

Currently proven:

- Model-fidelity governance is explicit in Combined/state/docs.
- A real formal coverage inventory exists and does not equate marker-only rows with formal enforcement.
- Runtime-fact authority is documented and backed by C#-originated fixture parity checks.
- At least one non-trivial rule family beyond the existing cooldown proof has real formal enforcement: active major/epic explicit core story-beat conflict detection.
- Differential evidence can now distinguish covered parity, validator-vs-formal drift, validator repair-only transitional behavior, and unsupported-by-formal regions.
- Current Wave10.5 safe behavior is re-grounded through fixture/live_mock ScenarioRunner evidence.

Not proven, and not required for this closeout:

- Full solver-backed parity for all director semantics.
- Solver validation for nested effects, directive biases, campaign ops, or causal-chain semantics.
- Retirement of transitional Java validator guards.
- Paid/live LLM behavior.
- Manual app smoke.
- Combat/campaign family formal fidelity.

## Residual Routing

- Future effects/biases/campaign/causal-chain formalization must use a new approved predicate-promotion or family-fidelity slice; do not silently treat current unsupported markers as formal parity.
- Any future ScenarioRunner lane/schema issue remains Track B-owned.
- Any new director/model/refinery semantic expansion must state whether it is transitional, marker-only, bridge/runtime-owned, or real formal enforcement.
- Future combat/campaign family fidelity remains a later post-Wave11/Wave11.5 candidate unless a blocking regression appears.

## W10.6 Resume Guardrails

- Coverage work remains baseline-only: no hard percentage threshold, no PR/push/scheduled CI fail, and no numeric coverage gate for Wave11.
- Raw coverage artifacts should stay under `.artifacts/coverage/<run-name>/` and must not be committed by default.
- W10.6-Q1/Q2 must not reopen refinery semantics, paid/live behavior, ScenarioRunner lane contracts, or formal-model scope unless a new Meta-approved plan explicitly does so.
