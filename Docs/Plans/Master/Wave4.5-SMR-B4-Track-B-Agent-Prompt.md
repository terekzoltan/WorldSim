# Wave 4.5 SMR-B4 — Track B Agent Prompt

**Session type:** Track B implementation agent  
**Epic:** SMR-B4 — Unified CLI surface + perf mode  
**Prereq:** SMR-B3 ✅  
**Goal:** Add a `MODE` env var that exposes all existing sub-pipelines (assert, compare, perf) through one unified surface, add per-tick stopwatch timing (`PerfAvgTickMs`, `PerfMaxTickMs`, `PerfP99TickMs`, `PerfPeakEntities`) to `ScenarioRunResult`, write a `perf.json` artifact, and emit `ANOM-PERF-*` anomaly records when perf budgets are exceeded.

---

## Context

Read `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` (Wave 4.5 section) and `AGENTS.md` for background and turn-gate protocol.

All files in scope are under `WorldSim.ScenarioRunner/` and `WorldSim.ScenarioRunner.Tests/`. Do **not** touch `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.App`, or Java.

**SimStats.cs is out of scope.** Per the plan, headless perf timing stays entirely inside `WorldSim.ScenarioRunner`'s own `Stopwatch`. `WorldSim.Runtime/Diagnostics/SimStats.cs` is a future Track B epic triggered separately (Wave 5+).

### Key implementation facts about current `Program.cs` (post SMR-B3, 1039 lines)

- Top-level statements only — no class wrapper. Keep it that way.
- `ScenarioArtifactManifest` is a **positional `sealed record`** (lines 930–947). Adding fields requires updating the **one** construction site in `WriteArtifactBundle` (lines 229–246). The agent must update both.
- `ScenarioRunResult` is a positional `sealed record` (lines 883–921). Adding perf fields requires updating both the record declaration and `BuildRunResult` (lines 108–154).
- Existing env vars active at top-level (lines 16–26): `WORLDSIM_SCENARIO_SEEDS`, `WORLDSIM_SCENARIO_PLANNERS`, `WORLDSIM_SCENARIO_OUTPUT`, `WORLDSIM_SCENARIO_ASSERT`, `WORLDSIM_SCENARIO_ANOMALY_FAIL`, `WORLDSIM_SCENARIO_COMPARE`, `WORLDSIM_SCENARIO_DELTA_FAIL`, `WORLDSIM_SCENARIO_BASELINE_PATH`, `WORLDSIM_SCENARIO_CONFIGS_JSON`, `WORLDSIM_SCENARIO_ARTIFACT_DIR`.
- Exit codes already implemented: `0=ok`, `2=assert_fail`, `3=config_error`, `4=anomaly_gate_fail`.
- The inner tick loop is `for (var i = 0; i < config.Ticks; i++) world.Update(config.Dt);` — this is the loop the Stopwatch must wrap **per tick**.
- Compare sub-pipeline already gracefully warns (not exits 3) when baseline is absent **if** `WORLDSIM_SCENARIO_COMPARE=true` is standalone (line 85–92). `MODE=all` with missing baseline must follow the same graceful-skip: compare enabled but missing → log a warning, skip compare report, do not fail with exit 3.
- `peakEntities` minimum viable definition: `world._people.Count(p => p.Health > 0f)` + `world._animals.Count(a => a.Health > 0f)` + `world._colonies.Sum(c => c.Houses.Count + c.DefensiveStructures.Count)`. Do not use a more complex measure.
- p99 computation: pre-allocate `new List<double>(config.Ticks)`, collect per-tick elapsed ms, sort, index at `(int)(0.99 * list.Count)` after the run. No streaming approximation needed.

---

## What to implement

### 1. `WORLDSIM_SCENARIO_MODE` env var

Parse a new top-level string env var `WORLDSIM_SCENARIO_MODE`. Valid values (case-insensitive):

| Value | Meaning |
|-------|---------|
| `standard` | Default — no assert, no compare, no perf |
| `assert` | Sets `assertEnabled = true` (equivalent to `WORLDSIM_SCENARIO_ASSERT=true`) |
| `compare` | Sets `compareEnabled = true` (equivalent to `WORLDSIM_SCENARIO_COMPARE=true`) |
| `perf` | Sets `perfEnabled = true` |
| `all` | Sets `assertEnabled = true`, `compareEnabled = true`, `perfEnabled = true` |

