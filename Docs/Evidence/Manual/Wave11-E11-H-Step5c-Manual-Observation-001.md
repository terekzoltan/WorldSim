# Wave 11 E11-H Step 5c Manual Observation 001

Date: 2026-07-10
Observer: User / Manual QA
Purpose: qualitative initial-topology and early-contact evidence for Step 5c planning
Acceptance authority: none; this packet cannot make E11-H GREEN

## Runtime Context

- App host manual runtime, not ScenarioRunner focused-gate parity.
- Default app world: `128x128`, initial human population `25`, no fixed app seed.
- Predator-human attacks enabled.
- Combat primitives enabled.
- HTN run observed in detail.
- GOAP run used as a low-effort qualitative control and appeared broadly similar.
- Main observation used approximately `3x` simulation speed.

## Observations

### Early Predator-Human Contact

- Contact occurred relatively early, within about 10 wall-clock seconds at `3x` speed, approximately 30 simulation seconds.
- Predator appeared or moved close to people early.
- The predator survived the observed contact.
- The predator killed two people and then moved away.
- This manual run did not reproduce the seed `202` pattern where human kills align with predator extinction.

### Predator-Prey Interaction

- Many predators and herbivores were visible.
- Predators visibly pursued herbivores.
- Successful hunts were observed.
- No obvious general predator extinction was observed during the manual window.
- This weakens a universal "predators cannot find or capture prey" hypothesis, but does not disprove seed/config-specific failures.

### Herbivore And Food Distribution

- Herbivore population appeared stable for part of the run.
- Local herbivore clusters formed, including around or near settlements.
- Food nodes depleted locally around clustered animals while food remained available elsewhere on the map.
- Some herbivores appeared clustered/stuck while others remained dispersed.
- The observer suggested a possible settlement-safety effect, but current runtime code has no explicit herbivore policy that seeks civilization or colony defense.
- Plausible mechanisms remain predator-driven fleeing, map/water corridors, local food attraction, random initial concentration, or insufficient migration out of depleted regions.

### Pathing And Clustering

- Clustering was visible but did not look globally broken in this run.
- Water and settlement topology may constrain local movement corridors.
- No manual claim is made that pathing is correct across seeds or long runs.

## Screenshot Evidence

Four chat-provided screenshots were reviewed qualitatively:

- distributed predators and herbivores with abundant global food;
- early predator/person proximity;
- local herbivore concentrations near settlements and water/topology boundaries;
- at least one predator visibly located on a water tile.

The screenshots are conversation attachments and do not currently have checked-in binary paths.

## Code-Reality Correlation

Code inspection after the manual run found:

- initial animals use `Animal.Spawn(RandomFreePos(), ...)`;
- `RandomFreePos()` currently returns an arbitrary map coordinate and does not validate land, movement safety, food, prey, colony distance, or regional capacity;
- initial species are independently selected with an approximate 70% herbivore / 30% predator random draw;
- herbivores do not explicitly seek settlements or human protection;
- predators prefer herbivore prey in vision and only use the nearby-person harass branch when prey is not available in vision.

The water-tile predator screenshot is therefore consistent with a real initial-placement defect, not only a rendering interpretation.

## UI And Manual-Diagnostic Gaps

- Plain `F8` AI debug and plain `F3` render-stats visibility were reported as not working.
- Current app wiring renders these inside the telemetry HUD path, so the observer should retry with `T` enabled first.
- If `T` then `F8`/`F3` still fails, route a concrete App/Graphics usability finding.
- A click-to-inspect actor/tile information panel would materially improve manual ecology diagnosis.
- Desired fields include actor kind/id, health, energy, age/maturity, starvation pressure, reproduction cooldown, behavior, ecology region, nearest food/prey distance, and local capacity/biomass.
- Inspection UI belongs to E11-I or a separately reviewed Track A/App diagnostic seam; it is not Step 5c1 Runtime/SMR scope.

## Diagnostic Classification

Supported:

- initial topology and early predator-human distance need explicit telemetry;
- local food accessibility and regional density matter more than global food count alone;
- random initialization can place animals in invalid or ecologically poor locations;
- general predator hunting/capture behavior can work in at least one manual world.

Not proven:

- seed `101` or `202` root cause;
- long-run species viability;
- planner parity;
- correctness of pathing or migration;
- E11-H acceptance.

## Step 5c Routing

- Step 5c1 must export initial animals on water/invalid tiles, species by region, food/prey access, human proximity, and first event ticks.
- Step 5c2 must make land safety a hard initial-seeding rule and count every fallback.
- Step 5c3 must compare initial distribution and early-contact outcomes under identical seeds/configs.
- Step 5c4 should repeat the manual lane only after SMR identifies one or two exact target profiles.
- E11-I should own any future click-to-inspect ecology UI unless a separate earlier Track A/App diagnostic plan is explicitly approved.
