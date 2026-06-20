# Review Findings Registry

Purpose:
- Capture reusable findings from plan reviews, step reviews, deep reviews, and post-review fix loops.
- Keep entries short and actionable so future Track agents can avoid repeated mistakes.
- Record only findings worth reusing; do not duplicate every routine review note.

Entry format:

```text
## YYYY-MM-DD - <Target> - <Severity> - <Short title>

- Track: <Track / role>
- Source: <review type, commit, PR, or doc reference>
- Finding: <what was wrong or risky>
- Impact: <why it matters>
- Resolution / guidance: <what to do next time>
- Status: open | fixed | guidance
```

Severity guide:
- `blocking`: must be fixed before commit or step closeout.
- `major`: likely bug, regression, ownership violation, or missing verification.
- `minor`: non-blocking improvement or maintainability issue.
- `guidance`: reusable process/coordination note.

Entries:

## 2026-06-20 - Wave 10 Step10B.5-F6 Closeout - Minor - Lifecycle progression does not prove manual command creation success

- Track: SMR Analyst / Track B campaign runtime evidence
- Source: Meta + Swarm step-review synthesis for `Step10B.5-F6`, artifacts `.artifacts/smr/wave10-manual-operator-lifecycle-002/` and `Docs/Evidence/SMR/wave10-step10b5-f6-full-recovery-closeout/README.md`
- Finding: The manual control package showed 18/18 lifecycle progression and clean hard gates, but manual runtime-command creation was only 16/18; two runs reported `CampaignRuntimeUnavailable` while other campaign launches still occurred.
- Impact: Future closeout reports could falsely claim the manual command path is fully healthy if they look only at lifecycle progression, launch totals, or `exitCode=0`.
- Resolution / guidance: Keep manual command availability as its own counter in evidence review. Step10B closes as YELLOW accepted evidence, and Step10C must explicitly classify the manual residual before Wave10.5 readiness.
- Status: open, actively routed to Step10C residual/manual gap triage

## 2026-06-17 - Wave 10 Step10B.5-F2-A Step Review - Blocking - Mobilization role quota must count launchable actors

- Track: Track B / Runtime organic campaign mobilization
- Source: Meta internal lanes + Meta Coordinator step-review synthesis for `Step10B.5-F2-A`
- Finding: The initial F2-A mobilization role-sync can assign persistent `PersonRole.Warrior` to home actors that are not launchable this cadence because the sync candidate/quota filter excludes active campaign actors but does not exclude `blockedCampaignActorIds` or transient campaign-blocked states (`Fight`, `Flee`, routing, active battle/group, raid/attack jobs). The later organic campaign context and home-defense counts do exclude these actors.
- Impact: Transient or blocked actors can satisfy the desired warrior-role quota while `GetOrganicCampaignEligibleMembers(...)` later filters them out, allowing the original `no_available_warriors_after_home_defense` blocker to remain under realistic combat/routing conditions.
- Resolution / guidance: Before F2-A closeout, align mobilization sync candidate/quota counting with organic launch eligibility by excluding blocked/transient actors, and add a focused regression where stronger blocked/transient home actors exist beside lower-strength idle candidates that must become launchable warriors.
- Status: fixed in Step10B.5-F2-A blocker-fix re-review; mobilization role-sync now excludes blocked/transient actors from the quota pool and focused regression coverage proves idle launchable actors are promoted instead.

## 2026-06-07 - Wave 10 SMR Prep Validation - Major - Probe lane presence is not feature proof

- Track: SMR Analyst / Track B evidence surface validation
- Source: Meta step-review synthesis for `Wave-10-SMR-prep-validation`, artifact `.artifacts/smr/wave10-smr-prep-validation-001/`
- Finding: The Step10A artifact/provenance surface validates successfully, but Wave10 closeout proof still depends on lane-specific classification rather than lane presence. After the Track B unavailable-lane fix pass, `.artifacts/smr/wave10-unavailable-lane-fix-001/` showed `multi_front_bounded` 9/9 positive as deterministic active multi-front proof, `campaign_siege_resolution` partial 3/9 positive, `forward_base_long_campaign` partial 5/9 positive, and `organic_campaign_launch` / `supply_line_convoy` / `scout_intel_campaign_choice` / `siege_unit_breach` still explicit routed `proof_unavailable`. The interim Step10C-B artifact `.artifacts/smr/wave10-step10c-b-runtime-evidence-001/` improved this to 7/8 positive lanes, and the follow-up artifact `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/` clears the scout blocker by measuring inside a fresh-intel window: 8/8 lanes are now positive.
- Impact: Wave10 final closeout cannot honestly claim campaign/logistics/siege/scout/multi-front feature proof from lane presence alone. Treating `wave10LaneNames` as proof would recreate a false-green evidence path.
- Resolution / guidance: Step10B reports must inspect `wave10-probes.json` statuses and non-claims, not just manifest lane names. For supply lanes, keep wording precise: cap/throttle/route/home-defense blocks are request-bound outcomes, not delivery lifecycle proof unless delivered/failed counters are positive.
- Status: fixed in Track B follow-up evidence; `.artifacts/smr/wave10-step10c-b-runtime-evidence-002/` reports 8/8 positive lanes, with scout proof captured under fresh-intel conditions.

## 2026-06-07 - Wave 10 Step10A Step Review - Blocking - SMR proof artifacts must not mix side-probe and run evidence

- Track: Track B / ScenarioRunner Wave10 SMR export-config
- Source: Meta internal review lanes + Meta Coordinator step-review synthesis for Wave 10 `Step10A`
- Finding: The initial Step10A implementation attaches `run.wave10` evidence generated from separate `SimulationRuntime` side-probes to normal ScenarioRunner runs whose metrics, perf, anomalies, and drilldown timeline come from a different executed `World` run. It also leaves required Wave10 closeout evidence domains incomplete versus the active SMR closeout plan, and drilldown Wave10 timeline samples are default/non-sampled even when a run-level Wave10 lane is configured.
- Impact: SMR Analyst could read a run artifact as proving Wave10 behavior that did not occur in the serialized run/timeline, while supply-line, forward-base, scout-intel, siege-unit, and campaign-resolution closeout questions remain only partially answerable.
- Resolution / guidance: Before Step10A closeout, either compute Wave10 evidence from the same executed run or split side-probe evidence into an explicitly separate/provenance-tagged artifact block. Add or actively defer deterministic lanes for every Wave10 closeout domain, and make drilldown semantics match the final evidence model. Do not use gameplay tuning to force evidence and do not stage unrelated `Docs/Architecture/` automation output.
- Status: fixed and accepted in Step10A re-review; side-probe evidence is separated into `wave10-probes.json`, normal run `wave10` remains main-run truth, and manifest/drilldown semantics are covered by focused tests. Step10B must inspect `summary.json.wave10ProbeEvidence` and `wave10-probes.json` to classify unavailable lanes before closeout.

## 2026-06-07 - Wave 10 P7-F Step Review - Blocking - ReinforceCampaign intent must be applied or disabled

- Track: Track C / siege-unit AI deployment with narrow Runtime mapping
- Source: Meta internal review lanes + Meta Coordinator step-review synthesis for Wave 10 `P7-F`
- Finding: The P7-F implementation makes `DefaultCampaignStrategist` return `ReinforceCampaign` with `CampaignSiegeUnitProtectionNeeded` for damaged active siege units, and Runtime advertises `CanReinforceCampaign: true`, but `SimulationRuntime.EvaluateOrganicCampaignLaunches(...)` applies only `LaunchCampaign` and `RequestConvoy`. The new reinforcement intent can therefore become a silent runtime no-op.
- Impact: P7-F can appear implemented because AI tests and fact-mapping tests pass, while no live runtime protection/escort/reinforcement effect occurs for vulnerable siege-unit campaigns. The no-op branch can also suppress normal launch evaluation during organic runtime ticks.
- Resolution / guidance: Before P7-F closeout, either implement a minimal Runtime-owned `ReinforceCampaign` application path that produces a concrete protected/reinforced campaign effect without taking siege-unit lifecycle ownership, or explicitly disable/downgrade runtime reinforcement capability and revise acceptance. Add a focused runtime regression proving the chosen behavior.
- Status: fixed in P7-F closeout; Runtime now has a protection-specific apply path for `CampaignSiegeUnitProtectionNeeded`, with focused regressions covering applied protection and no-op cases.

## 2026-06-07 - Wave 10 P7-F Fix Re-review - Blocking - Reinforcement tests must use production capacity semantics

