# Wave 10 Campaign Logistics Hardening Implementation Plan

> **For agentic workers:** Use repo-valid workflow skills such as `implementation-execution` or `sequence-planning` when appropriate. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Wave 10 campaign resolution, launch, logistics, scout, siege-unit, and multi-front work bounded, observable, and snapshot-driven before Wave 10 closeout.

**Architecture:** Track B owns campaign resolution, route/cap budgets, convoy/base state, scout runtime state, siege-unit entities, and multi-front limits. Track C consumes explicit strategist/scout/siege-unit surfaces. Track A renders structured campaign/logistics records and does not compute routes or infer combat state.

**Tech Stack:** C#/.NET 8, `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.Graphics`, `WorldSim.ScenarioRunner`, xUnit, MonoGame snapshot rendering.

---

## Current Scope

Wave 10 is blocked until Wave 9 closeout. This plan defines acceptance detail for the Wave 10 audit notes:
- `P6-G` must be a faction/campaign strategist, not a per-person `RuntimeNpcBrain` branch.
- `P6-I`/`P6-J` are tracked in the companion launch-catalyst plan: `Docs/Plans/Master/Wave10-Campaign-Launch-Catalyst-Plan.md`.
- `P7-A`, `P7-B`, and `P7-G` must define logistics caps before multi-front work.
- Wave 10 SMR closeout must require all implementation epics plus evidence, not only Track B runtime pieces.

Until Wave 10 is explicitly opened by Combined and `ops/PROJECT_STATE.md`, every task below is a non-launchable planning scaffold. The file is a source-plan reference, not standalone execution authority.

## File Ownership Map

- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/CampaignResolution.cs`
- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/CampaignLogistics.cs`
- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/SupplyConvoyState.cs`
- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/ForwardBaseState.cs`
- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/ScoutIntelState.cs`
- Create/modify after Wave 9: `WorldSim.Runtime/Simulation/Military/SiegeUnitState.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldSnapshotBuilder.cs`
- Modify: `WorldSim.AI/Abstractions.cs`
- Modify: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`
- Modify: `WorldSim.Graphics/UI/Panels/CampaignPanelRenderer.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Test new: `WorldSim.Runtime.Tests/Wave10CampaignResolutionTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave10CampaignLogisticsTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave10ScoutIntelTests.cs`
- Test new: `WorldSim.Runtime.Tests/Wave10SiegeUnitTests.cs`
- Test new: `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`

## Non-Negotiable Gates

- The campaign strategist is faction/campaign level. It may choose campaign goals, targets, aborts, and reinforcements, but it must not become a new branch of per-person `RuntimeNpcBrain`.
- Campaign launch must have both a manual/operator catalyst for app smoke and an organic runtime application path before Wave 10 can claim campaign gameplay completeness.
- Every faction must have caps for active campaigns, active convoys, active forward bases, active siege units, and simultaneous fronts.
- Home defense minimums must block campaign/convoy launches when a faction would empty its defended core.
- Path and route budgets must be measurable and bounded in ScenarioRunner evidence.
- Graphics must consume route waypoints, convoy positions, forward base positions, scout intel ranges, and siege-unit render records from snapshots.

## Task 1: P6-E/P6-F Campaign Resolution Runtime

**Files:**
- Create/modify: `WorldSim.Runtime/Simulation/Military/CampaignResolution.cs`
- Modify: `WorldSim.Runtime/Simulation/Military/CampaignState.cs`
- Test: `WorldSim.Runtime.Tests/Wave10CampaignResolutionTests.cs`

- [ ] **Step 1: Write resolution tests**

Cover siege reached, defender holds, attacker wins, loot budget, war-score delta, peace trigger, and no duplicate resolution.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave10CampaignResolutionTests --no-restore
```

Expected before implementation: fail because resolution state does not exist.

- [ ] **Step 2: Add deterministic resolution result**

Resolution output must include:
- result kind,
- attacker/defender faction ids,
- affected colonies or structures,
- loot delta,
- war score delta,
- peace eligibility,
- event/counter keys for ScenarioRunner.

- [ ] **Step 3: Add conservation tests**

Assert that loot does not create resources twice and war score changes once per resolved campaign.

## Task 2: P6-G Strategic Campaign AI Boundary

**Files:**
- Modify: `WorldSim.AI/Abstractions.cs`
- Create: `WorldSim.AI/CampaignStrategy.cs`
- Modify: `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs` only to pass through runtime facts, not to own strategy
- Test new: `WorldSim.AI.Tests/CampaignStrategyTests.cs`

- [ ] **Step 1: Define strategist input/output**

The strategist should consume campaign-level facts:
- faction resources,
- home defense score,
- active campaigns,
- visible enemy pressure,
- recent campaign outcomes,
- supply readiness.

It should output campaign-level decisions:
- launch campaign,
- reinforce campaign,
- abort campaign,
- request convoy,
- hold defensive posture.

- [ ] **Step 2: Keep per-person AI separate**

Per-person `RuntimeNpcBrain` may execute assigned jobs, but campaign goal selection belongs to the strategist.

- [ ] **Step 3: Add tests**

Run:

```powershell
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --filter CampaignStrategyTests --no-restore
```

