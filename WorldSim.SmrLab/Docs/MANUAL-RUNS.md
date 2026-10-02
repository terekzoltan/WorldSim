# SMR Lab B — bounded manual core runs

This is the B-only operator surface; [Lab A's README](README.md) documents its
already accepted, read-only browser and historical evidence. The process CWD for
every command here is **`C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-b-20260927`**.
Use the approved isolated worktree; the session home is not an execution directory.
The E11-H CPU/build window was released before the local builds and real runs.
The original budget was **one Lab Start and one direct CLI control**, 40 ticks
each, with a 120-second limit per process. On 2026-09-28 the Owner confirmed
that a second Lab Start was intentional (not a refresh duplicate) and explicitly
authorized **one third process**, the direct CLI control of the *first* Lab job.
All three slots have now been consumed: two retained Lab jobs and one CLI control.
No further real run, replacement, browser Start or CLI rerun is authorized.
The Owner accepted a separate **Codex Computer Use / Codex In-app Browser**
observation on 2026-09-30 as a substitute for the pending personal chart check;
the exact evidence, caveats and candidate snapshot are in
[LOCAL-QA-B.md](LOCAL-QA-B.md). Independent Meta review is still required.

## Historical host startup after resource release

The build commands compile binaries; they do not execute ScenarioRunner. Confirm
port 5217 is free; do not stop another listener. The two `--root` values are the
existing explicitly selected, read-only Lab A artifacts. B's local evidence
lives under `.artifacts/smr/lab-b-manual/` and the separately reserved
`.artifacts/smr/lab-b-cli-control-001/` in this checkout. This historical
startup command is for inspecting retained bundles; **do not press Start**.

```powershell
dotnet build "WorldSim.ScenarioRunner/WorldSim.ScenarioRunner.csproj" -c Debug -m:1
dotnet build "WorldSim.SmrLab/WorldSim.SmrLab.csproj" -c Debug -m:1
$occupied = Get-NetTCPConnection -LocalPort 5217 -State Listen -ErrorAction SilentlyContinue
if ($occupied) { throw 'Port 5217 is occupied; stop here without interrupting its owner.' }
dotnet "WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001" --run-root "C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-b-20260927/.artifacts/smr/lab-b-manual" --runner-dll "C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-b-20260927/WorldSim.ScenarioRunner/bin/Debug/net8.0/WorldSim.ScenarioRunner.dll" --port 5217
```

The Lab accepts only `core`, one seed/planner/config, 10..120 ticks, and
`standard|assert`. For the acceptance slot use `lab_small` (32×20, 12 people),
seed 101, Simple, standard, **40 ticks**, Dt 0.25. Both presets fix combat,
diplomacy, stone building and predator-human attacks OFF, siege ON, birth/movement
multipliers 1; Headless, drilldown top 1, sample interval 10. The typed adapter
serializes `ScenarioConfig` with Pascal-case names; the nonempty config supplies
ticks/Dt. Scenario assert/compare/perf/fail controls are explicitly false in
standard mode; compare/baseline/perf and ambient AI/refinery configuration do
not enter the child. The journal contains the actual effective environment,
checkout HEAD, DLL hash and process/terminal status; the selected DLL path is
specified at Lab startup and recorded in LOCAL-QA-B.md. A Git SHA alone
does not prove binary-to-source provenance; mark that relationship UNKNOWN
unless separately established.

The selected Lab A roots are read-only. Runner output first goes to the private
`.staging/<job-id>/` directory. Only a terminal, reconciled, readable bundle
is moved under `published/<job-id>/` for A. Missing journal, uncertain live
child or spawn/PID crash gap blocks new starts rather than relaunching. Exit
2/4 may retain failed measurements; exit 3 may still have artifacts. A process
exit, artifact completeness and measured failures/skips/anomalies are separate.
The job also reports `publicationStatus`: `PENDING` after a durable terminal
journal but before bundle settlement blocks a different Start; the same intent
returns its existing job without relaunch. `PUBLISHED` requires the verified
bundle ID before Open appears; `NO_BUNDLE` settles privately retained unreadable
or capacity-limited evidence without an Open link. `UNCERTAIN` blocks new starts
and offers no link. A host restart reconciles retained staging/published evidence
before opening Start, including journals written before this field existed;
`FinishedAt` by itself does not prove publication.
After 32 published directories, a new Start is refused before spawn; no evidence
is removed. For future work, only after separate authorization, select a new
*empty, Lab-owned* run root at startup and retain old published roots via
explicit read selection (eight-root Lab A limit).

## Owner-authorized delegated browser QA — recorded, bounded

The Owner reports that refresh retained first job
`e272d3fc186f480b90dd4d549f3950bc`; the second, separate job
`5e5238c09101490a969ee9e0f23968a2` was deliberately started. Their journals
show different intents and PIDs. The Owner's earlier report did not include a
personal chart comparison or browser/version/time; it is separate from the
2026-09-30 delegated observation. Codex used its **In-app Browser** (not Chrome
or a human-operated browser) at approximately **18:53–18:58 Europe/Budapest**.
It opened only existing first bundle `r2-52DFF64BE1DD` with the compiled Lab
host in read-only root mode (no build, run-root, runner-dll, Start or simulation),
viewed both People/Food lines and the expanded source-pair tables, and compared
ticks 1/10/20/30/40 and the 10-tick sampling annotation with the retained
timeline. The Owner explicitly accepts this delegated visual check in place of
the personal one. The plot lacked numeric axis labels; exact values came from
the rendered table, not verified tooltips. At 390x844, scrollWidth was 493
(horizontal overflow), so this is **not full mobile QA**. The delegated session
did not test Start, running/elapsed, refresh while running, cancellation or
restart. See LOCAL-QA-B.md for source hashes, exact values, console/network and
provenance limitations. The Owner separately accepted the missing actual desktop
browser lifecycle observation as an evidence limitation, not witnessed behavior;
LABB-R1/R2 still require technical FIX_RECHECK. The horizontal overflow is the
deferred Track A gate `SMR-LAB-UI-NARROW-LAYOUT-01` before a mobile-quality claim.
No new Start or further CLI control is authorized;
independent Meta review must assess the bounded evidence and layout caveat.

## Third and final slot: direct CLI control — completed

The 2026-09-28 Owner amendment permitted the direct CLI control before the
then-missing chart observation. It used the **same** selected Debug ScenarioRunner DLL
and the first job's exact `effectiveEnvironment`, changing only
`WORLDSIM_SCENARIO_ARTIFACT_DIR` to the new, exclusively reserved
`.artifacts/smr/lab-b-cli-control-001/` directory. PID 21044 exited 0 within
863 ms; the CLI bundle reports 0 assertion failures, 3 combat-disabled **SKIPs**
and 0 anomalies. The offline comparator reported `Semantic parity: MATCH` against
the first Lab bundle, exit 0. See LOCAL-QA-B.md for exact sources and limitations.
The following PowerShell 5.1 recipe documents the executed launch shape for
audit; **do not execute it again**. The final parity invocation below is offline
and does not launch a simulation.

```powershell
$work = 'C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-b-20260927'
$id = 'e272d3fc186f480b90dd4d549f3950bc'
$journal = Join-Path $work ".artifacts/smr/lab-b-manual/.jobs/$id.json"
$job = Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json
$parent = Join-Path $work '.artifacts/smr'
if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Expected B artifact parent is absent.' }
$cli = Join-Path $parent 'lab-b-cli-control-001'
if (Test-Path -LiteralPath $cli) { throw 'CLI output already exists; do not overwrite evidence.' }
$null = New-Item -ItemType Directory -Path $cli
$runner = Join-Path $work 'WorldSim.ScenarioRunner/bin/Debug/net8.0/WorldSim.ScenarioRunner.dll'
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Get-Command dotnet).Source
$psi.Arguments = '"' + $runner + '"'
$psi.WorkingDirectory = $work
$psi.UseShellExecute = $false
$psi.EnvironmentVariables.Clear()
foreach ($key in @('SystemRoot','WINDIR','DOTNET_ROOT','DOTNET_ROOT_X64','PATH','HOME','USERPROFILE','TEMP','TMP')) {
    $value = [Environment]::GetEnvironmentVariable($key)
    if ($value) { $psi.EnvironmentVariables[$key] = $value }
}
foreach ($property in $job.effectiveEnvironment.PSObject.Properties) {
    $psi.EnvironmentVariables[$property.Name] = [string]$property.Value
}
$psi.EnvironmentVariables['WORLDSIM_SCENARIO_ARTIFACT_DIR'] = $cli
$child = [Diagnostics.Process]::Start($psi)
if (-not $child.WaitForExit(120000)) { $child.Kill(); $child.WaitForExit(); throw 'CLI control timed out; no substitute run.' }
$child.ExitCode
dotnet "WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll" --parity-lab (Join-Path $work ".artifacts/smr/lab-b-manual/published/$id") --parity-cli $cli
```

The real manifest, summary, per-run path and parity result are recorded in
LOCAL-QA-B.md. The offline parity comparator ignores generated timestamps,
run IDs, artifact destinations and performance timing, but reports measured
metric/config/exit/assertion/anomaly differences. Any later discrepancy is an
unresolved gate, not a reason to launch a fourth process.

## Local candidate checks (no simulation)

```powershell
dotnet test "WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj" -c Debug -m:1
node --test "WorldSim.SmrLab.Tests/report-selection.test.cjs"
node --check "WorldSim.SmrLab/wwwroot/lab.js"
node --check "WorldSim.SmrLab/wwwroot/manual.js"
dotnet build "WorldSim.sln" -c Debug -m:1
```

These are **local** checks. The current push/PR `smr-headless.yml` filter does
not run Lab tests; Track A records local evidence and Meta/CI workflow owner
revisits remote Lab coverage at B step review or later authorized CI planning.
