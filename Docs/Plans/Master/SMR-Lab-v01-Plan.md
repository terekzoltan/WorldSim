# SMR Lab v0.1

Status: Owner-approved direction and project planning adoption, 2026-09-16.
Integration status: PLANNED, not implemented; no lifecycle dispatch by this PR.
Delivery: Track A (`worldsim.track-a`), with bounded Track B / SMR consultation.
Review and product acceptance: WorldSim Meta. No implementation or send occurred
as part of this planning adoption.

## Goal and settled decisions

A local browser application lets the Owner and SMR Analyst inspect existing
simulation results, compare cases and manually launch small new core runs using
the existing ScenarioRunner. An in-game graphical sandbox is a later extension.
This is a separate WorldSim tooling slice, not E11-H repair or E11-I completion.

Owner approved the September 13 planning package and asked Chief of Staff to
prepare the project plans, sequencing, ownership and handoff. Meta need not
rewrite this package or conduct a separate portfolio-planning ceremony. Normal
Delivery plan review and independent implementation review remain in place.

Sequence authority: [Combined](Combined-Execution-Sequencing-Plan.md), section
`SMR Lab v0.1 — independent tooling slice`. Detailed delivery checklist:
[Implementation plan](SMR-Lab-v01-Implementation-Plan.md).
Future diagnostic assignments use [Diagnostic loop](SMR-Diagnostic-Loop.md).

## Delivery slices

| ID | Deliverable | Prerequisites | Acceptance / unlock |
|---|---|---|---|
| SMR-LAB-A | Artifact browser, comparison, sampled charts, report export | Approved direction; isolated worktree; available Track A and artifact fixtures | Source-faithful viewer reviewed and locally closed; unlocks B |
| SMR-LAB-B | Manual core-run form and existing-runner process adapter | A accepted; bounded smoke budget in reviewed plan; nonconflicting runner/build and machine capacity | One manually launched batch produces a readable bundle; CLI semantic parity; closes v0.1 only |

Both use the usual Delivery/Meta lifecycle and short-lived branches under the
current Git policy. A can be used before B is complete. Track A is accountable
for the new Lab host, artifact reader and UI. Track B consults on runner/process
semantics; it owns changes to existing ScenarioRunner/Runtime surfaces if any
prove necessary. SMR consults on measurement semantics, not every UI edit.

## A — artifact browser

- Open Owner-selected local artifact roots; list bundles and runs.
- Show source/build provenance when present, seed, planner, config, horizon,
  assertions, anomalies, skips and unknowns. UNKNOWN is never zero or PASS.
- Sort by actual failures and selected metrics; no new opaque health score or
  acceptance thresholds. Keep measured outcome separate from Meta acceptance.
- Plot actual sampled population/food/ecology series with sampling interval;
  show events. If a series was not retained, display that limitation.
- Compare two runs and show configuration/source differences. Exploratory
  comparison is allowed; changed seed AND planner cannot prove planner causality.
- Export a short Markdown report with observations, source references and
  remaining uncertainty. Existing artifacts are read-only.

Support ordinary `smr/v1` bundles and one small explicit adapter for the
`e11h_smr_recruitment_mortality_v1` diagnostic payload when it is available.
The current E11-H test harness is not a standard ScenarioRunner bundle.
A README alone is a document, not raw measurement data. Import a retained
payload once if needed; do not invent values from prose or rerun the candidate.

The reader tolerates optional fields absent from older `smr/v1` bundles and
labels incomplete bundles PARTIAL. Unknown formats receive a clear explanation.
No universal schema migration or large analyzer framework is required.

## B — manual runs

- Choose an explicit core profile, seed/planner, tick bound and supported config.
  Default: one seed, one planner, one config. Show total combinations/ticks.
- Run the existing selected ScenarioRunner build with process-scoped settings;
  record effective configuration, build origin and a fresh artifact directory.
