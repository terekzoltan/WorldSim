# Track A - Phase 1 Sprint 3 Execution Plan

Status: planned
Owner: Track A (Graphics/UI)
Scope: Sprint 3 execution details (cinematic polish + showcase + combat preflight)
Depends on: `WorldSim.Graphics/Docs/Plans/Track-A-Phase1-Visual-Overhaul-Plan.md`

## 1. Sprint goal

Deliver a portfolio-ready visual build with a reproducible showcase flow, while preparing Track A integration points for the upcoming Combat Master Plan (without implementing combat rules in graphics).

Primary outcomes:

- Controlled post-process stack with quality toggles.
- Cinematic capture workflow (scripted camera + clean screenshots/video).
- Stable UX controls for theme/postfx/HUD/quality.
- Performance visibility and guardrails.
- Combat-ready UI/render scaffolds so Track B/C can plug data in with low friction.

Sprint length: 2 weeks

## 2. Non-goals

- No combat mechanics, damage logic, diplomacy rules, or campaign simulation state changes in runtime.
- No mandatory shader complexity escalation if perf cost is not acceptable.
- No deep AI behavior work beyond display/observability hooks.

## 3. Workstreams (S3-1..S3-5)

## S3-1 PostFx Foundation

### Objective

Introduce a controllable post-process pipeline that improves visual cohesion while preserving fallback compatibility.

### Deliverables

- `RenderTarget2D` scene path.
- Color grading pass.
- Vignette pass.
- Optional bloom-lite pass.
- Runtime quality switch controlling all postfx.

### Implementation tasks

1. Create postfx core contracts in `WorldSim.Graphics/Rendering/PostFx/`:
   - `IPostProcessPass`
   - `PostProcessFrameContext`
   - `PostProcessQualityProfile`
2. Add scene render target management in `WorldRenderer`:
   - world pass -> offscreen target
   - postfx chain -> backbuffer
3. Implement `ColorGradePass`:
   - matrix/tint based approach first
   - theme-driven parameters
4. Implement `VignettePass`:
   - center-weighted darkening
   - configurable intensity per quality profile
5. Implement `BloomLitePass` (quality-gated):
   - threshold
   - limited blur (1-2 pass)
   - additive composite
6. Add fallback behavior:
   - if postfx unavailable or disabled, direct draw path remains valid.

### Acceptance criteria

- Postfx can be toggled live without restart.
- Low profile has minimal overhead and visual stability.
- No breakage in fullscreen/windowed transitions.

## S3-2 Cinematic + Capture Workflow

### Objective

Create a repeatable showcase pipeline for portfolio captures.

### Deliverables

- Scripted camera route with easing.
- Cinematic mode with HUD fade.
- Screenshot capture utility.
- Clean-shot mode (hide overlays/UI/debug).

### Implementation tasks

1. Add camera route primitives in `WorldSim.Graphics/Camera/`:
   - `CameraKeyframe`
   - `CameraRoute`
   - `CameraRoutePlayer`
2. Add route controls in host (`WorldSim.App/GameHost.cs`):
   - start/stop route hotkeys
   - route state display in status line
3. Add HUD fade state in `HudRenderer`:
   - alpha multiplier by cinematic mode
4. Add screenshot command:
   - `Screenshots/` folder creation
   - timestamped PNG naming
5. Add clean-shot toggle:
   - disables telemetry panels/debug overlays
   - retains minimal optional watermark/status if desired.

### Acceptance criteria

- One-button route playback is deterministic.
- Screenshots save reliably with no exceptions.
- Clean-shot mode removes distracting overlays.

## S3-3 UX and Quality Controls

### Objective

Expose practical visual controls in-game so iteration and demos are fast.

### Deliverables

- Quick settings overlay.
- Low/Medium/High quality profiles.
- Updated hotkey legend/help strip.
- High-contrast HUD theme option.

### Implementation tasks

1. Add settings overlay renderer in `WorldSim.Graphics/UI/Panels/`:
   - quality
   - postfx on/off
   - FX intensity
   - HUD scale
2. Add quality profile mapping:
   - controls haze, pulse strength, postfx pass set, optional render scale.
3. Add host-level state machine for settings:
   - hotkeys to cycle/change values
   - persisted only in-session for now
4. Add high-contrast HUD preset in `HudTheme`.
5. Update planner/status line help text in `GameHost`.

### Acceptance criteria

- All settings changes apply live.
- Overlay is readable on 1080p and 1440p.
- No telemetry clipping regressions.

## S3-4 Performance and Stability Pass

### Objective

Keep the enhanced visuals stable on larger 16:9 maps.

### Deliverables

- Frame-time telemetry (avg + p99 windows).
- Expanded per-pass timing visibility.
- Allocation hotspot cleanup for render loop.
- Smoke matrix results documented.

### Implementation tasks

1. Extend render stats:
   - rolling frame samples
   - p99 calculation helper
2. Add debug telemetry line(s) for frame timing in HUD/debug mode.
3. Remove avoidable per-frame allocations:
   - avoid repeated list allocations in panel rendering where possible
   - reuse temporary buffers where safe
