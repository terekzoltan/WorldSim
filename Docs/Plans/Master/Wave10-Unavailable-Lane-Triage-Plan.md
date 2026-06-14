# Wave 10 Unavailable-Lane Triage and Evidence Fix Plan

Status: Track B Step10C-B follow-up evidence ready for Meta mini-review
Owner: Meta Coordinator
Date: 2026-06-12

Mini-review status: Track B evidence/probe fix pass accepted as commit-safe YELLOW. Follow-up Step10C-B implementation evidence is now available in local artifact `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/`: 72 probes, exit code 0, no assertions, no anomalies, and 8/8 lanes positive. The previous scout timing blocker is resolved: `scout_intel_campaign_choice` now captures telemetry inside a fresh scout-intel window (`ticks=20` in the probe artifact) and the probe shows `scoutIntelObserved=1`, `activeScoutIntel=1`, `freshScoutIntel=1`, `campaignTargetsWithScoutIntel=1` in representative runs.

## Purpose

Wave 10 Step10A export/provenance is accepted, and the SMR prep validation artifact `.artifacts/smr/wave10-smr-prep-validation-001/` completed 72 runs with exit code 0, no assertions, and no anomalies. The blocker is not artifact integrity; it is feature-proof completeness. Most Wave10 lanes are explicit `proof_unavailable`, so Step10B cannot treat lane presence in `manifest.wave10LaneNames` as proof.

This plan turns the unavailable-lane result into a bounded handoff: Track B gets an evidence/probe-only fix pass first, Track A and Track C get only conditional follow-up handoffs, and any expensive or genuinely low-incidence gap can be deferred to Step10C with explicit non-claims.

## Grill-me decisions locked

- Closeout policy: core proof should become positive before clean Step10B; expensive, rare, visual, or out-of-scope gaps may be explicitly deferred to Step10C/future work.
- Fix scope: Track B first pass is evidence/probe-only. ScenarioRunner/probe setup, config shape, artifact classification, and deterministic fixture setup are in scope. Gameplay tuning is out of scope.
- Ownership: split handoffs immediately. Track B owns evidence/runtime-probe surface first. Track A owns visual/manual readability only if Track B evidence or manual smoke identifies a UI gap. Track C owns strategist/AI behavior only if Track B proves the missing evidence is caused by strategist decision logic rather than probe setup.
- Commit timing: do not commit this package yet. Commit after Track B fix implementation and Meta mini-review, if the review is green enough.

## Non-goals

- Do not overclaim `simulation_runtime_probe` evidence as same-run/tick-sampled `main_world_run` evidence.
- Do not add gameplay tuning just to make a lane green.
- Do not reopen P6/P7 implementation epics unless the evidence pass proves a concrete implementation defect.
- Do not require direct manual visual proof for every lane before Step10B; visual/manual residuals belong in Step10C if runtime evidence is otherwise clear.
- Do not stage or commit `Docs/Architecture/` automation output with this work.

## Source evidence

- Artifact: `.artifacts/smr/wave10-smr-prep-validation-001/`
- Accepted Step10A code surface:
  - `WorldSim.Runtime/Diagnostics/ScenarioWave10Telemetry.cs`
  - `WorldSim.Runtime/SimulationRuntime.cs`
  - `WorldSim.ScenarioRunner/Program.cs`
  - `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`
