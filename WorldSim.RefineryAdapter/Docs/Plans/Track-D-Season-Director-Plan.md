# Track D - Season Director Plan

Status: superseded by strategic master plan (remains valid as tactical sprint reference)
Superseded by: `Docs/Plans/Director-Integration-Master-Plan.md`
Owner: Track D (Refinery boundary)

> **Note:** This document is the original tactical sprint plan (3 sprints).
> The strategic master plan (`Director-Integration-Master-Plan.md`, 2300+ lines, 5 phases, 7+ sprints)
> supersedes this document for architectural decisions, vocabulary design, and phasing.
> Sprint 1 epics here are largely aligned with Phase 0 of the master plan.
> When discrepancies exist, the master plan takes precedence.

## 1. Goal

Build a checkpoint-driven Director layer for WorldSim that produces two outcomes from the same world snapshot:

- `story beat`: narrative world-level event with optional light gameplay impact
- `planner nudge`: colony-level policy direction (not micro-management)

Director must run rarely (season checkpoint or configured tick interval), preserve emergence, and remain safe:

- LLM can be creative but never authoritative
- Refinery must be the formal gate
- Runtime applies only validated contracts through the adapter anti-corruption layer

## 2. Core behavior and toggles

Director outputs must be independently toggleable.

Output mode matrix:

- `both`: story beat + planner nudge
- `story_only`: only story beat
- `nudge_only`: only planner nudge
- `off`: none (director request can still run for telemetry/dry-run)

Planned control points:

- Java service config (`planner.director.outputMode`)
- C# adapter/runtime config override for test harness
- optional per-request override in constraints payload (future)

## 3. Architecture (target)

### 3.1 Responsibility split

- `WorldSim.App`: trigger and UI only
- `WorldSim.Runtime`: domain command endpoints + durable state + timed effects
- `WorldSim.Contracts`: versioned contract source of truth
- `WorldSim.RefineryClient`: HTTP + parser/applier helpers
- `WorldSim.RefineryAdapter`: anti-corruption translation, deterministic errors
- `refinery-service-java`: candidate generation, LLM (optional), Refinery validation/repair loop

### 3.2 Pipeline

1) Runtime builds checkpoint snapshot.
2) Adapter sends request to Java service.
3) Planner pipeline builds candidate output.
4) Refinery validates/repairs candidate.
5) Service returns contract patch.
6) Adapter translates patch ops to runtime commands.
7) Runtime applies commands and updates state.
8) Graphics shows beat/nudge state and event feed.

### 3.3 OnlabRefinery-aligned principles

- Layering: Design/Model/Runtime separation for director formal model.
- Iterative loop: invalid candidate -> feedback -> retry.
- Deterministic fallback after bounded retries.
- Strict output contract + op-level validation.

## 4. Sprint plan (3 sprints)

## Sprint 1 - End-to-end deterministic Director slice (no LLM)

Objective:
- Ship a working vertical slice with deterministic `story beat + planner nudge` at checkpoint, including independent output toggles.

### Epic S1-A: Contracts and API expansion

Tasks:
- Add Director contract set in `WorldSim.Contracts` (new version namespace, likely `v2`).
- Extend goal set with `SEASON_DIRECTOR_CHECKPOINT`.
- Add ops:
  - `addStoryBeat`
  - `setColonyDirective`
- Add output mode enum/string in contract.
- Keep `opId` mandatory.
- Preserve v1 compatibility.

Acceptance:
- Contract tests pass.
- Backward compatibility preserved for current v1 patch flow.

### Epic S1-B: Runtime Director state and commands

Tasks:
- Add runtime command endpoints:
  - `ApplyStoryBeat(...)`
  - `ApplyColonyDirective(...)`
- Add world/colony director state:
  - active beat list with ttl/expiry tick
  - active directive per colony with expiry tick
- Add timed effect scheduler in runtime tick update.
- Keep effects light (safe defaults):
  - story beat: event feed + small temporary multiplier
  - nudge: profession rebalance target bias

Acceptance:
- Director effects apply and expire deterministically.

### Epic S1-C: Adapter anti-corruption translation

Tasks:
- Add translation path:
  - `addStoryBeat -> AddStoryBeatCommand`
  - `setColonyDirective -> SetColonyDirectiveCommand`
- Keep unsupported ops deterministic fail.
- Keep unknown colony/unknown directive deterministic fail.

Acceptance:
- Adapter tests prove deterministic error handling.

### Epic S1-D: Java deterministic mock director planner

Tasks:
- Add mock branch for `SEASON_DIRECTOR_CHECKPOINT`.
- Implement output mode gating (`both/story_only/nudge_only/off`).
- Add explain markers:
  - `directorStage:mock`
  - `directorOutputMode:<...>`

