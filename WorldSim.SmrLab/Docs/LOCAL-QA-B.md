# SMR-LAB-B local QA and Owner-delegated observation

Status: **THREE REAL PROCESS SLOTS CONSUMED; CLI SEMANTIC PARITY MATCH;
OWNER-AUTHORIZED DELEGATED VISUAL QA RECORDED; LABB-R1/R2 FIX RECHECK PENDING**.
Implementation tests used fake children; real process results come from journals
and bundles, while browser observations come from the separately attributed
2026-09-30 Codex Computer Use note. This is evidence, not product acceptance.
Resource-release receipt provided in the B implementation routing: E11-H two-case
measurement and its fix Release build plus 12/12 focused tests completed; E11-H
Meta FIX_RECHECK is READ_ONLY. This receipt permits B local builds/tests, not an
invented browser observation. See [manual-run instructions](MANUAL-RUNS.md).

## Owner report versus retained Lab evidence

Owner's explicit 2026-09-28 answer: refreshing the first job retained its
identity `e272d3fc186f480b90dd4d549f3950bc`; the second Start
`5e5238c09101490a969ee9e0f23968a2` was **intentional**, not caused by that
refresh. That earlier personal report supplied no browser/version, actual
observation time, elapsed/running display, or chart-to-timeline tick/value.
The Owner separately approved the **delegated** Codex In-app Browser check below
on 2026-09-30 as a substitute for the personal visual check; it does not turn
the earlier refresh report into a 2026-09-30 refresh test. Port/CWD and effective
preview are not claimed as personally Owner-observed; the execution checkout and
configuration below come from local preflight and artifacts. No new Start is
authorized.

Two separate first-party journals (paths relative to this worktree):

| Lab job | Journal and process | Retained published bundle |
|---|---|---|
| First, parity target `e272d3fc186f480b90dd4d549f3950bc` | `.artifacts/smr/lab-b-manual/.jobs/e272d3fc186f480b90dd4d549f3950bc.json`; created 2026-09-28 15:11:26.9808254Z; PID 18624; `EXITED`, exit 0, `COMPLETE` | `.artifacts/smr/lab-b-manual/published/e272d3fc186f480b90dd4d549f3950bc/`; bundle ID `r2-52DFF64BE1DD` |
| Second, intentional `5e5238c09101490a969ee9e0f23968a2` | `.artifacts/smr/lab-b-manual/.jobs/5e5238c09101490a969ee9e0f23968a2.json`; created 2026-09-28 15:13:23.3680046Z; PID 7580; `EXITED`, exit 0, `COMPLETE` | `.artifacts/smr/lab-b-manual/published/5e5238c09101490a969ee9e0f23968a2/`; bundle ID `r2-A1961AE30343` |

Both bundles contain `manifest.json`, `summary.json`, one
`runs/lab_small_fd99d3f9_simple_3fee95da_headless_af9bfb72_101.json`,
`assertions.json`, `anomalies.json`, and retained drilldown. Each manifest is
`smr/v1`, totalRuns 1, exit 0 / `ok`, 0 assertion failures, **3 skips**, 0
anomalies, Headless, with drilldown every 10 ticks. Summary/per-run configuration:
core `lab_small`, Simple, seed 101, `standard`, 32x20, initialPop 12, 40 ticks,
Dt 0.25; combat/diplomacy/stone/predator-human attacks OFF, siege ON, birth and
movement multipliers 1. First-bundle source drilldown
`drilldown/lab_small_fd99d3f9_simple_3fee95da_headless_af9bfb72_101/timeline.json`
has file-derived tick 10 people 12 / food 5 and tick 40 people 12 / food 24.
These file-derived values alone are **not** browser observations; see the
separately attributed delegated visual check below. In both bundles, COMB-01,
COMB-02 and COMB-03 are `skipped: true` with
`skipReason: combat_primitives_disabled`, not passed measurements. `anomalies.json`
is empty. Historical `manifest.artifactDir` points to private staging; the
published directory and journal bundle ID locate the retained Lab evidence.

## One additional Owner-authorized direct CLI control — completed

On 2026-09-28 the Owner explicitly authorized **one third real process** for
direct-CLI parity with the *first* Lab job: same 40-tick configuration and selected
DLL, 120 seconds maximum, no replacement or rerun. Read-only preflight verified
the first journal and bundle, the empty/unoccupied CLI output path, and the DLL
SHA-256. The selected binary was
`WorldSim.ScenarioRunner/bin/Debug/net8.0/WorldSim.ScenarioRunner.dll`, SHA-256
`725C69BF21A96EE410B8A96AEC1109839772FF2E92762E492400554E67E333ED`
(matching the first journal). Journal `effectiveEnvironment` has 17 keys, including
one **Pascal-case** `WORLDSIM_SCENARIO_CONFIGS_JSON` element for the configuration
above, `WORLDSIM_SCENARIO_LANE=core`, `SEEDS=101`, `PLANNERS=Simple`, `MODE=standard`,
assert/compare/perf/anomaly-fail/delta-fail/perf-fail `false`, JSON output,
Headless and drilldown top 1 / sample every 10. The direct `ProcessStartInfo`
used a clean environment with the journal's exact keys/values; only
`WORLDSIM_SCENARIO_ARTIFACT_DIR` changed to the new directory below. The binary
hash does **not** establish binary-to-source provenance, which remains UNKNOWN.