**Backward compatibility rules:**
- If `WORLDSIM_SCENARIO_MODE` is absent or empty, behavior is determined by the individual bool env vars (exactly as before SMR-B4). No regression.
- `MODE` env var **adds to** the existing bools — it is not a replacement. If both `MODE=assert` and `WORLDSIM_SCENARIO_ASSERT=true` are set, the net result is `assertEnabled = true` (OR semantics).
- `MODE=all` without `WORLDSIM_SCENARIO_BASELINE_PATH` set: compare sub-pipeline gracefully skips (warning log, no compare report, no exit-3). This mirrors the existing standalone compare behavior.

### 2. `WORLDSIM_SCENARIO_PERF` standalone bool

Add `var perfEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PERF"), false)` alongside the other bools. `MODE=perf` maps to it. Both must activate the same perf pipeline.

### 3. `WORLDSIM_SCENARIO_PERF_FAIL` bool

Add `var perfFailEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PERF_FAIL"), false)`. When true, any `ANOM-PERF-*` anomaly causes exit code `4` (anomaly gate fail). When false (default), perf anomalies are recorded as evidence but do not change the exit code.

### 4. Stopwatch per-tick timing

Wrap the inner tick loop with a `Stopwatch` to collect per-tick elapsed time. The instrumentation must be **zero-cost when perf is disabled** — only allocate the list and start/stop the stopwatch when `perfEnabled == true`.

Pseudocode:

```csharp
List<double>? tickTimesMs = perfEnabled ? new List<double>(config.Ticks) : null;
long peakEntities = 0;

for (var i = 0; i < config.Ticks; i++)
{
    if (perfEnabled)
    {
        var sw = Stopwatch.StartNew();
        world.Update(config.Dt);
        sw.Stop();
        tickTimesMs!.Add(sw.Elapsed.TotalMilliseconds);

        var entityCount = world._people.Count(p => p.Health > 0f)
            + world._animals.Count(a => a.Health > 0f)
            + world._colonies.Sum(c => c.Houses.Count + c.DefensiveStructures.Count);
        if (entityCount > peakEntities) peakEntities = entityCount;
    }
    else
    {
        world.Update(config.Dt);
    }
}
```

Pass `tickTimesMs` and `peakEntities` into `BuildRunResult`.

### 5. New fields in `ScenarioRunResult`

Append to the existing positional record (after `LastTickDenseActors`):

```csharp
double PerfAvgTickMs,
double PerfMaxTickMs,
double PerfP99TickMs,
long PerfPeakEntities
```

In `BuildRunResult`:
- When `tickTimesMs` is null (perf disabled): all four fields = `0` / `0L`.
- When `tickTimesMs` is non-null and non-empty:
  - `PerfAvgTickMs = tickTimesMs.Average()`
  - `PerfMaxTickMs = tickTimesMs.Max()`
  - `PerfP99TickMs`: sort a copy, index at `(int)(0.99 * list.Count)`
  - `PerfPeakEntities = peakEntities`

Update `BuildRunResult` signature to accept `List<double>? tickTimesMs, long peakEntities`.

### 6. `ANOM-PERF-*` anomaly records

Add anomaly detection for perf budgets inside `DetectAnomalies` (or a new `DetectPerfAnomalies` helper called from there). Only run when `perfEnabled == true` and the run has non-zero perf data. Emit anomalies only for red-zone violations; yellow-zone violations are logged (warning text) but do not produce anomaly records.

Perf budget thresholds (from Combined-plan):

| Metric | Yellow | Red |
|--------|--------|-----|
| avg tick ms | > 4 ms | > 8 ms |
| p99 tick ms | > 8 ms | > 12 ms |
| peak entity count | > 5 000 | > 10 000 |

Anomaly ID format: `ANOM-PERF-TICK-AVG`, `ANOM-PERF-TICK-P99`, `ANOM-PERF-PEAK-ENTITIES`.

Each anomaly record uses the existing `ScenarioAnomaly` type:
- `Id`: e.g. `"ANOM-PERF-TICK-AVG"`
- `Category`: `"perf"`
- `Severity`: `"warning"` (perf anomalies are never hard errors)
- `RunKey`: `BuildRunKey(run)`
- `Message`: human-readable description
- `Value`: measured value as string
- `Threshold`: red-zone threshold as string

