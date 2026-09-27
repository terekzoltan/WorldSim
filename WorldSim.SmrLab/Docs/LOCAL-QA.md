# SMR-LAB-A local implementation evidence — 2026-09-23

Worktree: `C:/Users/ASUS/AppData/Local/Temp/opencode/WorldSim-smr-lab-a-20260923`  
Base: `5e24535ea3c078376d3b8028aa878e4dee1fc7ea` on `feature/smr-lab-a-20260923`  
Reviewed final plan: V2 `op-35aa93bf-c638-4341-af26-4977ba288762`; readiness clarification: `op-f5ffbddf-0965-49ba-abcb-58f0f27ca1db`.

## Real fixture provenance (read-only; not copied)

| Approved root | Source files checked | Automated Lab evidence |
|---|---|---|
| `C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001` | `manifest.json`, `summary.json`, `drilldown/index.json`, selected `standard-default_3fc58d82_goap_3fcd6560_202/timeline.json` and `events.json` | `smr/v1`, 27 displayed runs; older `visualLane` and `enableSiege` remain `UNKNOWN`; sample tick 1 people 72 food 0; tick 25 food 101; interval 25. |
| `C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001` | `manifest.json`, `summary.json`, `drilldown/index.json`, selected `default_37a8eec1_goap_3fcd6560_headless_af9bfb72_101/timeline.json` and `events.json` | `smr/v1`, 9 displayed runs; sample tick 1 people 24 food 0; interval 25. |

`WorldSim.SmrLab.Tests/ArtifactStoreTests.cs` verifies real reader imports,
run-specific samples and comparison; `BrowserEndpointTests.cs` verifies loopback
HTML/JS delivery, actual JSON responses for both selected timelines, config
comparison and Markdown response. These are **automated HTTP/UI-contract tests**,
not human/browser-rendered interactions. Neither manifest establishes a Git SHA:
provenance is `UNKNOWN`. The old selected run width is 192 and the newer one 64;
the comparison test checks that actual difference rather than assuming 128.

## Local commands and results

- `dotnet test "WorldSim.SmrLab.Tests/WorldSim.SmrLab.Tests.csproj" -m:1 --verbosity quiet`: **26 passed / 0 failed** after child-listener attribution; retains the R1-02–05 regressions, foreign-CWD guard, and real 27/9 import.
- `dotnet build "WorldSim.SmrLab/WorldSim.SmrLab.csproj" -c Debug -m:1`: **0 warnings / 0 errors** before the documented-port smoke.
- `dotnet build "WorldSim.sln" -m:1 --verbosity quiet`: **0 warnings / 0 errors** after R1 fixes.
- `node --check "WorldSim.SmrLab/wwwroot/lab.js"`: no syntax error.
- `node --test "WorldSim.SmrLab.Tests/report-selection.test.cjs"`: **1 passed / 0 failed** in a dependency-free DOM event harness; validates link invalidation and delayed-response suppression, **not** a human-rendered browser.
- `git diff --check`: no tracked whitespace errors; no staging or commit.

Focused synthetic test-owned artifacts exercise malformed/missing and unsupported
formats, mismatched run counts, missing/corrupt selected timelines, an oversized
summary, escape attempts, HTML/Markdown injection, same apparent run key with
changed flags/horizon, and a *synthetic structural* E11-H version dispatch.
They are generated under ignored Lab test output inside the execution worktree;
they are not evidence of real E11-H import.

## R1-06 direct-DLL launch evidence — 2026-09-26

- From the isolated worktree root, the README Debug build above was followed by
  `dotnet "WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001" --root "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001" --port 5217`.
  Loopback port 5217 was checked free **before** the launch. Owned child PID
  `38996` remained alive for the `GET /` HTTP **200** and `GET /api/bundles`
  HTTP **200** response with **27/9** runs; it was stopped and awaited by PID
  in `finally` (`exited=True`). No foreign listener was stopped.
- An initial smoke attempt reached both HTTP endpoints but its PowerShell
  wrapper incorrectly counted the JSON array as one bundle. Its owned child
  PID `22340` was stopped and awaited. Correcting only that wrapper's array
  enumeration produced the passing smoke above; Lab source and fixture data
  were not changed between attempts.
- `LabProcessLaunchTests` now repeats the direct-DLL command as owned child
  processes: a free test-selected port, live-child checks at both HTTP
  observations, bounded waits/diagnostics, and a separate foreign-CWD child
  whose nonzero exit and checkout-guard message are asserted. Each child
  is stopped/awaited only by its own test. Automated HTTP evidence does not
  replace the pending human-rendered browser observations.

## R1-06 child-listener attribution follow-up — 2026-09-26

