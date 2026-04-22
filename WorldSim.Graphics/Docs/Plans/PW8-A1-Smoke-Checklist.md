# PW8-A1 Visual-L Hybrid Smoke Checklist

Status: active (pre-Wave8)
Owner: Track A
Scope: PW8-A1 visual readability overhaul proof on top of Wave 7.5 low-cost baseline

## Build/Test gate

- Run `dotnet build WorldSim.sln`.
- Run `dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj`.
- Run `dotnet test WorldSim.Runtime.Tests/WorldSim.Runtime.Tests.csproj --filter LowCost`.
- Run `dotnet test WorldSim.ScenarioRunner.Tests/WorldSim.ScenarioRunner.Tests.csproj --filter LowCostProfileCompatibilityTests`.

## Lane policy guardrail (must remain true)

- App default visual lane is `DevLite` unless app-side override is provided.
- `Ctrl+F5` cycles only `DevLite <-> Showcase`.
- `Headless` remains `ScenarioRunner / batch only`.
- HUD and settings lane text still report requested/effective/source policy correctly.

## Core PW8-A1 visual proof

Run manual app smoke in both lanes:

1. `DevLite`
2. `Showcase`

For each lane verify:

- Predator and herbivore are no longer ambiguous full-tile blocks.
- Food node is no longer an ambiguous full-tile block.
- Category proportions are readable at common zoom levels (actors/resources/structures).
- Terrain/material readability is improved (grass/dirt separation, water/shore readability, better silhouette contrast).
- Structure grounding/footprint cues remain readable without introducing visual clutter.

## Optional-asset fallback proof

Validate both states:

- Optional visual asset missing (fallback rendering path)
- Optional visual asset present (only when a real asset batch is delivered and wired)

In fallback state verify:

- No magenta missing-texture markers for optional assets.
- Predator/herbivore/food remain clearly readable.
- Specialized building markers remain category-distinct.

## Low-cost regression guard (critical)

- Richer actor/structure fallback draws remain visible-tile guarded or strictly bounded draw.
- Rapid pan/zoom in both lanes does not show obvious actor/structure draw explosion.
- Terrain/resource draw-skip culling remains stable.

## Stress sample (visual proof only)

- Run at least one predator-heavy scene sample and capture visuals in both lanes.
- Treat this as readability/perf observation only; do not use it as justification for runtime coupling changes.

## Capture/evidence package

- Capture before/after screenshots with the same seed and similar camera framing:
  - one `DevLite`
  - one `Showcase`
- Save short notes for each screenshot (lane, zoom impression, fallback/art state).

## Exit criteria

PW8-A1 Track A smoke is passing when:

- Build/test gates are green.
- Lane policy guardrail remains intact.
- Predator/herbivore/food readability goals pass in both lanes.
- Optional asset fallback path is readable and deterministic.
- No obvious low-cost regression from richer actor/structure visuals.
