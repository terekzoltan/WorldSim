# Session: Performance Profiling

> Focused technical session for establishing FPS baselines, identifying bottlenecks, and validating
> that entity count growth from combat phases does not degrade the player experience.
>
> **This session also produces a reusable agent/tool**: the infrastructure it builds (SimStats,
> extended RenderStats, ScenarioRunner --perf mode) becomes a permanent part of the project
> that any session can invoke for perf checks.

Status: Planned (trigger: Combat Phase 3, or earlier if FPS < 60)
Last updated: 2026-02-26

---

## EMLEKEZTETOK (Persistent Reminders)

> **Ezt a szekciót minden session megnyitásakor olvasd el.**
>
> 1. **Perf Profiling agens letrehozasa:** Amint a Session elso futasa lezarul (Phase A infra kesz),
>    a benne epult tooling (SimStats + RenderStats + ScenarioRunner --perf) legyen csomagolva
>    ugy, hogy barmelyik masik session egyszeru paranccssal futtathatja.
>    Konkret kovetkezo lepes: keszits egy `perf-check` workflow-t a Meta Coordinator Runbook-ba,
>    ami barmelyik sessionbol triggerelheto.
>
> 2. **Minden combat phase vegen** (Phase 0, 1, 2, ...) a Combat Coordinator session kell hogy
>    triggeralje a perf baseline osszehasonlitast. Ha nincs meg ez a session, a Combat Coordinator
>    maga futtatja a ScenarioRunner --perf-et es a HUD F3 ellenorzest.
>
> 3. **Ha FPS < 60 barmelyik map preseten** (F3 HUD-ban latszik), azonnal nyiss Perf Profiling
>    session-t -- ne vard meg Phase 3-at.

---

## 1. When to open this session

| Trigger | Priority |
|---------|----------|
| Combat Phase 3 (Sprint 5) about to start -- formations and group combat introduce entity multiplication | PRIMARY |
| Frame times exceed 16ms (< 60 FPS) on any standard map preset during Phase 0-2 | EARLY TRIGGER |
| After each combat phase with entity count growth (Phase 3, 5, 6, 7) | RECURRING |
| If ScenarioRunner --perf reports tick time > 8ms on standard seeds | AUTOMATIC |

---

## 2. Current performance infrastructure (baseline)

| Component | Status | Location |
|---|---|---|
| `RenderStats` (per-pass timing via Stopwatch) | EXISTS | `WorldSim.Graphics/Rendering/RenderStats.cs` (34 lines) |
| HUD overlay for render stats (F3 toggle) | EXISTS | `WorldSim.Graphics/UI/HudRenderer.cs`, `WorldSim.App/GameHost.cs` |
| FPS counter (frames/second) | MISSING | -- |
| Simulation tick timing (`World.Update()`) | MISSING | -- |
| Snapshot build timing (`WorldSnapshotBuilder.Build()`) | MISSING | -- |
| Draw call counters | MISSING | Planned in Track A Visual Overhaul Plan, not implemented |
| Entity count in stats | MISSING | -- |
| Viewport culling | MISSING | All render passes iterate full snapshot |
| Snapshot builder optimization | MISSING | Uses LINQ `.Select().ToList()` chains |
| Performance benchmarks (BenchmarkDotNet etc.) | MISSING | -- |
| ScenarioRunner perf mode | MISSING | -- |

---

## 3. Infrastructure work items

### Phase A -- Measurement first (before optimization)

> Rule: measure before optimizing. Never optimize without a baseline number.

**A1. Extend `RenderStats`**

File: `WorldSim.Graphics/Rendering/RenderStats.cs`

Add:
- `double Fps` -- rolling 1-second average (frame count / elapsed seconds)
- `int TotalEntitiesRendered` -- sum of people + animals + houses + buildings + (future: walls, towers, armies)
- Frame time history buffer (last 60 frames) for min/max/avg reporting

**A2. Create `SimStats` in Runtime**

New file: `WorldSim.Runtime/Diagnostics/SimStats.cs`

Fields:
- `double LastTickMilliseconds` -- Stopwatch around `World.Update()`
- `double LastSnapshotBuildMilliseconds` -- Stopwatch around `WorldSnapshotBuilder.Build()`
- `EntityCountSnapshot` record: `People`, `Animals`, `Houses`, `Buildings`, `Tiles`, `TotalActors`

Exposure: via `SimulationRuntime` property or side-channel, consumed by `GameHost` for HUD display.
Must NOT create a dependency from Runtime to Graphics.

**A3. Extend HUD overlay (F3)**

File: `WorldSim.Graphics/UI/HudRenderer.cs`

Add to the render stats display:
- FPS (from `RenderStats.Fps`)
- Sim tick ms / Snapshot build ms (from `SimStats`, passed via `RenderFrameContext` or constructor)
- Entity counts
- Per-pass breakdown (already exists)

Target format:
```
60 FPS | Tick 1.2ms | Snap 0.8ms | Render 4.5ms | Entities 342
  Terrain:0.40 | Resource:0.31 | Structure:0.22 | Actor:0.48 | FogHaze:0.12 | ...
```

**A4. ScenarioRunner `--perf` mode**

File: `WorldSim.ScenarioRunner/Program.cs`

