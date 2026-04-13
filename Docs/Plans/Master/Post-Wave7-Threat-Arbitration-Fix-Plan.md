# Post-Wave7 Threat Arbitration Fix Plan

Status: approved non-wave behavior fix follow-up after valid contact-realization rerun
Owner: SMR Analyst
Last updated: 2026-04-12

## 1. Purpose

The valid `contact-realization-002` rerun established that the movement-speed perturbation now applies correctly, but still does not materially improve the canonical bad lane.

This changes the engineering target.

The next fix should not focus on raw movement speed or generic contact realization. It should focus on the shared runtime/AI behavior layer that keeps the simulation locally defensive even when movement speed changes.

## 2. Evidence Summary

The strongest evidence remains:

- `standard-default / seed 101` is the canonical bad lane,
- the problem is planner-independent (`simple`, `goap`, `htn`),
- the problem is not primarily caused by the siege toggle,
- and the valid `fastmove` rerun still produced near-identical bad-lane outcomes.

Observed suspicious pattern in the bad lane:

- `peakActiveCombatGroups > 0`,
- but `combatEngagements = 0`,
- `battleTicks = 0`,
- very high `noProgressBackoffFlee` and `noProgressBackoffCombat`,
- with AI telemetry still dominated by `DefendSelf`, `Flee`, and `retreat_refuge`.

Interpretation:

- the issue is no longer best explained by basic move-speed limitations,
- and is now best explained as a shared threat / defense / arbitration problem.

## 3. Problem Statement

Three coupled issues are suspected:

1. `DefendSelf` activates too easily from ambient war pressure.
2. Runtime threat-response hijack is too eager and planner-independent.
3. `Fight` / `Flee` / `RaidBorder` arbitration leaves too much of the population in local defensive loops.

## 4. Approved Engineering Direction

The chosen direction is:

1. unify the threat/context semantics used by planner and runtime execution,
2. separate direct threat from ambient war pressure,
3. reduce `DefendSelf` dominance under ambient-only pressure,
4. reduce runtime emergency override on ambient-only pressure,
5. preserve strong defensive reactions for true immediate threats and siege emergencies.

## 5. Scope

### 5.1 In scope

- shared threat context semantics,
- `DefendSelf` trigger decompression,
- `ShouldPrioritizeDefense` tightening,
- runtime non-warrior forced-flee tightening,
- re-engage suppression tuning where it currently prolongs defensive loops,
- focused AI/runtime tests,
- post-fix SMR reruns on the golden bad lane.

### 5.2 Out of scope

- campaign march system,
- new military entities,
- new strategic campaign feature work,
- UI changes,
- broad movement-system rewrites,
- unrelated siege feature expansions.

## 6. Design Principles

1. Prefer the smallest shared fix over planner-specific patches.
2. Preserve immediate self-defense against true nearby threats.
3. Do not use ambient war pressure as if it were direct contact.
4. Keep the fix compatible with existing siege-aware logic.
5. Use the same SMR bad lane to validate the outcome.

## 7. Concrete Technical Direction

### 7.1 Canonical context source

Use one canonical context builder for both:

- planner-side decision context,
- runtime emergency threat-response context.

This prevents drift between `RuntimeNpcBrain` and `Person`.

### 7.2 Threat split

Split threat semantics into:

- `DirectThreatScore`
- `AmbientThreatScore`
- `HasImmediateThreat`
- `HasImmediateFactionThreat`

Direct threat means nearby predators or nearby hostile people / enemy actors.
Ambient pressure means war/hostile/contested context without immediate contact.

### 7.3 DefendSelf tightening

`ThreatNearbyConsideration` should score direct nearby threat only.

This prevents `DefendSelf` from winning simply because:

- the colony is at war,
- the tile is contested,
- or ambient threat pressure is non-zero.

### 7.4 Runtime emergency hijack tightening

The `Person` threat-response override should act as an emergency brake, not as a general strategy layer.

