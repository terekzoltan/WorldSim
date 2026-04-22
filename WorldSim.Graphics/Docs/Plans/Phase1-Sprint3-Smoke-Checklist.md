# Phase 1 Sprint 3 Smoke Checklist

Status: active
Owner: Track A

## Build/Test gate

- Run `dotnet build WorldSim.sln`.
- Run `dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj`.

## Runtime smoke matrix

Run the app in the following display modes:

1. Fullscreen native resolution
2. Windowed 1600x900
3. Fullscreen after toggling from windowed (`F11`)

For each mode verify:

- Camera pan/zoom works and clamps correctly.
- HUD layout remains readable and does not clip unexpectedly.
- PostFx control toggles (`Ctrl+F3/F4`) update control-state text and do not crash.

## Hotkey verification checklist

General:

- `F11`: fullscreen/windowed toggle.
- `F9` / `F10`: theme cycle.
- `T`: telemetry HUD toggle.

Debug/graphics controls:

- `F3`: render stats on/off.
- `Ctrl+F3`: postfx control on/off toggle.
- `Ctrl+F4`: postfx control quality cycle.
- `Ctrl+F5`: visual lane cycle (`DevLite <-> Showcase`).

Cinematic/capture:

- `Ctrl+F9`: cinematic route play/stop.
- `Ctrl+F10`: screenshot capture (check `Screenshots/`).
- `F12`: clean-shot toggle.
- `Ctrl+F12`: settings overlay on/off.

Combat preflight scaffolds:

- `Ctrl+F1`: diplomacy panel scaffold.
- `Ctrl+F2`: campaign panel scaffold.
- `Ctrl+F7`: territory overlay scaffold.
- `Ctrl+F8`: combat overlay scaffold.
- `F2`: focus camera on tracked NPC.

Operator controls (visible in-app):

- `Ctrl+F6`: director requested output-mode cycle.
- `Ctrl+Shift+F6`: director operator preset cycle.

## Low-cost lane policy checks

- App startup default lane is `DevLite` when no `WORLDSIM_VISUAL_PROFILE` app override is set.
- `Ctrl+F5` cycles only `DevLite <-> Showcase`.
- HUD main status line shows `Lane:<effective>`.
- `Lane:<effective>` updates correctly when cycling `Ctrl+F5` between `DevLite` and `Showcase`.
- Settings overlay shows lane requested/effective/source fields.
- Settings overlay explicitly states `Headless` is `ScenarioRunner / batch only` (not app-side interactive lane).
- PostFx control-state wording is visible in HUD/settings (`PostFxCtl` / `PostFx control state`) and is treated as control-surface verification in this checklist.

Headless policy verification (runner-side, not app-side):

- Run a headless ScenarioRunner smoke with artifacts, for example:
  - `WORLDSIM_SCENARIO_MODE=standard WORLDSIM_SCENARIO_OUTPUT=json WORLDSIM_SCENARIO_SEEDS=101 WORLDSIM_SCENARIO_PLANNERS=simple WORLDSIM_SCENARIO_ARTIFACT_DIR=".artifacts/smr/lc1-a3-headless-smoke" dotnet run --project WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj`
- Verify `.artifacts/smr/lc1-a3-headless-smoke/manifest.json` has `effectiveVisualLane = "Headless"`.
- Verify at least one run artifact under `.artifacts/smr/lc1-a3-headless-smoke/runs/` reports `visualLane = "Headless"`.

## Visual/perf checks

- Render stats line should show:
  - frame time
  - rolling average
  - rolling p99
  - per-pass timings
- `DevLite` remains the cheap default path; `Showcase` is explicit/opt-in.
- Terrain/resource draw-skip culling path remains stable during rapid pan (actor/structure full culling is out of this baseline checklist).
- Fog/haze and resource rendering should still appear correct after quality/profile changes.

## Capture validation

- Trigger at least 2 screenshots in different modes.
- Verify files are created and non-empty in `Screenshots/`.
- Verify clean-shot image has no telemetry panels.

## Exit criteria

Sprint 3 Track A smoke is considered passing when:

- All hotkeys above behave as described.
- Low-cost lane checks pass for app-side `DevLite`/`Showcase` visibility and runner-side `Headless` evidence.
- No crashes or rendering corruption under the runtime matrix.
- Build/test gate is green.