- Current routing:
  - `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
  - `Docs/Review-Findings-Registry.md`
  - `ops/PROJECT_STATE.md`

## Lane triage matrix

| Lane | Current validation result | Step10B target | Owner | Routing |
|------|---------------------------|----------------|-------|---------|
| `manual_operator_launch` | Positive 9/9 | Keep as positive prep signal; keep non-claim that it is not organic proof | Track B | No fix required |
| `organic_campaign_launch` | `proof_unavailable` 9/9 | Positive organic proof or explicit proof that only probe setup was missing; must not be replaced by manual proof | Track B | Fix-now core lane |
| `campaign_siege_resolution` | Partial positive 3/9, weak siege/breach proof | Positive deterministic campaign/siege evidence with at least siege entry/pressure plus resolution or breach-related counter; if breach remains rare, classify separately | Track B | Fix-now core lane |
| `supply_line_convoy` | `proof_unavailable` 9/9 | Positive deterministic convoy request-bound outcome evidence: spawn plus delivered/failed or cap/throttle/route/home-defense outcome; call it delivery lifecycle proof only when delivered/failed is positive | Track B | Fix-now core lane |
| `multi_front_bounded` | `proof_unavailable` 9/9 | Positive bound evidence: multiple active campaigns or cap/pair-cap/home-defense/route-budget block counters | Track B | Fix-now core lane |
| `forward_base_long_campaign` | `proof_unavailable` 9/9 | Try deterministic forward-base evidence via existing setup only: established plus rest/expired/abandoned signal | Track B | Fix if evidence-only; otherwise defer to Step10C-B |
| `scout_intel_campaign_choice` | `proof_unavailable` 9/9 | Try deterministic scout-intel observe/refresh plus campaign target-with-intel signal | Track B first, Track C only if strategist consume is the proven gap | Fix if evidence-only; otherwise defer to P7-C(C)/Step10C |
| `siege_unit_breach` | `proof_unavailable` 9/9 | Try deterministic siege-unit spawn/action/breach evidence via existing tech/manual campaign setup only | Track B first, Track A only for visual/manual consume, Track C only for protection/AI gap | Defer likely acceptable to Step10C-B/A if low-incidence remains |

## Track B Step10C-B implementation evidence

Artifact: `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/` (local raw artifact, do not commit unless explicitly requested).

Result summary:

| Lane | Step10C-B result | Route |
|------|------------------|-------|
| `manual_operator_launch` | 9/9 positive manual-operator probe; still non-claims organic proof | Keep as prep/smoke signal |
| `multi_front_bounded` | 9/9 positive deterministic active multi-front proof | Fixed |
| `organic_campaign_launch` | 9/9 positive organic proof after runtime evidence setup reaches launch cadence | Fixed core lane |
| `campaign_siege_resolution` | 9/9 positive deterministic siege entry plus pressure/resolution signal | Fixed core lane |
| `supply_line_convoy` | 9/9 positive deterministic convoy spawn plus request-bound outcome signal | Fixed core lane |
| `forward_base_long_campaign` | 9/9 positive deterministic forward-base establishment plus lifecycle signal | Fixed conditional lane |
| `siege_unit_breach` | 9/9 positive deterministic siege-unit spawn plus action/pressure signal | Fixed conditional lane |
| `scout_intel_campaign_choice` | 9/9 positive; scout intel is observed, remains fresh, and target-with-intel proof is positive inside the bounded probe window | Fixed after Track B timing follow-up |

Current Meta gate input: the scout timing blocker is cleared in Track B evidence. Supply-line wording still stays precise: cap/throttle/route/home-defense blocks are request-bound outcomes, not delivery lifecycle proof unless `ConvoysDelivered` or `ConvoysFailed` is positive.

## Track B implementation handoff

Track B should make one bounded evidence/probe pass over Step10A. The goal is not to invent new gameplay policy; it is to make the current proof lanes honest and useful.

Required Track B tasks:

1. Inspect each `Build*Telemetry(...)` branch in `WorldSim.ScenarioRunner/Program.cs` and identify whether the failure is caused by insufficient deterministic setup, insufficient probe duration/scale, missing existing counter export, or a real runtime behavior gap.
2. Keep `run.wave10` as `main_world_run` default-safe truth. Do not move side-probe counters back into normal run proof.
3. Improve deterministic probe setup only where it uses existing runtime behavior. Allowed examples: existing public runtime commands, existing tech unlock helper, existing manual campaign command, existing config/tick/population/size choices, and existing telemetry counters.
4. For the four fix-now core lanes, target positive evidence: `organic_campaign_launch`, `campaign_siege_resolution`, `supply_line_convoy`, and `multi_front_bounded`.
5. For the three conditional lanes, attempt evidence-only positive proof; if it cannot be achieved without gameplay tuning, keep `proof_unavailable` but upgrade the reason/non-claims into a deliberate Step10C/future classification instead of generic `lane_not_configured`.
6. Add or update focused tests in `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs` so the lane classification cannot silently regress. Tests should assert positive counters for fixed lanes and explicit reason/non-claims for deferred lanes.
7. Produce a small validation artifact after the fix with the same 3 seeds x 3 planners x 8 configs shape, or a focused equivalent plus justification if runtime is too heavy.

Track B must not:

- tune combat/campaign balance just to make a probe pass,
- claim manual/operator launch as organic proof,
- claim deterministic side probes as same-run/tick-sampled proof,
- edit Track A rendering or Track C strategist behavior unless a separate handoff is accepted.

## Conditional Track A handoff

Open Track A only if Track B or manual smoke proves a visual/manual consume gap after runtime evidence is classified.

Candidate Step10C-A scope:

- siege-unit visibility/readability if `siege_unit_breach` has runtime evidence but remains hard to observe manually,
- wall/watchtower/fortification icon scale/readability from manual smoke,
- campaign/logistics panel clipping/readability if it blocks operator verification.

Track A must not be asked to prove logistics correctness or organic runtime behavior.

## Conditional Track C handoff

Open Track C only if Track B proves an unavailable lane is caused by strategist/AI decision behavior rather than ScenarioRunner setup or Track B runtime evidence surface.

Candidate Step10C-C scope:

- scout-intel campaign-choice consume if existing intel is present but strategist ignores it,
- siege-unit protection/deployment behavior if runtime units exist but AI never produces the relevant support intent,
- organic campaign decision arbitration if runtime preconditions are present but strategist never emits a launchable intent.

Track C must not be opened just because a deterministic probe is missing setup.

## Acceptance gate before Step10B

Before clean Step10B final evidence starts, Meta review should require:

- `Wave10CampaignEvidenceTests` pass.
- Focused ScenarioRunner artifact tests pass, including old-baseline compatibility if touched.
- Full solution build pass.
- Updated validation artifact or focused evidence package exists and is referenced in handoff.
- The four core lanes are positive or have an explicit user/Meta-approved reason why Step10B is allowed to proceed YELLOW.
- Every remaining unavailable lane has a specific route: `Step10C-B`, `Step10C-A`, `Step10C-C`, or future-wave accepted limitation.
- `ops/PROJECT_STATE.md` points to the next exact role/action.

## Recommended next action

Send the Track B handoff from this plan. After Track B returns, run a Meta mini-review focused on:

- whether core lanes are now positive,
- whether any remaining `proof_unavailable` lane is deliberately routed rather than accidental,
- whether the evidence still preserves provenance boundaries,
- whether Step10B can start or Step10C must open first.
