# SMR Lab A — local read-only artifact browser

Scope: SMR-LAB-A only. This host does not launch ScenarioRunner, edit evidence,
promote baselines, or decide Meta acceptance. `replay.json` is metadata, not a
world-state stream. The E11-H custom payload is **real import unverified**: no
approved raw E11-H payload is available in this worktree. A README is not data.

## Start from the isolated worktree

Use `C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-a-20260923`
as the process working directory **for this assignment**. Startup infers the
checkout from the launched `WorldSim.SmrLab/bin/{configuration}/{target}` build
and requires process CWD to equal that checkout before reading artifacts. A
binary built in this isolated checkout cannot authorize a session-home CWD.
No temporary execution-root or machine-specific artifact path is compiled into
the Lab. The local operator explicitly selects absolute, read-only `--root`
paths; no browser control or artifact metadata can select an additional root.
For this assignment, select **only** these two Owner-approved external fixtures:

```powershell
dotnet build "WorldSim.SmrLab/WorldSim.SmrLab.csproj" -c Debug -m:1
dotnet "WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001" --port 5217
```

Check that loopback port 5217 is free before starting the second command; if it
is occupied, report the conflict rather than stopping a different listener.
Open `http://127.0.0.1:5217/`. Stop only the Lab process that you started.
The host does not browse the rest of the primary
checkout. The original artifacts and `run.log` stay untouched; do not copy raw
bundles or logs into Git. To inspect a synthetic fixture, create it **inside**
the isolated worktree and pass that contained directory as `--root`.
The Markdown export is generated in memory; if you save its browser download
during this assignment, select a destination inside the isolated worktree.

Root validation rejects relative roots, reparse points and paths supplied by
artifacts; a root outside the checkout must be explicitly selected at startup
under the applicable Owner scope. Browser calls use opaque IDs. The reader caps
roots at 8, bundles at 32 per root, runs at 128,
samples at 512 per retained timeline, events at 1024, JSON depth at 32, each
input file at 4 MiB and cumulative read volume per index/detail operation at
32 MiB. Unsupported formats and incomplete bundles are labeled, not treated as
successful runs. A selected timeline is available only for drilldown-indexed
runs; other runs say **not retained**. Intervals come from the manifest; ticks
and plotted values come only from `timeline.json`.

## Verification and provenance

Run only the Lab tests and build in the isolated worktree:

```powershell
dotnet test "WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj" -m:1
node --test "WorldSim.SmrLab.Tests/report-selection.test.cjs"
node --check "WorldSim.SmrLab/wwwroot/lab.js"
dotnet build "WorldSim.sln" -m:1
```

These tests read the two real bundles in place and generate small synthetic
partial/corrupt/injection examples under the Lab test output directory. They
run **no simulation**. The real roots were observed as `smr/v1`: older 27 runs,
newer 9 runs; both have a selected drilldown and 25-tick sampling. The older
selected `standard-default_3fc58d82_goap_3fcd6560_202` starts at tick 1,
people 72, food 0, then tick 25, food 101. The newer selected
`default_37a8eec1_goap_3fcd6560_headless_af9bfb72_101` starts at tick 1,
people 24, food 0. Recheck `manifest.json`, `summary.json`,
`drilldown/index.json`, and the selected `timeline.json` in both roots **at
usage time**. If either is absent, unsafe or changing, stop and report the
real-import acceptance blocker; synthetic tests cannot substitute for it.

The two manifests do not establish a Git SHA. Source/build identity therefore
displays `UNKNOWN`. Old absent fields such as `visualLane` and `enableSiege`
also display `UNKNOWN`, never zero or PASS. The Lab displays actual assertion
failures, skips and anomalies separately from bundle completeness and Meta
acceptance; an exit code alone does not promote a run.

## Human browser QA checklist (record observations before clean visual closure)

This checklist is **not** a claim that a person has completed the walkthrough.
Automated HTTP/UI-contract tests exercise routes, page delivery, data and report
responses, but do not verify rendered browser interaction or visual readability.

1. After launching from the isolated checkout with the command above, open both
   configured bundles. Record observer name/date/browser, observed index counts
   (27/9), status,
   readable runs and older missing fields showing `UNKNOWN`.
2. Inspect the older selected `standard-default...goap...202` and newer selected
   `default...goap...headless...101`. Choose People and Food. In each browser
   chart confirm tick/value against the corresponding retained source timeline,
   check the **25 ticks** interval label, and record the exact tick and value.
   Check an unselected run says **not retained** rather than drawing a flat line.
3. Set Left and Right across bundles; compare displayed config, seed, planner,
   horizon, flags and source/provenance. Record differences and UNKNOWN source
   Git SHA. If both seed and planner change, check the causal caveat.
4. Save the Markdown report inside the isolated worktree. Check both root-relative source references,
   bundle completeness, actual assertion failures/skips, run/bundle anomalies,
   measured people/food, differences, measured-vs-accepted wording and remaining
   uncertainty. Change either selection after Compare: the prior download must
   disappear until a successful comparison of the new pair. Inspect
   partial/unknown-format and escaped-text examples through focused tests; do
   not paste untrusted artifact text into the DOM as HTML.
5. Record date, browser, tested source paths, observed values, pass/fail per
   action and any limitation in the **isolated worktree** Lab QA handoff. Mark
   this checklist pending until a human actually performs it.

Local automated proof and later manual observations remain separate from remote
CI/integration proof. Track A submits a frozen Lab-only candidate to independent
Meta `/step-review`. Acceptance of Lab A alone may unlock Lab B; it changes no
E11-H or ecological gate.
