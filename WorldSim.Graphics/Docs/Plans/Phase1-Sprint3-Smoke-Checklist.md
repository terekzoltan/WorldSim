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
- PostFx toggle and quality changes do not crash.

## Hotkey verification checklist

General:

- `F11`: fullscreen/windowed toggle.
- `F9` / `F10`: theme cycle.
- `T`: telemetry HUD toggle.

Debug/graphics controls:

- `F3`: render stats on/off.
- `Shift+F3`: postfx on/off.
- `Shift+F4`: postfx quality cycle.
- `Shift+F5`: quality profile cycle (Low/Medium/High).
- `Shift+F6`: HUD scale cycle.

Cinematic/capture:

- `Shift+F9`: cinematic route play/stop.
- `Shift+F10`: screenshot capture (check `Screenshots/`).
- `F12`: clean-shot toggle.
- `Shift+F12`: settings overlay on/off.

Combat preflight scaffolds:

- `Shift+F1`: diplomacy panel scaffold.
- `Shift+F2`: campaign panel scaffold.
- `Shift+F7`: territory overlay scaffold.
- `Shift+F8`: combat overlay scaffold.
- `F2`: focus camera on tracked NPC.

## Visual/perf checks

- Render stats line should show:
  - frame time
  - rolling average
  - rolling p99
  - per-pass timings
- Large map traversal should not hard-stutter during rapid pan (terrain/resource culling path).
- Fog/haze and resource rendering should still appear correct after quality/profile changes.

## Capture validation

- Trigger at least 2 screenshots in different modes.
- Verify files are created and non-empty in `Screenshots/`.
- Verify clean-shot image has no telemetry panels.

## Exit criteria

Sprint 3 Track A smoke is considered passing when:

- All hotkeys above behave as described.
- No crashes or rendering corruption under the runtime matrix.
- Build/test gate is green.
