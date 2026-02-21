# Track A - Phase 1 Visual Overhaul Plan

Status: planned
Owner: Track A (Graphics/UI)
Scope: visuals + UX polish without changing core gameplay rules

## Context and goal

This is a portfolio-first, passion project direction. The target is a strong visual identity and a "wow" first impression, while keeping architecture clean and demo-ready.

Phase 1 North Star:

- Fullscreen and windowed both feel correct (no dead gray space, proper camera fit).
- The world feels alive (motion, atmosphere, subtle animation, event feedback).
- HUD is readable and modular (not raw debug text walls).
- Performance is stable at 1080p and acceptable at 1440p.
- A 60-90 second showcase run is easy to record.

Timebox: 3 sprints x 2 weeks (6 weeks total)

## Sprint 1 - Visual Foundation + Design Freeze

Goal: build a robust rendering/UI base and freeze art direction primitives.

### S1-A Render pipeline formalization

Large task:
- Define a formal pass pipeline and central render context.

Implementation tasks:
- Create `IRenderPass` and `RenderFrameContext` in `WorldSim.Graphics/Rendering/`.
- Lock pass order: terrain -> resources -> structures -> actors -> overlays.
- Adapt current passes (`TerrainRenderPass`, `ResourceRenderPass`, `StructureRenderPass`, `ActorRenderPass`) to the common interface.
- Add lightweight `RenderStats` (pass times, draw call counters) in debug mode.
- Keep `WorldRenderer` as orchestrator-only (`WorldSim.Graphics/Rendering/WorldRenderer.cs`).

### S1-B Theme/token system upgrade

Large task:
- Move to shared visual tokens for world and HUD.

Implementation tasks:
- Expand `WorldRenderTheme` tokens: panel bg, border, highlight, warning, success.
- Align `HudTheme` with world tokens (`WorldSim.Graphics/UI/HudTheme.cs`).
- Add preset switching hotkeys (F9/F10) in host for quick visual iteration.
- Finalize 3 core presets: Daylight, Parchment, Industrial.
- Add deterministic fallback behavior for missing tokens.

### S1-C HUD 2.0 shell

Large task:
- Turn HUD into stylized modular panels.

Implementation tasks:
- Add card-like UI primitives: panel background, border, title, line spacing.
- Introduce safe-area based layout (left stack: colony+eco, right stack: AI debug).
- Convert event feed to ticker-like block with cap and fade.
- Improve tech menu visual grouping (section header, numbered rows, spacing).
- Add HUD scaling presets: 1.0 / 1.25 / 1.5.

### S1-D Camera and viewport hardening

Large task:
- Make fullscreen/windowed camera behavior production-solid.

Implementation tasks:
- Keep `FitCameraToViewport()` as the source of truth for framing.
- Ensure `F11` toggle and resize path always run fit+clamp.
- Compute min/max zoom with viewport-aware constraints.
- Add startup hero framing preset (center + best-fit zoom).
- Add subtle pan/zoom damping.

### S1-E Asset and typography baseline

Large task:
- Establish art and text foundations for overhaul work.

Implementation tasks:
- Add 2 sprite fonts in content: `UiFont` and `TitleFont`.
- Add small HUD icon set (food/wood/stone/iron/gold/morale/warning).
- Extend `TextureCatalog` for UI assets (`WorldSim.Graphics/Assets/TextureCatalog.cs`).
- Standardize naming for faction/specialized-building visual assets.
- Add a missing-texture fallback marker for debug.

Sprint 1 DoD:

- Pass architecture stable and reusable.
- HUD panelized and readable.
- Theme switching works live.
- Fullscreen/windowed transitions stable.
- `dotnet build WorldSim.sln` and arch tests pass.

## Sprint 2 - Motion + Atmosphere + Juice

Goal: make the world feel alive and expressive.

### S2-A Temporal rendering and interpolation

Large task:
- Remove visual snapping and improve perceived smoothness.

Implementation tasks:
- Add render-side `tickAlpha` interpolation value.
- Keep previous/current snapshot data in render context for interpolation.
- Interpolate people/animal movement between ticks.
- Add micro-motion for entities (idle bob/pulse).
- Add stable identity support where needed to avoid interpolation artifacts.

### S2-B Terrain and biome animation

Large task:
- Animate map surfaces and resources.

Implementation tasks:
- Add water movement effect (uv/noise or frame-based offset).
- Add subtle grass shimmer/wind modulation.
- Add occasional ore glint and food-node pulse.
- Add specialized-building micro-activity effects.
- Add season-sensitive color modulation.

### S2-C Atmosphere and weather layer

Large task:
- Add environmental overlays and weather mood.

Implementation tasks:
- Create `WeatherRenderPass` in `WorldSim.Graphics/Rendering/`.
- Add rain/fog variants with pooled particles.
- Add drought visual mode (haze/tint shift).
- Add event-driven visual cues (season switch pulse, threat pulse).
- Bind overlay intensity to snapshot state (or deterministic fallback).

### S2-D Camera feel package

Large task:
- Improve camera feel and cinematic controllability.

