# Workflow Runbooks

Purpose:
- provide reusable workflow runbooks for repositories that use a `Meta Coordinator + track agents + evidence analyst` operating model,
- keep the docs role-first instead of project-first,
- and make the workflow portable to other projects with similar multi-agent coordination.

Status:
- reusable template pack
- safe to copy to other repos after replacing the placeholder artifact names

## Included documents

- `Meta-Coordinator-Workflow-Runbook.md`
  - strategic coordination, plan review, gate decisions, lock notes, and cross-session truth maintenance.
- `Track-Implementation-Runbook.md`
  - implementation workflow for feature tracks or ownership lanes.
- `Evidence-Analyst-Runbook.md`
  - evidence-running and artifact-review workflow for simulation, benchmark, regression, or scenario-runner style roles.
- `Cross-Track-Handoff-Protocol.md`
  - handoff artifact types, status vocabulary, and copy-paste templates.

## Recommended core artifacts in any adopting repo

Before using these runbooks in another project, define the equivalent of these artifacts:

| Placeholder | Meaning |
|---|---|
| `[STATUS_DOC]` | central status board or note relay doc |
| `[MASTER_PLAN]` | main sequencing or roadmap document |
| `[TRACK_PLAN_DIR]` | folder that holds track- or epic-specific implementation plans |
| `[EVIDENCE_DIR]` | folder that stores reviewed evidence or summaries |
| `[ARCH_RULES_DOC]` | architecture or dependency-boundary policy |
| `[RUN_COMMAND]` | canonical evidence or benchmark command |
| `[QUALITY_GATES]` | required build, test, lint, perf, smoke, or replay gates |

If the target repo already has equivalent docs, keep their names and map the placeholders mentally.

## Suggested role model

Minimum reusable workflow:

1. `Meta Coordinator`
   - owns sequencing, verdicts, lock notes, and cross-track consistency.
2. `Track Agent`
   - owns one implementation lane and ships scoped changes plus verification.
3. `Evidence Analyst`
   - runs the right evidence profile, reads artifacts, and reports findings without over-claiming ownership of product decisions.

Optional extra roles:

- manual test helper
- combat/domain coordinator
- performance profiler
- balance or tuning lane

## Adaptation checklist for another repo

1. Rename the placeholders in all four docs.
2. Decide what the canonical truth hierarchy is.
3. Decide what state vocabulary you want to standardize.
4. Decide what artifacts are mandatory before a milestone can be marked done.
5. Decide where lock notes live.
6. Decide how cross-track notes are relayed.
7. Decide what the minimum re-entry bundle should be after context compression or a new session.

## Recommended minimum re-entry bundle

If a session must regain context quickly, start with:

1. `[STATUS_DOC]`
2. `[MASTER_PLAN]`
3. the relevant track plan or follow-up plan
4. the most recent evidence summary if the issue is evidence-driven
5. the relevant runtime or architecture boundary doc if ownership is ambiguous

This bundle is intentionally small. It is a re-entry aid, not the main purpose of the runbooks.
