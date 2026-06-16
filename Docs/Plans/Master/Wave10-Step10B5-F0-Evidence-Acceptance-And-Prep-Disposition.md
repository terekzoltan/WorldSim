# Wave 10 Step10B.5-F0 - Evidence Acceptance And Prep-Slice Disposition

Status: accepted / closed
Owner: Meta Coordinator
Parent: `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`

F0 is a coordination and disposition slice inside Step10B.5. It is not a new Combined-plan step. It decides what evidence and local prep work Track B starts from.

## Purpose

Accept Step10B.2 as RED evidence and prevent Track B from mixing unreviewed evidence-surface prep, behavior fixes, and SMR artifact interpretation in one change.

The immediate output is a clean Track B handoff for F1 diagnostics-only work.

## Inputs

- `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`
- `Docs/Plans/Master/Wave10-Step10B2-Organic-Campaign-Lifecycle-SMR-Plan.md`
- `Docs/Plans/Master/Wave10-Step10B5-Organic-Campaign-RED-Recovery-Plan.md`
- `Docs/Plans/Master/Wave10-Step10B5-Track-B-Implementation-Checklist.md`
- Local Step10B.2-A prep-slice diff, if still uncommitted.

## Decisions To Record

Decide whether Step10B.2-A prep-slice code changes are:

- accepted as evidence-surface prep and can be committed before Track B behavior work,
- accepted only as local context but must be reworked by Track B,
- rejected/deferred and should not be used by F1,
- or still pending review and therefore a blocker before implementation.

Decide whether the default F2 policy is accepted:

- `War` targets are baseline-known enough for first organic launch.
- Scout intel remains a quality/target-choice signal.
- Neutral/Tense targets remain non-launchable.

If this policy is not accepted, Track B must implement natural scout availability/observation instead of war-known target policy.

## Actions

Review the RED evidence note and confirm the RED reasons are accepted:

- hostile organic `0/90` launch,
- pure organic `0/90` launch,
- stress hostile `0/240` launch,
- stress `SURV-01/02/04` failures,
- manual launch works but only proves manual/operator path.

Inspect the worktree and classify current local changes:

- docs created by Meta,
- SMR Analyst evidence note,
- Step10B.2-A runtime/runner prep-slice code,
- architecture automation outputs,
- raw artifact directories.

Update handoff wording so Track B starts from F1 diagnostics-only and does not modify launch behavior yet.

## Non-Goals

- Do not implement runtime fixes.
- Do not run more ad hoc SMR packages.
- Do not open Track C.
- Do not open Track A.
- Do not commit raw `.artifacts/smr/...` bundles.

## Acceptance

F0 is accepted when:

- Step10B.2 RED evidence is explicitly accepted for routing.
- Step10B.2-A prep-slice status is explicit.
- Track B has a clear F1 handoff.
- Step10C and Wave10.5 remain blocked until Step10B.5 disposition.
- `ops/PROJECT_STATE.md` points to the correct next role/action.

## Handoff To F1

Track B should load, in order:

- `ops/PROJECT_STATE.md`
- `Docs/Plans/Master/Wave10-Step10B5-F1-Organic-Launch-Decision-Trail-Diagnostics.md`
- `Docs/Plans/Master/Wave10-Step10B5-Track-B-Implementation-Checklist.md`
- `Docs/Evidence/SMR/wave10-step10b2-organic-manual-lifecycle/README.md`

Track B must state in its first handoff whether it is using the Step10B.2-A prep slice as-is, rebasing it, or replacing it.

## F0 Closeout Record

Date: 2026-06-16

Decisions:

- Step10B.2 RED evidence is accepted for routing into Step10B.5 recovery.
- Step10B.2-A evidence-surface prep slice is accepted as committed prep work in `e4bb0a1 feat(wave10): add step10b2 lifecycle evidence surface`.
- The prep slice is not a behavior fix and must not be used to claim organic launch recovery.
- Default F2 policy remains accepted as the starting policy: `War` targets may be baseline-known for first organic campaign launch, scout intel remains a quality/target-choice signal, and Neutral/Tense targets remain non-launchable.
- Step10C residual disposition and Wave10.5 readiness remain blocked until Step10B.5 is closed or explicitly deferred by Meta.
- Track C and Track A remain closed/deferred for Step10B.5 until Track B diagnostics produce an explicit routing reason.

F1 handoff:

- Track B is cleared to start Step10B.5-F1 diagnostics-only.
- F1 must not change launch behavior.
- F1 should treat `e4bb0a1` as the accepted baseline evidence-surface prep and add decision-trail diagnostics on top of it.
- F1 must classify hostile organic no-launch using runtime/main-run evidence before any F2 behavior fix begins.