- Track: Track C / siege-unit AI deployment with Runtime protection apply path
- Source: Meta internal review + Meta Coordinator step-review synthesis for Wave 10 `P7-F` fix re-review
- Finding: The attempted `CampaignSiegeUnitProtectionNeeded` runtime apply path adds warriors through `ArmyState.TryAddMemberActorId(...)`, but that method rejects additions once `MemberCount >= RequestedMemberCount`. The positive test makes reinforcement succeed by reflection-mutating the private `RequestedMemberCount` backing field, so it does not prove production-realistic protection for a normal full active campaign.
- Impact: P7-F can still pass tests while failing to reinforce damaged siege-unit campaigns in normal runtime conditions. This repeats the earlier pattern of proving a nearby/plumbing condition rather than the requested deployed behavior.
- Resolution / guidance: Before P7-F closeout, either add a production-owned reinforcement capacity/member path that works for a full active campaign without test-only reflection, or explicitly downgrade acceptance to spare-capacity refill only. Preferred fix: production-realistic test where a full active campaign with damaged active siege units and reserve warriors receives a concrete reinforcement/protection effect.
- Status: fixed in P7-F closeout; `ArmyState.TryAddProtectionReinforcementMemberActorId(...)` provides a production-owned post-assembly reinforcement path, and the positive regression no longer reflection-expands `RequestedMemberCount`.

## 2026-06-04 - Wave 10 P7-E Step Review - Major - Siege units must follow campaign pressure suppression

- Track: Track B / Runtime dedicated siege units
- Source: Meta internal review + Swarm Assistant step-review synthesis for Wave 10 `P7-E`
- Finding: The initial dedicated siege-unit implementation marks units inactive for some invalid paths, but not all non-pressure lifecycle paths after units have spawned. `SuppressActivePressure(...)` or `MarkNoTarget(...)` can reset/stop campaign siege pressure while `SiegeUnitState` remains `Active` in runtime/render snapshots. Follow-up review found two related lifecycle gaps: post-world sync initially used health-only validation, and then same-tick `LastPressureTick == Tick` could short-circuit before invalid roster cleanup.
- Impact: Downstream `P7-F` AI and `P7-H` graphics could consume active siege units for a campaign that no longer has active siege pressure, creating stale snapshot truth across the runtime/read-model boundary.
- Resolution / guidance: Fixed in the P7-E lifecycle fix passes before closeout. Runtime now deactivates campaign siege units when campaign siege pressure is stopped due to invalid/incomplete roster, post-world sync invalid roster including alive-but-pressure-invalid members, no-target reporter, resolver-disabled, and resolved campaign conditions. Same-tick sync validates pressure-capable roster before the `LastPressureTick == Tick` continue when active dedicated siege units exist, while preserving `Breached`, `NoTarget`, and non-unit campaign semantics. Focused regressions cover same-tick sync cleanup, no-target reporter cleanup, resolver-disabled-after-spawn cleanup, runtime inactive state, and snapshot inactive state.
- Status: fixed in P7-E closeout; Step 9 may open after Meta synthesis/closeout routing.

## 2026-06-04 - Wave 10 P7-D Manual Smoke - Major - Campaign logistics panel clips under real smoke

- Track: Track A / Graphics campaign logistics UI
- Source: Track A -> Meta Coordinator manual smoke handoff for Wave 10 `P7-D`
- Finding: Real app smoke on the existing `Ctrl+F2` campaign/logistics panel showed the panel text does not fit cleanly at the current size. Dense campaign rows plus the Logistics summary visibly clip/truncate, especially on lower rows.
- Impact: P7-D can compile and pass boundary checks while still failing its primary user-visible goal: readable convoy/base UI for manual verification.
- Resolution / guidance: Keep the fix Graphics-only. Reduce row density, shorten Logistics summary wording, reserve more vertical space, or fall back to compact counts when space is constrained. Re-run manual smoke before claiming clean GREEN.
- Status: closed for P7-D; Graphics-only readability pass implemented and manual evidence shows compact campaign/logistics rows plus top-N fallback are readable enough for visual-consume closeout. Evidence: `Docs/Evidence/Manual/P7-D-Manual-Smoke-Followup.md`.

## 2026-06-04 - Wave 10 P7-D Manual Smoke - Major - Manual/operator campaign launch can stall before logistics become visible

- Track: Track B / Runtime manual-operator smoke path
- Source: Track A -> Meta Coordinator manual smoke handoff for Wave 10 `P7-D`
- Finding: Manual `Ctrl+Q` creates a real campaign entity, but the smoke run stayed at `assembling_pending` with `Army 0/1`, `anchor:none`, rally coordinate present, and `route:no path` for extended runtime despite a populated civilization. Because assembly never completed, no marching/logistics flow appeared.
- Impact: Track A cannot fully verify convoy/base markers in the running app, so P7-D visual consume remains only partially proven. The current manual/operator path is insufficient for a deterministic smoke-ready logistics campaign.
- Resolution / guidance: Add a runtime-owned smoke-ready preparation/launch path or deterministic explicit failure for operator smoke. The App should keep only hotkey routing/toast duties; Runtime should guarantee or clearly reject member preparation/assignment quickly enough to exercise route/logistics UI.
- Status: deferred/routed; no longer blocks P7-D Track A closeout or Step 8 P7-E. Manual evidence showed intermittent operator-smoke behavior and one-person-probe limitations, but this is now routed to Wave 10 SMR prep Step10A / later Track B campaign hardening for durable non-interactive proof and operator-smoke policy, not another P7-D UI loop. Evidence: `Docs/Evidence/Manual/P7-D-Manual-Smoke-Followup.md`.

## 2026-06-03 - Wave 10 P7-C(B) Step Review - Major - Scout intel refresh identity must include owner faction coverage

- Track: Track B / Runtime scout-intel lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 10 `P7-C(B)`
- Finding: The initial P7-C(B) scout-intel tests proved same-owner multi-scout refresh de-duplication, but did not cover two different owner factions observing the same target colony. A future regression that keyed refresh only by observed colony and observation kind could collapse different factions' intel records into one.
- Impact: Track C scout/strategy consume and later Step10A evidence could see incorrect faction-owned intel if owner-dimensional identity regressed.
- Resolution / guidance: Refresh identity must include owner faction + observed colony + observation kind, and tests should assert different owners observing the same target produce separate active records.
- Status: fixed in P7-C(B) fix pass; `DifferentOwnerFactionsObservingSameTargetKeepSeparateIntelRecords` covers separate owner records and the runtime refresh key includes owner faction

## 2026-06-03 - Wave 10 P7-C(B) Step Review - Major - Scout intel freshness must be exported explicitly

- Track: Track B / Runtime scout-intel read-model contract
- Source: Meta step-review synthesis for Wave 10 `P7-C(B)`
- Finding: The initial P7-C(B) export included `CreatedTick`, `LastRefreshTick`, and `ExpirationTick`, but did not expose a consumer-safe age/freshness field even though downstream read-model consumers do not receive the runtime tick needed to compute freshness.
- Impact: Track C, Track A, and Step10A consumers could infer scout intel age inconsistently or ignore freshness entirely.
- Resolution / guidance: Runtime-owned scout intel exports must include an explicit freshness field computed by Runtime; for refreshable records, prefer `TicksSinceRefresh` over ambiguous creation age.
- Status: fixed in P7-C(B) fix pass; `ScoutIntelRuntimeSnapshot` and `ScoutIntelRenderData` expose `TicksSinceRefresh`, with focused tests for initial zero, stale active greater-than-zero, and refresh reset to zero

## 2026-06-02 - Wave 10 P7-B Step Review - Major - Forward-base liveness must not reuse rest eligibility filtering

- Track: Track B / Runtime forward-base lifecycle
- Source: Meta internal step-review + Swarm Assistant review synthesis for Wave 10 `P7-B`
- Finding: The initial P7-B forward-base implementation uses `GetValidCampaignMembers(...)` for both nearby live-member liveness and stamina-rest eligibility. That helper rejects combat/routing/raid/attack/transient members, so a live assigned army member physically near the base can fail to refresh `LastLiveMemberNearTick` and the base can abandon as `no_live_member` after the window.
- Impact: Combat-heavy encounter/siege phases can close a valid forward base even though live assigned campaign members are present nearby, creating incorrect lifecycle/counter evidence for later P7-D/P7-E/Step10A consumers.
- Resolution / guidance: Before P7-B closeout, split forward-base liveness from rest eligibility. Liveness should count any assigned actor still present, `Health > 0`, and within base radius; rest may keep the stricter non-transient/non-blocked filter. Add a regression where a live assigned nearby member is combat/transient (`IsInCombat`, `Job.AttackStructure`, routing, etc.): base remains active/no abandonment, while stamina/rest counters do not increment if rest-ineligible.
- Status: fixed in P7-B second fix pass before closeout; production `AdvanceTick(...)` pruning now preserves live assigned transient actors near an active matching forward base, rest remains strict via `GetValidCampaignMembers(...)`, and focused regression coverage proves member retention, active base liveness, and no rest/counter gain