### 7. `perf.json` artifact

When `perfEnabled == true` and `artifactDir` is set, write a `perf.json` artifact. The file is an array of per-run perf summaries:

```json
[
  {
    "runKey": "default/Simple/101",
    "avgTickMs": 1.23,
    "maxTickMs": 4.56,
    "p99TickMs": 3.21,
    "peakEntities": 312,
    "budget": {
      "avgTickStatus": "green",
      "p99TickStatus": "green",
      "peakEntitiesStatus": "green"
    }
  }
]
```

Status values: `"green"`, `"yellow"`, `"red"`.

Write `perf.json` inside `WriteArtifactBundle`. Only write it when `perfEnabled == true`; skip otherwise (do not write an empty file).

### 8. Manifest perf fields

Add four fields to `ScenarioArtifactManifest` (after `CompareThresholdBreaches`):

```csharp
bool PerfEnabled,
int PerfRunCount,         // number of runs with non-zero perf data
int PerfRedCount,         // number of ANOM-PERF-* anomalies with severity "warning" and red threshold breach
int PerfYellowCount       // number of yellow-zone log events (not anomaly records — count them separately)
```

Populate in `WriteArtifactBundle`. For yellow-zone, track a counter during `DetectPerfAnomalies` and pass it through (e.g. as an out param or a return type). Keep this simple; a tuple return is fine.

**Remember:** `ScenarioArtifactManifest` is a positional record — update the **single** construction site in `WriteArtifactBundle` to include the new fields.

### 9. `EvaluateScenario` / `ResolveExitCode` perf wiring

Pass `perfEnabled` and `perfFailEnabled` into `EvaluateScenario`. After anomalies are collected, resolve the exit code with perf-anomaly awareness:

```csharp
var perfAnomalies = anomalies.Count(a => a.Category == "perf");
var anomalyGateFailed = (anomalyFailEnabled && nonPerfAnomalies > 0)
    || (deltaFailEnabled && compareFailures > 0)
    || (perfFailEnabled && perfAnomalies > 0);
```

This ensures perf anomalies only trigger exit code `4` when `WORLDSIM_SCENARIO_PERF_FAIL=true`.

---

## Acceptance criteria

1. **Zero regression** on all existing tests (`WorldSim.ScenarioRunner.Tests` passes fully, including `ArtifactBundleTests`, `AssertionEngineTests`, `ComparisonTests`).
2. Full solution builds with zero errors/warnings.
3. **MODE env var:**
   - `MODE=standard` (or unset): no perf timing, no assertions, no compare — identical to pre-SMR-B4.
   - `MODE=assert`: same result as `WORLDSIM_SCENARIO_ASSERT=true`.
   - `MODE=perf`: `perf.json` written to artifact dir, perf fields non-zero in run results.
   - `MODE=all` with baseline: all three sub-pipelines active; `assertions.json`, `compare.json`, `perf.json` all present.
   - `MODE=all` without baseline: compare gracefully skips (no `compare.json`, no exit-3), assert and perf still active.
4. **Perf fields in run results:** when `WORLDSIM_SCENARIO_PERF=true` (or `MODE=perf/all`), `PerfAvgTickMs > 0`, `PerfMaxTickMs >= PerfAvgTickMs`, `PerfP99TickMs <= PerfMaxTickMs`, `PerfPeakEntities > 0`.
5. **`perf.json` artifact:** written when `perfEnabled == true` and artifact dir is set; each entry has correct `runKey`, numeric fields, and `budget.avgTickStatus` ∈ `{"green","yellow","red"}`.
6. **`ANOM-PERF-*` anomalies:** only emitted for red-zone violations; category is `"perf"`, severity is `"warning"`.
7. **`WORLDSIM_SCENARIO_PERF_FAIL=true`:** when any `ANOM-PERF-*` anomaly exists, exit code is `4`.
8. **Manifest perf fields:** `perfEnabled`, `perfRunCount`, `perfRedCount`, `perfYellowCount` present in `manifest.json`.

### Required new tests in `WorldSim.ScenarioRunner.Tests/PerfModeTests.cs`

Follow the subprocess-harness pattern from `ComparisonTests.cs` (`RunScenarioRunner` + `FindRepoRoot` + `CreateArtifactDir` helpers — copy these into the new file, or extract to a shared `TestHelpers` static class if both files need them).