Implementation tasks:
- Add inertial panning decay.
- Add smooth zoom-to-target interpolation.
- Add quick-focus hotkey to recent event area.
- Add lightweight screen shake API for impactful events.
- Add photo mode baseline (hide HUD, lock controls).

### S2-E HUD information redesign

Large task:
- Increase clarity while reducing visual noise.

Implementation tasks:
- Replace long lines with grouped stat chips/rows.
- Add tiny trend sparklines (ring buffer of recent values).
- Split colony panel into sections (resources, population, losses).
- Refine AI debug readability (top goals vs recent decisions).
- Add status coloring (ok/warn/error semantics).

### S2-F Map expansion (new requirement)

Large task:
- Increase playable world size and keep visual quality/performance.

Implementation tasks:
- Add runtime map size config in host startup (debug presets: 128, 192, 256).
- Expand default map target to a larger size (recommended: 192x192 for first step).
- Re-tune startup camera fit and max zoom for larger maps.
- Validate HUD readability and interaction pacing on larger worlds.
- Profile frame time and apply pass-level optimizations for the larger map.

Sprint 2 DoD:

- Motion and atmosphere are visibly improved.
- Camera handling feels smooth and intentional.
- Larger map mode works without breaking UX.
- 1080p remains stable with quality settings.

## Sprint 3 - Cinematic Polish + Showcase Pack

Goal: deliver a portfolio-ready wow build and clear demo flow.

### S3-A Post-process stack

Large task:
- Introduce a controlled post-fx pipeline.

Implementation tasks:
- Add scene `RenderTarget2D` pipeline.
- Add color grading pass (matrix or LUT-like profile).
- Add subtle vignette pass.
- Add bloom-lite (threshold + limited blur passes, quality gated).
- Add optional grain/film overlay.

### S3-B Cinematic mode

Large task:
- Provide scripted camera experience for captures.

Implementation tasks:
- Add keyframed showcase camera route with easing.
- Add short intro reveal sequence.
- Add HUD auto-fade in cinematic mode.
- Add screenshot hotkey (PNG to `Screenshots/`).
- Add clean capture mode hotkey (hide debug/UI layers).

### S3-C UX polish and quality settings

Large task:
- Expose practical visual controls.

Implementation tasks:
- Add quick settings overlay (theme, postfx, HUD scale, weather).
- Add quality profiles Low/Medium/High.
- Add input hint strip for key controls.
- Add high-contrast accessibility HUD theme.
- Fix clipping/overflow and font fallback edge cases.

### S3-D Performance and stability pass

Large task:
- Make visuals robust under longer sessions.

Implementation tasks:
- Add frame-time sampling (avg and 99th percentile).
- Expand `RenderStats` with pass-level timings.
- Add strict particle pool/memory caps.
- Remove render-loop allocation hotspots.
- Run smoke matrix: 1080p fullscreen, 1440p fullscreen, windowed.

### S3-E Portfolio packaging

Large task:
- Prepare final showcase artifacts.

Implementation tasks:
- Capture before/after shots using same seed/camera framing.
- Write 60-90 second demo run script.
- Update README showcase section with GIF/video and architecture notes.
- Add short technical note: snapshot boundary + pass pipeline design.
- Highlight 3 signature features for presentation.

Sprint 3 DoD:

- Cinematic wow pass complete.
- Demo capture flow ready.
- Quality/perf controls in place.

## Phase 1 side-cleanup while implementing

These are not separate sprints, but should be completed during Phase 1:

- Rename host class/file from `Game1` to `GameHost` in `WorldSim.App/`.
- Reorganize graphics folders:
  - `Rendering/Core/`
  - `Rendering/Passes/`
  - `Rendering/PostFx/`
  - `UI/Panels/`
  - `UI/Layout/`
- Extend arch tests in `WorldSim.ArchTests/BoundaryRulesTests.cs`:
  - graphics must not depend on mutable simulation internals
  - app host must not contain heavy draw logic
- Add smoke checklist doc: `WorldSim.Runtime/Docs/Plans/Phase1-SmokeChecklist.md`.

## Risk management

- If FPS drops during Sprint 2, postpone bloom/grain to late Sprint 3 or optional profile.
- If shader complexity is too high, use CPU-side tint/overlay fallback with same art direction.
- If HUD scope expands too much, prioritize layout+typography first, then mini-graphs.
- Keep strict quality gates after each major task:
  - build
  - quick run
  - 5-minute manual smoke

## Quality gates and validation

- Required per PR:
  - `dotnet build WorldSim.sln`
  - target tests for touched projects
  - manual smoke: fullscreen/windowed, camera pan/zoom, HUD readability
- Required before Phase 1 close:
  - visual compare sheet (before/after)
  - short demo recording
  - stable run in fullscreen and windowed

## Out-of-scope for Phase 1

- Deep simulation systems redesign (Phase 2)
- Major AI behavior overhauls beyond debug/readability support (Phase 3)
- Scenario runner/persistence-heavy work unless needed for immediate visual validation

## Notes

- This plan is Track A specific and intentionally overbuilt for visual impact.
- Phase 2+ roadmap remains active but parked while Phase 1 executes.
