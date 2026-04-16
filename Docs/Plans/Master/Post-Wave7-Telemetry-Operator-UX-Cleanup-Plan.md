# Post-Wave7 Telemetry / Operator UX Cleanup Plan

Status: proposed follow-up after Wave 7 closeout
Owner: Track A primary, Track D support for operator semantics and wording
Last updated: 2026-04-10

## 1. Purpose

Wave 7 delivered the operator seam and in-app control path for refinery/director workflows:

- restartless requested output-mode cycling,
- restartless preset cycling,
- settings overlay visibility for operator state,
- and HUD/status-line consume of director state.

Manual fixture, `live_mock`, and `live_director` smoke show the feature is functionally useful, but the current presentation is still noisy and text-heavy.

This follow-up exists to improve readability and operator usability without changing gameplay semantics.

## 2. Primary Goals

1. Reduce the current "wall of text" feel in HUD/status telemetry.
2. Separate always-visible operator information from debug-only and failure-only detail.
3. Make director/refinery state readable at a glance during live smoke.
4. Introduce low-cost visual widgets where they improve comprehension.
5. Preserve the existing snapshot/runtime boundary and low-cost rendering constraints.

## 3. Non-Goals

This plan does not aim to:

- redesign the full HUD system,
- replace text with a large dashboard,
- add per-frame expensive analytics logic to Graphics,
- move gameplay/state computation into rendering code,
- or introduce a new telemetry backend/service.

## 4. Current Pain Points

Observed from Wave 7 smoke and code review:

- Top planner/status line carries too much information at once.
- Director/operator data mixes control state and outcome state in one textual stream.
- Failure details are available, but not visually separated enough from normal info.
- Pending chain info is useful, but currently competes with other debug text.
- Settings overlay exposes state, but not in a strongly structured visual hierarchy.

## 5. Design Constraints

The cleanup must respect project-level constraints:

- low-cost 2D baseline stays primary,
- Graphics consumes snapshot/read-model data only,
- runtime remains source of truth for telemetry-driving state,
- visuals should remain deterministic and profile-aware,
- the default developer view should stay lightweight, not dashboard-heavy.

## 5.1 TU1-D1 wording guardrail

Before layout cleanup (`TU1-A2`), Track D freezes terminology + status semantics only:

- `preset` = named control action bundle
- `profile` = currently active operator-facing label
- `lane` = integration transport lane (`off|fixture|live`)
- `requested mode` = operator control-state output mode
- `effective mode` = response/apply output mode

Mode ambiguity cleanup target:
- use `lane=` only for `off|fixture|live`
- use `requested=` only for operator requested output mode
- use `mode=` only for effective output mode on outcome lines

Scope guardrail:
- TU1-D1 does not pre-implement future TU1-A2 visual layout contracts.
- TU1-D1 updates wording/taxonomy and Track D-owned status copy only.

## 6. Recommended Information Architecture

Split refinery/director telemetry into 3 visibility levels.

### 6.1 Always-visible operator summary

Keep only the most actionable items always visible:

- effective director mode,
- apply status,
- current profile,
- current lane,
- requested mode.

This should be compact and readable without opening debug UI.

### 6.2 Debug-visible director detail

Show richer director state only when the user explicitly wants debugging:

- stage/source detail,
- pending causal chains,
- active directive summary,
- budget detail,
- compact cooldown / checkpoint indicators.

### 6.3 Failure-only diagnostics

When something fails, elevate the exact issue visually:

- `request_failed` reason,
- `apply_failed` reason,
- timeout / connection / HTTP error detail,
- fallback warning text where relevant.

This should stand out from normal green-path telemetry.

## 7. UI Cleanup Workstreams

### 7.1 Top status line cleanup

Target:

- one short operator summary line,
- less abbreviation overload,
- fewer concatenated fields.

Suggested shape:

```text
Dir: eff=both applied | requested=auto | profile=live_director | lane=live
```

Do not include large debug detail here.

