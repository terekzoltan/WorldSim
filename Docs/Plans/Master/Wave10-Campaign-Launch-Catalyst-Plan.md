# Wave 10 Campaign Launch Catalyst Plan

## Purpose

P6-H manual smoke exposed a campaign launch catalyst gap: the runtime can create campaigns, tests and ScenarioRunner can create deterministic campaigns, and Graphics can render `WorldRenderSnapshot.Campaigns`, but the interactive app currently has no operator path or organic gameplay path that creates a campaign entity.

This is a Track B/C integration gap, not a Track A rendering issue. Track A must not synthesize campaign state or infer campaign lifecycle from events.

## Current Facts

- `SimulationRuntime.TryCreateCampaign(...)` is the runtime-owned campaign creation endpoint.
- ScenarioRunner and runtime tests can create campaigns deterministically.
- `Ctrl+F2` in the app toggles the campaign panel/overlay only.
- Director `declareWar` affects relation/event state but does not create a campaign.
- P6-G currently provides an AI-only advisory strategist; its output is not runtime-applied.
- Therefore, interactive gameplay cannot currently guarantee populated or resolved campaign UI smoke.

## Non-Goals

- Do not make Track A create, fake, or mutate campaign state.
- Do not add exact siege progress to Graphics.
- Do not add runtime event emission as part of P6-H.
- Do not claim P6-G is runtime-applied until a Track B-owned adapter exists.
- Do not bypass `TryCreateCampaign(...)` validation or mutate `World` directly from App/Graphics.

## P6-I - Manual Operator Campaign Launch (Track B + minimal App routing)

### Goal

Provide a deterministic local operator/debug path so a running app can create a real campaign for manual smoke and debugging.

### Ownership

- Track B owns command semantics, validation, and runtime status.
- App may route the debug hotkey and show a toast/status message.
- Graphics remains consume-only through `WorldRenderSnapshot.Campaigns`.

### Hotkey

- Use `Ctrl+Q` for manual campaign launch.
- Keep `Ctrl+F2` as campaign panel/overlay toggle.

### Expected Shape

- Add a runtime-owned manual/operator command or helper that calls `TryCreateCampaign(...)` and returns structured success/failure.
- App `Ctrl+Q` invokes the runtime-owned path; it must not manipulate mutable simulation state directly.
- Owner faction, target faction, and requested member count must be explicit and deterministic.
- Acceptable configuration options:
  - fixed smoke default, e.g. `Obsidari -> Aetheri`, `requestedMemberCount=1`, plus clear toast, or
  - env/debug settings for owner/target/count if the implementation keeps defaults deterministic.
- Failure toast/status must include `CampaignCreationStatus` and a readable message.

### Acceptance

- From the running app, pressing `Ctrl+Q` can force a real campaign creation when runtime gates allow it.
- Immediately after `Ctrl+Q`, pressing `Ctrl+F2` shows a populated campaign panel/overlay.
- Failure cases are deterministic and visible, including campaign runtime unavailable, same faction, invalid count, missing owner colony, and missing target colony.
- No Graphics state synthesis or Runtime bypass is introduced.
- P6-H manual smoke can verify populated campaign UI after P6-I.

### Suggested Files

- `WorldSim.Runtime/RuntimeCommands.cs` or a runtime-local command/helper type if a command shape is needed.
- `WorldSim.Runtime/SimulationRuntime.cs` only for runtime-owned operator wrapper/status if `TryCreateCampaign(...)` alone is not sufficient.
- `WorldSim.App/GameHost.cs` for `Ctrl+Q` routing and toast/status only.
- `WorldSim.Runtime.Tests/*Campaign*Tests.cs` for command/status behavior.
- Optional App/arch test only if an existing low-cost seam exists.

### Verification

- Runtime focused tests for success and deterministic failure statuses.
- App/solution build.
- `git diff --check`.
- Manual smoke: launch app -> `Ctrl+Q` -> `Ctrl+F2` -> populated campaign panel/overlay visible.

## P6-J(B) - Organic Campaign Launch Runtime Application (Track B)

### Goal

Make campaigns form during normal gameplay under bounded conditions, rather than only tests, ScenarioRunner, or manual debug commands.

### Ownership

