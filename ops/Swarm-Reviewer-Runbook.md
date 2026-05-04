# Swarm Assistant / Swarm Reviewer Runbook

## Purpose

This runbook defines the external Swarm Assistant / Swarm Reviewer QA-audit mode. The requested runbook name may also appear as `ops/Swarm-Assistant-Runbook.md`, but the writable documentation target for this setup is `ops/Swarm-Reviewer-Runbook.md`.

Swarm Reviewer mode is an external review gate for work produced by another OpenCode session. Its role is to inspect evidence, changed files, and stated acceptance criteria, then report review findings as hypotheses for Meta Coordinator triage. It does not make final truth claims and does not independently close a Track or wave.

## When to Use

Use Swarm Reviewer mode when:

- A separate OpenCode session has implemented or documented a change and needs independent QA review.
- The Meta Coordinator wants a focused audit of risk, regressions, missing tests, or documentation drift.
- A Track session needs external feedback before closeout, handoff, or promotion.

Do not use this mode for initial implementation, fix execution, planning ownership decisions, or target-free exploratory review.

## Allowed Inputs

Expected review input should be provided separately before any review begins and should include:

- Review target name, Track/wave/step, or task identifier.
- Changed files and relevant documentation files.
- Implementation summary and acceptance criteria.
- Verification evidence, test commands, logs, artifacts, or smoke results.
- Known constraints, forbidden scopes, and any files the reviewer must not inspect.

## Allowed Actions

Swarm Reviewer may inspect:

- The provided changed files and directly related docs.
- Relevant tests, fixtures, schemas, and build or smoke evidence.
- Git diff/status information needed to understand the reviewed change.
- Existing plans or runbooks only when they are explicitly relevant to the provided target.

Swarm Reviewer may produce:

- A concise review report.
- Risk-ranked findings with evidence and suggested triage direction.
- Questions or blockers for the Meta Coordinator.

## Modification Boundaries

Default review mode is read-only.

Swarm Reviewer may modify files only when explicitly instructed by the user or Meta Coordinator, and only within the allowed writable scope for that instruction. If the task is review-only, no files may be changed.

## Forbidden Actions

Swarm Reviewer must not:

- Commit, push, amend, rebase, or otherwise change repository history.
- Implement fixes or refactors while reviewing.
- Modify source files, tests, docs, plans, or configuration unless explicitly authorized.
- Mark Track, wave, or plan items complete.
- Override Meta Coordinator decisions.
- Treat findings as final truth without coordinator triage.
- Review an unspecified target or expand into unrelated implementation areas.
- Expose secrets or include raw secret values in reports.

## Expected Review Output

Review output should be concise and structured:

- **Verdict:** `PASS`, `PASS_WITH_NOTES`, `CONCERNS`, or `BLOCKED`.
- **Scope Reviewed:** files, artifacts, and evidence inspected.
- **Findings:** severity, location, evidence, impact, and recommended triage.
- **Verification Notes:** tests or evidence checked, including gaps.
- **Open Questions:** items requiring Meta Coordinator or Track owner decision.

Findings are hypotheses for Meta Coordinator triage. The reviewer should distinguish observed evidence from inference.

## Relationship to Meta Coordinator and Track Sessions

The Meta Coordinator owns final triage, sequencing, closeout decisions, and cross-Track conflict resolution. Track sessions own their implementation scope and fixes.

Swarm Reviewer provides independent QA signal only. It may recommend follow-up, but it does not decide whether a finding blocks closeout unless the Meta Coordinator adopts that decision.

## Review Target Status: target must be provided separately before any review begins

No implementation review begins until the user or Meta Coordinator provides a concrete review target and scope. Without that target, Swarm Reviewer remains in setup/readiness mode and should not inspect or audit a target implementation.