## 2026-06-02 - Wave 10 P7-A Step Review - Blocking - Supply convoy delivery must prove a live army recipient

- Track: Track B / Runtime supply convoy logistics
- Source: Meta internal step-review + Swarm Assistant review synthesis for Wave 10 `P7-A`
- Finding: The initial P7-A convoy implementation resolved a static convoy target from the campaign objective / route target and delivered `PayloadFood` into `ArmyRationPoolState` once the convoy reached that tile, without proving that the target army or a live campaign recipient was actually at or adjacent to the convoy.
- Impact: A supply convoy can remotely refill a campaign army when the convoy reaches the enemy objective while the army is still elsewhere, weakening the supply-line contract before P7-B/P7-D/P7-G build on this foundation.
- Resolution / guidance: Before P7-A closeout, Track B must make convoy delivery require a deterministic live recipient condition, preferably a live target-army member/anchor adjacent to the convoy, or explicitly rename/redefine the behavior as objective cache delivery. Add a regression where the army is stationary/stalled away from the old static objective and ration pool does not increase until the convoy reaches the army/recipient.
- Status: fixed in P7-A fix pass before closeout; delivery now requires a live assigned target-army recipient adjacent to the convoy, static-target/no-recipient arrival stalls without delivery, and focused regressions cover no-recipient, live-recipient, dead/missing-recipient, and resolved-target cases

## 2026-06-02 - Wave 10 P7-A Step Review - Minor - Convoy home-defense telemetry must not be conflated with campaign launch blocks

- Track: Track B / Runtime logistics counters and future SMR evidence
- Source: Meta internal step-review + Swarm Assistant review synthesis for Wave 10 `P7-A`
- Finding: Convoy request home-defense failure records `CampaignLaunchBlockedByHomeDefense`, and the focused test currently asserts the campaign-launch counter for a convoy spawn block.
- Impact: Later Step 10A ScenarioRunner/SMR export can misclassify whether campaign launch or logistics convoy spawn was blocked by home-defense policy.
- Resolution / guidance: Add a convoy-specific home-defense block counter, or explicitly document counter reuse if intentionally shared. Preferred fix before P7-A closeout: add `ConvoySpawnBlockedByHomeDefense` and update the focused test to assert the convoy-specific counter.
- Status: fixed in P7-A fix pass before closeout; convoy home-defense blocks now use `ConvoySpawnBlockedByHomeDefense` with focused test coverage, leaving campaign-launch home-defense telemetry for launch-path failures

## 2026-06-02 - Wave 10 P6-J(B) Step Review - Blocking - Organic launch must enforce full launch-time campaign gates

- Track: Track B / Runtime organic campaign launch
- Source: Meta internal step-review + Swarm Assistant review synthesis for Wave 10 `P6-J(B)`
- Finding: The initial organic launch implementation created campaigns before proving all active launch gates, including route/path budget suppression, unordered same-faction-pair active-cap semantics, and explicit owner/target distinctness on the organic internal application path. The implementation also risked contract drift by validating a specific `TargetColonyId` but applying through faction-only `TryCreateCampaign(...)` and by filling `CampaignStrategyContext.AvailableWarriors` from all assembly-eligible roles.
- Impact: A campaign can be organically created even when the active P6-J(B) acceptance says it should be suppressed, or can route to a different target colony than the strategy selected once multi-colony states exist. This converts a runtime application slice into hidden policy drift across campaign creation, strategy facts, and future SMR proof.
- Resolution / guidance: Before P6-J(B) closeout, Track B must add launch-time route/path budget suppression, unordered unresolved faction-pair cap coverage if same-pair semantics are intended unordered, explicit organic same-faction suppression, selected-colony-preserving runtime creation/application, and warrior/carrier semantic tests or explicit policy wording. Keep public/manual `TryCreateCampaign(...)` compatibility unless a separate approved contract change exists.
- Status: fixed in P6-J(B) fix pass before closeout; route/path preflight, unordered pair cap, selected-colony creation, warrior/carrier semantics, and explicit organic same-faction suppression were covered by focused runtime tests

## 2026-06-02 - Wave 10 P6-J(B) Step Review - Major - Organic proof-type and gate semantics must stay explicit

- Track: Track B / Runtime and future SMR prep
- Source: Meta internal step-review + Swarm Assistant review synthesis for Wave 10 `P6-J(B)`
- Finding: The initial organic launch implementation kept manual/operator and organic paths behaviorally separate, but persisted campaigns through the same state shape without a visible source/proof seam. The review also found ambiguity around whether the campaign gate means existing diplomacy+combat availability or a distinct `EnableCampaigns` flag.
- Impact: Future Wave 10 SMR prep could overclaim manual or deterministic campaign creation as organic if no active proof-type route exists, and gate wording can drift from implemented runtime availability semantics.
- Resolution / guidance: P6-J(B) closeout must either add a lightweight runtime-owned source/evidence seam or actively route proof-type export to Wave 10 SMR prep Step 10A. It must also explicitly document whether `campaign runtime available` means the existing diplomacy+combat gates or a distinct campaign feature gate.
- Status: deferred actively to Wave 10 SMR prep Step 10A for durable artifact/lane proof-type export; P6-J(B) closeout clarifies runtime proof is organic runtime-test proof only and campaign runtime availability remains diplomacy+combat in this slice

## 2026-06-01 - Wave 10 P6-I Step Review - Minor - App host boundary test targeted stale shim

- Track: Track B / App routing verification
- Source: Swarm Assistant + Meta synthesis for Wave 10 `P6-I`
- Finding: `BoundaryRulesTests.AppGameHost_DoesNotUseDirectWorldOrTechTreeMutation` claimed to verify the App host boundary but read `WorldSim.App/Game1.cs`, while P6-I modified `WorldSim.App/GameHost.cs`.
- Impact: A boundary test can pass while the real host file that routes operator/debug commands is not checked, creating false confidence during App/Runtime seam reviews.
- Resolution / guidance: Keep architecture tests pointed at the actual active host file, or explicitly cover both shim and active host when a compatibility shim remains.
- Status: fixed now in P6-I review follow-up by targeting `WorldSim.App/GameHost.cs`

## 2026-06-01 - Wave 10 P6-H Manual Smoke - Major - App needs a real campaign launch catalyst

- Track: Track B / Track C integration, discovered during Track A campaign UI smoke
- Source: P6-H manual smoke discovery after P6-G/P6-H step-review fixups
- Finding: Runtime/tests/ScenarioRunner can create campaigns through `SimulationRuntime.TryCreateCampaign(...)`, but the interactive app has no operator or organic gameplay path that creates a campaign entity. `Ctrl+F2` only toggles the campaign panel/overlay, Director `declareWar` changes relations/events only, and P6-G strategist output is advisory/not runtime-applied.
- Impact: P6-H can only smoke empty-state/static rendering in the live app, and the broader campaign stack cannot be claimed gameplay-complete because real campaigns do not emerge during normal interactive play.
- Resolution / guidance: Add P6-I manual/operator launch (`Ctrl+Q`, runtime-owned command/API + App routing) for deterministic smoke, then P6-J(B) Track B organic strategist-to-runtime campaign launch application. P6-J(C) Track C opens only if P6-J(B) identifies a concrete advisory strategy contract gap. Track A must keep rendering snapshot-only and must not synthesize campaign state.
- Status: P6-I manual/operator launch and populated panel smoke fixed the manual catalyst portion; P6-J(B) organic strategist-to-runtime application remains the active gate for gameplay-complete campaign launch, with P6-J(C) conditional on a Track B handoff gap

## 2026-05-31 - Wave 10 P6-H Step Review - Major - Broad event keywords must not override source-specific tags

- Track: Track A / Graphics event feed
- Source: Meta + external Swarm step-review synthesis for Wave 10 `P6-H`
- Finding: Display-only campaign keyword expansion can classify Director events containing words like victory, retreat, loot, or ceasefire as Campaign before Director severity handling runs.
- Impact: Event feed colors and operator readability drift from the true event source, even though no runtime event emission changed.
- Resolution / guidance: Prioritize explicit source tags/signals such as `[Director:*]` before broad generic keyword categories, and add a focused classifier regression when expanding shared event-feed keywords.
- Status: fixed and accepted in P6-H closeout; P6-I manual/operator populated smoke is now complete, organic launch proof remains deferred to P6-J