- Track B owns runtime fact mapping, cadence, validation, caps, suppression reasons, application, and campaign creation.
- Track B consumes the already-accepted P6-G advisory strategist contract where sufficient.
- Track C owns strategy intent and advisory decision logic only if P6-J(B) identifies a concrete contract gap and explicitly hands off P6-J(C).
- Runtime remains authoritative for all campaign creation gates and side effects.

### Required Policy

- Run at faction/campaign cadence, not per-person `RuntimeNpcBrain` branch cadence.
- Consume explicit runtime facts: stance/war state, colonies, eligible members, active campaigns, home defense, supply readiness, visible pressure, and recent campaign outcomes where available.
- Apply only through Track B validation and `TryCreateCampaign(...)` or a runtime-owned wrapper.
- Respect gates:
  - diplomacy/combat/campaign runtime enabled,
  - owner faction and target faction are valid and distinct,
  - owner and target colonies exist,
  - requested member count is positive,
  - enough eligible members exist,
  - home defense minimum remains satisfied,
  - max active campaigns/faction and campaign-pair caps,
  - route/path budget constraints,
  - future logistics constraints from P7-A/P7-B when those hooks exist.

### P6-J(B) Acceptance

- Under hostile/war conditions and sufficient eligible members, a civilization can autonomously launch at least one bounded campaign in normal app/runtime flow.
- Peaceful or insufficient-force conditions do not launch campaigns.
- Launch decisions are deterministic for a fixed seed/state.
- No per-person campaign-goal branch is added to `RuntimeNpcBrain`.
- ScenarioRunner/SMR can distinguish organic campaign launches from deterministic/manual probes.

### Suggested Sequencing

- P6-J(B) opens after P6-G and P6-I are accepted.
- P6-J(B) does not block P7-A initial supply-line foundation, but Wave 10 cannot claim campaign gameplay-complete without it.
- If P7-A changes supply/logistics constraints first, P6-J(B) must consume those caps rather than inventing parallel logistics rules.
- P6-J(B) closeout must explicitly state one of: `P6-J(C) not needed - existing P6-G strategy contract was sufficient`, or `P6-J(C) needed - Track C must adjust/extend advisory strategy because <specific contract gap>`.

## P6-J(C) - Organic Campaign Strategy Follow-Up, If Needed (Track C)

### Goal

Adjust the advisory campaign strategist only if P6-J(B) proves that the existing P6-G contract is insufficient for runtime-owned organic launch application.

### Ownership

- Track C owns advisory scoring/intent shape only.
- Track B remains owner of runtime fact mapping, caps, validation, suppression reasons, and campaign creation.
- P6-J(C) must not add Runtime/App/Graphics references to `WorldSim.AI`.

### Acceptance, If Opened

- The change directly addresses the named P6-J(B) handoff gap.
- The advisory strategy remains pure/deterministic and covered by AI tests.
- Runtime application remains owned by the P6-J(B) path.
- If P6-J(B) says no Track C changes are needed, mark P6-J(C) N/A rather than opening a no-op implementation session.

### Verification

- Runtime tests for organic launch success under war/hostile conditions.
- Runtime tests for disabled gates, peaceful stance, home-defense minimum, active campaign cap, and invalid target suppression.
- AI tests remain pure and deterministic.
- ScenarioRunner evidence later must label proof type (`organic` vs `deterministic_probe` vs `manual_operator`).
- Full solution build and boundary checks: AI must not depend on Runtime; Graphics must remain snapshot-only.

## P6-H Closeout Policy

P6-H may close as a Track A snapshot-render UI slice with an explicit limited-smoke caveat:

- Empty-state panel/overlay toggle.
- Static/render scope verification.
- Automated build/test/scope checks.

P6-H must not claim populated/resolved app smoke until real campaigns can be created from the running app. After P6-I, the deferred populated UI smoke should be rerun with `Ctrl+Q` followed by `Ctrl+F2`. After P6-J(B), organic campaign launch smoke/evidence should prove normal gameplay campaign formation separately from manual/operator launch; P6-J(C) is required only if P6-J(B) hands off a concrete Track C strategy gap.

## Wave 10 Closeout Policy

- P6-I is required before claiming populated campaign UI app-smoke coverage.
- P6-J(B) is required before claiming campaign gameplay completeness or organic campaign behavior; P6-J(C) is required only when P6-J(B) explicitly opens it.
- Wave 10 SMR prep must explicitly tag campaign-launch lanes by proof type and must not use manual/operator launch evidence as organic campaign proof.
