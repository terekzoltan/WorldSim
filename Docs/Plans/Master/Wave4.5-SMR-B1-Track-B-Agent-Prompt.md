# Wave 4.5 SMR-B1 — Track B Agent Prompt

**Session type:** Track B implementation agent  
**Epic:** SMR-B1 — Artifact bundle contract + output directory layout  
**Prereq:** Wave 3.6 ✅ (W3.6-B4 ScenarioRunner already delivers multi-seed/config/planner JSON output)  
**Goal:** Promote the existing ScenarioRunner output into a durable, agent-grade **artifact bundle** with a stable directory layout, a machine-readable manifest, and structured per-run files so that later SMR epics (B2–B6) and future OpenCode/LLM sessions can consume outputs without ad hoc terminal scraping.

---

## Context

Read `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` (Wave 4.5 section) and `AGENTS.md` for background and turn-gate protocol.

**W3.6-B4 is SMR Phase 0** — it already works. This session adds the structural layer on top:
a predictable output directory with well-known filenames, a top-level manifest, and a runs subdirectory.
No new simulation logic is needed. All changes are in `WorldSim.ScenarioRunner`.

---

## What to implement

### 1. Artifact output directory

When `WORLDSIM_SCENARIO_ARTIFACT_DIR=<path>` is set, the runner writes a **bundle directory** instead of (or in addition to) stdout.

Directory layout:

```
<artifact_dir>/
  manifest.json          ← top-level metadata (schema version, run timestamp, config summary, exit code intent)
  runs/
    <configName>_<planner>_<seed>.json   ← one file per (config, planner, seed) combination
  summary.json           ← aggregated metrics across all runs (same shape as the existing stdout envelope)
  anomalies.json         ← placeholder for SMR-B2; write an empty array `[]` for now
  run.log                ← copy of stdout text output (always written even if JSON mode active)
```

**Schema version:** use `"smr/v1"` as the `schemaVersion` field in `manifest.json`.

### 2. `manifest.json` schema

```json
{
  "schemaVersion": "smr/v1",
  "generatedAtUtc": "2026-03-10T12:00:00Z",
  "runId": "<guid>",
  "seedCount": 3,
  "plannerCount": 3,
  "configCount": 1,
  "totalRuns": 9,
  "artifactDir": "<absolute path>",
  "exitCode": 0,
  "exitReason": "ok"
}
```

`exitCode` and `exitReason` are **populated by the runner at the end** of the run. For SMR-B1 this is always `0` / `"ok"` — SMR-B2 will add assertion-based exit codes on top.

### 3. Per-run files (`runs/<configName>_<planner>_<seed>.json`)

Each file contains the full `ScenarioRunResult` record serialized to JSON (indented, camelCase). Filename must be filesystem-safe: replace spaces and special chars with `_`, normalize to lowercase. Example:

```
runs/default_simple_101.json
runs/default_goap_101.json
runs/medium-combat_htn_303.json
```

### 4. `summary.json`

Same structure as the current `ScenarioRunEnvelope` (the existing `--output=json` envelope), written to `summary.json`. Also still written to stdout if `WORLDSIM_SCENARIO_OUTPUT=json` is set — the artifact dir does not replace stdout, it's additive.

### 5. `anomalies.json`

Stub only for SMR-B1. Write `[]` (empty JSON array). SMR-B2 will populate this with assertion results.

### 6. `run.log`

Capture all text that would normally go to stdout (both the text-mode matrix lines and any JSON) and write it to `run.log` in the artifact dir. This gives a plain-text audit trail even when the caller is consuming stdout as JSON.

### 7. Env var contract

| Env var | Purpose | Default |
|---------|---------|---------|
| `WORLDSIM_SCENARIO_ARTIFACT_DIR` | If set, write bundle to this path (create if missing) | not set (no bundle written) |
| All existing env vars | Unchanged | Unchanged |

### 8. Exit code policy (stub for SMR-B1)

For SMR-B1, always exit with `0`. Do **not** implement assertion logic yet — that is SMR-B2. Add a `return Environment.ExitCode;` (or equivalent top-level return) as a stub so SMR-B2 can set it.

---

## Acceptance criteria

1. When `WORLDSIM_SCENARIO_ARTIFACT_DIR` is **not** set, behavior is **identical** to current W3.6-B4 — zero regression on stdout output or test suite.
2. When `WORLDSIM_SCENARIO_ARTIFACT_DIR` is set:
   - `manifest.json` is written with correct `schemaVersion`, `runId` (non-empty GUID), counts, and `exitCode: 0`.
   - `summary.json` is written with the same content as the JSON envelope (same structure as `ScenarioRunEnvelope`).
   - `runs/` directory exists and contains exactly `configCount × plannerCount × seedCount` `.json` files.
   - Each per-run file deserializes back to a `ScenarioRunResult`-compatible object without errors.
   - `anomalies.json` exists and contains `[]`.
   - `run.log` exists and is non-empty.
3. New tests in `WorldSim.ScenarioRunner.Tests` (or a new test project if none exists) cover:
   - `ArtifactBundle_ManifestHasCorrectRunCount` — run with 2 seeds × 1 planner × 1 config, verify manifest `totalRuns == 2`.
   - `ArtifactBundle_PerRunFilesExist` — verify the `runs/` directory contains the expected filenames.
   - `ArtifactBundle_SummaryMatchesRunCount` — verify `summary.json` `runs` array length equals `totalRuns`.
   - `ArtifactBundle_AnomaliesIsEmptyArray` — verify `anomalies.json` deserializes to empty list.
   - `ArtifactBundle_NotWrittenWhenDirNotSet` — verify no directory is created when env var is absent.
4. Full solution builds with zero errors/warnings.
5. All existing tests pass (zero regressions).

---

## Non-goals for this session

- Do NOT implement assertion logic or exit code != 0 paths — that is SMR-B2.
- Do NOT add `SimStats` / perf timing — that is SMR-B4.
- Do NOT add baseline comparison — that is SMR-B3.
- Do NOT create `.github/workflows/` — that is SMR-B6.
- Do NOT touch `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.App`, or Java.
- Do NOT change existing stdout output format — the artifact dir is strictly additive.

---

## Implementation notes

- `WorldSim.ScenarioRunner` is a top-level `Program.cs` (no class structure). Refactor into helper methods as needed but keep the file self-contained — do not create a separate class library.
- If a test project for the runner does not exist (`WorldSim.ScenarioRunner.Tests`), create it as a new `xunit` project referencing the runner's output. Check the `.sln` file to confirm whether one already exists before creating.
- Use `System.Text.Json` (already imported) for all JSON serialization. Use `JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` for artifact files.
- Use `Guid.NewGuid().ToString("D")` for `runId`.
- Directory creation: `Directory.CreateDirectory(artifactDir)` (idempotent).
- `run.log` can be written by capturing `Console.Out` via a `TextWriter` wrapper, or by buffering lines in a `StringBuilder` during the run — either approach is fine.

---

## Turn-gate protocol reminder

- Check prereq status in `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` before starting.
- On completion: update SMR-B1 status to ✅ in the Combined-plan, add AGENTS.md cross-track note.
- Signal Meta Coordinator (via AGENTS.md entry) that SMR-B2 is unblocked.
