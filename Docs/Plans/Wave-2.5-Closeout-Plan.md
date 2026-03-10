# Wave 2.5 Closeout Plan

Status: Active
Owner: Meta Coordinator
Last updated: 2026-03-04

This wave exists to close gaps discovered after Wave 2 was marked ✅ in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`.
It is intentionally scoped to **integration correctness + determinism + observability**, not new feature expansion.

References:
- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/Director-Integration-Master-Plan.md` (Phase 1 / Sprint 2 acceptance + DoD)
- `Docs/Plans/Master/Combat-Defense-Campaign-Master-Plan.md` (Phase 1 / Sprint 2 DoD)

---

## Goal

- Make Director beats/directives produce **real gameplay effects** by wiring v2 `effects`/`biases` through Adapter -> Runtime -> Engines.
- Ensure Diplomacy/Territory work is **activatable in the app** (without test-only flags), and is observable via UI.
- Remove plan drift: align master plan contract/wire examples with the actual v2 contracts (or explicitly mark future fields).

---

## Sequenced Execution (Coordinator View)

### Block 1 (Track D + Track B, sequential inside the block)

1) Adapter translation carries v2 payload
- Extend runtime commands so they can carry:
  - Story beat domain modifier effects (from `AddStoryBeatOp.effects`)
  - Colony directive goal biases (from `SetColonyDirectiveOp.biases`)
- Translator produces deterministic errors for:
  - Unknown `EffectEntry.type` / `GoalBiasEntry.type`
  - Unknown domains / goal categories
  - Duration mismatch between op duration and entry duration (Wave 2.5 policy)

2) Runtime endpoints apply effects/biases
- `SimulationRuntime.ApplyStoryBeat(...)` registers domain modifiers in `DomainModifierEngine`.
- `SimulationRuntime.ApplyColonyDirective(...)` registers goal biases in `GoalBiasEngine`.
- Both paths use a global dampening factor (0..1) applied at registration time.

Concrete env toggle:
- `REFINERY_DIRECTOR_DAMPENING=0.0..1.0` (0.0 = narrative-only; 1.0 = full effect)

3) Minimal observability
- Applying a beat/directive adds a `[Director] ...` entry to the world event feed so the existing UI shows it.
- Director snapshot (refinery snapshot JSON) includes enough fields to confirm:
  - active beats/directives timers
  - active modifiers/biases (for debugging and parity tests)

Acceptance (Block 1):
- Director story beat with `effects` changes domain multipliers and the effect decays/expires deterministically.
- Director directive with `biases` changes NPC behavior measurably (goal scoring + profession rebalance already consumes priority thresholds).
- Setting dampening to 0.0 yields narrative-only (no gameplay delta).

### Block 2 (Track B, sequential)

- Add an app-level activation path for diplomacy (and optionally combat primitives) so Sprint 2 DoD is not “tests-only”.
  - Recommended: environment toggle(s) read by runtime on startup (safe default OFF).

Concrete env toggles (current implementation proposal):
- `WORLDSIM_ENABLE_DIPLOMACY=true|false`
- `WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true|false`
- `WORLDSIM_ENABLE_PREDATOR_ATTACKS=true|false`
- If diplomacy is enabled, make sure stances can change in a normal run and are visible in the Diplomacy panel.

Acceptance (Block 2):
- A developer can enable diplomacy without code edits and observe stance changes + territory overlay.

### Block 3 (Parallel: Track A + Track C + Track D)

Track A:
- Fix UI legend/keybind drift (documentation + minor UI text as needed).

Track C:
- Verify goal bias categories used in AI goals match the director vocabulary list.

Track D:
- Update fixture examples (if any) so at least one director response includes non-empty `effects`/`biases`.

Acceptance (Block 3):
- “Director” output is visible and debuggable without reading logs.

### Block 4 (Meta docs, sequential)

- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`: add Wave 2.5 section referencing this doc.
- `Docs/Plans/Master/Director-Integration-Master-Plan.md`: resolve contract schema drift (either align to current v2 types or mark future fields explicitly).
- `Docs/Plans/Master/Combat-Defense-Campaign-Master-Plan.md`: note that keybinds are suggested and may be overridden by implementation.

Acceptance (Block 4):
- Plans are self-consistent: Combined plan references the closeout wave; master plan examples match the actual contract shape.