- One Lab batch at a time. No scheduled matrix, external paid/LLM/Refinery lane,
  gameplay tuning, automatic acceptance or baseline promotion.
- Show running/completed/process-error/partial/unknown separately from assertion
  results. Exit 2/4 may contain useful failed measurements rather than a crash.
- Browser refresh or duplicate submission must not launch another process.
  Host restart exposes uncertain/interrupted work without automatic replay.
- No headless pause/resume in v0.1. Existing GameHost pause/speed/single-step
  controls are not a ScenarioRunner process API.

Manual exploratory runs do not bypass existing project acceptance restrictions:
no E11-H expanded9/full45, rejected-candidate reconstruction, paid runs or
baseline promotion is granted here. The reviewed B plan supplies its own small
tooling-test budget; new scientific investigations have their own scope.

## Minimal architecture and implementation surface

Suggested new surfaces: `WorldSim.SmrLab/**`, `WorldSim.SmrLab.Tests/**` and
necessary solution wiring. A local .NET host plus a small browser UI fits the
current stack; Track A selects compatible packages in its implementation plan.
The reader/view model remains UI-neutral inside the Lab project. No database,
second simulation engine, new generic framework, FAL action or AWC command.

The host binds loopback, checks local origins for run requests, constrains file
access to selected roots, uses argument-list process invocation and typed config,
and escapes artifact text. Artifact absolute paths cannot override local roots.
Use bounded reads; do not load every raw log into each view. No arbitrary shell
command input. A simple local run identifier/status record is sufficient.

Runtime and ScenarioRunner remain the source of simulation behavior. The Lab
does not send OpenCode lifecycle commands; FAL continues to carry agent work.
Dirty/unknown build provenance can be shown for exploratory use but must not
look like an accepted baseline. Older results without Git SHA remain UNKNOWN.
`replay.json` is currently metadata, not a complete world-state replay stream.

## Parallelism and state ownership

E11-H diagnostic closeout and the subsequent bounded investigation may proceed
independently. Lab A may use completed artifacts on an isolated worktree while
they run. Do not read a changing bundle as a finalized result.

Lab B test batches share CPU/memory with SMR: agree a run window instead of
pausing agent sessions. Changes to common runner/schema/build/governance files
are reconciled at a stable boundary. Agents never interrupt another session.
Meta review requests sharing the same Meta session are serialized normally.

Keep the E11-H frontier and Lab frontier distinct in the single project state;
do not replace the primary next-action line with the Lab's next action. No
separate per-track state registry is needed. Only scoped accepted changes enter
integration; unrelated dirty tests and plans stay outside the candidate.

Chief of Staff prepares planning and maintenance recommendations. The
orchestrator handles addressed delivery. Meta owns independent review, evidence
classification, acceptance and necessary reconciliation of shared state.

## Deferred

In-game Lab, complete replay, live multi-world mosaic, detailed actor inspection,
automatic hypothesis generation/tuning, general scorecard engine and baseline
management. None is required to finish A+B. E11-I and E11-J gates are unchanged.

This approved scope selects useful parts of the historical Balance Lab inbox;
it does not activate every BL-0..6 proposal or old tuning defaults.

## Evidence references

- [SMR minimum operations](SMR-Minimum-Ops-Checklist.md)
- [Evidence review protocol](SMR-M2-Evidence-Review-Protocol.md)
- [Historical Balance Lab proposal](../../Ideas/Meta-Ideas-Inbox.md)
- `WorldSim.ScenarioRunner/Program.cs`: settings, bundle layout, sampled drilldown.
- `WorldSim.ScenarioRunner.Tests`: bundle, comparison and drilldown coverage.
- `WorldSim.App/GameHost.cs`: existing graphical manual controls.
- September 16 observation: stable WorldSim orchestrator reports diagnostic
  `ACK_ONLY`, no pending server work and closeout still pending. This is planning
  context, not a claim that the diagnostic package or E11-H has been closed.