Use `WORLDSIM_SCENARIO_TICKS=8` for fast runs (same as all other tests).

| Test name | What it verifies |
|-----------|-----------------|
| `PerfMode_PerfJsonArtifact_IsWritten` | `WORLDSIM_SCENARIO_PERF=true`, artifact dir set → `perf.json` exists |
| `PerfMode_RunResults_HaveNonZeroPerfFields` | `WORLDSIM_SCENARIO_PERF=true`, read `summary.json` → first run's `perfAvgTickMs > 0` |
| `PerfMode_Disabled_PerfFieldsAreZero` | perf NOT enabled → first run's `perfAvgTickMs == 0.0` |
| `PerfMode_PerfFail_Returns4OnRedZone` | inject an artificial red-zone violation (patch `perf.json` or use a tiny map + many ticks to stress); or use `WORLDSIM_SCENARIO_PERF_FAIL=true` with a run and assert exit=4 if any red anomaly exists (can rely on a comment that the test may exit 0 on fast hardware — see note) |
| `ModeAll_ProducesCompatibleArtifacts` | use two-run pattern from `ComparisonTests.cs`: first run saves baseline → second run uses `MODE=all` with that baseline path → verify `assertions.json`, `compare.json`, `perf.json` all exist |
| `ModeAll_WithoutBaseline_GracefulSkip` | `MODE=all` without baseline path → exit code is `0` (not `3`), no `compare.json` written |
| `ModeAssert_EquivalentToAssertFlag` | `MODE=assert` exit code equals `WORLDSIM_SCENARIO_ASSERT=true` exit code for same seed |

**Note on `PerfMode_PerfFail_Returns4OnRedZone`:** Red-zone may not be triggered on fast hardware with only 8 ticks. It is acceptable to skip the assertion-on-exit-code part with `Assert.True(exitCode == 0 || exitCode == 4, ...)` and instead assert that `ANOM-PERF-*` anomalies in `anomalies.json` have `category == "perf"` when any are present. The structural wiring is what matters, not hitting the red budget in CI.

---

## Non-goals for this session

- Do NOT implement `WorldSim.Runtime/Diagnostics/SimStats.cs` — that is a future Track B epic.
- Do NOT add render/FPS timing — that is a future Track A epic.
- Do NOT implement `.github/workflows/` — that is SMR-B6.
- Do NOT implement SMR-B5 worst-run drilldown bundles — that is the next epic after this one.
- Do NOT change the existing assert/compare/anomaly logic — only add perf and mode wiring on top.
- Do NOT touch `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.App`, or Java.

---

## Implementation checklist

Work in this order to minimize the risk of positional-record mismatches:

1. Add `PerfAvgTickMs`, `PerfMaxTickMs`, `PerfP99TickMs`, `PerfPeakEntities` to `ScenarioRunResult` (record declaration + `BuildRunResult`).
2. Add `PerfEnabled`, `PerfRunCount`, `PerfRedCount`, `PerfYellowCount` to `ScenarioArtifactManifest` (record declaration + single construction site in `WriteArtifactBundle`).
3. Add `perfEnabled` and `perfFailEnabled` top-level bools (env vars).
4. Add `WORLDSIM_SCENARIO_MODE` parsing — sets the combination flags via OR semantics.
5. Instrument the tick loop with Stopwatch (zero-cost guard).
6. Add `DetectPerfAnomalies` and integrate into `DetectAnomalies` / `EvaluateScenario`.
7. Wire perf anomalies into `ResolveExitCode` / `anomalyGateFailed`.
8. Write `perf.json` in `WriteArtifactBundle`.
9. Populate manifest perf fields.
10. Build — fix any positional record errors before writing tests.
11. Write `WorldSim.ScenarioRunner.Tests/PerfModeTests.cs` with the 7 required tests.
12. Build + full test suite — zero regressions required.
13. Update `SMR-B4` status to ✅ in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`.
14. Add AGENTS.md cross-track note.

---

## Turn-gate protocol reminder

- Check prereq (`SMR-B3 ✅`) in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` before starting.
- On start: set SMR-B4 status to 🔄.
- On completion (build + tests green): set SMR-B4 status to ✅, add AGENTS.md entry, signal that SMR-B5 is unblocked.
