# Wave 11 - Closed-Loop Ecology Redesign Plan

Status: planned
Owner: Meta Coordinator
Last updated: 2026-04-26

## Purpose

Wave 11 replaces the Pre-Wave8 current-model ecology stabilization with an emergent ecology loop.

The goal is not to keep animals alive by normal respawn. The goal is for plants, herbivores, and predators to remain viable through explicit lifecycle state, resource availability, reproduction, starvation, hunting pressure, and map carrying capacity.

Pre-Wave8 `PW8-B2` remains a safety baseline only: it reduced trivial predator collapse through tuned replenishment and predator rescue. Wave 11 is the deeper model that should make that rescue path unnecessary in normal runs.

## Source Inputs

- `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
- `Docs/Plans/Master/Pre-Wave8-Visual-L-Hybrid-And-Ecology-Stabilization-Plan.md`
- `Docs/Evidence/SMR/pre-wave8-pw8-b1-ecology/README.md`
- `Docs/Evidence/SMR/pre-wave8-pw8-b2-ecology/README.md`
- `WorldSim.Runtime/Docs/Plans/TODO.md`

## Coordinator Decisions Locked By Grill-Me

- Scope depth: emergent sim, not a light spawn upgrade.
- Normal respawn policy: debug-only emergency fallback, not the primary ecology driver.
- Animal model: individual animal energy/lifecycle state.
- Wave placement: after Wave 10.5.
- Acceptance: SMR hard invariants plus visual/manual proof.
- Supply link: staged plant/meat split.
- Domestication/farming: out of Wave 11; wild ecology first.
- Predator-human interaction: included in the Wave 11 baseline lanes, with bounded safety and evidence gates.
- Map ecology source of truth: tile fertility plus region-level carrying capacity aggregates.

## Non-Goals

- Domestication, animal pens, milk, eggs, or passive livestock income.
- Full food taxonomy beyond the first plant/meat split.
- Replacing inventory/supply systems from Waves 8-10.
- Paid-live Refinery or director behavior as part of the ecology gate.
- Renderer-side gameplay/ecology computation.
- Making emergency rescue the normal balancing mechanism.

## Target Runtime Model

### Map Ecology State

Track B owns the runtime truth.

Required concepts:
- Tile fertility, clamped and deterministic.
- Optional moisture/biome class if the existing map state is not enough to explain growth variance.
- Region aggregates for carrying capacity, pressure, and available plant food.
- Seasonal/drought modifiers applied to growth and recovery through runtime-owned state.

Design intent:
- Tiles explain local food availability and visual cues.
- Regions explain population viability and keep per-tick computation bounded.
- Graphics consumes snapshot/read-model fields only.

### Plant/Food Loop

The plant loop should stop being only timer-based node replacement.

Required behavior:
- Plant biomass or plant-food availability recovers from fertility, season, drought, and grazing pressure.
- Overgrazed areas recover more slowly.
- Food nodes are a presentation/gameplay surface over runtime plant availability, not the sole ecology source of truth.
- Regrowth is deterministic for the same seed and scenario config.

### Herbivore Lifecycle

Each visible herbivore should carry individual state:
- Energy/hunger.
- Age or maturity band.
- Reproduction cooldown/eligibility.
- Starvation risk.
- Local grazing target or migration pressure.

Required behavior:
- Herbivores seek plant food based on local availability.
- Herbivores lose energy over time and gain energy from grazing.
- Reproduction requires adequate energy and local carrying capacity headroom.
- Starvation happens if energy remains too low.
- Reproduction, starvation, and grazing counters are exported to SMR.

### Predator Lifecycle

Each visible predator should carry individual state:
- Energy/hunger.
- Age or maturity band.
- Reproduction cooldown/eligibility.
- Starvation risk.
- Current hunting target or migration pressure.

Required behavior:
- Predators hunt herbivores using bounded speed/vision/capture rules.
- Successful capture gives energy and creates meat yield where applicable.
- Reproduction requires sustained energy and prey availability.
- Starvation happens if hunts fail for too long.
- Predator population is limited by prey availability and region capacity rather than hard respawn floors.

### Predator-Human Baseline

Wave 11 baseline lanes include predator-human interaction ON.

Required policy:
- Predator-human attacks must be bounded so ecology evidence does not become random colony wipe evidence.
- Human retaliation/defense counters must be visible in SMR.
- Predator-human pressure should be one ecology stress factor, not the only proof of predator viability.

### Emergency Rescue Policy

Emergency rescue may remain only as a debug/safety fallback.

Required policy:
- Disabled or inactive in normal acceptance unless explicitly configured.
- If activated, it must emit counters and timeline markers.
- SMR must distinguish lifecycle births from emergency rescues.
- A Wave 11 acceptance run should not depend on emergency rescue to pass.

## Supply Bridge

Wave 11 introduces a staged food split:
- Plant food: from plant biomass/forage/farming-adjacent future hooks.
- Meat: from hunted animals and predator/prey deaths where appropriate.

Initial policy:
- Keep integration narrow enough not to redesign all inventory at once.
- Export plant/meat production and consumption counters to SMR.
- Keep domestication and animal products for a later wave.

## SMR Acceptance Gates

Wave 11 should add hard ecology invariants for dedicated ecology lanes.

Required invariant families:
- `ECO-SPECIES`: no permanent predator/herbivore zero windows in baseline lanes.
- `ECO-PLANT`: food/plant depletion ratio stays within configured limits.
- `ECO-OSC`: population oscillation remains bounded over long runs.
- `ECO-RESCUE`: emergency rescue count is zero in normal acceptance lanes.
- `ECO-SUPPLY`: plant/meat outputs remain positive and plausible when Wave 8+ supply hooks are enabled.
- `ECO-HUMAN`: predator-human ON lanes do not cause immediate colony wipe or unbounded death spikes.

Suggested evidence matrix:
- Seeds: at least `101,202,303`.
- Planners: `simple,goap,htn` unless an explicit AI-independent ecology lane is introduced.
- Lanes: default, medium-stress, drought-stress, predator-human ON baseline, long-run oscillation.
- Minimum closeout: current accepted baseline plus compare against Pre-Wave8 evidence where comparable.

## Visual And Debug Proof

Track A consumes runtime/snapshot fields only.

Required proof surfaces:
- Ecology debug overlay showing plant pressure, region capacity, herbivore pressure, predator pressure.
- Optional tile tint for fertility/overgrazing in debug mode.
- Animal state debug markers for starving, hunting, reproducing, migrating, and emergency rescue.
- HUD/SMR-aligned counters for population, births, starvation deaths, predator kills, and rescue activations.

Manual app smoke should verify that the world looks alive because behavior is working, not because invisible respawn keeps counts nonzero.

## Architecture Boundaries

- `WorldSim.Runtime` owns lifecycle state, plant growth, carrying capacity, counters, and scenario snapshots.
- `WorldSim.AI` owns decision policy for animals/NPC reactions only behind interfaces where practical.
- `WorldSim.ScenarioRunner` owns artifact export, invariant evaluation, and matrix execution.
- `WorldSim.Graphics` owns visualization of exported read models only.
- `WorldSim.App` owns controls/smoke access only.

Forbidden:
- `Graphics` computing gameplay fertility/population logic.
- ScenarioRunner mutating runtime internals outside documented config knobs.
- Emergency rescue being counted as normal reproduction.

## Wave 11 Epic Outline

### E11-A - Ecology State Contract (Track B)

Define runtime and snapshot contracts for tile fertility, region carrying capacity, plant biomass, animal lifecycle fields, and ecology counters.

### E11-B - Plant Growth + Region Capacity (Track B)

Implement deterministic plant biomass recovery, overgrazing pressure, season/drought modifiers, and region-level capacity caches.

### E11-C - Herbivore Lifecycle (Track B)

Add herbivore energy, grazing, starvation, reproduction, and migration pressure without relying on normal respawn.

### E11-D - Predator Lifecycle (Track B)

Add predator energy, hunting, capture energy gain, starvation, reproduction, and prey-linked carrying capacity.

### E11-E - Emergency Rescue Demotion (Track B)

Move existing replenishment/rescue behavior behind explicit debug/safety policy and export rescue counters separately from births.

### E11-F - Animal/AI Behavior Alignment (Track C)

Wire animal and NPC behaviors to the new ecology context, including predator-human ON baseline policy and bounded retaliation/safety behavior.

### E11-G - Plant/Meat Supply Bridge (Track B)

Introduce staged plant/meat production counters and minimal supply hooks without full domestication or food taxonomy expansion.

### E11-H - SMR Ecology Invariant Pack (SMR Analyst / Track B)

Add hard ecology invariant families, scenario configs, compare policy, and drilldown selection that is ecology-aware.

### E11-I - Ecology Snapshot + Debug Overlay (Track A after Track B)

Render ecology pressure, fertility/overgrazing, animal lifecycle markers, and HUD counters from snapshot fields.

### E11-J - Evidence Closeout + Baseline Decision (SMR Analyst + Meta Coordinator)

Run the full ecology matrix, review evidence, decide whether to promote a new baseline, and close Wave 11 only if normal runs do not depend on emergency rescue.

## Execution Principles

- Build the model in small runtime slices with focused tests before broad visual work.
- Add SMR counters before tuning, then promote only evidence-backed behavior.
- Keep default lanes deterministic and reproducible.
- Prefer explicit state and counters over hidden balancing magic.
- Treat predator-human ON as a required Wave 11 stress surface, not as a substitute for animal ecology proof.

## Definition Of Done

- Plant/herbivore/predator loop remains viable across the accepted multi-seed matrix without normal emergency rescue.
- Herbivore and predator populations are sustained by lifecycle reproduction and food/prey availability.
- Plant/food availability responds to fertility, season/drought, grazing, and recovery.
- Predator-human ON baseline is bounded and observable.
- SMR hard ecology invariants pass.
- Debug overlay and manual app smoke make the ecology state understandable.
- Remaining balancing caveats are documented with evidence, not hidden behind respawn.