## 2026-05-31 - Wave 10 P6-G Step Review - Major - Advisory strategists must reject impossible launch outputs

- Track: Track C / AI campaign strategist
- Source: Meta + external Swarm step-review synthesis for Wave 10 `P6-G`
- Finding: The advisory campaign strategist could emit launch decisions for self-target factions or zero-warrior target definitions unless the future runtime mapper sanitized those inputs.
- Impact: A later Track B adapter could accidentally promote nonsensical strategy decisions into runtime campaign commands, creating hidden coupling between AI assumptions and runtime validation.
- Resolution / guidance: AI strategy contracts should reject impossible outputs locally and test the boundary even when runtime remains the authoritative executor. Keep future-facing decisions advisory until Track B application hooks exist.
- Status: fixed and accepted in P6-G closeout; runtime application deferred to P6-J(B) Track B gate, with P6-J(C) Track C follow-up only if P6-J(B) identifies a concrete advisory contract gap

## 2026-05-31 - Wave 10 P6-H Step Review - Major - Visual UI polish needs manual smoke before closeout

- Track: Track A / Graphics campaign UI
- Source: Meta + external Swarm step-review synthesis for Wave 10 `P6-H`
- Finding: Build/syntax/scope tests prove the campaign UI compiles and stays in-bounds architecturally, but do not prove visible row readability, overlay marker placement, zoom/pan/order sanity, or resolved outcome visibility.
- Impact: A visually broken or unreadable P6-H implementation could be marked complete based only on automated gates.
- Resolution / guidance: Keep manual visual smoke as an active closeout gate for visible UI steps; if a resolved state cannot be reproduced manually, record that limitation and do not overclaim outcome visibility.
- Status: P6-I manual/operator populated smoke is now complete; P6-H remains accepted with organic launch proof deferred to P6-J

## 2026-05-31 - Wave 10 P6-F Re-review - Blocking - Historical breached campaigns must not suppress future same-pair resolution

- Track: Track B / Runtime campaign resolution
- Source: Meta + external Swarm re-review synthesis for Wave 10 `P6-F`
- Finding: The same-pair suppression helper can consider any historical same ordered-pair campaign with sticky `Siege.Status == Breached`, including campaigns already resolved as attacker victory.
- Impact: A later same ordered-pair campaign that legitimately reaches `NoTarget` or defender-held timeout can be suppressed forever and remain stuck in `Encounter`, converting a shared-flow fix into hidden lifecycle coupling across future campaigns.
- Resolution / guidance: Before P6-F closeout, restrict suppression to the intended current shared-flow/current resolution context, not historical resolved campaigns. Add a regression where a later same ordered-pair campaign after an earlier resolved breach can resolve no-target or timeout normally.
- Status: fixed and accepted in final P6-F Meta re-review; P6-F closeout GREEN

## 2026-05-31 - Wave 10 P6-F Re-review - Major - Opposite-direction score and peace need end-to-end campaign coverage

- Track: Track B / Runtime campaign resolution
- Source: External Swarm re-review synthesis for Wave 10 `P6-F`, accepted by Meta synthesis as verification gap
- Finding: Opposite-direction pair-scoped score coverage currently exercises the private `RecordCampaignWarScore` helper via reflection rather than actual campaign resolution, state export, treaty direction, and peace eligibility behavior.
- Impact: Private ledger math can pass while runtime campaign resolution or peace eligibility diverges from the locked pair-scoped signed contract.
- Resolution / guidance: Before P6-F closeout, add an end-to-end opposite-direction campaign resolution test proving signed cumulative score and attacker-perspective peace eligibility through `CampaignResolutionState` and, where relevant, read-model export.
- Status: fixed and accepted in final P6-F Meta re-review; P6-F closeout GREEN

## 2026-05-31 - Wave 10 P6-F Step Review - Blocking - Same-pair resolution must not score contradictory outcomes

- Track: Track B / Runtime campaign resolution
- Source: Meta internal step-review synthesis for Wave 10 `P6-F`, before external Swarm synthesis
- Finding: A same-pair campaign flow can resolve one campaign as attacker victory from campaign-owned `Siege.Breached` while another same-pair non-driver/no-target campaign resolves as defender-held in the same resolution pass.
- Impact: The same attacker/defender pair can receive contradictory campaign-resolution side effects from one shared World siege path, including a positive victory delta and a negative defender-held delta. This overclaims independent campaign truth across the accepted pair-keyed World siege constraint.
- Resolution / guidance: Before P6-F closeout, suppress or non-score same-pair non-driver/no-target resolution when another same-pair campaign owns breach/driver truth for the shared flow. Add a regression asserting the non-driver does not receive defender-held resolution or war-score delta in the driver-breach path.
- Status: fixed and accepted in final P6-F Meta re-review; P6-F closeout GREEN

## 2026-05-31 - Wave 10 P6-F Step Review - Major - War-score ledger must match pair-scoped contract or be renamed

- Track: Track B / Runtime campaign resolution
- Source: Meta internal step-review synthesis for Wave 10 `P6-F`
- Finding: The implementation/Combined note claims a pair-scoped campaign war-score ledger, but the observed implementation keys score directionally by `(attacker, defender)`.
- Impact: Opposite-direction campaigns in the same war relation can accumulate separate score truth, which can make peace eligibility and downstream read-model/SMR interpretation diverge from the documented pair-scoped contract.
- Resolution / guidance: Before P6-F closeout, either normalize the faction pair with signed score semantics and add opposite-direction coverage, or explicitly narrow the model/wording/tests to directional campaign score.
- Status: fixed and accepted in final P6-F Meta re-review; P6-F closeout GREEN

## 2026-05-31 - Wave 10 P6-F Step Review - Minor - Read-model resolution export needs direct assertion

- Track: Track B / Runtime/read-model campaign resolution
- Source: External Swarm Assistant step-review input for Wave 10 `P6-F`, accepted by Meta synthesis as valid test hardening
- Finding: The implementation maps `CampaignResolutionState` into `CampaignResolutionRenderData`, but focused tests do not directly assert `runtime.GetSnapshot().Campaigns.Single().Resolution.*` fields.
- Impact: P6-F explicitly creates read-model surface for P6-G/P6-H/SMR consumers; a mapping regression could compile and pass current runtime-state assertions.
- Resolution / guidance: Add direct focused snapshot assertions for resolved campaign `Resolution` fields, preferably in an existing attacker-victory or defender-held test.
- Status: fixed and accepted in final P6-F Meta re-review; P6-F closeout GREEN

## 2026-05-28 - Wave 10 P6-E Step Review - Blocking - Encounter campaigns must not create ghost siege pressure

- Track: Track B / Runtime campaign-siege integration
- Source: Meta + Swarm step-review synthesis for Wave 10 `P6-E`
- Finding: `QueueCampaignSiegePressureForActiveEncounters(...)` queues siege pressure for every `Encounter` campaign, while `PruneInvalidCampaignMembers(...)` skips `CampaignPhase.Encounter`. A campaign that entered encounter validly can later lose/invalid members and still feed World siege pressure.
- Impact: Creates ghost campaign siege pressure, can keep World siege sessions active with no valid campaign attackers, and overclaims the P6-E understrength guard.
- Resolution / guidance: Before P6-E closeout, revalidate encounter campaign roster/living members against `RequestedMemberCount` before recording/queueing siege pressure, or transition/suppress invalid encounter campaigns deterministically. Add a regression where an encounter campaign loses its member before the next positive tick and asserts no further siege pressure/active siege/outcome.
- Re-review note: P6-E fix-loop re-review accepted that the dead-member case is covered, but found the fix still only counts `Health > 0f`; alive-but-invalid members (`IsRouting`, `IsInCombat`, active battle/group, `Fight`/`Flee`/`RaidBorder`/`AttackStructure`) can still be pressure-capable unless the stricter campaign-member validity model is reused.
- Status: fixed and accepted in final P6-E Meta + external Swarm deep-review synthesis; P6-E closeout GREEN

## 2026-05-28 - Wave 10 P6-E Step Review - Blocking - Breach/no-target sync must preserve campaign-owned truth

