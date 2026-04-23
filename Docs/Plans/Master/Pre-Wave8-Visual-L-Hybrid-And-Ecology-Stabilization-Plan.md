# Pre-Wave8 Visual-L Hybrid And Ecology Stabilization Plan

Status: coordinator-approved pre-Wave8 addendum
Owner: Meta Coordinator
Last updated: 2026-04-22

## Purpose

Lock two explicit pre-Wave8 slices before Supply & Inventory begins:

1. `PW8-A1` - a larger Track A visual readability/overhaul pass on top of the Wave 7.5 low-cost baseline.
2. `PW8-B1` + `PW8-B2` - Track B ecology observability first, then current-model stabilization with SMR evidence.

This addendum exists because both slices are materially useful before Wave 8, but neither is currently represented as a Wave 8 prerequisite in the Combined plan.

## Source Of Truth

- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`
- `Docs/Plans/Master/Balance-Loop-Specification.md`
- `Docs/Plans/Master/balance-surface.md`

## Why Before Wave 8

- The current renderer still has visible readability gaps: missing animal/food visuals, weak category proportions, and under-developed terrain/material grounding.
- The current plant/herbivore/predator loop is not stable enough for later supply/campaign assumptions, and ScenarioRunner does not yet expose enough ecology evidence for disciplined tuning.
- Wave 8 is inventory-first. It should not start while these two known broad preconditions remain implicit.

## Slice Summary

### PW8-A1 - Visual-L Hybrid

Primary owner: Track A

Intent:
- Deliver a bigger-than-LC1 visual readability pass while preserving the low-cost, snapshot-driven baseline.
- Use a hybrid strategy: a small number of new manual art assets where they materially help, plus broader non-icon rendering improvements.

In scope:
- Missing visuals for the most important currently placeholder-like entities:
  - herbivore
  - predator
  - food
- Additive content/catalog plumbing for optional manual art intake.
- Per-category render sizing/anchoring cleanup for:
  - people
  - animals
  - resources
  - houses
  - specialized buildings
  - defensive structures
- Broader terrain/material/art pass, still low-cost and deterministic:
  - stronger grass/dirt readability
  - better water/shore readability
  - stronger colony/structure grounding and footprint cues
  - stronger silhouette separation between actors/resources/structures and terrain
  - theme/palette rebalance where useful
- Specialized building and defense readability may improve through icons, silhouettes, footprints, outlines, shadows, or palette separation.

Explicitly out of scope:
- campaign panel completion
- Wave 9/10 campaign overlays or campaign snapshot consume
- heavy shader/material rewrite as required baseline
- particle/weather system becoming mandatory baseline behavior
- renderer-side gameplay-state recomputation outside existing snapshot boundaries

Manual asset intake policy:
- The user provides any new art assets manually.
- Track A should make the asset seams ready even if final art is delivered in batches.
- Preferred first-pass asset slots:
  - `predator.png`
  - `herbivore.png`
  - `food.png`
  - optional: `farmplot.png`, `workshop.png`, `storehouse.png`
- If an optional asset is still missing, fallback rendering must stay readable rather than silently regressing to ambiguous full-tile blocks.

Recommended implementation shape:
- keep runtime/read-model unchanged unless a very small additive render field is proven necessary
- prefer `TextureCatalog` + render-pass + theme/settings changes first
- split shared render-scale seams into category-safe knobs instead of one broad `IconScale`

### PW8-B1 - Ecology Observability

Primary owner: Track B
Evidence owner: SMR Analyst

Intent:
- Make the existing ecology loop measurable before tuning it.
- Reuse existing runtime-owned truth where possible and extend only the smallest missing counters needed for balancing.

In scope:
- Export existing ecology truth into ScenarioRunner run-level and timeline/drilldown artifacts.
- Reuse or expose existing runtime/snapshot state for:
  - herbivore count
  - predator count
  - active food nodes
  - depleted food nodes
  - predator deaths
  - predator-human hits
- Add small additive runtime-owned counters only where they are needed for balancing evidence, for example:
  - herbivore replenishment spawns
  - predator replenishment spawns
  - ticks with zero herbivores
  - ticks with zero predators
  - first extinction tick per species, if affordable and deterministic
- Keep the slice observability-only: no behavior retuning yet.

Explicitly out of scope:
- closed-loop redesign
- AI/planner redesign
- broad anomaly/assert policy rewrite on day one
- supply/campaign coupling

Deliverable:
- a first ecology evidence lane that SMR Analyst can use without manual code inspection.

### PW8-B2 - Ecology Stabilization

Primary owner: Track B
Evidence owner: SMR Analyst

Intent:
- Stabilize the current ecology model using measured evidence.
- This is not the later full closed-loop redesign.

Stage policy:
- Pre-Wave8 target is the current-model stabilization path.
- Post-Wave10.5 remains the intended home for a deeper reusable closed-loop framework that can later apply to ecology and other simulation systems.

In scope:
- Tune the current model after `PW8-B1` observability lands.
- Prefer a staged tuning surface:

Safe-first candidates:
- initial herbivore / predator multipliers
- herbivore replenishment floor
- predator replenishment floor
- predator replenishment chance
- food regrowth min/max time
- food respawn amount min/max

Guarded second-wave candidates, only if evidence says the safe-first batch is insufficient:
- predator speed
- predator vision
- predator capture success
- predator energy drain
- predator energy gain on capture

Explicitly out of scope:
- true plant spread / propagation system
- herbivore hunger-energy-starvation lifecycle redesign
- herbivore reproduction model
- predator reproduction model
- generalized ecology carrying-capacity redesign

Success criteria:
- the loop no longer collapses trivially under the agreed baseline matrix
- tuning remains evidence-backed, not screenshot-driven
- the resulting model is good enough to coexist with upcoming Wave 8 inventory work

## Execution Order

### Step 1 - parallel kickoff after Wave 7.5

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track A agent | PW8-A1 | Wave 7.5 ✅ | Visual-L Hybrid pass may start immediately on top of the low-cost baseline |
| Track B agent | PW8-B1 | Wave 7.5 ✅ | Observability must land before ecology tuning starts |

### Step 2 - ecology baseline evidence

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | PW8-B1 evidence | PW8-B1 ✅ | Run baseline and drilldown package; confirm artifacts are sufficient for tuning |

Evidence closeout:
- `Docs/Evidence/SMR/pre-wave8-pw8-b1-ecology/README.md`
- Decision: `PW8-B1 evidence sufficient for PW8-B2`.

### Step 3 - ecology stabilization

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| Track B agent | PW8-B2 | PW8-B1 ✅ + PW8-B1 evidence ✅ | Do not tune blind; use the newly exported ecology evidence |

### Step 4 - stabilization verification

| Session | Epic(s) | Prereq | Notes |
|---------|---------|--------|-------|
| SMR Analyst | PW8-B2 evidence | PW8-B2 ✅ | Validate the tuned model across the agreed matrix; compare against the captured pre-tuning baseline |

## Role Split

### Track A

- implement the Visual-L Hybrid slice
- prepare asset intake seams for user-provided art
- keep the pass additive and low-cost compatible
- own app/manual smoke for visual readability

### Track B

- expose ecology observability in runtime + ScenarioRunner artifacts
- add the smallest necessary runtime counters for ecology evidence
- perform the stabilization tuning on the current model
- keep the tuning seam deterministic and runner-friendly

### SMR Analyst

- run ScenarioRunner/SMR packages after `PW8-B1` and `PW8-B2`
- produce baseline/compare/drilldown evidence
- identify which knobs materially help versus which regress other lanes
- provide evidence-backed tuning feedback rather than authoring the broad runtime changes directly

## Suggested SMR Workflow

Baseline package after `PW8-B1`:
- one cheap default lane
- one medium/stress lane
- multiple seeds
- at least `simple`, `goap`, `htn` where runtime behavior differences matter

Tuning loop during `PW8-B2`:
1. Track B changes a small ecology batch.
2. SMR Analyst runs compare against the last accepted baseline.
3. Inspect `summary.json`, `compare.json`, and drilldown timelines.
4. Promote only changes that improve ecology stability without obvious survival/combat regressions.

Important:
- the recently requested `2x` initial predator default is valid as a stress lane input, but should not be treated as the only ecology baseline if it makes prey-stability conclusions misleading.
- ScenarioRunner config may also explicitly toggle `EnablePredatorHumanAttacks` for dedicated predator-pressure evidence lanes; keep it deliberate and lane-specific rather than silently broadening the default baseline matrix.

## Proof Targets

### PW8-A1

- Manual app smoke in `DevLite` and `Showcase`.
- Missing animal/food visuals are no longer ambiguous blocks.
- Category proportions read more naturally at common zoom levels.
- Terrain/material pass materially improves readability without violating the cheap baseline.

### PW8-B1

- ScenarioRunner artifacts include the agreed ecology fields.
- Drilldown/timeline outputs are sufficient to identify collapse timing and species disappearance.
- Evidence can be reviewed without opening runtime code.

### PW8-B2

- Baseline matrix no longer shows trivial ecology collapse as the default expected outcome.
- A compare-based evidence note exists for the retained tuning state.
- No obvious regression is introduced in broader survival/combat headline metrics.

## Out-Of-Scope Future Note

After Wave 10.5, a broader reusable closed-loop design initiative is still desirable.
That later effort may generalize beyond ecology into other simulation aspects, but this addendum does not pre-implement that future system.