4. Add optional viewport culling policy for large maps:
   - terrain/resource draw window with small padding
5. Run smoke matrix:
   - fullscreen 1080p
   - fullscreen 1440p
   - windowed 1600x900
   - map presets cycle (`F5`).

### Acceptance criteria

- Stable runtime with no frame-time spikes caused by obvious allocation churn.
- Quality profile low remains performant on large maps.
- No camera/HUD regressions under resolution changes.

## S3-5 Combat Master Plan Preflight (Track A-side)

### Objective

Prepare graphics/UI extension seams so upcoming combat and campaign data can be integrated with minimal refactor.

### Deliverables

- Snapshot-forward-compatible render handling.
- Combat/diplomacy/campaign overlay scaffolds (render-only placeholders).
- Event feed category slots ready for combat/siege/campaign messages.

### Implementation tasks

1. Snapshot compatibility pass:
   - ensure renderer tolerates additive read-model fields
   - avoid assumptions that break when `PersonRenderData` expands.
2. Add placeholder pass hooks:
   - `TerritoryOverlayPass` (disabled by default)
   - `CombatOverlayPass` (disabled by default)
   - wiring only, no runtime dependency on combat yet
3. Add UI panel placeholders in `WorldSim.Graphics/UI/Panels/`:
   - `DiplomacyPanelRenderer` (basic stub)
   - `CampaignPanelRenderer` (basic stub)
4. Event feed categorization scaffolding:
   - support source tags (World, Combat, Siege, Campaign)
   - style mapping by category.
5. Add integration note in docs:
   - exact fields Track A expects once Combat Phase 0 starts.

### Acceptance criteria

- Track B can add combat snapshot fields without breaking Track A compile path.
- Overlay/panel stubs are available behind toggles.
- No accidental runtime mutable state coupling introduced.

## 4. Proposed file-level implementation map

App:

- `WorldSim.App/GameHost.cs`
  - cinematic mode toggles
  - settings/profile state
  - capture hotkeys

Graphics rendering:

- `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- `WorldSim.Graphics/Rendering/RenderFrameContext.cs`
- `WorldSim.Graphics/Rendering/PostFx/*`
- `WorldSim.Graphics/Rendering/Passes/*` (if folder split is done during sprint)

Graphics UI:

- `WorldSim.Graphics/UI/HudRenderer.cs`
- `WorldSim.Graphics/UI/Panels/SettingsPanelRenderer.cs`
- `WorldSim.Graphics/UI/Panels/DiplomacyPanelRenderer.cs` (stub)
- `WorldSim.Graphics/UI/Panels/CampaignPanelRenderer.cs` (stub)

Docs/tests:

- `AGENTS.md` cross-track notes update at sprint close.
- `WorldSim.ArchTests/BoundaryRulesTests.cs` updates if file moves/renames occur.
- `WorldSim.Graphics/Docs/Plans/Phase1-SmokeChecklist.md` (new, if missing).

## 5. Recommended hotkeys for Sprint 3

- `F2`: focus tracked NPC (existing)
- `F5`: map preset cycle (existing)
- `F9/F10`: theme cycle (existing)
- `F11`: fullscreen toggle (existing)
- `F12`: FX intensity/quality quick toggle (existing low/full behavior can remain)
- `F3`: render stats toggle (existing)
- `F4`: AI compact toggle (existing)
- New proposal:
  - `F1`: Tech menu (keep)
  - `F6`: refinery trigger (keep)
  - `Shift+F3`: postfx overlay toggle
  - `Shift+F9`: cinematic route play/pause
  - `Shift+F10`: screenshot capture

## 6. Risks and mitigations

- Postfx cost spikes on large map:
  - Mitigation: strict quality profiles, bloom optional, fallback direct path.
- Folder refactor churn during sprint:
  - Mitigation: move files in one dedicated PR, feature work in follow-up PRs.
- Overlay/panel placeholders drifting from runtime realities:
  - Mitigation: maintain small contract note synced with Combat Master Plan.
- Camera route conflicts with manual input:
  - Mitigation: route player owns camera while active; input disabled or blended deterministically.

## 7. Definition of Done (Sprint 3)

- Postfx stack (color grade + vignette + optional bloom) is functional and quality-gated.
- Cinematic route + screenshot flow works and is demo-ready.
- In-game quality/settings UX exists and is stable.
- Frame-time and pass timing telemetry are visible in debug mode.
- Combat-preflight stubs are in place (overlay/panel/event categories) without runtime coupling regressions.
- Validation passes:
  - `dotnet build WorldSim.sln`
  - `dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj`
  - manual smoke matrix on fullscreen/windowed + map preset cycle.

## 8. Relationship to Combat Master Plan

We are currently executing Track A Phase 1 (visual overhaul) and not yet executing Combat Master Plan phases.

This sprint intentionally includes a Track A preflight subset so Combat Phase 0 can start with lower integration risk:

- renderer/context contracts are stable,
- overlay hooks exist,
- UI has reserved surfaces for diplomacy/campaign/combat observability.
