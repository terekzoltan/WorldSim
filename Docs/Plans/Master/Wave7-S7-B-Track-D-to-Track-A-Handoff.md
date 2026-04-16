# Wave 7 S7-B Track D -> Track A Handoff

Purpose:
- Provide a stable operator/runtime seam for Track A UX consume without introducing Track A-side policy decisions.

## TU1-D1 terminology lock

This terminology is frozen for Wave 7.1 Step 1 (`TU1-D1`) and should be reused across runtime status copy and operator docs.

| Term | Meaning | Values / examples |
|---|---|---|
| `preset` | Named control action bundle (cycled by hotkey) | `fixture_smoke`, `live_mock`, `live_director` |
| `profile` | Currently active operator-facing state label | `fixture_smoke`, `live_mock`, `live_director`, custom override |
| `lane` | Integration transport lane | `off`, `fixture`, `live` |
| `requested mode` | Operator control-state output mode | `auto`, `both`, `story_only`, `nudge_only`, `off` |
| `effective mode` | Applied/response director output mode result | `both`, `story_only`, `nudge_only`, `off`, `unknown` |
| `apply` | Local C# apply outcome state | `not_triggered`, `applied`, `apply_failed`, `request_failed` |
| `request failure kind` | Request-failure subtype | `timeout`, `connection_refused`, `http_<status>`, `request_error` |

Disambiguation rule for status copy:
- use `lane=` only for `off|fixture|live`
- use `requested=` for operator requested output mode
- use `mode=` only for effective director output mode

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
- Profile is the currently visible operator-facing state label.
- Output mode vocabulary remains unchanged.
- Effective mode/source in HUD remains runtime response/apply state.
- Requested mode/source and preset/lane are operator-side control state.

## Smoke references

- Java-only lane: `java_planner_smoke`
- Full-stack lane: `full_stack_smoke` (manual app/runtime smoke, not helper-script driven)

Scripts are convenience helpers only; source of truth remains the runtime + Java docs.
