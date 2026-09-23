# SMR Lab v0.1 implementation outline

Status: Owner-approved project outline, 2026-09-16; Delivery must bind it to the
selected source/build in its normal `/seq-next` plan. No product implementation
has been started by this document. Scope: [Lab plan](SMR-Lab-v01-Plan.md).

## Step 1 — Track A planning, no second portfolio-planning pass

Use the approved scope, acceptance below and Combined side-slice row directly.
Choose a clean implementation worktree and compatible local web stack. State the
exact paths and any necessary solution wiring. Consult Track B only about runner
or artifact ambiguities; consult SMR about interpretation. Do not ask Meta to
rewrite the product brief. Meta performs the ordinary focused plan review.

Source fixtures available at preparation time:

- `.artifacts/smr/all-around-smoke-001`: older smr/v1, 27 runs;
- `.artifacts/smr/all-around-smoke-wave10-001`: newer smr/v1, 9 runs;
- the retained E11-H custom payload, if available, for the explicit adapter.

Recheck availability. Commit only small sanitized fixture examples; local raw
bundles remain outside Git. Missing local custom data permits a synthetic
structural fixture plus an explicit real-import limitation, not a simulation rerun.

## Step 2 — SMR-LAB-A: reader and browser

Build a UI-neutral reader inside the small Lab project; handle absent optional
fields, partial export, unknown format and bounded input sizes. Read real samples,
not computed curves from aggregate totals. Pair runs by actual configuration and
provenance, surfacing differences rather than silently treating names as identity.

Build bundle/run selection, failure/warning/skip views, available series/events,
comparison and short Markdown export. Keep missing data and acceptance status
visible. Existing artifacts cannot be mutated by browsing or export.

Acceptance:

1. Both old/new bundles are readable; optional missing fields remain UNKNOWN.
2. Same run key with different flags/horizon is not represented as identical.
3. Displayed chart samples agree with source values and show their interval.
4. A partial/corrupt bundle does not break the whole index or appear accepted.
5. Owner can identify settings, result, differences and limitations without
   terminal/JSON browsing. Browser interaction checks cover these flows.
6. Reviewed local closure of A permits B; no ecological gate is thereby closed.

## Step 3 — SMR-LAB-B: controlled manual core runs

Use the existing ScenarioRunner binary through a typed process adapter. Explicit
core settings and unique output directory; do not inherit ambient WorldSim/paid
configuration. Show combinations/ticks before launch. Record actual build origin
and effective config. Do not expose unwired balance-surface knobs as active.

One batch at a time; repeated browser submission of one start intent returns the
same local job. A small status record suffices. On host restart, first reconcile
known process/artifacts; never relaunch automatically. Show elapsed time rather
than a fabricated percentage when runner progress is not measurable.

Acceptance:

1. One deliberate start produces one process and one new artifact directory.
2. Refresh/repeated submission does not duplicate a batch or overwrite evidence.
3. Core settings are isolated; arbitrary shell input/out-of-root path and foreign
   origin run requests are rejected. Artifact text is rendered safely.
4. Exit 2/4 retain failed measurement results; process errors/partial data are
   distinguishable. Optional missing compare/perf is not a failure by itself.
5. One bounded real core smoke can be reopened in A. Same-config CLI parity
   compares semantic output, not timestamp/ID/performance byte equality.
6. The reviewed plan gives the smoke's exact run/tick/time budget and schedules
   it around active SMR workloads. It is not the expanded9/full45 acceptance gate.

## Step 4 — review, adoption and integration

Use focused parser/process tests and browser QA proportional to changed surfaces.
No full-world acceptance rerun for a read-only UI. If instrumentation or existing
runner behavior must change, name that dependency before implementation and use
the owning Track plus relevant regression checks.

Deliver a startup guide, evidence of A/B acceptance and a short Owner walkthrough.
Close source/integration under the current Git and Owner envelope; this outline
does not itself grant push/deploy or project-wide closeout effects.

Chief of Staff has already prepared the Combined/ops-checklist/inbox adoption.
Meta only reviews the specific Delivery plan/result and reconciles shared state
as the slice actually progresses. Do not add another generic adoption Epic.

## Completion boundary

Existing results are understandable, and a small manual simulation uses the
same Runtime and produces readable artifacts. No E11-H repair, full replay,
auto-tuning, baseline promotion, new AWC release or FAL feature is needed.