Expected: decisions change at faction/campaign level without requiring individual actor context scans.

## Task 3: P7-A/P7-B Logistics Caps and Forward Bases

**Files:**
- Create: `WorldSim.Runtime/Simulation/Military/CampaignLogistics.cs`
- Create: `WorldSim.Runtime/Simulation/Military/SupplyConvoyState.cs`
- Create: `WorldSim.Runtime/Simulation/Military/ForwardBaseState.cs`
- Test: `WorldSim.Runtime.Tests/Wave10CampaignLogisticsTests.cs`

- [ ] **Step 1: Write cap tests**

Cover:
- max active campaigns per faction,
- max active convoys per faction,
- max forward bases per faction,
- home garrison minimum,
- convoy spawn throttle,
- route/path budget exceeded.

- [ ] **Step 2: Add cap configuration**

Use runtime-local configuration first. If later promoted to JSON, preserve the same defaults.

Required defaults:
- no faction may launch a new campaign if it violates home garrison minimum,
- no faction may spawn a convoy every tick,
- route/path budget exhaustion must skip or defer launches deterministically.

- [ ] **Step 3: Add counters**

Counters must include:
- `campaignLaunchBlockedByCap`,
- `campaignLaunchBlockedByHomeDefense`,
- `convoySpawnBlockedByThrottle`,
- `routeBudgetExhausted`,
- `forwardBaseBuildBlockedByCap`.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave10CampaignLogisticsTests --no-restore
```

Expected: pass with deterministic cap behavior.

## Task 4: P7-C Scout Runtime and AI Consume

**Files:**
- Create: `WorldSim.Runtime/Simulation/Military/ScoutIntelState.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.AI/Abstractions.cs`
- Test: `WorldSim.Runtime.Tests/Wave10ScoutIntelTests.cs`

- [ ] **Step 1: Add scout intel state**

State must include scout faction, observed tile/region, confidence, age in ticks, source actor or scout group id, and expiration tick.

- [ ] **Step 2: Add AI consume fields**

Campaign strategist and per-person scout behavior should consume scout state through explicit context fields. Do not infer scout success from actor position alone.

- [ ] **Step 3: Add tests**

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave10ScoutIntelTests --no-restore
```

Expected: scout intel ages out deterministically and campaign decisions can use fresh intel.

## Task 5: P7-E/P7-F/P7-H Siege Units

**Files:**
- Create: `WorldSim.Runtime/Simulation/Military/SiegeUnitState.cs`
- Modify: `WorldSim.Runtime/ReadModel/WorldRenderSnapshot.cs`
- Modify: `WorldSim.Graphics/Rendering/WorldRenderer.cs`
- Test: `WorldSim.Runtime.Tests/Wave10SiegeUnitTests.cs`

- [ ] **Step 1: Add unit types**

Required types:
- ram,
- siege tower,
- mobile catapult.

Each unit needs faction id, health, tile, target tile, deployment phase, and active/inactive reason.

- [ ] **Step 2: Add AI deployment commands**

Track C must receive runtime unit capabilities and target constraints. It must not create units or bypass Track B caps.

- [ ] **Step 3: Add snapshot render records**

Track A consumes `SiegeUnitRenderData` with type, faction, tile, health, target, and recent action effect.

Run:

```powershell
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave10SiegeUnitTests --no-restore
```

Expected: pass.

## Task 6: Wave 10 SMR Evidence

**Files:**
- Modify: `WorldSim.ScenarioRunner/Program.cs`
- Test: `WorldSim.ScenarioRunner.Tests/Wave10CampaignEvidenceTests.cs`
- Artifact docs: `Docs/Evidence/SMR/wave10-advanced-campaign/README.md`

- [ ] **Step 1: Add evidence lanes**

Required lanes:
- `campaign_resolution`,
- `supply_line_convoy`,
- `forward_base`,
- `scout_intel`,
- `siege_units`,
- `multi_front_bounds`.

- [ ] **Step 2: Add artifact fields**

Required fields:
- campaign resolution result,
- loot/war-score deltas,
- active convoy count,
- convoy throttle blocks,
- forward base count,
- scout intel fresh/expired counts,
- siege unit deployed/active/destroyed counts,
- multi-front cap blocks,
- route/path budget consumption.

- [ ] **Step 3: Run evidence tests**

Run:

```powershell
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore
```

Expected: pass and prove each Wave 10 family with dedicated fields.

## Verification Matrix

```powershell
dotnet test WorldSim.AI.Tests\WorldSim.AI.Tests.csproj --no-restore
dotnet test WorldSim.Runtime.Tests\WorldSim.Runtime.Tests.csproj --filter Wave10 --no-restore
dotnet test WorldSim.ScenarioRunner.Tests\WorldSim.ScenarioRunner.Tests.csproj --filter Wave10CampaignEvidenceTests --no-restore
dotnet test WorldSim.ArchTests\WorldSim.ArchTests.csproj --no-restore
dotnet build WorldSim.sln --no-restore
git diff --check
```

Closeout expected: every Wave 10 implementation epic is complete, Wave 10 SMR prep is accepted, and the final Wave 10 SMR package proves campaign resolution, logistics, scouts, siege units, and multi-front caps.