- Exclusive CLI artifact root: `.artifacts/smr/lab-b-cli-control-001/` (was absent
  before reservation; never overwritten). CLI PID **21044**, started
  `2026-09-28T16:15:48.9526917Z`, finished `16:15:49.7966650Z`, elapsed
  **863 ms**, exit **0**, no stderr; no replacement process.
- CLI sources: `lab-b-cli-control-001/manifest.json` (`smr/v1`, totalRuns 1,
  exit 0 / `ok`, 0 failures, **3 SKIPs**, 0 anomalies), `summary.json`,
  `runs/lab_small_fd99d3f9_simple_3fee95da_headless_af9bfb72_101.json`,
  `assertions.json`, `anomalies.json` (`[]`), and `drilldown/index.json` plus
  `drilldown/lab_small_fd99d3f9_simple_3fee95da_headless_af9bfb72_101/timeline.json`.
  CLI summary/per-run config agrees with first Lab bundle; COMB-01/02/03 are
  `combat_primitives_disabled` **SKIPs**, not successes.
- Offline comparison (Lab B's `ManualParity.Compare` path, no ScenarioRunner
  process):
  `dotnet WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll --parity-lab <worktree>/.artifacts/smr/lab-b-manual/published/e272d3fc186f480b90dd4d549f3950bc --parity-cli <worktree>/.artifacts/smr/lab-b-cli-control-001`.
  Result **`Semantic parity: MATCH`**, exit **0**, no semantic differences in
  manifest, summary, assertions, anomalies, per-run or drilldown JSON. Generated
  times/IDs/artifact destinations/performance timing are excluded by the existing
  comparator. This is CLI-to-*first-Lab* parity, not a browser chart observation.

## Owner-authorized delegated browser QA — 2026-09-30

**Source and provenance:** Owner explicitly selected
`C:/EGYETEM/FUNSTUFF/workflow-fix-temp/worldsim-lab-b-browser-qa-20260930/OBSERVATION.md`
as the delegated substitute for the pending personal chart/timeline check.
Source-note SHA-256: `2008915AF462066AA894C4B0522FBDFA7B6B8E644045272E5520D8CD11B2EF5C`.
Observer was **Codex using Computer Use**, not a human; the actual browser was
**Codex In-app Browser**, **not Chrome**. Observation time was approximately
**2026-09-30 18:53–18:58 Europe/Budapest**. The note reports inspected rendered
screenshots; this candidate-local record retains the observation and source
pointer, not screenshots or a claim that Track A operated that browser.

The note says the original host was not listening, so the existing compiled Lab
DLL was started at `http://127.0.0.1:5217/` with only
`--root <worktree>/.artifacts/smr/lab-b-manual/published --port 5217`.
There was **no build, `--run-root`, `--runner-dll`, Start, new simulation or CLI
control** in this delegated session. Read-only hash verification in the B worktree
matched the note: `WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll` SHA-256
`2F1947A87761F25A3C5AD3D2855279AE8C5773ED943AE1E2353DABAB50788666`;
the first bundle's `drilldown/lab_small_fd99d3f9_simple_3fee95da_headless_af9bfb72_101/timeline.json`
SHA-256 `0431A0865705B4E0B097DC7CAE410B1D7B20568FAEA1CD1F2BF8E81855CB3D4D`.
The note reports both pre-existing journal filenames remained and no new job.
Only the existing **first** bundle `e272d3fc186f480b90dd4d549f3950bc`
(`r2-52DFF64BE1DD`) was opened via its visible Open button and Inspect; the
detail panel showed failures 0, **skips 3**, timeline available, and explicitly
identified the source path. It identified `lab_small / Simple / seed 101`,
Headless, 32x20, 12 initial people, 40 ticks and Dt 0.25.

The delegated observer saw a horizontal **People** line and a rising **Food**
line (Food selected with the visible series dropdown). Both expanded five-row
source-pair tables matched the retained first-job JSON:

| Tick | People — browser table and timeline | Food — browser table and timeline |
|---|---:|---:|
| 1 | 12 | 0 |
| 10 | 12 | 5 |
| 20 | 12 | 12 |
| 30 | 12 | 16 |
| 40 | 12 | 24 |

