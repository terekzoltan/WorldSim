# P7-D Manual Smoke Follow-up Evidence

Date: 2026-06-04

Scope: Wave 10 `P7-D` manual app smoke for campaign/logistics UI consume and Track B manual/operator campaign launch readiness.

This is manual/operator evidence only. It must not be treated as organic campaign proof or SMR/ScenarioRunner evidence.

## Setup

- App manual smoke flow: launch the MonoGame app, press `Ctrl+Q`, then press `Ctrl+F2`.
- Visual profile used for the P7-D smoke lane: `Showcase`.
- Runtime gates expected on for this lane: diplomacy, combat primitives, siege.
- Goal: make a real runtime campaign visible through the existing campaign/logistics panel and overlay so Track A can verify supply convoy / forward-base rendering without fabricating state.

## Observations

### Initial P7-D smoke before Track B follow-up

- `Ctrl+F2` opened the campaign/logistics panel.
- Panel showed the Logistics section and summary rows, but the dense rows clipped/truncated in constrained space.
- `Ctrl+Q` could produce a campaign that remained stuck as `assembling_pending` / `Army 0/1` / `anchor:none` / `route:no path`, blocking convoy/base visual proof.
- Result: Track A readability follow-up and Track B smoke-ready launch follow-up were both required.

### Track A readability follow-up result

- The panel became more compact and readable in manual screenshots.
- Empty and no-active-campaign states were readable:
  - `No active campaigns.`
  - `Logistics`
  - `Conv 0/0/0 act/del/fail`
  - `Base 0/0/0 act/exp/abn`
- Dense logistics rows now degrade with top-N display and a `+N more ... records` line.
- Remaining issues in this document are runtime/manual-launch behavior, not Graphics clipping.

### Track B manual launch follow-up attempts

- First route/path fix changed operator smoke to fallback across viable colony pairs and to preflight from rally point with a larger app-sized path budget.
- Manual smoke still failed on some fresh app runs with a bottom toast equivalent to:
  - `Campaign launch failed (CampaignRuntimeUnavailable): Manual smoke launch could not find a viable campaign pair ...`
- Follow-up diagnosis: some fresh starts did not yet have a role-eligible campaign member (`Warrior`, `SupplyCarrier`, or `Hunter`) for any route-valid owner colony.
- A second runtime fix allowed operator-smoke-only healthy home-member fallback, without changing exact manual commands or organic campaign strategy semantics.

### Successful manual smoke examples

- After one run waited roughly 30 seconds, `Ctrl+Q` eventually produced a real campaign for `Syl->Obs`:
  - phase: `resolved/resolved`
  - army: `1/1`
  - anchor/rally visible
  - result row visible
  - forward-base records appeared and were abandoned after resolution/member absence.
- Other successful runs showed `Aet->Syl` campaigns:
  - `marching/marching`
  - `Army 1/1`
  - route progress visible, for example `Route 1/29 ...` then later `Route 3/13 ...` or similar progress rows
  - active and abandoned forward-base rows appeared, proving Track A can consume base snapshot rows when runtime reaches that flow.
- A later successful run showed multiple `Aet->Syl` campaigns after repeated `Ctrl+Q`, including several `assembling/assembling` entries.

### Remaining runtime concerns found by manual smoke

- Success was not deterministic on immediate fresh app start before the latest operator-smoke member fallback.
- Repeated `Ctrl+Q` could create multiple unresolved manual campaigns, making the panel noisy and producing several `Army 0/1` / `Route no path` rows.
- The default operator-smoke command originally requested only one member, so successful campaigns behaved like a single-person probe running toward an enemy rather than a squad-level campaign.
- Forward bases could appear and then abandon quickly when the campaign resolved or when no live assigned member remained near the base.
- Long bottom-toast diagnostics are too wide for the screen and get truncated; this is useful for developer diagnosis but poor as operator UI.
- A later experimental change to make operator smoke a 3-member squad and prevent repeated active-campaign launches was manually rejected: after that change, repeated fresh simulations failed more often to find a viable campaign pair. That behavior was reverted back to the prior 1-member operator-smoke baseline while retaining the useful app-sized route-budget and no-role fallback coverage.

## Runtime condition summary

Manual/operator launch currently depends on these runtime checks:

- Runtime gates must allow campaign creation: diplomacy and combat primitives are enabled.
- Command config must be valid: owner faction and target faction exist, owner != target, requested member count > 0.
- In operator-smoke fallback mode, runtime tries the default pair first and then scans distinct faction-colony pairs deterministically.
- A candidate pair must have:
  - resolvable owner colony and target colony,
  - passable rally point near owner origin,
  - passable march objective near target origin or allowed fallback objective,
  - path from rally point to objective within operator-smoke path budget,
  - enough eligible campaign members in the owner colony.
- Normal role-eligible campaign members are `Warrior`, `SupplyCarrier`, or `Hunter`.
- Operator-smoke fallback can use healthy home-colony members if role-eligible members are not available.
- Members are rejected if dead, already assigned, blocked by transient combat ownership, routing, in combat, in an active battle/combat group, or currently doing hard combat jobs (`Fight`, `Flee`, `RaidBorder`, `AttackStructure`).
- Assembly completes only when requested member count is assigned and all assigned members are within Manhattan distance <= 1 of the rally point.
- Marching can return to assembly if assigned members become invalid or member count drops below requested count.

## Current interpretation

- P7-D Graphics readability is no longer the primary blocker.
- The Track A UI/visual-consume slice is acceptable as a manual-evidence closeout with caveats: the panel renders campaign/logistics rows, active/abandoned forward-base rows, and constrained top-N fallback rows when runtime reaches those states.
- The important remaining behavior is Track B runtime/operator-smoke stability and durable proof, not more P7-D UI polish:
  - choose an actually viable pair from current world state,
  - avoid repeated unresolved campaign spam,
  - decide later whether manual smoke should remain a one-person probe or become a squad-sized scenario,
  - preserve exact/manual and organic campaign semantics outside the operator-smoke path.
- Step 10A / SMR should convert these manual observations into durable ScenarioRunner proof types instead of relying on interactive app smoke.
- Recommendation: do not block Wave 10 Step 8 (`P7-E`) on more manual P7-D smoke chasing. Treat this document as evidence input for Step10A and future Track B campaign/operator-smoke hardening.

## Follow-up reference

- Combined plan gate: `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`, Wave 10 `P7-D manual smoke follow-up gate`.
- Relevant runtime files:
  - `WorldSim.Runtime/RuntimeCommands.cs`
  - `WorldSim.Runtime/SimulationRuntime.cs`
  - `WorldSim.Runtime.Tests/Wave9CampaignRuntimeTests.cs`
- Relevant graphics files:
  - `WorldSim.Graphics/UI/Panels/CampaignPanelRenderer.cs`
  - `WorldSim.Graphics/Rendering/CampaignOverlayPass.cs`
