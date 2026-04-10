# Wave 7 S7-B Track D -> Track A Handoff

Purpose:
- Provide a stable operator/runtime seam for Track A UX consume without introducing Track A-side policy decisions.

## Stable consume surface

- Current operator preset name:
  - `fixture_smoke`
  - `live_mock`
  - `live_director`
- Current operator preset source label:
  - `env` or `operator`
- Current integration lane label:
  - `off|fixture|live`
- Requested director mode vocabulary:
  - `auto|both|story_only|nudge_only|off`
- Requested director mode source label:
  - `env|profile|operator`

## Runtime/app seam methods

- `CycleOperatorPreset()`
  - cycles `fixture_smoke -> live_mock -> live_director -> ...`
  - applies immediately (no restart)
- `CycleDirectorOutputMode()`
  - cycles `auto -> both -> story_only -> nudge_only -> off -> auto`
  - applies immediately (no restart)

## Operator wording lock

- Preset is an operational preset, not a transport/schema concept.
- Output mode vocabulary remains unchanged.
- Effective mode/source in HUD remains runtime response/apply state.
- Requested mode/source and preset/lane are operator-side control state.

## Smoke references

- Java-only lane: `java_planner_smoke`
- Full-stack lane: `full_stack_smoke` (manual app/runtime smoke, not helper-script driven)

Scripts are convenience helpers only; source of truth remains the runtime + Java docs.