- Track: Track B / Runtime campaign-siege integration
- Source: Meta + Swarm step-review synthesis for Wave 10 `P6-E`
- Finding: `MarkNoTarget(...)` can overwrite campaign siege status after a breach/target destruction, and breach sync can match same-pair recent breaches when `TargetStructureId < 0`, allowing no-target encounters to inherit stale same-pair breach evidence.
- Impact: Campaign encounter read-model can flip from `siege_breached` to `no_siege_target`, or report `siege_breached` for a campaign that has no current target. This weakens P6-E observability and can mislead P6-F resolution.
- Resolution / guidance: Before P6-E closeout, make breached campaign state/read-model outcome persistent for the campaign target, and require known/current campaign target identity before observing breaches. Add regressions for post-breach target disappearance and prior same-pair breach followed by a no-target campaign.
- Status: fixed and accepted in final P6-E Meta + external Swarm deep-review synthesis; P6-E closeout GREEN

## 2026-05-28 - Wave 10 P6-E Step Review - Major - Same-pair campaign siege aliasing needs policy or guard

- Track: Track B / Runtime campaign-siege integration
- Source: Meta + Swarm step-review synthesis for Wave 10 `P6-E`
- Finding: `TryCreateCampaign(...)` can create multiple campaigns with the same attacker/defender pair, while World siege sessions and campaign sync match by attacker/defender pair. Multiple same-pair campaigns can observe the same world siege and overclaim independent campaign progress.
- Impact: Creates ambiguous campaign-to-siege attribution and hidden coupling to World's pair-keyed siege identity.
- Resolution / guidance: Before P6-E closeout or as part of the same fix loop, either enforce one active encounter siege per attacker/defender campaign pair, or explicitly document and test shared pair-level siege semantics so counters/read-models cannot overclaim independent sieges.
- Status: fixed and accepted in final P6-E Meta + external Swarm deep-review synthesis; P6-E closeout GREEN

## 2026-05-28 - Wave 10 P6-E Step Review - Minor - Disabled siege mode must not overclaim campaign pressure

- Track: Track B / Runtime campaign-siege integration
- Source: Swarm step-review for Wave 10 `P6-E`, accepted by Meta synthesis as non-blocking fix-loop item
- Finding: Campaign siege pressure can be recorded when `World.EnableSiege` or `EnableCombatPrimitives` disables the existing World siege resolver.
- Impact: Campaign state can report seeking/pressure semantics even though World intentionally refuses to create active siege state, weakening the claim that campaign siege state mirrors World siege flow.
- Resolution / guidance: In the P6-E fix loop, either gate campaign pressure recording on the same World siege enablement semantics or explicitly document/test disabled-mode behavior so no read-model/counter overclaim occurs.
- Re-review note: P6-E fix-loop re-review accepted the disabled-from-start regression, but found `SyncCampaignSiegeStates(...)` can still observe a matching recent breach after a campaign previously recorded a target and the resolver is later disabled. Disabled flow must not promote that path to `siege_breached`.
- Second re-review note: the local narrow fix suppresses breach sync while the resolver remains disabled, but `TargetStructureId` survives suppression. If the target is breached while disabled and the resolver is re-enabled before the 120-tick recent-breach window expires, or if a same-pair takeover driver breaches the retained target, the suppressed campaign can still inherit stale breach evidence.
- Status: fixed and accepted in final P6-E Meta + external Swarm deep-review synthesis; non-breached suppression clears retained target identity, and focused regressions cover disabled re-enable stale breach inheritance for both resolver flags plus same-pair suppressed prior driver inheritance.

## 2026-05-21 - Wave 9 Deep Review - Major - Partial campaign rosters must not complete then churn

- Track: Track B / Runtime campaign lifecycle
- Source: Meta deep-review synthesis for Wave 9, after external Swarm GREEN review
- Finding: `IsCampaignAssemblyComplete(...)` can mark assembly complete when a non-empty roster cannot grow, even if `MemberCount < RequestedMemberCount`; marching validation then immediately returns understrength campaigns to assembly. This can create assembly-complete / march-start / returned-or-aborted churn with no real route progress for campaigns that requested more members than are eligible.
- Impact: Wave 10 siege integration could inherit misleading campaign lifecycle counters or a stuck campaign loop, especially once larger campaign rosters become common.
- Resolution / guidance: Before Wave 10 `P6-E` starts, add a focused regression with `requestedMemberCount > eligible candidates` and align the invariant: either require full requested roster before `MarkAssemblyComplete(...)`, or explicitly support partial campaigns and update marching validation/counters accordingly.
- Status: fixed and accepted in Meta P6-E preflight re-review; strict requested-strength regression added; `P6-E` proper unblocked

## 2026-05-21 - Wave 9 Deep Review - Major - Carrier/resupply evidence must not overclaim delivery semantics

- Track: Track B + SMR/Meta / ScenarioRunner Wave 9 SMR evidence semantics
- Source: Meta deep-review synthesis for Wave 9, after external Swarm GREEN review
- Finding: The deterministic `carrier_resupply` lane proves carrier assignment plus direct carried-inventory/ration-pool supply-source consumption, but the final evidence wording and counters use delivery/resupply language. Runtime AI still leaves `SupplyCarrierCanDeliver=false`, so actual carrier delivery command/path behavior is not proven by Wave 9.
- Impact: Wave 10 planning may incorrectly treat carrier delivery/resupply as proven runtime behavior and build logistics or convoy assumptions on an evidence overclaim.
- Resolution / guidance: Before Wave 10 `P6-E` starts, reword/additively clarify Wave 9 evidence and counters as carrier assignment + supply-source consumption probes, or add a focused probe that proves actual delivery semantics before claiming delivery/resupply executed.
- Status: fixed and accepted in Meta P6-E preflight re-review via additive `carrierSupplyApplications` evidence and wording cleanup; actual actor command/path delivery remains unproven/out of Wave9 scope

## 2026-05-21 - Wave 9 Deep Review - Minor - Retired hardening plan should not look like active frontier

- Track: Meta / documentation state
- Source: Meta deep-review synthesis for Wave 9
- Finding: `Docs/Plans/Master/Wave9-Runtime-Campaign-Hardening-Plan.md` still says the implementation frontier is `P5-G (B part)` and uses unchecked task scaffolding, while Combined and evidence docs mark Wave 9 closed.
- Impact: External reviewers or future Track agents may mistake a retired acceptance-detail plan for current sequence authority.
- Resolution / guidance: Mark the hardening plan as historical/retired or add a top-level pointer to Combined as the current source of truth during the next docs cleanup.
- Status: open; routed to Meta docs cleanup after P6-E preflight blockers are handled

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Blocking - Drilldown timeline must not stamp final Wave9 counters onto every sample

- Track: Track B / ScenarioRunner Wave 9 SMR evidence surface
- Source: Meta + Swarm step-review synthesis for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: `Program.cs` captures drilldown timeline samples during the normal run, builds Wave9 telemetry only after the run, then applies one final `ScenarioWave9TimelineSnapshot` to every sample. The focused test asserts positive carrier evidence in the first timeline sample, locking in retroactive final-counter stamping.
- Impact: Drilldown artifacts appear temporal but can report future/final Wave9 evidence at tick 1 or every tick, creating false-green SMR validation risk for the exact evidence surface Step 12A is supposed to prepare.
- Resolution / guidance: Before Step 12A closeout, either capture tick-accurate Wave9 telemetry at sample time or keep Wave9 evidence run-level only and leave timeline `wave9` empty/default unless values are tick-accurate. Update tests so early timeline samples cannot validate retroactively stamped final counters.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review; SMR Analyst validation remains the next active gate

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Major - Synthetic Wave9 probes must not be mislabeled as run telemetry

- Track: Track B / ScenarioRunner Wave 9 SMR evidence semantics
- Source: Meta internal step-review for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: Several Wave9 deterministic lanes build telemetry from separate mini-worlds or a separate `SimulationRuntime` after the normal ScenarioRunner run and attach the result to the run artifact.
- Impact: If these outputs are presented as ordinary run telemetry, SMR Analyst may believe the actual reported run executed the mechanics. This is acceptable only if clearly separated or explicitly documented as deterministic prep/probe evidence, not final organic run proof.
- Resolution / guidance: Before Step 12A closeout, either execute Wave9 lanes inside the reported run/runtime state or label/route them as deterministic probe evidence with clear docs/tests and SMR Analyst validation caveat. Do not use sidecar probes as final Wave 9 acceptance.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review via explicit `deterministic_probe` / `not_tick_sampled` metadata and docs; SMR Analyst validation remains the next active gate

## 2026-05-19 - Wave 9 SMR Prep Export/Config - Minor - Wave9Scenario alias matrix should be explicit