Both series showed **sampling interval 10 ticks** and warned that first/final
samples can be irregular (here first tick 1 and last tick 40). The read-only
timeline hash and all five file-side tick/value pairs were independently
confirmed in the retained source. The delegated note calls this a PASS for
**this first-bundle chart/table/timeline consistency and visible sampling
annotation only**; independent Meta acceptance has not occurred.

**Limitations preserved for Meta:** the plot had no visible numeric axis labels;
exact values were checked in the rendered expandable source-pair table alongside
the lines, **not** via verified hover tooltips or independently labeled axes.
The browser capture returned no console warnings/errors during this check; it
was **not** a complete network audit or historical Chrome proof. At a narrow
**390x844** viewport the document had **scrollWidth 493**, so horizontal
overflow was observed; the desktop screenshot was readable, but this was **not
full mobile QA or a responsive-layout PASS**. The UI retained the three COMB
**SKIPs as skips** and source/build **UNKNOWN**, not proven binary-to-source
provenance. This delegated session did **not** test new Start, running/elapsed
UI, refresh while running, cancellation or restart; the Owner's earlier refresh
report remains separate. The source note says the host and selected tab were left
available at the end of its session; their current availability is not claimed.
No code, production fix, governance, lifecycle or publication action is implied
by the delegated observation.

The Owner accepted this bounded delegated check instead of a personal chart
inspection. With the previously completed CLI parity above, the B candidate
was handed to independent Meta `/step-review`, which identified LABB-R1/R2
technical findings. Meta must independently recheck the repair and limitations;
Track A does not self-declare GREEN. No further
Lab Start, CLI process or larger simulation is authorized.

## LABB-QA1 Owner disposition and deferred narrow-layout gate

Owner authorization revision `9f73da36868a91f48383690b8b2d51492c3275da56e4cad0050ee80a35691de8`
records the explicit 2026-09-30 answer **“Korlatot elfogadom”**. The missing
*actual desktop-browser* preview, running/elapsed and terminal process/artifact/
measurement-three-axis observation is an **Owner-accepted evidence limitation**,
not an observation that occurred and not a waiver of LABB-R1/R2. The retained
first and intentional second job journals, the Owner's earlier first-job refresh
report, synthetic fake-runner and UI-state checks, and the separately attributed
Codex Computer Use **In-app Browser** chart/table/timeline inspection above have
distinct provenance. Fake UI assertions cannot become witnessed browser results.
Meta independently decides whether this limited proof suffices at FIX_RECHECK.

LABB-LAYOUT1 remains deferred under **`SMR-LAB-UI-NARROW-LAYOUT-01`**: Track A,
under separate approval after B's desktop gate, must identify the node responsible
for the observed **390×844 viewport / scrollWidth 493** horizontal overflow,
then verify narrow-viewport width and content before any mobile-quality claim.
The overflowing node remains unknown; no responsive-layout PASS is asserted.

## Technical local candidate checks

Technical implementation checks previously performed in the isolated B checkout,
before the separate real jobs documented above:

- `dotnet build "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj" -c Debug -m:1`: PASS, 0 warnings/errors; builds the selected DLL without executing it.
- `dotnet test "WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj" -c Debug -m:1`: PASS, 42/42 including fake child, security, restart and existing Lab A regressions.
- `node --test "WorldSim.SmrLab.Tests/report-selection.test.cjs"`: PASS, 1/1.
- `node --check "WorldSim.SmrLab/wwwroot/lab.js"` and `node --check "WorldSim.SmrLab/wwwroot/manual.js"`: PASS.
- `dotnet build "WorldSim.sln" -c Debug -m:1`: PASS, 0 warnings/errors.
- `git diff --check`: no whitespace error (Git warns that checked-out LF may be normalized to CRLF).

The local checks and fake-child outcomes alone cannot replace the separate,
bounded delegated browser observation recorded above. That observation in turn
does not establish the untested lifecycle or full responsive-layout behaviors.
The push/PR workflow does not currently filter in the Lab test project. Remote
coverage revisit owner: Meta/CI workflow owner at B candidate review or an
authorized CI follow-up. No workflow or E11-H state is edited by B.

### LABB-R1/R2 repair check (no real process)

After ordinary `dotnet restore WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj`
succeeded, the build-enabled **Debug** test attempt could not copy the Lab DLL:
the still-running delegated read-only host (`dotnet` PID 29992, started
2026-09-30 18:56:07) holds `WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll`.
It ran **no tests**, was not interrupted, and is not reported as a green Debug
gate. A build-enabled **Release** `dotnet test WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj -c Release -m:1`
compiled Lab and tests and **executed 51/51 passing tests** after the repair,
including separate malformed-EXITED cases, valid/null-code controls, API
publication window, staged restart and legacy-journal controls. Node UI-state
and report-selection tests executed **3/3**; both Lab JS syntax checks passed.
These are synthetic checks, not desktop-browser lifecycle observations. The
subsequent Meta FIX_RECHECK accepted build-enabled Release as adequate for these
paths; the locked Debug attempt remains an unexecuted test, not a pass.

