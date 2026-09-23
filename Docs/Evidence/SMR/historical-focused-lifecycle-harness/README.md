# Historical focused lifecycle harness

Preserved 2026-09-23 from the July 2026 uncommitted test extension. This is
diagnostic source evidence, not an accepted green regression or an executable
addition to default test discovery. No simulation was rerun for this archive.

## Exact preservation

- [Original test extension](focused-lifecycle-tests.patch): only
  `WorldSim.Runtime.Tests/Wave11AnimalLifecycleTests.cs`, +114/-0.
- Base Git blob: `e42bf0e606f0c5a0bdc2721b1832d5c650af3929`.
- Extended Git blob: `984fb06232adaad3edc954e8da3159652cbd3276`.
- Original local raw-file SHA-256:
  `c16fc7469a347e883add066a3f4f913f6114dc718fca8788fb102abb4b44a961`.
- The exact original file is also retained in the operator's private backup.
  The patch preserves every substantive local hunk; line-ending representation
  is normalized by Git, not represented as a behavior change.

The harness uses the real SimulationRuntime path, 64x40 world, population 24,
1200 ticks per case, and five seed/planner pairs with predator-human interaction
ON and the same five OFF. It requires both species throughout and no rescue or
replenishment. The ten cases total 12,000 ticks if explicitly authorized together.

## Known result and unchanged gate

The [July evidence](../wave11-e11-h-step5c4-seeding-focused-route/README.md)
records ON 4/5 and OFF 1/5: **RED**. These are historical measurements, not fresh
reruns or guarantees about another source version. The open focused-lifecycle
finding and E11-H expanded/full-matrix gates remain; preservation does not waive
them, weaken assertions, tune Runtime or turn a red result green.

These tests were never part of the committed baseline. Preserving their source
outside the compiled test tree prevents accidental packaging/discovery while
keeping the defect executable under a separately authorized investigation.
Do not add Skip/xfail, weaken thresholds, or infer that normal smoke CI covers
this ecological acceptance question.

## Future use

An accountable Track/SMR assignment must specify source, cases and run budget.
Use an isolated authorized worktree, compare the baseline blob and inspect
`git apply --check Docs/Evidence/SMR/historical-focused-lifecycle-harness/focused-lifecycle-tests.patch`
before applying. Stop on drift; do not force the patch or reconstruct the rejected
hunger-gate candidate. This artifact is the earlier baseline test harness only.
Any rerun records its own source/configuration and actual result; review and
publication remain separate. This document itself authorizes no execution.

## Separate sentinel plan

The [archived combat-intent plan](../../../Plans/Archive/Wave11-E11-H-Step5c4-Combat-Intent-Sentinel-Implementation-Plan.md)
describes an implementation already committed as `3e77d92`. It is not a new task
and does not resolve the independent species-continuity failure. The former
CombatPrimitivesTests dirty indicator had identical Git content to HEAD and
contained no additional implementation to publish.