- Track: Track B / ScenarioRunner config surface
- Source: Swarm step-review for Wave 9 `Wave-9-SMR-prep-export/config`
- Finding: The closeout-plan alias `foraging-extension` normalizes to `campaign_foraging`, while `campaign-foraging` returns config error. This may be intentional, but the implementation brief phrase "hyphen aliases" can be read as accepting `campaign-foraging`.
- Impact: Operator confusion or inconsistent evidence recipe usage can create avoidable config errors during SMR prep/validation.
- Resolution / guidance: Before Step 12A closeout, either explicitly document that `campaign-foraging` is intentionally invalid or accept it as an additional harmless alias. Add alias matrix coverage for all canonical lanes.
- Status: fixed and accepted in Step 12A Meta + Swarm re-review by accepting `campaign-foraging` as an alias and adding alias matrix coverage; SMR Analyst validation remains the next active gate

## 2026-05-18 - Wave 9 P6-D(A) Campaign Overlay - Blocking - Invalid army anchor sentinel must not render as map coordinate

- Track: Track A / Graphics campaign overlay and panel consume
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(A-part)`
- Finding: `CampaignOverlayPass.DrawArmyMarker(...)` renders `ArmyRenderData.AnchorX/AnchorY` unconditionally, and `CampaignPanelRenderer` displays `anchor(x,y)` unconditionally. P6-D(B) read-model can validly export `AnchorActorId=-1`, `AnchorX=-1`, `AnchorY=-1` when no assigned/living anchor exists.
- Impact: Pending/memberless campaigns can draw a bogus off-map/top-left marker and show misleading `anchor(-1,-1)` text, violating safe snapshot consume for default/sentinel campaign states.
- Resolution / guidance: Before P6-D(A) closeout, guard `AnchorActorId < 0`; skip the army marker or use an explicit valid fallback such as route origin/rally with distinct pending styling, and display `anchor:none` in the panel. Include this case in focused review/manual smoke.
- Status: fixed in P6-D(A) targeted Track A follow-up; manual smoke remains a separate closeout gate

## 2026-05-18 - Wave 9 P6-D(A) Campaign Overlay - Minor - Low-cost wording should distinguish shared pixel texture from content textures

- Track: Track A / Graphics campaign overlay documentation and handoff wording
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(A-part)`
- Finding: The handoff wording says the overlay uses "no textures", but `CampaignOverlayPass` correctly draws simple primitives with the shared `context.Textures.Pixel` texture.
- Impact: The implementation remains low-cost, but the literal claim is imprecise and can confuse future reviewers about allowed primitive rendering.
- Resolution / guidance: Use precise wording: "no custom/content texture assets; uses only the shared pixel texture for primitive drawing."
- Status: fixed in P6-D(A) targeted Track A follow-up; wording now distinguishes shared pixel primitive drawing from custom/content texture assets

## 2026-05-18 - Wave 9 P6-D(B) Snapshot Handoff - Blocking - Interpolated snapshots must preserve Campaigns

- Track: Track A / Graphics snapshot consume, discovered during Track B P6-D(B) handoff review
- Source: Meta internal correctness lane + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: `WorldSnapshotInterpolator.Interpolate(...)` constructs `WorldRenderSnapshot` through the compatibility constructor, which defaults `Campaigns` to `Array.Empty<CampaignRenderData>()`, so interpolated snapshots drop populated campaign read-model data.
- Impact: Track A can receive an apparently stable P6-D(B) campaign snapshot from runtime but lose it in the existing Graphics interpolation path before overlay consume.
- Resolution / guidance: Before P6-D handoff closeout / Track A overlay consume, preserve `current.Campaigns` in `WorldSnapshotInterpolator.Interpolate(...)` and add a focused regression proving interpolation keeps non-interpolated campaign collections. This requires explicit Graphics/Track A scope approval if fixed before Step 11.
- Status: fixed in P6-D(B) after authorized first-gate Graphics handoff patch and Meta re-review

## 2026-05-18 - Wave 9 P6-D(B) Read Model - Blocking - Public strings must not leak simulation enum names

- Track: Track B / Runtime campaign read-model contract
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: `SimulationRuntime.BuildCampaignRenderData(...)` exports public read-model strings via simulation enum `.ToString()` for campaign phase, supply source, forage status, and forage failure reason, and tests assert those PascalCase enum names.
- Impact: Track A would couple to C# enum member names/casing before the UI consume step, making the snapshot contract brittle and violating the P6-D acceptance that phase/status/source/failure fields be read-model-safe.
- Resolution / guidance: Before P6-D(B) closeout, replace enum `.ToString()` exports with explicit stable read-model mapping helpers (prefer lower_snake_case strings or dedicated read-model enums) and update tests to assert those stable contract literals.
- Status: fixed in P6-D(B) after Meta re-review

## 2026-05-18 - Wave 9 P6-D(B) Read Model - Major - Prove route fields and snapshot no-mutation contract

- Track: Track B / Runtime campaign read-model tests
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-D(B-part)`
- Finding: Current tests cover campaign presence, detachment, supply, counters, waypoint count, and encounter marker, but do not directly assert route intent coordinates, resolved objective coordinates, `NextWaypointIndex` to `IsNext` consistency, or before/after `GetSnapshot()` counter/cache immutability.
- Impact: P6-D(B) is the Track A handoff contract; without these checks, later consumers may rely on unpinned fields or miss accidental side effects in snapshot mapping.
- Resolution / guidance: Before P6-D(B) closeout, add focused assertions for route intent/resolved objective/next waypoint consistency and a before/after `GetSnapshot()` no-mutation check for route counters/cache state. Keep mapping side-effect free.
- Status: fixed in P6-D(B) after Meta re-review

## 2026-05-17 - Wave 9 P6-C March Supply - Blocking - Revalidate roster after supply-induced routing

- Track: Track B / Runtime campaign march lifecycle
- Source: Meta + Swarm re-review synthesis for Wave 9 `P6-C` targeted fix pass
- Finding: March supply ticks before route/path/movement work, and `ArmySupplyModel` can route members via `BeginRouting(...)`; the current march loop can then keep using the pre-supply member list for same-tick movement/encounter checks.
- Impact: A member made invalid by supply attrition can still march or trigger encounter in the same tick, violating the P6-C lifecycle invariant that routing/invalid members must not march.
- Resolution / guidance: Before P6-C closeout, revalidate/recompute march-capable members immediately after supply ticking and before pathing, movement, or encounter transition. If supply causes understrength/invalid roster, return to assembly or skip movement for that tick. Add carried-inventory/no-supply regression and either add ration-pool/multi-member variants or document why focused coverage is sufficient.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C March Supply - Blocking - No-progress marching ticks skip logistics cost

- Track: Track B / Runtime campaign march lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: Current march supply ticking happens only after successful march progress; valid `Marching` campaigns that hit no-target/no-path/no-move branches record `NoProgress` and skip `TickCampaignMarchSupply(...)`.
- Impact: A blocked or stuck marching army can avoid supply consumption indefinitely, contradicting the submitted P6-C contract that march supply uses exactly one source per campaign tick.
- Resolution / guidance: Before P6-C closeout, either tick exactly one supply source after roster validation on every eligible positive-dt marching tick, independent of movement success, or explicitly change the contract/docs/tests to progress-only logistics. Add a deterministic no-progress supply regression.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C Route Cache - Blocking - Cache validity must use persistent world topology

- Track: Track B / Runtime campaign route lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: Campaign route cache validation is based on a newly constructed `NavigationGrid` per call, whose topology version is instance-local rather than tied to persistent world topology.
- Impact: Cached routes can remain apparently valid across topology changes until the immediate next-step blocked check catches them, weakening deterministic recompute/cache-hit semantics and delayed blocked-route coverage.
- Resolution / guidance: Before P6-C closeout, tie campaign route cache validity to persistent world topology or an equivalent persisted topology signature, and add delayed blocked cached-step coverage beyond `PeekNext`.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-C Encounter Target - Major - Fallback route target can diverge from encounter trigger

- Track: Track B / Runtime campaign encounter lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-C`
- Finding: `TryGetCampaignMarchTarget(...)` can choose a fallback passable tile around the target colony, while encounter detection still checks only original route target proximity.
- Impact: If fallback radius exceeds encounter proximity, a campaign can reach the resolved route target but never enter `Encounter`, then stall as path-to-current-target returns no usable path.
- Resolution / guidance: Before P6-C closeout, either store/use the resolved march objective for encounter proximity or constrain fallback selection to encounter-valid proximity. Add a regression for blocked target/proximity with fallback radius greater than one.
- Status: fixed in P6-C after Meta + Swarm re-review

## 2026-05-17 - Wave 9 P6-B Lifecycle Fix - Minor - Add adversarial roster lifecycle coverage before march semantics harden