Acceptance:
- Same seed+tick+snapshot -> same output.
- Output mode matrix works.

### Epic S1-E: UI and observability

Tasks:
- Include story beat and directive summary in read model.
- Render in HUD/event feed.
- Add status line fields:
  - stage marker
  - output mode marker
  - first warning

Acceptance:
- Manual smoke clearly shows if beat/nudge were active.

## Sprint 2 - Formal Refinery gate for Director slice

Objective:
- Replace pure heuristic acceptance with a true Refinery validation/repair stage for director candidates.

### Epic S2-A: Director formal model layers

Tasks:
- Add Director formal model layers in Java:
  - Design: entities and relation skeleton
  - Model: constraints and invariants
  - Runtime: checkpoint context facts
- Add snapshot -> formal facts mapper.

Acceptance:
- Director candidate can be encoded/validated against formal model.

### Epic S2-B: Candidate -> validate/repair loop

Tasks:
- Build candidate from deterministic planner.
- Validate with Refinery.
- On invalid:
  - generate feedback hint
  - bounded retry
  - deterministic fallback if retries exhausted
- Add explain markers:
  - `directorStage:refinery-validated`
  - fallback warning when used

Acceptance:
- Invalid candidates never pass through unchecked.

### Epic S2-C: First invariant pack

Tasks:
- Enforce at least:
  - one major beat at a time
  - beat cooldown
  - valid directive type and intensity range
  - no contradictory same-colony directive in one checkpoint

Acceptance:
- Unit tests cover each invariant violation and resulting behavior.

### Epic S2-D: Runtime hardening

Tasks:
- Command dedupe guarantees by `opId` remain intact.
- Add counters/telemetry:
  - director requests
  - validated outputs
  - fallback count
  - rejected command count

Acceptance:
- Diagnostics are sufficient to debug parity/failures.

## Sprint 3 - LLM creativity under strict formal control

Objective:
- Add optional LLM proposal stage while keeping formal safety guarantees and deterministic fallback.

### Epic S3-A: LLM Director proposal stage (flagged)

Tasks:
- Add `LlmDirectorPlanner` behind feature flag.
- Prompt policy:
  - Design/Runtime read-only
  - output only candidate payload fields
  - strict JSON schema
- Parse and sanitize candidate output.

Acceptance:
- LLM stage can be enabled/disabled without breaking pipeline.

### Epic S3-B: LLM + Refinery iterative correction

Tasks:
- If LLM candidate invalid:
  - feed back concise formal errors
  - retry bounded times
- Keep deterministic fallback after max retries.

Acceptance:
- Hallucinations are bounded and blocked by Refinery gate.

### Epic S3-C: Validation strategy shift for parity

Tasks:
- Keep hash parity for deterministic mock path.
- Add invariant parity for LLM path:
  - contract-valid
  - refinery-valid
  - no forbidden operations

Acceptance:
- CI has separate checks for deterministic path and LLM path.

### Epic S3-D: Operational UX simplification

Tasks:
- Reduce env overload by adding local profiles and/or in-game debug toggles.
- Keep env variables for CI/scripted runs.

Acceptance:
- Developers can run common modes from profile presets.

## 5. Cross-track dependencies

Track B (Runtime):
- director command endpoints
- timed effect state + persistence hooks
- checkpoint scheduler hooks

Track A (Graphics/UI):
- beat/nudge visibility in HUD/event feed
- debug overlays for stage/mode markers

Track C (AI):
- colony directive bias integration in profession/decision layers
- preserve emergent behavior by soft weighting only

## 6. Definition of Done for Season Director program

- Checkpoint-only operation (not per-frame)
- Output mode matrix works (`both/story_only/nudge_only/off`)
- Formal gate mandatory when director pipeline active
- Deterministic fallback always available
- Fixture and live parity for deterministic mode
- Deterministic error handling for unknown/invalid ops
- HUD can verify stage/mode/beat/nudge status

## 7. Risks and mitigations

- Risk: LLM non-determinism reduces reproducibility
  - Mitigation: bounded retries + deterministic fallback + invariant parity
- Risk: Director over-controls simulation and kills emergence
  - Mitigation: low-frequency checkpoints + soft nudges + budget/cooldown
- Risk: Cross-track drift
  - Mitigation: shared noticeboard entries and explicit command contracts

## 8. Open decisions (pre-implementation)

- Contract version for Director (`v2` recommended)
- Exact directive taxonomy for first slice
- Checkpoint cadence defaults (season-only vs season+tick)
- Persistence format for director state (planned with runtime save/load)
