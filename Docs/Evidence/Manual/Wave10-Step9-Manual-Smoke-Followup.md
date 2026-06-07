# Wave 10 Step 9 Manual Smoke Follow-up

Date: 2026-06-07

Scope:
- User-driven manual app smoke after Wave 10 Step 9 (`P7-F` accepted, `P7-H` still manual-visual gated).
- This is manual/operator evidence only. It must not be treated as SMR/ScenarioRunner proof.

## Setup

- App launched with campaign/siege-relevant runtime gates enabled:
  - `WORLDSIM_ENABLE_DIPLOMACY=true`
  - `WORLDSIM_ENABLE_COMBAT_PRIMITIVES=true`
  - `WORLDSIM_ENABLE_SIEGE=true`
- Visual profile used: `Showcase`
- Interactive flow used:
  - `Ctrl+Q` manual/operator campaign launch
  - `Ctrl+F2` campaign/logistics panel
  - `Ctrl+F8` combat/siege overlay when relevant

## What looked good

- Baseline campaign/logistics panel behavior looked usable in the sampled runs.
- A first sampled run showed a normal visible campaign row (`Obs->Aet`) and later a larger army state (`Army 4/1`), so runtime-side campaign growth/reinforcement did become visible in at least one manual world.
- In a later world, multiple factions were able to go hostile at the same time, so the broader campaign surface did not look frozen to a single isolated lane.
- First-pass impression from the user: the major baseline systems looked healthy enough to continue toward Wave 10 SMR.

## Findings / observations

### 1. Dedicated siege units were not directly observed in manual smoke

- No ram / siege tower / mobile catapult was clearly seen in the sampled runs.
- The user was unsure how long the sim should be left running, what visual signature to expect, and whether the absence means "too early / unlucky" or a real incidence problem.
- Interpretation: this is not enough to fail P7-F, but it means the active P7-H manual visual gate is still only partially satisfied. Step10A/10B should classify whether low manual incidence is expected or a follow-up gap.

### 2. Manual/operator launch still tends to produce single-member probe behavior

- In one observed run, the active army started at `1/1` and the lead attacker pushed into an enemy base alone.
- The user specifically called out that the default should remain `1` only as a fallback, but when the runtime can assemble more members, it should prefer a larger viable squad ("the max" within existing guards) for more representative manual smoke.
- Interpretation: candidate Track B operator/runtime follow-up, not a current blocker for accepted `P7-F`.

### 3. Watchtower lethality is very visible against tiny probes

- The user observed the single attacker walking into an enemy base and dying to watchtower fire.
- Interpretation: this is consistent with the current runtime/defense model, but it strengthens the argument that one-member operator probes under-exercise siege/campaign behavior and make manual proof misleading.

### 4. Wood wall visual scale looks too large

- The user reported that wall icons / wall footprint look oversized in the current presentation.
- Screenshots show large, dense wall sprites dominating small colony footprints.
- Interpretation: candidate Track A visual readability fix.

### 5. Random wall placement gives poor defensive value

- The user reported that walls are built in a scattered, fragmented shape rather than a coherent protective line.
- Screenshots show irregular clusters and gaps, which look weak both visually and functionally.
- Interpretation: likely Track B fortification/placement logic follow-up, potentially paired with a Track A readability pass.

### 6. Multi-campaign assembly / `Army 0/1` / `anchor:none` style rows still appear in some runs

- Later screenshots show multiple simultaneous `assembling/assembling` rows, several `Army 0/1`, and `anchor:none` entries.
- The user did not flag this as the main problem in this round, but it is useful evidence for future operator/runtime quality triage.
- Interpretation: candidate Track B operator/manual launch hardening item if SMR or later manual review confirms it is still too noisy.

## Current interpretation

- This manual smoke is good enough to continue into Step10A/10B SMR work.
- It does not yet prove direct dedicated siege-unit visibility in live app conditions.
- The findings above are best treated as:
  - evidence input for Step10A / Step10B classification,
  - and candidate fixes for a conditional Step10C post-SMR/manual gap-closure bucket.

## Suggested Step10C candidate list

- Track B candidate: operator/manual campaign launch should prefer a larger viable squad when available instead of staying effectively one-member by default in representative smoke conditions.
- Track B candidate: investigate whether dedicated siege units are too rare / too late / too opaque in organic/manual play, but only after Step10A/10B evidence distinguishes expected low incidence from a real gap.
- Track A candidate: wall/watchtower icon scale/readability pass.
- Track B candidate: wall placement coherence / defensive usefulness pass.
- Track B candidate: reduce noisy repeated `Army 0/1` / `anchor:none` assembly spam if Step10A/10B evidence shows it remains a recurring operator/runtime issue.

## Non-claims

- This document does not claim organic siege-unit proof.
- This document does not replace Step10A/10B SMR evidence.
- This document does not claim wall placement is a formally triaged blocker yet; it is recorded as a candidate post-SMR fix bucket item.

## Evidence source

- User-provided screenshots and manual observations from 2026-06-07 chat session.
- Related current plan/state locations:
  - `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md`
  - `ops/PROJECT_STATE.md`