### LABB-R2-RECOVERY bounded follow-up (no real process)

Published-directory restart now applies the same retained request/settings
inspection used before staging publication. A synthetic, parseable one-run bundle
with seed 202 against a seed 101 journal remains retained but reconciles to
`UNCERTAIN` with a null bundle ID; a different intent gets 409 and no fake child
launches. The matching pre-field legacy published-journal control still opens
without rewriting its journal. After ordinary restore, a fresh build-enabled
Release focused Lab test run executed **52/52 PASS**; the existing Node UI and
report-selection tests executed **3/3 PASS** and both Lab JS syntax checks passed.
This is local synthetic proof for Meta's next FIX_RECHECK, not a new browser
observation or product acceptance. The delegated Debug host was not interrupted.

### LABB-R2-RECOVERY typed-manifest follow-up (no real process)

`Inspect` now treats malformed JSON field type/shape or `Int32` range as unreadable
terminal evidence. Five synthetic published fixtures (numeric-string, null,
wrong-kind object, `Int32` overflow and wrong-kind array) each first indexed via
`ArtifactStore` as `smr/v1` with exactly one run, then recovered as `UNCERTAIN`
with no bundle link, no replay and byte-identical retained files. Existing
matching legacy and parseable config-mismatch controls remain in the focused
suite. Ordinary restore and fresh build-enabled **Release** Lab tests passed
**57/57**; Release Lab build had **0 warnings/errors**; Node tests passed
**3/3**, and both Lab JavaScript syntax checks passed. This is synthetic
FIX_RECHECK evidence only; the Debug host was not interrupted and no additional
real process or browser lifecycle observation occurred.

## B candidate hash snapshot for independent review

This table is the **historical pre-fix snapshot**, not the repaired R1/R2 candidate.
Candidate `smr-lab-b-20260930-delegated-qa-v1` was the **uncommitted** B change
set in `feature/smr-lab-b-20260927`, base/HEAD
`8a6ba4d5325e541d851c33c7f86c9804cecd1502`, reviewed main-plan revision
`op-9cd560d2-817b-42dc-9a72-4403eb2be4b0`. SHA-256 values below are the
pre-fix code/test file bytes (tracked modifications and new B files), not
a Git commit or a claim that the mutable worktree is protected. The two B-local
docs are part of this candidate; their final hashes are recorded in Track A's
evidence handoff rather than self-referenced here. Use the repaired-candidate
handoff hashes for FIX_RECHECK instead of this historical table.

| B-owned code/test path | SHA-256 |
|---|---|
| `WorldSim.SmrLab/LabHost.cs` | `BD070CD369559046831239CDD12F694659E7A96EB83790342197476BB451104F` |
| `WorldSim.SmrLab/Program.cs` | `5BCE1181B3852E266998C709E388C097D91FDCDD079D26A4FD4D06396629120A` |
| `WorldSim.SmrLab/wwwroot/index.html` | `BBC4822C766C66A8446779497F8B877070761CCA9E492BDC532EA43CD57B95D8` |
| `WorldSim.SmrLab/wwwroot/lab.css` | `BC1BA4A0AD3ED0F518E575A32DEEF8B7E809AFDFFC3C5886ADA510D29DA9FFCD` |
| `WorldSim.SmrLab/wwwroot/lab.js` | `4E1547B950B1FC6B5E6B7D9B4FDD44DC2EFE970DF4EF335DDC81799D06C32210` |
| `WorldSim.SmrLab/wwwroot/manual.js` | `B8450424646B901C898E58CB96B1F38C70FD7DAA9D2579801010D665ED7E3142` |
| `WorldSim.SmrLab/ManualRuns/ManualRunManager.cs` | `02ADF5E1C4B11484BE8F6B389DF0A63A84143F33BC0D3FC31D3A7ECE8A7889DA` |
| `WorldSim.SmrLab/ManualRuns/ManualRunContract.cs` | `E2CF93AE90EF5A7E718BE2AD8349734B4D369FD9A963218DAEE37EB2F0A36761` |
| `WorldSim.SmrLab/ManualRuns/ManualParity.cs` | `DF16D386B1A01DF98B156BE0E259BC414D478D6ABAEBEB02681743ACB1EE5C5E` |
| `WorldSim.SmrLab.Tests/ManualRunTests.cs` | `95E6C7DCE6CD7012647E078D1E0B07F773170CE707D8C8F76D6AB7CBF38F8296` |