- Track: Track B / Runtime campaign lifecycle
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B` lifecycle fix
- Finding: Shared predicates cover assigned-member invalidation broadly, but explicit regressions are still sparse for individual edge states such as `Health <= 0`, isolated `IsInCombat`, invalidation caused during `_world.Update`, and max-one replacement after pruning with multiple candidates.
- Impact: P6-C will add march semantics on top of assembled rosters; without adversarial coverage, later refactors could assume roster permanence or miss a specific invalidation path even though the shared helper currently handles it.
- Resolution / guidance: Before or during P6-C march start, add focused guard tests for health-zero assigned members, isolated in-combat invalidation, world-update-induced invalidation, multi-candidate max-one replacement after pruning, and roster revalidation before first march step.
- Status: fixed in P6-C

## 2026-05-17 - Wave 9 P6-C March Handoff - Guidance - Revalidate roster before first march step

- Track: Track B / Runtime campaign march handoff
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B` lifecycle fix
- Finding: P6-B now assembles rosters and hands off with `CampaignPhase.Marching`, but P6-C must not assume roster permanence between assembly completion and first march tick.
- Impact: Actors can die, route, enter combat, or otherwise become unavailable after assembly; starting march without revalidation could reintroduce the same lifecycle class of bugs fixed in P6-B.
- Resolution / guidance: P6-C must revalidate campaign roster before the first march movement/counter update and define deterministic prune/replacement/non-complete behavior for newly invalid members.
- Status: fixed in P6-C

## 2026-05-17 - Wave 9 P6-B Campaign Assembly - Blocking - Revalidate assigned members before movement and completion

- Track: Track B / Runtime campaign assembly
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B`
- Finding: Initial campaign roster eligibility can exclude routing, combat, and hard-job actors, but already assigned army members still need lifecycle revalidation before later rally movement and assembly completion.
- Impact: An actor can become routing, in-combat, active in a battle/group, or hard-combat-job-owned after recruitment; moving or completing that actor as campaign-owned can violate combat/routing ownership and hand P6-C an invalid roster.
- Resolution / guidance: Before moving rostered members or counting them for completion, revalidate current actor state or define explicit campaign ownership semantics. Add regressions where an assigned member becomes routing, active combat/battle/group, or hard-job-owned after assignment.
- Status: fixed

## 2026-05-17 - Wave 9 P6-B Campaign Assembly - Blocking - Handle dead or missing assigned members deterministically

- Track: Track B / Runtime campaign assembly
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-B`
- Finding: A campaign roster that stores only actor IDs can become permanently stuck if an assigned actor dies or is removed before rally completion and no prune/replacement/failure policy exists.
- Impact: `MemberCount >= RequestedMemberCount` can prevent replacement while completion can never succeed because a roster ID no longer resolves to a living person, leaving assembly blocked forever and making P6-C handoff unsafe.
- Resolution / guidance: During assembly, prune dead/missing members and allow deterministic replacement, or introduce an explicit failed/disbanding/recruiting fallback state. Add tests for assigned-member death/removal before rally completion and full-roster recovery/failure behavior.
- Status: fixed

## 2026-05-15 - Wave 9 P6-A1 Campaign Query Boundary - Minor - Prove detached roster snapshots once rosters become non-empty

- Track: Track B / Runtime campaign query boundary
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-A1`
- Finding: Empty-roster snapshot tests can prove the public query seam no longer exposes the live roster collection, but they do not prove copied roster contents remain stable after future assembly/rally mutation fills `MemberActorIds`.
- Impact: P6-B will introduce real roster assignment/mutation; without a non-empty retained-snapshot regression, a later mapper refactor could accidentally re-expose live roster state while existing empty-roster tests still pass.
- Resolution / guidance: When P6-B adds roster mutation, add a focused non-empty roster regression that captures a `CampaignRuntimeSnapshot`, mutates internal campaign/army roster state through runtime methods, and asserts the retained snapshot keeps the original copied member IDs.
- Status: fixed

## 2026-05-15 - Wave 9 P6-A Campaign Entities - Guidance - Keep runtime state query seams internal until read-model export

- Track: Track B / Runtime campaign entities
- Source: Meta + Swarm step-review synthesis for Wave 9 `P6-A`
- Finding: Returning a copied collection of live runtime entity objects is useful for runtime tests, but it is not an immutable read-model contract for UI, ScenarioRunner, or cross-track consumption.
- Impact: Later P6-B/P6-C progression may add mutability to campaign/army state; if downstream tracks consume live state directly, snapshot boundary and ownership can drift.
- Resolution / guidance: Treat `SimulationRuntime.Campaigns` as a runtime-internal/test seam for P6-A. Before Track A/SMR consumption in P6-D or Wave 9 SMR prep, add explicit immutable read-model/export DTOs rather than exposing live runtime objects.
- Status: guidance

## 2026-05-13 - Wave 9 P5-H Foraging AI - Blocking - Zero-score support goals must not become selected trace goals

- Track: Track C / AI foraging behavior
- Source: Meta + Swarm step-review synthesis for Wave 9 `P5-H (C part)`
- Finding: `GoalSelector` can select the first non-cooldown goal even when its score is `0f`. For trace-only support goals such as `ForageArmySupply`, command-level no-demand guards can return `Idle` while `Trace.SelectedGoal` still reports the support goal after cooldown rotation.
- Impact: A runtime-created no-demand context can still pollute AI traces with a support goal that acceptance says must never be selected without explicit demand. Focused command/preview tests can pass while the selected-goal invariant remains unproven.
- Resolution / guidance: Prevent zero-score support goals from becoming selected goals, either with a positive-score eligibility guard in goal selection or an explicit selector policy for trace-only support goals. Add runtime multi-planner/no-demand regressions that assert `Trace.SelectedGoal`, command, and preview together.
- Status: fixed

## 2026-05-13 - Wave 9 P5-G Supply Carrier AI - Blocking - Do not let trace-only commands dominate default utility selection

- Track: Track C / AI supply-carrier behavior
- Source: Meta + Swarm step-review synthesis for Wave 9 `P5-G (C part)`
- Finding: A trace-only AI command that maps to `Job.Idle` can become the highest-scoring default utility goal if it is not gated by explicit demand or execution readiness. In `P5-G (C part)`, `MaintainArmySupply -> AssignSupplyCarrier` could be selected in safe no-carrier runtime context while no runtime carrier state is created.
- Impact: The actor can repeatedly choose a no-effect idle command, suppressing normal productive goals and creating a no-progress loop despite focused one-tick command trace tests passing.
- Resolution / guidance: Either execute the command by mutating the intended runtime state, or keep it trace-only but gate it behind explicit demand/readiness so it cannot dominate default runtime utility selection. Add a multi-tick regression proving carrier commands do not repeat indefinitely as no-op idle work and that useful fallback/progress resumes.
- Status: fixed

## 2026-05-12 - Review Process - Guidance - Document accepted residual findings before step closeout

- Track: Meta Coordinator / all Tracks
- Source: User policy after Wave 9 `P5-H (B part)` review synthesis
- Finding: Minor, nit, or residual-risk findings that are accepted rather than fixed can be lost if they remain only in chat review output.
- Impact: Future agents may repeat the issue or assume the risk was resolved instead of consciously deferred.
- Resolution / guidance: Any non-fixed review finding that remains after final synthesis must be recorded in a durable artifact, preferably `Docs/Review-Findings-Registry.md` and, when tied to future implementation, the relevant master/sequence plan as a deferred follow-up.
- Status: guidance

## 2026-05-12 - Wave 9 P5-H Foraging - Guidance - Treat HarvestFailed as defensive until harvest seam changes

- Track: Track B / Runtime foraging model
- Source: Step review synthesis for Wave 9 `P5-H (B part)`
- Finding: `ArmyForageFailureReason.HarvestFailed` is a defensive branch after prevalidation; current `World.TryHarvest(...)` / `Tile.Harvest(...)` behavior makes it effectively unreachable without introducing a mockable harvest seam or concurrent mutation path.
- Impact: Adding test-only seams just to force this branch would be unnecessary churn now, but future harvest refactors could make the branch reachable without coverage.
- Resolution / guidance: Keep the branch as defensive. If a mockable harvest seam, concurrent harvest path, or changed `World.TryHarvest(...)` contract is introduced, add a focused `HarvestFailed` regression test at that time.
- Status: guidance

## 2026-05-12 - Wave 9 P5-H Foraging - Minor - Cover no-yield and no-capacity branches explicitly

- Track: Track B / Runtime foraging model
- Source: Step review synthesis for Wave 9 `P5-H (B part)`
- Finding: A foraging model can implement safe no-yield/no-capacity handling while focused tests only cover successful saturation near capacity, leaving the explicit failure branch unprotected.
- Impact: Future cap or pool-capacity changes could silently convert a zero-capacity attempt into a mutation, overflow, or misleading success.
- Resolution / guidance: Add a focused test for full ration-pool or zero-yield conditions that asserts `NoYield`, unchanged source node, unchanged pool, and failure counter updates.
- Status: fixed

## 2026-05-12 - Wave 9 P5-G Supply Carrier - Major - Keep singular assignment state and actor role flags synchronized

- Track: Track B / Runtime supply carrier hook
- Source: Step review synthesis for Wave 9 `P5-G (B part)`
- Finding: A singular carrier state such as `AssignedCarrierActorId` can drift from per-actor role flags if reassignment does not clear the previous carrier role, or if clear operations accept an actor that is not the assigned carrier.
- Impact: Later runtime, AI, snapshot, or UI consumers can observe multiple `SupplyCarrier` actors, or an empty carrier state while an actor remains snapshot-visible as a carrier.
- Resolution / guidance: Lock the lifecycle with focused tests for assign A -> assign B, wrong-actor clear, assigned-actor clear, and snapshot consistency. Reassignment should either reject until explicit clear or atomically transfer the role; wrong-actor clear should reject/no-op without mutating state.
- Status: fixed

## 2026-05-11 - Wave 9 P5-I Ration Pool - Minor - Prove lifecycle conservation before integration

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-I`
- Finding: Reservation-pool supply models can pass isolated reserve, consume, and return tests while still lacking one end-to-end conservation proof across the whole lifecycle.
- Impact: Later carrier/campaign wiring could accidentally duplicate or lose food when combining reservation, consumption, and return paths.
- Resolution / guidance: Add a focused reserve -> consume -> return test proving final colony food equals original food minus consumed food, and pool/member inventory state remains consistent.
- Status: fixed