- After the Meta FIX_RECHECK identified a conditional port-collision false-pass,
  the positive process test now waits **before HTTP** for its own redirected
  stdout to emit a complete `Now listening on: http://127.0.0.1:<selectedPort>`
  line. This post-bind signal belongs to the started child PID, is tracked
  independently of the 4096-character diagnostic buffer, and fails closed if
  absent, if that child exits or if the finite wait expires. HTTP 200 and the
  real 27/9 API response remain separately checked while that child is alive.
- A new test-owned listener holds the selected loopback port while a separate
  Lab child attempts to launch; that child cannot produce the owned-listener
  signal and exits nonzero. The listener and child are cleaned up only by their
  respective test. This collision test does **not** simulate a foreign server
  returning a forged 27/9 response while a Lab child is still alive; the
  mandatory stdout readiness gate precedes any HTTP in the positive test.
- Focused process tests **3/3**, full Lab tests **26/26**, JS syntax check,
  JS selection test **1/1** and solution build **0 warnings / 0 errors** passed.
  The earlier documented-port smoke remains launch evidence, not proof that
  its HTTP responses were child-attributed under a collision. Neither test nor
  smoke is human-rendered browser QA.

## Pending human browser observation (historical 2026-09-23 record)

At the time of this record, the manual browser walkthrough in `README.md` had
**not** been performed by a human in this API session. No visual readability or
actual human download/save claim was made. Before clean visual acceptance, the Owner/reviewer could open both
bundles and record actual rendered index/detail/chart/compare/report observations
using that checklist. Revalidate root safety and source files at that time;
missing real fixtures are an acceptance blocker, not replaceable by synthetic
tests. E11-H real payload import remains unverified. The later Owner-adopted
supervised observation and final disposition are recorded below. No remote CI
or integration proof exists for this local candidate.

## Bounded R1 repair evidence / remaining gate

- `LABA-R1-02`: missing/non-boolean assertion state is UNKNOWN with nullable
  counts and PARTIAL bundle; real legacy `passed:true, skipped:true` remains SKIP
  (COMB combat-disabled source convention), never PASS.
- `LABA-R1-03`: summary/per-run source contradictions for ticks, flags and people
  are PARTIAL with both source paths and the differing field; displayed summary
  values cite `summary.json#runs[index]` rather than the contradictory file.
- `LABA-R1-04`: Markdown now includes each selected run's completeness, assertion
  failures/skips, run-scoped vs unscoped bundle anomaly counts and bounded
  anomaly details, and people/food; missing anomaly evidence is UNKNOWN.
- `LABA-R1-05`: Left/Right changes clear the report link; a prior delayed Compare
  response cannot restore it. Node DOM-event test is automated, not visual QA.
- `LABA-R1-06`: host startup infers checkout from the launched Lab project/build
  layout and rejects a mismatched process CWD before reading roots. Tests exercise
  the current checkout and a **different, test-owned relocated checkout** with
  the same project/build layout, reject home/foreign CWD, reject relative roots,
  and show that an unselected root cannot be reached through opaque IDs. The
  two Owner-approved read-only external fixtures are selected explicitly for
  local proof; no temporary or machine-specific root is compiled into source.
  The earlier `dotnet run --project` README command was reopened by Meta after
  the Owner/coordinator reproduced its CWD failure. The supported command is now
  the direct-DLL invocation above; process-level regression and smoke evidence
  are recorded separately from the original guard unit tests. The
  child-listener proof gap found in the next Meta FIX_RECHECK is now
  addressed in the test as described above; final finding disposition still
  belongs to independent Meta review.
- `LABA-R1-01` (status at the time of this repair record): **OPEN** pending
  Owner/human observation of the two-bundle walkthrough and a saved report.
  Automated HTTP tests alone did not resolve this gate; the later Owner-adopted
  observation and independent Meta disposition are recorded below.

## 2026-09-27 Owner-adopted supervised Chrome observation — closeout addendum

Codex operated the real Chrome two-bundle walkthrough; the Owner watched the
actions and expressly adopted that supervised observation as acceptance evidence.
The server Markdown report was saved to the isolated worktree via PowerShell and
read back, and Meta inspected its source references and measured results. This
does not claim that the Owner clicked browser controls or personally opened the
report, or that a browser-native download completed; the latter is unverified.
Meta GREEN `op-b173d650-1819-4d18-bc1d-fc8b068bfc6e` resolved `LABA-R1-01`
for the frozen candidate, with `LABA-R1-02`–`LABA-R1-06` remaining fixed; Track A
acknowledged that synthesis with ACK_ONLY `op-01dacc17-1e0e-4384-ae60-8e4006281849`.
The generated report remains local-only and outside the closeout commit. This
addendum records the accepted evidence and attribution, not a new test run or
remote integration proof.

This evidence describes only read-only Lab A. Meta controls candidate review;
Lab B, simulation, E11-H and governance state are separate.