When `WORLDSIM_SCENARIO_PERF=true`:
- Wrap each `world.Update(dt)` in Stopwatch
- Report: avg/min/max/p99 tick time over all ticks
- Report: peak entity count at end of run
- Output as structured JSON line for machine parsing:
```json
{"seed":101,"ticks":1200,"avgTickMs":1.23,"maxTickMs":4.56,"p99TickMs":3.21,"peakEntities":289}
```

### Phase B -- Optimization targets (when numbers justify it)

> Only pursue these when Phase A measurements show a specific bottleneck.

**B1. Viewport culling in render passes**

- `Camera2D` exposes visible tile bounds: `MinVisibleTileX/Y`, `MaxVisibleTileX/Y`
- `TerrainRenderPass` and `ResourceRenderPass` skip tiles outside visible bounds
- `StructureRenderPass` and `ActorRenderPass` skip entities outside visible bounds
- Expected impact: on 384x216 map with 1920x1080 viewport at default zoom, ~85-95% of tiles culled

**B2. Snapshot builder optimization**

- Replace LINQ `.Select().ToList()` chains with pre-allocated `List<T>(capacity)` + foreach
- Colony HUD data: cache computed aggregates, recompute only when colony state is dirty
- `ComputeAverageFoodPerPerson` / `ComputeFoodPerPersonSpread` -- avoid redundant iteration
- Consider: partial snapshot (only visible region) -- but conflicts with interpolator (needs full state)

**B3. Entity pooling for combat phases**

- Walls, towers, army units: reuse snapshot record instances where feasible
- Compact representations (ID + enum + position) instead of full records
- Pool allocation for per-frame lists

**B4. Sim tick optimization**

- Profile `World.Update()` to find hotspots (likely: territory recompute, NPC decision loops)
- Territory recompute: spatial hash or influence delta instead of full recompute
- NPC decision: batch context building, reduce per-person allocation

---

## 4. Performance budget

| Metric | Target (green) | Warning (yellow) | Red line |
|---|---|---|---|
| FPS (384x216, 1080p, fullscreen) | >= 60 | 30-59 | < 30 |
| Sim tick time (avg) | <= 4ms | 4-8ms | > 8ms |
| Snapshot build time | <= 2ms | 2-5ms | > 5ms |
| Render frame time | <= 12ms | 12-16ms | > 16ms |
| Total entity count | <= 5000 | 5000-10000 | > 10000 |

These budgets should be revisited at each combat phase boundary.

---

## 5. Per-session workflow

```
1. ESTABLISH BASELINE
   a. Run game on each map preset:
      - 64x40 (small, debug)
      - 128x72 (medium)
      - 192x108 (standard)
      - 384x216 (large)
   b. Record per preset: FPS, render frame ms, sim tick ms, entity counts
   c. Run ScenarioRunner --perf for headless baseline (1200 ticks, 5 seeds)
   d. Document in a "Perf Baseline" table (append to this plan or a separate report)

2. IDENTIFY BOTTLENECKS
   a. Which render pass is slowest? (RenderStats per-pass breakdown via F3)
   b. Is sim tick or snapshot build the bottleneck? (SimStats)
   c. How do entity counts correlate with frame time? (scale test)
   d. Is the bottleneck CPU-bound (sim/snapshot) or GPU/draw-bound (render passes)?

3. TARGET OPTIMIZATION
   a. Pick the highest-impact optimization from Phase B list
   b. Implement
   c. Re-measure on same presets
   d. Record delta: "B1 viewport culling: 384x216 FPS 28 -> 55 (+96%)"

4. REPORT
   a. Write uzenofal entry with key findings
   b. If any metric is in red zone, flag as blocker to Combat Coordinator
   c. Update Perf Baseline table
```

---

## 6. Map preset reference

| Preset | Tiles | Typical entities (pre-combat) | Notes |
|---|---|---|---|
| 64x40 | 2,560 | ~30-50 | Debug, always fast |
| 128x72 | 9,216 | ~80-120 | Medium test |
| 192x108 | 20,736 | ~150-250 | Standard gameplay |
| 384x216 | 82,944 | ~300-500 | Stress test, viewport culling critical |

---

## 7. Relationship to other sessions

| Session | Relationship |
|---------|-------------|
| **Combat Coordinator** | Triggers this session at Phase 3 or on FPS drop. Receives perf reports. |
| **Balance/QA Agent** | Shares ScenarioRunner `--perf` infrastructure. Can run perf tests as part of balance smoke. |
| **Meta Coordinator** | Receives summary at phase boundaries. Updates risk registry if perf budget violated. |
| **Track A sessions** | Viewport culling and render pass optimization are Track A implementation work. |
| **Track B sessions** | SimStats, snapshot builder optimization, sim tick profiling are Track B implementation work. |

---

## 8. Agent/tool formalization (post Phase A)

Once Phase A infrastructure is complete, formalize as a reusable tool:

1. Add `perf-check` to Meta Coordinator Runbook:
   - Input: map preset (or "all"), tick count, seed count
   - Steps: run ScenarioRunner --perf, read F3 stats, compare to budget
   - Output: pass/fail per metric, delta from last baseline

2. Any session can invoke `perf-check` by referencing this plan's workflow (section 5).

3. The Combat Coordinator's sprint gate checklist should include `perf-check` for Phase 3+ sprints.