### 7.2 Director HUD block cleanup

Target:

- visually separate summary from detail,
- use 1-3 compact pending-chain rows max,
- show diagnostic text only when it matters.

Suggested sections:

1. Summary line
2. Directive / chain block
3. Optional budget/detail block
4. Optional failure callout

### 7.3 Settings overlay cleanup

Target:

- make it the primary operator "readable state" panel,
- cleaner grouping than the top HUD,
- clear hotkey help and state grouping.

Suggested groups:

1. General controls
2. Director control state
3. Director effective state
4. Failure/detail line if present

## 8. Possible Graph / Visual Widget Options

These are intentionally low-cost and snapshot-driven.

### 8.1 Tier 1 — Strongly recommended

#### A. Status badges / chips

Use compact colored labels for:

- `applied`
- `apply_failed`
- `request_failed`
- `response`
- `fallback`
- `fixture_smoke`
- `live_mock`
- `live_director`

Why:

- cheapest visual improvement,
- much easier to scan than long text,
- pairs well with existing text lines.

#### B. Progress bars / gauges

Use simple horizontal bars for:

- budget used vs remaining,
- beat cooldown remaining,
- pending chain window progress,
- optional directive remaining duration.

Why:

- easy to read quickly,
- deterministic,
- cheap to render.

### 8.2 Tier 2 — Good follow-up if Tier 1 works well

#### C. Tiny sparklines

Use short history trend lines for a very small set of metrics:

- food-per-person,
- morale average,
- maybe predator pressure or active battle count.

Constraint:

- only if runtime exports a small rolling history cleanly,
- keep to 1-2 sparklines max.

#### D. Event timeline strip

Use a small horizontal strip for recent director/operator outcomes:

- trigger,
- applied,
- request failed,
- apply failed,
- fallback used.

This could help manual smoke and quick operator diagnosis.

### 8.3 Tier 3 — Optional later expansion

#### E. Compact histogram / stacked bar blocks

Possible only if later proven necessary:

- error categories over recent checkpoints,
- distribution of directive/beat outcome categories,
- scenario-run telemetry summaries.

This should not be part of the first cleanup pass.

## 9. Recommended Execution Order

### Step 1 — Textual cleanup first

- reduce top-line text density,
- restructure director HUD block,
- improve settings overlay grouping,
- separate failure messaging more clearly.

### Step 2 — Add Tier 1 visual widgets

- status badges,
- budget/cooldown/progress bars.

### Step 3 — Re-evaluate after manual smoke

If the UX is already good enough, stop here.

### Step 4 — Only then consider Tier 2 widgets

- 1-2 sparklines,
- optional event timeline strip.

## 10. Suggested Track Split

### Track A

- HUD readability cleanup
- settings overlay restructuring
- badge/bar visual implementation
- manual smoke for readability across resolutions/HUD scales

### Track D

- canonical wording for operator states
- failure taxonomy wording alignment
- ensure preset/mode/source labels stay stable and short
- docs alignment for operator-facing terminology

## 11. Acceptance Criteria

The cleanup is successful if:

1. A developer can read current director/operator state quickly during manual smoke.
2. Normal green-path telemetry is visually distinct from failure diagnostics.
3. The top status area is shorter and less noisy than the Wave 7 version.
4. At least one visual aid (badge or bar) improves clarity without adding clutter.
5. No gameplay/state logic moves into Graphics.
6. Rendering cost remains low and deterministic.

## 12. Manual Smoke Checklist

Run after implementation on:

- `fixture_smoke`
- `live_mock`
- `live_director`
- narrow window / smaller viewport
- default HUD scale
- larger HUD scale
- `request_failed`
- `apply_failed`
- fallback-visible response

## 13. Recommendation

Implement this as a small post-Wave7 cleanup slice, not as a major new wave.

Preferred first pass:

1. textual cleanup,
2. badges,
3. progress bars,
4. stop and reassess,
5. only then consider sparklines/timeline widgets.