That means:

- immediate threat still triggers defense,
- siege-specific emergency retreat/sortie still works,
- ambient-only pressure should not force generic `Fight` / `Flee` hijack.

### 7.5 Fight/Flee suppression tuning

Re-engage suppression should stay meaningful for:

- routing,
- severe morale collapse,
- siege emergency.

It should not keep actors in prolonged defensive suppression purely from ambient war pressure.

## 8. File-Level Work Plan

### 8.1 `WorldSim.AI/Abstractions.cs`

- extend `NpcAiContext` with direct-vs-ambient threat fields and immediate-threat booleans.

### 8.2 `WorldSim.Runtime/Simulation/AI/RuntimeNpcBrain.cs`

- centralize context building,
- compute direct and ambient threat separately,
- expose a reusable runtime-owned context seam for both planner and runtime execution.

### 8.3 `WorldSim.Runtime/Simulation/Person.cs`

- consume the canonical context builder,
- tighten runtime emergency defense entry conditions,
- reduce ambient-only forced flee behavior.

### 8.4 `WorldSim.AI/ConcreteConsiderations.cs`

- narrow `ThreatNearbyConsideration` to direct nearby threat.

### 8.5 `WorldSim.AI/ThreatDecisionPolicy.cs`

- add helpers for immediate-vs-ambient threat,
- tighten `ShouldPrioritizeDefense`,
- tighten civilian/non-warrior forced fight eligibility,
- reduce ambient-only re-engage suppression.

### 8.6 Tests

- `WorldSim.AI.Tests/DecisionTests.cs`
- `WorldSim.Runtime.Tests/RuntimeNpcBrainTests.cs`

## 9. Acceptance Criteria

The fix is accepted when all of the following hold:

1. AI and runtime tests pass.
2. The canonical bad lane can still reproduce before the post-fix rerun is taken as evidence.
3. A post-fix rerun shows at least one of the following in `standard-default / 101`:
   - non-zero contact where there was previously zero contact,
   - reduced `DefendSelf` dominance,
   - reduced `Flee` dominance,
   - reduced `retreat_refuge` dominance,
   - lower `noProgressBackoffFlee` and/or `noProgressBackoffCombat`.
4. The fix does not collapse immediate self-defense against true nearby threats.

## 10. Test Plan

### AI tests

Required coverage:

- ambient war pressure alone does not score `ThreatNearbyConsideration`,
- ambient war pressure alone does not trigger `ShouldPrioritizeDefense`,
- default goal selection under ambient-only pressure does not select `DefendSelf`.

### Runtime tests

Required coverage:

- runtime context exposes both direct and ambient threat components,
- immediate and ambient threat are separated in a reproducible context,
- existing commander/combat context tests remain green.

## 11. Verification SMR Plan

After implementation:

1. rerun `planner-compare-wave7-contact-realization-medium-003`
2. rerun `planner-compare-wave7-contact-realization-standard-003`

Primary interpretation fields:

- `combatEngagements`
- `battleTicks`
- `ticksWithActiveBattle`
- `combatDeaths`
- `noProgressBackoffFlee`
- `noProgressBackoffCombat`
- `goalCounts`
- `commandCounts`
- `debugCauseCounts`
- `targetKindCounts`
- `latestDecision`

## 12. Success Interpretation

Signs of improvement:

- `DefendSelf` no longer dominates ambient-only bad lanes,
- `RaidBorder` and/or `Fight` rises in war/contested contexts,
- `retreat_refuge` loses share in bad lanes,
- realized battle contact appears where it previously did not,
- and backoff pressure drops.

## 13. Deferred Alternatives

If this fix fails to improve the bad lane, deferred next directions include:

- deeper combat contact initiation review,
- more explicit raid assembly logic,
- or dedicated campaign-layer systems from later roadmap phases.

Those are explicitly deferred until after the targeted threat-arbitration rerun.