## 2026-05-11 - Wave 9 P5-I Ration Pool - Minor - Saturate all reservation arithmetic consistently

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-I`
- Finding: If only some reservation calculations use saturating arithmetic, extreme caller/config values can overflow an unguarded intermediate such as home-reserve computation.
- Impact: Overflow can violate the home-reserve contract and allow reservation from food that should remain protected.
- Resolution / guidance: Use the same saturating/normalized arithmetic policy for home reserve, desired budget, pool add, and return paths, and cover boundary inputs with focused tests.
- Status: fixed

## 2026-05-11 - Wave 9 P5-F Army Supply - Minor - Lock fractional zero-supply semantics

- Track: Track B / Runtime supply model
- Source: Step review synthesis for Wave 9 `P5-F`
- Finding: Integer inventory-consumption models can leave zero carried food plus sub-unit fractional demand in an ambiguous state if tests do not explicitly define whether supply pressure starts immediately or only after a whole food unit is unmet.
- Impact: Downstream fallback budget, carrier, foraging, or campaign steps may reinterpret `IsOutOfSupply` differently and create inconsistent attrition/routing behavior.
- Resolution / guidance: Add focused regression coverage or documentation that locks the intended semantics before closing the step: either zero food with fractional demand is not out-of-supply until whole-unit demand is unmet, or change the model to apply immediate zero-supply pressure.
- Status: fixed

## 2026-04-30 - Wave 8 ScenarioRunner Tests - Minor - Bound nested runner process helpers

- Track: Track B / SMR Analyst test harness
- Source: Wave 8 deep review after Step 7B closeout
- Finding: ScenarioRunner xUnit helpers launched nested `dotnet run` processes with unbounded waits and no process-tree cleanup.
- Impact: A hung or externally timed-out test could leave orphan `dotnet.exe`/MSBuild processes and poison later verification with file locks.
- Resolution / guidance: Use a shared test-only process helper with a generous default timeout, env override (`WORLDSIM_SCENARIO_TEST_TIMEOUT_MINUTES`), concurrent stdout/stderr reads, and `Kill(entireProcessTree: true)` cleanup. Do not apply this timeout to direct/manual SMR CLI runs.
- Status: fixed

## 2026-04-30 - SMR Clustering Deep Evidence - Minor - Drilldown topN may miss clustering-worst runs

- Track: SMR Analyst / ScenarioRunner observability
- Source: Step review for `clustering-deep-wave8-prewave9-001`
- Finding: The drilldown `topN` selector can choose runs with `score=0` and omit the highest clustering/backoff anomaly runs, because the generic drilldown score is not clustering-focused.
- Impact: A clustering investigation can correctly rank worst runs from `summary.json`/`anomalies.json`, but drilldown artifacts may not include detailed timelines for the actual clustering-worst runs.
- Resolution / guidance: For clustering-focused evidence, explicitly verify whether `drilldown/index.json` covers the worst anomaly runs. If not, rank from `summary.json`/`anomalies.json` and either record that limitation or add a future clustering-aware drilldown selector.
- Status: guidance

## 2026-04-30 - TR2-B Minimal Solver Slice - Minor - Report unsupported nested output features explicitly

- Track: Track D / Tools.Refinery migration
- Source: Step review for Wave 8.5 `TR2-B`
- Finding: Minimal solver slices may intentionally model only a subset of the existing internal assertion DTO. If nested fields are omitted from the formal problem, they can be mistaken as solver-validated unless diagnostics or tests make the omission explicit.
- Impact: Later bridge-mapping work could accidentally assume fields such as effects, biases, campaign, or causal chains were formally validated when the minimal slice only proved story/directive shell consistency.
- Resolution / guidance: For every intentionally unsupported assertion field, either emit an explicit unsupported-feature diagnostic or add a test that proves the field is out of scope for the current slice. Do not silently treat omitted fields as solved/validated facts.
- Status: guidance

## 2026-05-03 - TR2-D Cross-Track Review - Guidance - Classify no-churn diffs before closeout

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.5 `TR2-D`
- Finding: A Track D no-churn gate can legitimately detect parallel Track B-owned C#/ScenarioRunner diffs during a coordinated cross-track step.
- Impact: Treating all non-empty no-churn gates as Track D bugs can incorrectly block allowed parallel work, while ignoring them can hide ownership violations.
- Resolution / guidance: Stop Track D closeout, classify the foreign diffs by owner/scope, then resume only the owning track's verification. Mark the step YELLOW until the dependent Track B consume/evidence pass and Track D final verification are both complete.
- Status: guidance

## 2026-05-03 - TR2-D Evidence Fixtures - Major - Keep marker-rich evidence payloads semantically consistent

- Track: Track B / ScenarioRunner evidence
- Source: Step review for Wave 8.5 `TR2-D` Track B B2
- Finding: Repo-local marker-rich fixture/mock responses can claim `directorSolverValidatedCoverage:story_core` while omitting a core field such as story beat `severity` from the response patch payload.
- Impact: The artifact proves parser persistence but overclaims solver-backed semantic coverage, weakening truth-in-labeling evidence for TR2-D.
- Resolution / guidance: Marker-rich evidence responses must include every core field implied by their claimed coverage. For `story_core`, include `beatId`, `text`, `durationTicks`, and `severity`; for `directive_core`, include `colonyId`, `directive`, and `durationTicks`.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Prove documented env vars map to runtime properties

- Track: Track D / Java refinery config
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: A policy handoff documented `PLANNER_DIRECTOR_SOLVER_OBSERVABILITY_ENABLED` as the switch for `directorSolver*` markers before `application.yml` explicitly mapped that env var to `planner.director.solverObservabilityEnabled`.
- Impact: Track B could follow the handoff and still fail to enable solver observability, producing paid/rehearsal artifacts without the expected `directorSolver*` evidence.
- Resolution / guidance: Any documented operator env var used as a cross-track contract must be backed by code/config or the exact supported property mechanism must be documented and tested. Prefer explicit `application.yml` mappings for Java Spring properties.
- Status: fixed

## 2026-05-04 - W8.6-D1 Paid-Live Policy Lock - Major - Separate current enforcement from planned guardrails

- Track: Meta Coordinator / Track D / Track B
- Source: Step review for Wave 8.6 `W8.6-D1`
- Finding: Refinery live SMR docs described paid-live guardrails as code-enforced even though `refinery_live_validator` and `refinery_live_paid` guardrail implementation belonged to the later Track B `W8.6-B1` step.
- Impact: Future agents could assume paid confirmation, rehearsal proof, completion caps, or concurrency locks already existed and start paid or review work on a false safety premise.
- Resolution / guidance: Planning docs must distinguish currently enforced behavior from planned downstream enforcement, especially around paid/live/secret/cost guardrails.
- Status: fixed
