# Director Integration Master Plan

Status: approved, implementation pending
Owner: Track D (primary), with explicit cross-track requests to Track A/B/C
Supersedes: `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md` (sprint plan remains valid as
tactical reference, this document is the strategic master plan)

---

## Table of Contents

1. [Vision and Design Philosophy](#1-vision-and-design-philosophy)
2. [Architecture Overview](#2-architecture-overview)
3. [Core Design: Effect Vocabulary System](#3-core-design-effect-vocabulary-system)
4. [Story Beat System](#4-story-beat-system)
5. [Colony Directive System](#5-colony-directive-system)
6. [Influence Tuning System](#6-influence-tuning-system)
7. [Checkpoint Trigger System](#7-checkpoint-trigger-system)
8. [Contract v2 Design](#8-contract-v2-design)
9. [Generic Runtime Engine Design](#9-generic-runtime-engine-design)
10. [Refinery Formal Model Design](#10-refinery-formal-model-design)
11. [LLM Integration Design](#11-llm-integration-design)
12. [Implementation Phases](#12-implementation-phases)
13. [Gap Analysis](#13-gap-analysis)
14. [Cross-Track Integration Points](#14-cross-track-integration-points)
15. [Definition of Done](#15-definition-of-done)
16. [Risks and Mitigations](#16-risks-and-mitigations)
17. [Relationship to Existing Plans](#17-relationship-to-existing-plans)
18. [Appendix A: Wire Format Examples](#appendix-a-wire-format-examples)
19. [Appendix B: Effect Vocabulary Reference](#appendix-b-effect-vocabulary-reference)
20. [Appendix C: Formal Model Constraint Reference](#appendix-c-formal-model-constraint-reference)

---

## 1. Vision and Design Philosophy

### 1.1 The Director as a "God"

The Season Director is WorldSim's emergent narrative engine. It operates as a behind-the-scenes "god" that
observes the world state at infrequent checkpoints and intervenes through two channels:

- **Story beats**: world-level narrative events with optional gameplay effects
- **Colony directives**: colony-level policy nudges that subtly bias NPC decision-making

The Director does not micromanage. It does not run every frame. It fires at checkpoints (currently F6 manual
trigger), observes a snapshot of the world, and proposes interventions that the simulation then absorbs
organically. The goal is to promote emergent behavior — not to script outcomes.

### 1.2 The OnlabRefinery Pattern

This system is built on the architecture proven in the OnlabRefinery project (BSc Onlab, 2025/26 I. semester):

**Core principle: separation of responsibilities.**

- **LLM = proposal generator (javaslatkezo)**. It is creative, non-deterministic, and generates candidates
  for world interventions. It is NEVER authoritative. Its output is always a proposal, never a final decision.
- **Refinery = quality gate (minosegkapu)**. It formally validates every candidate against explicit constraints.
  Invalid candidates receive targeted feedback and the LLM retries. After bounded retries, a deterministic
  fallback takes over. The Refinery ensures that LLM hallucinations never reach the simulation.
- **Runtime = generic execution engine**. It applies validated effects without caring what they are called
  or who proposed them. It is a dumb-but-reliable modifier engine.

**Layered model architecture:**

- **Design layer**: stable rules and entity skeleton that the LLM cannot modify
- **Model layer**: schema and constraints (what combinations are legal)
- **Runtime layer**: dynamic checkpoint context facts (current world state)

The LLM receives all three layers as context but can only propose output within a designated area.
The Refinery validates proposals against the design and model layers.

Reference: `OnlabRefineryDocumentation.txt` (sections 2.2-2.4)

### 1.3 Emergence Preservation Principle

The Director must enhance emergence, not suppress it. Design rules:

1. **Low frequency**: checkpoint-only intervention, not per-frame
2. **Soft effects**: multipliers and biases, never hard overrides
3. **Bounded magnitude**: 3-layer influence tuning system prevents overreach
4. **Temporal decay**: all effects expire, the simulation always returns to its natural state
5. **No scripted outcomes**: the Director nudges probabilities, never forces specific events

The guiding question: "Would this intervention be interesting if the player didn't know it happened?"
If the answer is yes — if the world simply feels more alive and surprising — the Director is working correctly.

### 1.4 The Creativity Gradient

The system is designed to progressively unlock LLM creativity across phases:

| Phase | LLM Freedom | What is Fixed | What LLM Controls |
|-------|-------------|---------------|-------------------|
| 0-1 | None (mock) | Everything | Nothing — deterministic baseline |
| 2 | None (Refinery validation wired) | Everything | Nothing — but validation pipeline proven |
| 3 | Compositional | Vocabulary schema, constraint bounds | Effect composition, narrative text, intensity, duration |
| 4+ | Open (instance assertions) | Formal model constraints only | Novel conditions, novel directives, creative effects |

The key insight: **if the LLM only picks from a fixed enum, you do not need a Refinery — a switch statement
suffices**. The entire value of the OnlabRefinery pattern only materializes when the LLM has genuine creative
freedom to compose novel effects. The architecture is designed from day one to support this, even though
early phases use deterministic mock data.

### 1.5 Relationship to the OnlabRefinery Documentation

The OnlabRefinery documentation establishes these principles that this plan directly implements:

- "Az LLM nem onallo donteshozokkent mukodik, hanem a modellalkotasi folyamatot tamogato javaslatkepezo
  szerepet tolt be." — The LLM is a proposal generator, not a decision maker.
- "A Refinery nem csak utologos ellenorzo, hanem a folyamat aktiv resze." — The Refinery is an active
  participant with feedback capability, not just a post-hoc filter.
- "Az LLM nem nyulhat bele mindenbe, hanem egy kijelolt kimeneti teruletet kap." — The LLM gets a
  designated output area, not free reign over the entire model.
- "A generalas lehet kreativ, de az eredmeny csak akkor fogadhato el, ha formalisan is rendben van."
  — Creativity under formal control.

---

## 2. Architecture Overview

### 2.1 End-to-End Pipeline

```
Step 1: TRIGGER
  User presses F6 (or future: season boundary / event trigger)
  GameHost calls RefineryTriggerAdapter.TriggerDirectorCheckpoint()

Step 2: SNAPSHOT
  SimulationRuntime.BuildDirectorSnapshot() creates immutable checkpoint data:
    - current tick, season, colony stats
    - active effects list (beats + directives)
    - cooldown state
    - world summary (population, resources, morale, faction tensions)

Step 3: REQUEST
  RefineryPatchRuntime sends HTTP POST to Java service:
    goal: SEASON_DIRECTOR_CHECKPOINT
    snapshot: { ...checkpoint data... }
    constraints: { outputMode, maxBudget, ... }

Step 4: CANDIDATE GENERATION (Java)
  ComposedPatchPlanner routes to DirectorRefineryPlanner:
    a) MockPlanner generates deterministic baseline candidate
    b) OR LlmPlanner (Phase 3+) proposes creative candidate via OpenRouter
    c) DirectorModelValidator validates candidate against formal model
    d) If invalid: feedback → retry (bounded) → deterministic fallback

Step 5: RESPONSE
  Java service returns validated PatchResponse:
    ops: [ addStoryBeat {...}, setColonyDirective {...} ]
    explain: { stage, outputMode, warnings, retryCount }

Step 6: TRANSLATION (C# Adapter)
  PatchCommandTranslation converts ops to runtime commands:
    addStoryBeat → ApplyStoryBeatCommand
    setColonyDirective → ApplyColonyDirectiveCommand
  Unknown/invalid ops → deterministic error (not crash)

Step 7: RUNTIME APPLICATION
  SimulationRuntime applies commands:
    - Registers beat effects in domain modifier engine
    - Registers directive biases in goal bias engine
    - Starts timed effect countdowns
    - Updates director state for next snapshot

Step 8: VISUALIZATION
  WorldSnapshotBuilder includes director state in read model
  Graphics layer renders:
    - Event feed entries (narrative text)
    - HUD status indicators (active beat, active directive, stage marker)
    - Debug overlay (effect intensities, timers, budget spent)
```

### 2.2 Component Responsibility Map

```
WorldSim.App (GameHost)
  - F6 trigger routing
  - Refinery status display wiring
  - NO domain logic

WorldSim.Runtime (SimulationRuntime)
  - Generic domain modifier engine
  - Generic goal bias engine
  - Timed effect scheduler
  - Director state (active beats, active directives, cooldowns)
  - Director snapshot builder
  - Command endpoints: ApplyStoryBeat(), ApplyColonyDirective()
  - NO Java-specific knowledge

WorldSim.Contracts (v2/)
  - addStoryBeat op schema
  - setColonyDirective op schema
  - SEASON_DIRECTOR_CHECKPOINT goal
  - Effect vocabulary type definitions
  - Shared between Java and C#

WorldSim.RefineryClient
  - HTTP client
  - PatchResponseParser (extended for new ops)
  - PatchApplier (extended for new ops)
  - Canonical hash / dedupe

WorldSim.RefineryAdapter
  - Anti-corruption translation layer
  - addStoryBeat → ApplyStoryBeatCommand
  - setColonyDirective → ApplyColonyDirectiveCommand
  - Deterministic error for unknown ops/directives
  - Tech/directive ID mapping config
  - Output mode gating (C# side override)

refinery-service-java
  - DirectorRefineryPlanner (orchestrator)
  - DirectorModelValidator (formal model constraints)
  - DirectorDesign (design layer constants)
  - MockPlanner (deterministic baseline)
  - LlmPlanner + OpenRouterClient (Phase 3+)
  - DirectorPromptFactory (Phase 3+)
  - DirectorCandidateParser (Phase 3+)
  - Output mode gating (Java side)

WorldSim.Graphics (cross-track request to Track A)
  - Event feed rendering for beats
  - HUD directive status indicator
  - Debug overlay for director state

WorldSim.AI (cross-track request to Track C)
  - Goal bias integration in NPC decision layers
  - Job priority integration in profession assignment
```

### 2.3 Dependency Flow

```
WorldSim.App
  → WorldSim.RefineryAdapter  (trigger + status)
  → WorldSim.Runtime           (snapshot + command apply)
  → WorldSim.Graphics          (visualization)

WorldSim.RefineryAdapter
  → WorldSim.Contracts          (op types, goal types)
  → WorldSim.Runtime            (command endpoints)
  → WorldSim.RefineryClient     (HTTP + parser)

WorldSim.RefineryClient
  → WorldSim.Contracts          (serialization types)

WorldSim.Runtime
  → WorldSim.Contracts          (effect vocabulary types)
  → WorldSim.AI                 (goal bias interface)

WorldSim.Graphics
  → WorldSim.Runtime            (read model / snapshot only)

refinery-service-java
  → WorldSim.Contracts          (schema parity, not compile dep)
```

No new dependency arrows added. All flows follow the existing AGENTS.md dependency graph.

---

## 3. Core Design: Effect Vocabulary System

### 3.1 Overview

The Effect Vocabulary is the central abstraction of the Director system. Instead of fixed beat/directive
type enums with per-type handler code, the system defines a vocabulary of composable effect primitives.
The LLM composes effects from this vocabulary. The Refinery validates compositions. The runtime executes
them generically.

This means:
- **No switch-per-beat-type in runtime**. Adding a new beat requires zero C# code changes.
- **LLM creativity is architectural, not bolted on**. The system is designed for composition from day one.
- **The Refinery validates structure, not names**. It does not care what a beat is called — it validates
  that the effect composition satisfies formal constraints.

### 3.2 Vocabulary Layers

Each vocabulary layer is a type of effect primitive that the Director can compose into beats and directives.
Layers are introduced incrementally across phases.

| Layer | Type | Target | Phase | Track |
|-------|------|--------|-------|-------|
| Domain Modifier | `domain_modifier` | World / Colony | 0-1 | D + B |
| Goal Bias | `goal_bias` | Colony AI | 0-1 | D + C |
| Causal Chain | `causal_chain` | World (conditional follow-up) | 3+ | D |
| Entity Hint | `entity_hint` | NPC / specific entity | 4+ | D + C |
| Spatial Hint | `spatial_hint` | Tile region / map area | 4+ | D + A + B |
| Faction Hint | `faction_hint` | Faction pair relations | 4+ | D + B |

### 3.3 Domain Modifier Layer (Phase 0-1)

The foundational effect type. Modifies a world or colony domain by a multiplier for a duration.

Schema:
```json
{
  "type": "domain_modifier",
  "domain": "food | morale | economy | military | research",
  "modifier": -0.30 .. +0.30,
  "durationTicks": 5 .. 50,
  "target": "world | colony:<id>"
}
```

Runtime behavior:
- Applied as a multiplicative modifier to the domain's production/value per tick
- Linear decay over duration (modifier * remainingTicks / totalTicks)
- Multiple modifiers on the same domain stack additively
- Expired modifiers are removed automatically

Example: `{ "domain": "food", "modifier": -0.15, "durationTicks": 25 }` means food production
is reduced by up to 15%, decaying linearly over 25 ticks.

### 3.4 Goal Bias Layer (Phase 0-1)

Subtly biases NPC goal selection by adjusting utility scores for goal categories.

Schema:
```json
{
  "type": "goal_bias",
  "goalCategory": "farming | gathering | crafting | building | social | military | research | rest",
  "weight": 0.0 .. 0.50,
  "durationTicks": 5 .. 50,
  "target": "colony:<id>"
}
```

Runtime behavior:
- Applied as an additive bonus to utility scores for goals in the specified category
- NPC decision layer (Track C) reads active biases before goal evaluation
- Multiple biases stack, but total additional weight per category is capped at 0.5
- Job slot priority: when goal bias for a category exceeds 0.25, that category's job slots
  are filled preferentially in the job assignment pass (secondary mechanism)

This is the primary directive mechanism. It preserves emergence because NPCs still make individual
decisions — they are nudged, not commanded.

### 3.5 Causal Chain Layer (Phase 3+)

Allows the LLM to propose conditional follow-up beats that trigger based on simulation state.

Schema:
```json
{
  "type": "causal_chain",
  "condition": {
    "metric": "food_reserves | morale_avg | population | ...",
    "operator": "lt | gt | eq",
    "threshold": <number>
  },
  "followUpBeat": { ... beat definition ... },
  "windowTicks": 10 .. 100,
  "maxTriggers": 1
}
```

Runtime behavior:
- After the parent beat is applied, the causal chain enters a monitoring window
- If the condition is met within the window, the follow-up beat is applied
- Follow-up beat goes through the same modifier engine (no special handling)
- The follow-up's effects count against the NEXT checkpoint's budget retrospectively

Refinery validates:
- No loops (follow-up cannot reference the parent)
- Combined budget (parent + follow-up) within bounds
- Condition metric exists in the formal model
- Window duration is reasonable

This layer enables the LLM to build narratives that EVOLVE with the simulation state.

### 3.6 Entity Hint Layer (Phase 4+)

Allows the LLM to attach temporary traits or states to specific entities.

Schema:
```json
{
  "type": "entity_hint",
  "targetSelection": "random_npc | npc_with_trait:<trait> | specific:<id>",
  "trait": "<trait_name>",
  "durationTicks": 10 .. 50
}
```

Runtime behavior:
- Selected entity receives a temporary trait that Track C AI considers in decisions
- Traits registered in a formal trait vocabulary (not arbitrary strings)
- Max active entity hints per checkpoint is bounded

Example: `{ "targetSelection": "random_npc", "trait": "inspired_builder", "durationTicks": 30 }`
gives one NPC a temporary trait that boosts their building-related utility scores.

### 3.7 Spatial Hint Layer (Phase 4+)

Allows the LLM to create temporary tile-region conditions with visual and gameplay effects.

Schema:
```json
{
  "type": "spatial_hint",
  "regionType": "river_adjacent | forest | mountain_base | settlement_radius | ...",
  "condition": "flooded | fertile | blighted | ...",
  "durationTicks": 10 .. 50,
  "modifier": { "domain": "...", "modifier": <number> }
}
```

Runtime behavior:
- Tiles matching the region type receive a temporary condition
- Condition affects domain modifiers for entities/buildings on those tiles
- Track A renders the condition visually (color overlay, particle effect)

### 3.8 Faction Hint Layer (Phase 4+)

Allows the LLM to shift faction relations, relevant in the combat era.

Schema:
```json
{
  "type": "faction_hint",
  "factionA": "colony_primary | faction:<id>",
  "factionB": "nearest_rival | faction:<id> | all_neighbors",
  "relationDelta": -0.30 .. +0.30,
  "durationTicks": 10 .. 50
}
```

Runtime behavior:
- Faction relation value shifted by delta, decaying over duration
- Feeds into diplomacy/war state machine (Combat Master Plan Phase 1 Sprint 2)
- Requires FactionRelations system to exist (not yet implemented)

### 3.9 Formal Model Constraints (All Layers)

Every vocabulary layer has formal constraints that the Refinery enforces. See Section 10 and
Appendix C for the full constraint reference. Summary:

| Constraint category | Examples |
|--------------------|----------|
| **Value bounds** | modifier in [-0.3, +0.3], duration in [5, 50], weight in [0.0, 0.5] |
| **Cardinality** | max 3 effects per beat, max 2 biases per directive, max 1 active epic beat |
| **Compatibility** | no contradictory same-domain modifiers in one checkpoint |
| **Budget** | total checkpoint influence cost within budget |
| **Cooldown** | temporal spacing between same-tier beats |
| **Vocabulary** | domains, goal categories, traits must exist in formal model |

---

## 4. Story Beat System

### 4.1 Beat Severity Tiers

Story beats are world-level narrative events. They use a layered severity system where higher tiers
have stronger gameplay effects but are rarer and more constrained.

| Tier | Frequency Target | Max Effects | Gameplay Impact | Budget Cost |
|------|-----------------|-------------|-----------------|-------------|
| **Minor** | ~60% of beats | 0 | Event feed text only. Atmosphere and flavor. | 0 |
| **Major** | ~30% of beats | 1-2 domain modifiers | Timed multiplier on one domain. Noticeable but not game-changing. | 2-3 |
| **Epic** | ~10% of beats | 2-3 domain modifiers + optional faction/spatial hint | Multi-domain impact. Colony-defining moment. | 4-5 |

The tier is determined by the effect composition, not by a label. A beat with zero effects is minor.
A beat with 1-2 bounded modifiers is major. A beat with 3 modifiers or cross-layer effects is epic.
The Refinery validates tier classification and enforces tier-appropriate constraints.

### 4.2 Beat Lifecycle

```
1. PROPOSED:  LLM/mock generates beat candidate
2. VALIDATED: Refinery checks constraints, repairs if possible
3. APPLIED:   Runtime registers effects in modifier engine, starts timers
4. ACTIVE:    Effects are ticking down, visible in snapshot
5. DECAYING:  Effects linearly decreasing toward zero
6. EXPIRED:   All effects reached zero, beat removed from active list
7. COOLDOWN:  Tier-specific cooldown before next beat of same/higher tier
```

### 4.3 Beat Cooldown and Stacking Rules

| Rule | Value | Rationale |
|------|-------|-----------|
| Max active minor beats | unlimited | Flavor text has no gameplay cost |
| Max active major beats | 1 | Prevents modifier overload |
| Max active epic beats | 1 | Epic moments should be singular |
| Major beat cooldown | 15 ticks after expiry | Let the simulation breathe |
| Epic beat cooldown | 30 ticks after expiry | Epic moments need spacing |
| Same-domain stacking | additive, capped at abs(0.4) | Prevents runaway modifiers |

### 4.4 Beat Examples

**Minor beat (Phase 0 mock):**
```json
{
  "op": "addStoryBeat",
  "opId": "beat-001-mock-tick-120",
  "severity": "minor",
  "beatName": "Starlit Gathering",
  "narrative": "The colony gathers under a brilliant night sky. Elders share tales of the old world.",
  "effects": []
}
```

**Major beat (Phase 3 LLM):**
```json
{
  "op": "addStoryBeat",
  "opId": "beat-042-llm-tick-480",
  "severity": "major",
  "beatName": "The Scorching Winds of Ashara",
  "narrative": "Hot winds from the southern wastes sweep across the farmlands. Crops wither under the relentless sun.",
  "effects": [
    { "type": "domain_modifier", "domain": "food", "modifier": -0.15, "durationTicks": 25 }
  ]
}
```

**Epic beat (Phase 3+ LLM):**
```json
{
  "op": "addStoryBeat",
  "opId": "beat-099-llm-tick-960",
  "severity": "epic",
  "beatName": "The Awakening of the Deep Forest",
  "narrative": "Strange lights pulse from the ancient forest. Trees seem to shift, and the soil grows impossibly fertile. But a sense of dread settles over the colony...",
  "effects": [
    { "type": "domain_modifier", "domain": "food", "modifier": 0.20, "durationTicks": 30 },
    { "type": "domain_modifier", "domain": "morale", "modifier": -0.10, "durationTicks": 20 }
  ]
}
```

**Epic beat with causal chain (Phase 3+):**
```json
{
  "op": "addStoryBeat",
  "opId": "beat-150-llm-tick-1200",
  "severity": "epic",
  "beatName": "The Great Drought",
  "narrative": "The rivers run dry. Dust clouds rise from cracked earth.",
  "effects": [
    { "type": "domain_modifier", "domain": "food", "modifier": -0.20, "durationTicks": 30 },
    { "type": "domain_modifier", "domain": "morale", "modifier": -0.05, "durationTicks": 15 }
  ],
  "causalChain": {
    "type": "causal_chain",
    "condition": { "metric": "food_reserves_pct", "operator": "lt", "threshold": 20 },
    "followUpBeat": {
      "severity": "major",
      "beatName": "Famine Riots",
      "narrative": "Hunger drives the desperate to violence. Granaries are raided under cover of night.",
      "effects": [
        { "type": "domain_modifier", "domain": "morale", "modifier": -0.20, "durationTicks": 15 }
      ]
    },
    "windowTicks": 40,
    "maxTriggers": 1
  }
}
```

---

## 5. Colony Directive System

### 5.1 Directive Mechanism

Colony directives use a dual mechanism that preserves NPC agency while providing meaningful guidance.

**Primary mechanism: AI decision bias (soft weight)**

When a directive is active, the NPC goal evaluation system adds a bonus weight to the specified goal
categories. NPCs still make individual decisions based on their own needs, personality, and situation —
the directive only nudges the probability distribution.

Example: `PrioritizeFood` at intensity 0.6 → farming and gathering goals get +0.12 additive utility bonus
(intensity * base_weight_factor). An NPC that was already slightly hungry will strongly prefer farming.
An NPC that desperately needs rest will still rest.

**Secondary mechanism: job slot priority**

When a directive's goal bias exceeds the threshold (0.25 effective weight), the colony's job assignment
system fills slots for that category first. This ensures the directive has visible effect even if individual
NPCs don't shift their behavior enough.

Example: `PrioritizeFood` at intensity 0.6 → farming job slots are filled before crafting slots.
But NPCs assigned to farming can still switch if their personal needs become critical.

### 5.2 Current Taxonomy (3 Economic Directives)

| Directive | Goal Categories Biased | Primary Domain |
|-----------|----------------------|----------------|
| `PrioritizeFood` | farming, gathering | food production |
| `StabilizeMorale` | social, rest, entertainment | morale recovery |
| `BoostIndustry` | crafting, building, mining | economy/production |

These are the Phase 0-1 directives. In Phase 3 (LLM era), the LLM can compose novel directives from
the goal bias vocabulary rather than picking from this fixed list.

### 5.3 Extensibility Hooks (Combat-Era Directives)

The following directives are planned but NOT implemented until the Combat Master Plan is underway.
The architecture supports them without code changes — they are just new goal bias compositions.

| Future Directive | Goal Categories | Trigger |
|-----------------|----------------|---------|
| `FortifyDefenses` | building (defensive), military | Combat Phase 1+ |
| `RallyMilitia` | military, training | Combat Phase 1+ |
| `SeekDiplomacy` | social (diplomatic), trading | Combat Phase 1 Sprint 2+ |
| `EvacuateOutskirts` | movement (inward), gathering | Combat Phase 2+ (siege) |

Implementation: when combat goal categories exist in Track C, these directives become valid
goal bias compositions. No new vocabulary layer needed — they use the existing `goal_bias` type.

### 5.4 Directive Lifecycle

```
1. PROPOSED:  LLM/mock generates directive candidate
2. VALIDATED: Refinery checks: categories exist, weights in bounds, no conflicts
3. APPLIED:   Runtime registers biases in goal bias engine, starts timer
4. ACTIVE:    Biases affecting NPC decisions, job priority shifted
5. DECAYING:  Biases linearly decreasing (like domain modifiers)
6. EXPIRED:   Biases removed, NPC decisions return to natural state
```

Rules:
- Max 1 active directive per colony at a time
- New directive replaces existing (with 5-tick blend transition)
- Directive duration: 10-40 ticks (Refinery enforces)
- Directive intensity: 0.0-1.0 (maps to effective weight via base_weight_factor)

### 5.5 Directive Examples

**Phase 0 deterministic:**
```json
{
  "op": "setColonyDirective",
  "opId": "dir-001-mock-tick-120",
  "directiveName": "PrioritizeFood",
  "biases": [
    { "type": "goal_bias", "goalCategory": "farming", "weight": 0.20 },
    { "type": "goal_bias", "goalCategory": "gathering", "weight": 0.15 }
  ],
  "durationTicks": 25,
  "target": "colony:primary"
}
```

**Phase 3 LLM-composed:**
```json
{
  "op": "setColonyDirective",
  "opId": "dir-088-llm-tick-960",
  "directiveName": "Prepare for the Long Winter",
  "biases": [
    { "type": "goal_bias", "goalCategory": "gathering", "weight": 0.25 },
    { "type": "goal_bias", "goalCategory": "building", "weight": 0.10 },
    { "type": "goal_bias", "goalCategory": "crafting", "weight": 0.10 }
  ],
  "durationTicks": 30,
  "target": "colony:primary"
}
```

The LLM invented "Prepare for the Long Winter" — it is not in any enum. It composed a novel
combination of existing goal categories. The Refinery validates the composition is legal.

---

## 6. Influence Tuning System

### 6.1 Design Goal

The Director's influence must be "just right" — enough to create interesting emergent behavior,
not so much that it overrides player/NPC agency or makes the simulation feel scripted. The tuning
system has 3 safety layers, introduced incrementally.

### 6.2 Layer 1: Per-Effect Intensity (Phase 0-1)

Every effect in the vocabulary carries explicit magnitude parameters:

- Domain modifiers: `modifier` field (-0.30 to +0.30)
- Goal biases: `weight` field (0.0 to 0.50)
- Duration: `durationTicks` field (5 to 50)

These are the values that the LLM proposes (Phase 3+) and the Refinery validates (Phase 2+).
In Phase 0-1, mock planners set these to conservative defaults.

The per-effect intensity is the primary tuning knob. A drought with modifier -0.05 is a light event.
A drought with modifier -0.25 is a crisis. The same effect type, different intensity.

### 6.3 Layer 2: Global Dampening Factor (Phase 0-1)

A single configuration value that multiplicatively scales ALL director effects:

```
REFINERY_DIRECTOR_DAMPENING = 1.0  (default, full effect)
REFINERY_DIRECTOR_DAMPENING = 0.5  (halved effects, conservative)
REFINERY_DIRECTOR_DAMPENING = 0.0  (director has zero gameplay effect, narrative only)
REFINERY_DIRECTOR_DAMPENING = 1.5  (amplified effects, for testing drama)
```

This is a developer/operator knob. The LLM does not control it. The Refinery does not see it.
It is applied in the C# runtime at the moment effects are registered in the modifier engine.

Effective modifier = proposed_modifier * dampening_factor

This provides a simple global safety valve during development and balance testing.

### 6.4 Layer 3: Influence Budget per Checkpoint (Phase 3)

When the LLM is active, a budget system prevents it from proposing too many or too strong effects
in a single checkpoint.

**Budget calculation:**

Each effect has a cost based on type and magnitude:

| Effect type | Cost formula |
|-------------|-------------|
| Minor beat (0 effects) | 0 points |
| Domain modifier | `abs(modifier) * durationTicks * 0.5` |
| Goal bias | `weight * durationTicks * 0.3` |
| Causal chain | 2 points flat + follow-up cost |
| Entity hint | 1 point flat |
| Spatial hint | 1.5 points flat + modifier cost |
| Faction hint | `abs(relationDelta) * durationTicks * 0.4` |

**Budget per checkpoint:** 5.0 points (configurable via `REFINERY_DIRECTOR_BUDGET`)

Example budget usage:
- Minor beat (flavor text) = 0 points
- Major beat with food -0.15 for 25 ticks = 0.15 * 25 * 0.5 = 1.875 points
- Directive with 2 biases (0.20 + 0.15) for 25 ticks = (0.20 * 25 * 0.3) + (0.15 * 25 * 0.3) = 1.5 + 1.125 = 2.625 points
- Total = 4.5 points, within budget

The Refinery validates that the proposed checkpoint's total cost is within budget.
The LLM receives the remaining budget in its prompt context.

### 6.5 Effect Decay

All timed effects use linear decay, not step-function cutoff:

```
effectiveModifier = baseModifier * (remainingTicks / totalDurationTicks)
```

At tick 0 (just applied): full modifier.
At tick totalDuration/2: half modifier.
At tick totalDuration: zero, effect removed.

This prevents jarring transitions and makes effects feel organic. A drought does not suddenly end —
it gradually eases as conditions improve.

### 6.6 Tuning Summary

| Layer | When | Who controls | What it does |
|-------|------|-------------|-------------|
| Per-effect intensity | Phase 0-1 | Mock planner / LLM | Granular per-effect magnitude |
| Global dampening | Phase 0-1 | Developer/operator | Scales ALL effects, safety valve |
| Influence budget | Phase 3 | Refinery validates, LLM must respect | Self-balancing total intervention cap |

---

## 7. Checkpoint Trigger System

### 7.1 F6 Manual Trigger (Current and Phase 0-3)

The Director checkpoint is currently triggered by pressing F6 in the game. This is intentional:

- Simple, predictable, debuggable
- Player controls when the "god" intervenes
- No risk of unwanted automated intervention during development
- Aligns with the existing F6 refinery trigger flow

F6 remains the primary trigger for all implementation phases. Automated triggers are future work.

### 7.2 Future: Configurable Tick Interval (Phase 4+)

Optional automated checkpoint at configurable intervals:

```
REFINERY_DIRECTOR_AUTO_INTERVAL = 0     (disabled, F6 only — default)
REFINERY_DIRECTOR_AUTO_INTERVAL = 300   (every 300 ticks, ~1 season)
```

When enabled, the runtime fires a checkpoint automatically every N ticks.
F6 manual trigger remains available alongside automated triggers.
Cooldown applies: automated trigger respects the same cooldown as F6.

### 7.3 Future: Event-Driven Triggers (Phase 4+)

Reactive triggers that fire on significant world events:

| Trigger | Condition | Rationale |
|---------|-----------|-----------|
| Season change | Season enum transition | Natural narrative breakpoint |
| Population milestone | Population crosses 50, 100, 200... | Colony growth moments |
| Crisis detection | Food or morale below critical threshold | Director can respond to emergencies |
| War state change | ColonyWarState transitions | Geopolitical turning points |

Event-driven triggers are future scope. They require the runtime to emit state-change events,
which is not yet implemented. Planned for Phase 4+ or as a Track B incremental.

---

## 8. Contract v2 Design

### 8.1 Namespace Strategy

Director ops live in a `v2` contract namespace, separate from existing `v1` tech patch ops.

```
WorldSim.Contracts/
  v1/
    PatchOp.cs          (existing: AddTechOp, TweakTechOp, AddWorldEventOp)
    PatchRequest.cs     (existing)
    PatchResponse.cs    (existing)
    PatchGoals.cs       (existing: TECH_TREE_PATCH, WORLD_EVENT, NPC_POLICY)
  v2/
    DirectorOps.cs      (new: AddStoryBeatOp, SetColonyDirectiveOp)
    DirectorGoals.cs    (new: SEASON_DIRECTOR_CHECKPOINT)
    EffectVocabulary.cs (new: DomainModifierEffect, GoalBiasEffect, ...)
    DirectorSnapshot.cs (new: checkpoint snapshot extension)
```

v1 remains untouched. v2 imports shared base types (opId pattern, request/response envelope)
but defines its own op types and goal.

### 8.2 New Goal: SEASON_DIRECTOR_CHECKPOINT

```csharp
// WorldSim.Contracts/v2/DirectorGoals.cs
public static class DirectorGoals
{
    public const string SeasonDirectorCheckpoint = "SEASON_DIRECTOR_CHECKPOINT";
}
```

Java parity:
```java
// Goal.java — add to enum
SEASON_DIRECTOR_CHECKPOINT
```

### 8.3 Op: addStoryBeat Schema

```csharp
// WorldSim.Contracts/v2/DirectorOps.cs
public sealed record AddStoryBeatOp(
    string OpId,
    string Severity,         // "minor" | "major" | "epic"
    string BeatName,         // LLM-generated name (display/debug only)
    string Narrative,        // LLM-generated narrative text
    IReadOnlyList<EffectEntry> Effects,
    CausalChainEntry? CausalChain  // Phase 3+, nullable
) : IDirectorOp;
```

### 8.4 Op: setColonyDirective Schema

```csharp
// WorldSim.Contracts/v2/DirectorOps.cs
public sealed record SetColonyDirectiveOp(
    string OpId,
    string DirectiveName,    // LLM-generated name (display/debug only)
    IReadOnlyList<GoalBiasEntry> Biases,
    int DurationTicks,
    string Target            // "colony:primary" | "colony:<id>"
) : IDirectorOp;
```

### 8.5 Effect Vocabulary Types

```csharp
// WorldSim.Contracts/v2/EffectVocabulary.cs
public sealed record EffectEntry(
    string Type,            // "domain_modifier"
    string Domain,          // "food" | "morale" | "economy" | "military" | "research"
    double Modifier,        // -0.30 .. +0.30
    int DurationTicks       // 5 .. 50
);

public sealed record GoalBiasEntry(
    string Type,            // "goal_bias"
    string GoalCategory,    // "farming" | "gathering" | "crafting" | ...
    double Weight,          // 0.0 .. 0.50
    int DurationTicks       // 5 .. 50 (optional, inherits from directive if omitted)
);

// Phase 3+ (nullable/optional in schema)
public sealed record CausalChainEntry(
    string Type,            // "causal_chain"
    CausalCondition Condition,
    AddStoryBeatOp FollowUpBeat,
    int WindowTicks,
    int MaxTriggers
);

public sealed record CausalCondition(
    string Metric,
    string Operator,        // "lt" | "gt" | "eq"
    double Threshold
);
```

### 8.6 Snapshot Extension

The director checkpoint snapshot extends the existing PatchRequest snapshot with director-specific fields:

```csharp
// WorldSim.Contracts/v2/DirectorSnapshot.cs
public sealed record DirectorSnapshotData(
    int CurrentTick,
    string CurrentSeason,
    int ColonyPopulation,
    double FoodReservesPct,
    double MoraleAvg,
    double EconomyOutput,
    IReadOnlyList<ActiveBeatSummary> ActiveBeats,
    IReadOnlyList<ActiveDirectiveSummary> ActiveDirectives,
    int BeatCooldownRemainingTicks,
    double RemainingInfluenceBudget    // Phase 3+
);
```

Java parity: `DirectorSnapshotMapper.java` already maps to `DirectorRuntimeFacts.java` —
extend with matching fields.

### 8.7 Wire Format

See Appendix A for complete wire format examples. The JSON structure follows the existing
PatchRequest/PatchResponse envelope with v2 ops nested inside.

### 8.8 v1 Backward Compatibility

- v1 ops (`addTech`, `tweakTech`, `addWorldEvent`) are unchanged
- v1 goals (`TECH_TREE_PATCH`, `WORLD_EVENT`, `NPC_POLICY`) are unchanged
- v2 goal (`SEASON_DIRECTOR_CHECKPOINT`) routes through a different planner path
- The parser distinguishes v1 and v2 ops by the `op` field name
- A single PatchResponse can contain mixed v1 + v2 ops (future, for combined patches)
- The `ValidateKnownOps()` whitelist in the parser is extended, not replaced

---

## 9. Generic Runtime Engine Design

### 9.1 Domain Modifier Engine

The domain modifier engine is a runtime subsystem that manages timed domain multipliers.
It is completely generic — it does not know about beats, directives, or the Director system.
It just applies modifiers to domains.

```csharp
// WorldSim.Runtime (conceptual API)
public class DomainModifierEngine
{
    // Register a new modifier (called by ApplyStoryBeat command)
    void RegisterModifier(string sourceId, string domain, double modifier,
                          int durationTicks, double dampeningFactor);

    // Called every tick in the simulation update loop
    void Tick();

    // Query current effective modifier for a domain
    double GetEffectiveModifier(string domain);

    // Snapshot for read model
    IReadOnlyList<ActiveModifierInfo> GetActiveModifiers();
}
```

Implementation notes:
- Modifiers are stored in a flat list (expected count: 0-10 at any time)
- Each tick: decrement remaining ticks, recalculate effective modifier
- Effective modifier = sum of all active modifiers for domain (each decayed by linear formula)
- Capped at abs(0.4) per domain to prevent runaway
- Expired modifiers removed lazily (on next tick or query)
- The engine is consumed by the simulation's ecology/economy/morale update methods,
  which multiply their base values by (1.0 + effectiveModifier)

### 9.2 Goal Bias Engine

The goal bias engine manages timed biases to NPC goal utility scores. Like the modifier engine,
it is generic.

```csharp
// WorldSim.Runtime (conceptual API)
public class GoalBiasEngine
{
    // Register biases (called by ApplyColonyDirective command)
    void RegisterBiases(string sourceId, string colonyId,
                        IReadOnlyList<GoalBiasSpec> biases,
                        int durationTicks, double dampeningFactor);

    // Replace active directive for a colony (blend transition)
    void ReplaceDirective(string colonyId, string newSourceId,
                          IReadOnlyList<GoalBiasSpec> biases,
                          int durationTicks, double dampeningFactor);

    // Called every tick
    void Tick();

    // Query current bias for a goal category in a colony (called by AI layer)
    double GetEffectiveBias(string colonyId, string goalCategory);

    // Query if job priority threshold is met for a category
    bool IsJobPriorityActive(string colonyId, string goalCategory);

    // Snapshot for read model
    IReadOnlyList<ActiveBiasInfo> GetActiveBiases(string colonyId);
}
```

Implementation notes:
- One active directive per colony (replacement policy with 5-tick blend)
- Biases decay linearly like domain modifiers
- Job priority threshold: 0.25 effective weight
- Track C (AI module) calls `GetEffectiveBias()` during NPC goal evaluation
- Track C calls `IsJobPriorityActive()` during job slot assignment

### 9.3 Timed Effect Scheduler

Both engines share a common timed-effect pattern. This can be factored into a shared scheduler
or kept inline per engine. The pattern is:

```
each tick:
  for each active effect:
    effect.remainingTicks--
    if effect.remainingTicks <= 0:
      remove effect
    else:
      effect.effectiveValue = effect.baseValue * (effect.remainingTicks / effect.totalDuration)
```

The scheduler does not need to be a separate class — it is an implementation detail within each engine.

### 9.4 Director State Model

The runtime maintains a `DirectorState` object that tracks the Director's operational state:

```csharp
// WorldSim.Runtime (conceptual)
public class DirectorState
{
    // Active beats with remaining durations
    IReadOnlyList<ActiveBeat> ActiveBeats { get; }

    // Active directive per colony
    IReadOnlyDictionary<string, ActiveDirective> ActiveDirectives { get; }

    // Cooldown tracking
    int MajorBeatCooldownRemainingTicks { get; }
    int EpicBeatCooldownRemainingTicks { get; }

    // Budget tracking (Phase 3+)
    double LastCheckpointBudgetUsed { get; }

    // Causal chain monitoring (Phase 3+)
    IReadOnlyList<PendingCausalChain> PendingChains { get; }

    // Methods
    void ApplyBeat(ActiveBeat beat);
    void ApplyDirective(string colonyId, ActiveDirective directive);
    void Tick(); // advance cooldowns, check causal conditions
}
```

`DirectorState` is owned by `SimulationRuntime` and updated in the main tick loop.

### 9.5 Snapshot Builder Extension

`WorldSnapshotBuilder` is extended to include director state in the read model:

```csharp
// Added to WorldRenderSnapshot or a nested DirectorSnapshot
public record DirectorRenderState(
    IReadOnlyList<ActiveBeatDisplay> ActiveBeats,
    string? ActiveDirectiveName,        // for HUD display
    string? ActiveDirectiveTarget,
    int DirectiveDurationRemaining,
    IReadOnlyList<EventFeedEntry> RecentBeatNarratives,
    string DirectorStage,               // "mock" | "refinery" | "llm"
    string OutputMode                   // "both" | "story_only" | "nudge_only" | "off"
);
```

The Graphics layer (Track A) consumes this for:
- Event feed: beat narrative text with severity-based formatting
- HUD: active directive indicator, timer, stage marker
- Debug overlay: modifier values, bias values, budget, cooldowns

### 9.6 Command Endpoints

Two new command endpoints in `SimulationRuntime`:

```csharp
public CommandResult ApplyStoryBeat(AddStoryBeatOp op)
{
    // 1. Validate severity tier matches effects count
    // 2. Apply dampening factor to all effects
    // 3. Register domain modifiers in DomainModifierEngine
    // 4. Update DirectorState (active beats, cooldowns)
    // 5. Add event feed entry (narrative text)
    // 6. Return success/failure with explain
}

public CommandResult ApplyColonyDirective(SetColonyDirectiveOp op)
{
    // 1. Validate target colony exists
    // 2. Apply dampening factor to all biases
    // 3. Register biases in GoalBiasEngine (replace existing)
    // 4. Update DirectorState (active directives)
    // 5. Return success/failure with explain
}
```

---

## 10. Refinery Formal Model Design

### 10.1 Overview

The Refinery formal model follows the OnlabRefinery layered pattern. It is implemented as imperative
Java validation (not the actual `tools.refinery` solver SDK), consistent with the existing WorldSim
Java service architecture.

The model defines what the LLM CAN propose. Everything not explicitly allowed is rejected.

### 10.2 Design Layer (Stable Rules)

The design layer defines the vocabulary and entity skeleton. The LLM receives this as read-only context
but cannot modify it.

```java
// DirectorDesign.java (extended)
public final class DirectorDesign {
    // Valid domains
    public static final Set<String> VALID_DOMAINS =
        Set.of("food", "morale", "economy", "military", "research");

    // Valid goal categories
    public static final Set<String> VALID_GOAL_CATEGORIES =
        Set.of("farming", "gathering", "crafting", "building",
               "social", "military", "research", "rest");

    // Beat severity tiers
    public static final Set<String> VALID_SEVERITIES =
        Set.of("minor", "major", "epic");

    // Bounds
    public static final double MODIFIER_MIN = -0.30;
    public static final double MODIFIER_MAX = +0.30;
    public static final double WEIGHT_MIN = 0.0;
    public static final double WEIGHT_MAX = 0.50;
    public static final int DURATION_MIN = 5;
    public static final int DURATION_MAX = 50;
    public static final int MAX_EFFECTS_PER_BEAT = 3;
    public static final int MAX_BIASES_PER_DIRECTIVE = 3;
    public static final double DEFAULT_BUDGET = 5.0;

    // Cooldowns
    public static final int MAJOR_BEAT_COOLDOWN = 15;
    public static final int EPIC_BEAT_COOLDOWN = 30;

    // Stacking
    public static final double MAX_DOMAIN_STACK = 0.40;
}
```

### 10.3 Model Layer (Constraints and Invariants)

The model layer defines validation rules. Each rule is a predicate that returns pass/fail + error message.

```java
// DirectorModelValidator.java (extended from existing 109 lines)
public final class DirectorModelValidator {

    public ValidationResult validate(DirectorCandidate candidate, DirectorRuntimeFacts facts) {
        List<String> errors = new ArrayList<>();

        // --- Beat constraints ---
        validateBeatSeverityMatchesEffects(candidate, errors);
        validateEffectDomains(candidate, errors);
        validateEffectBounds(candidate, errors);
        validateBeatCooldown(candidate, facts, errors);
        validateMaxActiveBeats(candidate, facts, errors);
        validateDomainStacking(candidate, facts, errors);

        // --- Directive constraints ---
        validateGoalCategories(candidate, errors);
        validateBiasBounds(candidate, errors);
        validateMaxBiasesPerDirective(candidate, errors);
        validateOneDirectivePerColony(candidate, errors);

        // --- Budget constraints (Phase 3+) ---
        validateInfluenceBudget(candidate, facts, errors);

        // --- Causal chain constraints (Phase 3+) ---
        validateCausalChainNoLoop(candidate, errors);
        validateCausalChainBudget(candidate, facts, errors);
        validateCausalConditionMetric(candidate, errors);

        return errors.isEmpty()
            ? ValidationResult.valid()
            : ValidationResult.invalid(errors);
    }
}
```

### 10.4 Runtime Layer (Checkpoint Context Facts)

The runtime layer provides current world state that the validator uses for contextual checks
(e.g., cooldown remaining, active effects).

```java
// DirectorRuntimeFacts.java (extended)
public record DirectorRuntimeFacts(
    int tick,
    String season,
    int colonyPopulation,
    double foodReservesPct,
    double moraleAvg,
    double economyOutput,
    int beatCooldownRemainingTicks,
    List<ActiveBeatFact> activeBeats,
    List<ActiveDirectiveFact> activeDirectives,
    double remainingInfluenceBudget  // Phase 3+
) {}
```

### 10.5 Invariant Pack

Comprehensive list of all invariants enforced by the Refinery:

| ID | Invariant | Phase |
|----|-----------|-------|
| INV-01 | Beat severity matches effect count (minor=0, major=1-2, epic=2-3) | 0 |
| INV-02 | All effect domains are in VALID_DOMAINS | 0 |
| INV-03 | All modifier values within [MODIFIER_MIN, MODIFIER_MAX] | 0 |
| INV-04 | All duration values within [DURATION_MIN, DURATION_MAX] | 0 |
| INV-05 | Max MAX_EFFECTS_PER_BEAT effects per beat | 0 |
| INV-06 | Major beat cooldown respected | 0 |
| INV-07 | Epic beat cooldown respected | 0 |
| INV-08 | Max 1 active major beat | 0 |
| INV-09 | Max 1 active epic beat | 0 |
| INV-10 | Domain stacking cap: sum of active modifiers per domain <= MAX_DOMAIN_STACK | 0 |
| INV-11 | All goal categories in VALID_GOAL_CATEGORIES | 0 |
| INV-12 | All bias weights within [WEIGHT_MIN, WEIGHT_MAX] | 0 |
| INV-13 | Max MAX_BIASES_PER_DIRECTIVE biases per directive | 0 |
| INV-14 | Max 1 directive per colony | 0 |
| INV-15 | Total checkpoint cost within influence budget | 3 |
| INV-16 | Causal chain: no loops (follow-up cannot reference parent) | 3 |
| INV-17 | Causal chain: combined budget within bounds | 3 |
| INV-18 | Causal chain: condition metric exists | 3 |
| INV-19 | Causal chain: window duration within bounds | 3 |
| INV-20 | No contradictory same-domain modifiers in one checkpoint | 2 |

### 10.6 Validation/Repair Loop

The Refinery uses an iterative validation loop following the OnlabRefinery pattern:

```
attempt = 0
maxAttempts = 5

while attempt < maxAttempts:
    candidate = planner.propose(facts, previousErrors)
    result = validator.validate(candidate, facts)

    if result.isValid():
        return candidate  // success

    attempt++
    previousErrors = result.errors()  // feedback for next attempt

// All retries exhausted — deterministic fallback
return fallbackPlanner.generate(facts)
```

The fallback planner is a pure-deterministic generator that always produces a valid (but uncreative)
candidate. It guarantees the pipeline never fails.

In Phase 0-1 (mock only): the loop runs once, mock always produces valid output.
In Phase 2 (Refinery wired): the loop validates even mock output (defense in depth).
In Phase 3 (LLM active): the loop handles LLM hallucinations via feedback + retry.

---

## 11. LLM Integration Design

### 11.1 Prompt Architecture (Phase 3)

The LLM receives a structured prompt with three sections, matching the layered model:

```
SYSTEM PROMPT:
  You are the Season Director for a colony simulation.
  You observe the world state and propose narrative events and policy nudges.

  DESIGN RULES (read-only, do not violate):
    - Valid domains: [food, morale, economy, military, research]
    - Valid goal categories: [farming, gathering, ...]
    - Modifier bounds: [-0.30, +0.30]
    - Duration bounds: [5, 50]
    - Max effects per beat: 3
    - Max biases per directive: 3
    - Influence budget this checkpoint: {remaining_budget}

  OUTPUT FORMAT:
    Respond with a single JSON object containing:
    - "explanation": brief reasoning for your choices
    - "storyBeat": { ... } or null
    - "colonyDirective": { ... } or null
    - "warnings": [] (self-identified concerns)

USER PROMPT:
  CURRENT WORLD STATE (runtime layer):
    Tick: {tick}, Season: {season}
    Population: {pop}, Food reserves: {food_pct}%
    Morale: {morale_avg}, Economy: {econ_output}
    Active beats: [{...}]
    Active directives: [{...}]
    Beat cooldown remaining: {cooldown} ticks

  RECENT HISTORY:
    Last 3 beats: [{names and effects}]
    Last directive: {name and biases}

  INSTRUCTION:
    Propose a story beat and/or colony directive for this checkpoint.
    Be creative with names and narratives. Compose effects from the vocabulary.
    Stay within the influence budget. Respect cooldowns.
    If the world is stable and nothing interesting is happening, a minor
    beat (flavor text only) is perfectly acceptable.
```

### 11.2 Candidate Format

The LLM returns a JSON candidate that the `DirectorCandidateParser` deserializes:

```json
{
  "explanation": "The colony is struggling with food. A drought event creates narrative tension and a food directive helps them respond.",
  "storyBeat": {
    "severity": "major",
    "beatName": "Dust and Silence",
    "narrative": "The wells run shallow. Farmers stare at cracked earth where rivers once flowed.",
    "effects": [
      { "type": "domain_modifier", "domain": "food", "modifier": -0.12, "durationTicks": 20 }
    ]
  },
  "colonyDirective": {
    "directiveName": "Emergency Harvest Protocol",
    "biases": [
      { "type": "goal_bias", "goalCategory": "farming", "weight": 0.25 },
      { "type": "goal_bias", "goalCategory": "gathering", "weight": 0.20 }
    ],
    "durationTicks": 25,
    "target": "colony:primary"
  },
  "warnings": []
}
```

### 11.3 Iterative Correction

When the Refinery rejects a candidate, the feedback is prepended to the next LLM prompt:

```
PREVIOUS ATTEMPT REJECTED. Errors:
  - INV-03: modifier -0.45 exceeds maximum -0.30 for domain "food"
  - INV-15: total budget cost 6.2 exceeds limit 5.0

Please fix these specific issues and resubmit. Do not change parts that were valid.
```

The LLM receives the original context + the error feedback. Per OnlabRefinery documentation:
"Az LLM-ek jellemzoen jobban tudnak javitani egy konkret, lokalizalt hibat, mint elsore tokeletesre
megcsinalni egy modellt." — LLMs are typically better at fixing localized errors than getting it
perfect on the first try.

### 11.4 Deterministic Fallback

After maxAttempts (5) failed LLM attempts, the system falls back to a deterministic generator:

```java
public class FallbackDirectorPlanner {
    public DirectorCandidate generate(DirectorRuntimeFacts facts) {
        // Always produces a valid minor beat + conservative directive
        // Based on simple heuristics from world state:
        //   - Low food → PrioritizeFood directive + food-themed minor beat
        //   - Low morale → StabilizeMorale directive + morale-themed minor beat
        //   - Otherwise → generic minor beat + BoostIndustry directive
        // No LLM involved, always valid, always deterministic
    }
}
```

The fallback is boring but safe. It ensures the pipeline never fails and the user always gets
a response from the Director, even if the LLM is unavailable or consistently hallucinating.

### 11.5 Output Mode Matrix

Director output is independently toggleable:

| Mode | Story Beat | Colony Directive | Use Case |
|------|-----------|-----------------|----------|
| `both` | yes | yes | Normal operation |
| `story_only` | yes | no | Narrative without gameplay nudges |
| `nudge_only` | no | yes | Policy nudges without narrative |
| `off` | no | no | Director runs for telemetry/dry-run only |

Controlled via:
- Java: `planner.director.outputMode` config property
- C#: `REFINERY_DIRECTOR_OUTPUT_MODE` env var (adapter-level override)
- Future: per-request constraint in payload

---

## 12. Implementation Phases

### Phase 0: Contract and Plumbing Foundation (Sprint 1)

**Objective:** Ship a complete end-to-end pipeline with deterministic mock data. Every component is
wired, parsing works, translation works, but the effects are trivial. This proves the plumbing.

**Track ownership:** Track D primary.

#### Sprint 1 — Epic S1-A: Contract v2 Expansion

Track: D

Tasks:
1. Create `WorldSim.Contracts/v2/` namespace
2. Add `DirectorGoals.cs` with `SEASON_DIRECTOR_CHECKPOINT`
3. Add `DirectorOps.cs` with `AddStoryBeatOp`, `SetColonyDirectiveOp`
4. Add `EffectVocabulary.cs` with `EffectEntry`, `GoalBiasEntry`
5. Add `DirectorSnapshot.cs` with checkpoint snapshot type
6. Keep v1 untouched
7. Add contract unit tests

Acceptance:
- Contract compiles independently
- v1 backward compatibility preserved
- Contract types serialize/deserialize correctly

#### Sprint 1 — Epic S1-B: Java PatchOp/Goal Expansion + Mock Director

Track: D

Tasks:
1. Add `AddStoryBeat` and `SetColonyDirective` to `PatchOp.java` sealed interface
2. Add `SEASON_DIRECTOR_CHECKPOINT` to `Goal.java` enum
3. Wire `ComposedPatchPlanner` to route Director goals to `DirectorRefineryPlanner`
4. Implement mock branch in planner: deterministic minor beat + conservative directive
5. Add output mode gating (`both/story_only/nudge_only/off`)
6. Update JSON fixtures and schemas
7. Add explain markers: `directorStage:mock`, `directorOutputMode:<mode>`

Acceptance:
- `POST /v1/patch` with goal `SEASON_DIRECTOR_CHECKPOINT` returns valid response
- Same seed + tick + snapshot → same output (deterministic)
- Output mode matrix works
- Existing v1 goals still work unchanged

#### Sprint 1 — Epic S1-C: C# Parser/Applier Expansion

Track: D

Tasks:
1. Extend `PatchResponseParser.ValidateKnownOps()` to accept `addStoryBeat`, `setColonyDirective`
2. Add deserialization for v2 op types
3. Extend `PatchApplier` with cases for new ops
4. Update canonical hash calculation
5. Add parser unit tests for new ops

Acceptance:
- Parser correctly deserializes v2 ops from JSON
- Unknown ops still fail deterministically
- Dedupe works for new op types

#### Sprint 1 — Epic S1-D: Adapter Translation Paths

Track: D

Tasks:
1. Extend `PatchCommandTranslation` with:
   - `addStoryBeat` → `ApplyStoryBeatCommand`
   - `setColonyDirective` → `ApplyColonyDirectiveCommand`
2. Add command types in `RuntimeCommands.cs`
3. Wire through `RefineryPatchRuntime`
4. Add deterministic error for unknown colony/directive
5. Add translation unit tests

Acceptance:
- Translation maps new ops to runtime commands
- Unknown ops produce deterministic error
- Existing addTech translation unchanged

### Phase 1: Runtime Effects Core (Sprint 2-3)

**Objective:** The generic modifier and bias engines work. Effects are applied, decay, and expire.
The Director state is tracked. The HUD shows director status. Effects are felt in gameplay.

**Track ownership:** Track D primary. Cross-track requests to Track B (runtime engines) and
Track A (HUD) and Track C (AI bias integration).

#### Sprint 2 — Epic S2-A: Domain Modifier Engine

Track: B (runtime), requested by Track D

Tasks:
1. Implement `DomainModifierEngine` in `WorldSim.Runtime`
2. Register/tick/query pattern with linear decay
3. Per-domain stacking cap (abs 0.4)
4. Wire into ecology/economy/morale update methods
5. Apply global dampening factor at registration time
6. Unit tests: registration, decay, stacking, cap, expiry

Acceptance:
- Modifiers affect domain production values
- Linear decay works correctly
- Stacking cap prevents runaway

#### Sprint 2 — Epic S2-B: Goal Bias Engine

Track: B (runtime) + C (AI integration), requested by Track D

Tasks:
1. Implement `GoalBiasEngine` in `WorldSim.Runtime`
2. Register/tick/query pattern with linear decay
3. One-directive-per-colony replacement with 5-tick blend
4. Job priority threshold at effective weight 0.25
5. Expose `GetEffectiveBias()` and `IsJobPriorityActive()` via interface for Track C
6. Track C: integrate bias query into NPC goal evaluation
7. Track C: integrate job priority into profession assignment
8. Unit tests: registration, decay, replacement blend, priority threshold

Acceptance:
- Active directive visibly changes NPC behavior (NPCs shift toward biased goals)
- Job priority fills directive-aligned slots first
- Replacement transitions smoothly (no sudden jumps)

#### Sprint 2 — Epic S2-C: Director State and Tick Integration

Track: B (runtime), requested by Track D

Tasks:
1. Implement `DirectorState` in `WorldSim.Runtime`
2. Wire `DirectorState.Tick()` into main simulation tick loop
3. Implement cooldown tracking (major/epic beat cooldowns)
4. Implement `ApplyStoryBeat()` command endpoint in `SimulationRuntime`
5. Implement `ApplyColonyDirective()` command endpoint in `SimulationRuntime`
6. Build director snapshot for checkpoint requests
7. Unit tests: cooldown advance, beat apply, directive apply, snapshot correctness

Acceptance:
- Director effects apply and expire deterministically
- Cooldowns are enforced
- Snapshot reflects current director state

#### Sprint 3 — Epic S3-A: Beat Severity Tier Implementation

Track: D (adapter validation) + B (runtime tier logic)

Tasks:
1. Validate beat severity matches effect count in adapter
2. Apply tier-specific cooldown logic
3. Minor beats: event feed only, no modifier registration
4. Major beats: register up to 2 modifiers
5. Epic beats: register up to 3 modifiers
6. Add severity-based event feed formatting

Acceptance:
- Minor beats have zero gameplay effect
- Major/epic beats have visible gameplay effect
- Tier classification is deterministic based on effect count

#### Sprint 3 — Epic S3-B: HUD and Event Feed Integration

Track: A (graphics), requested by Track D

Tasks:
1. Extend `WorldSnapshotBuilder` with `DirectorRenderState`
2. Add beat narrative entries to event feed with severity-based styling
3. Add HUD indicator: active directive name + timer
4. Add debug overlay: modifier values, bias values, cooldowns, stage marker
5. Add output mode indicator in HUD status line

Acceptance:
- Beat narratives appear in event feed
- Active directive visible in HUD
- Debug overlay shows all director internals
- Manual smoke test: trigger F6, observe feed + HUD + overlay

#### Sprint 3 — Epic S3-C: Output Mode Matrix (End-to-End)

Track: D

Tasks:
1. Wire output mode from Java response through adapter to runtime
2. Implement C#-side output mode override via env var
3. Test all 4 modes: both, story_only, nudge_only, off
4. Add smoke test for each mode

Acceptance:
- Each mode produces correct subset of effects
- `off` mode: no effects applied, but response is logged
- Modes switchable at runtime via env var

#### Sprint 3 — Epic S3-D: Fixture Parity and Smoke Test

Track: D

Tasks:
1. Create fixture test: recorded snapshot → expected response → apply → expected state
2. Verify C#/Java fixture parity (same input → same output)
3. Create manual smoke test checklist:
   - F6 → event feed shows beat
   - HUD shows directive
   - Domain modifier visible in debug overlay
   - Modifier decays over ticks
   - Cooldown prevents rapid re-trigger
4. Verify dampening factor works (set to 0.0, verify no gameplay effect)

Acceptance:
- Fixture tests pass
- All smoke test items verified
- Dampening factor confirmed functional

### Phase 2: Refinery Gate (Sprint 4-5)

**Objective:** The Refinery formal model is implemented. Even mock output is validated.
The iterative correction loop is wired. Deterministic fallback exists.

**Track ownership:** Track D only (Java service + adapter hardening).

#### Sprint 4 — Epic S4-A: Formal Model Layers in Java

Track: D

Tasks:
1. Extend `DirectorDesign.java` with full vocabulary constants (Section 10.2)
2. Extend `DirectorModelValidator.java` with all Phase 0-2 invariants (INV-01 through INV-14, INV-20)
3. Extend `DirectorRuntimeFacts.java` with active beats/directives/cooldowns
4. Extend `DirectorSnapshotMapper.java` to populate runtime facts from request snapshot
5. Unit tests for each invariant (positive and negative cases)

Acceptance:
- Every invariant from the pack is tested
- Valid mock output passes validation
- Known-invalid candidates are rejected with correct error messages

#### Sprint 4 — Epic S4-B: Validation/Repair Loop

Track: D

Tasks:
1. Wire `DirectorRefineryPlanner` to call validator after mock planner
2. Implement retry loop (max 5 attempts) with error feedback
3. For mock path: first attempt should always pass (defense in depth)
4. Implement `FallbackDirectorPlanner` for deterministic safe output
5. Wire fallback after retry exhaustion
6. Add explain markers: `directorStage:refinery-validated`, fallback warning

Acceptance:
- Mock path: validated in 1 attempt, no fallback
- Injected-invalid candidate: retried, eventually falls back
- Fallback always produces valid output

#### Sprint 5 — Epic S5-A: Runtime Hardening

Track: D

Tasks:
1. Verify opId dedupe works for director ops
2. Add counters/telemetry:
   - Director requests count
   - Validated outputs count
   - Fallback count
   - Rejected command count
   - Average retry count
3. Add diagnostic logging for each pipeline step

Acceptance:
- Counters are queryable for debugging
- No duplicate effects from same opId
- Pipeline failures are logged with full context

#### Sprint 5 — Epic S5-B: Invariant Pack Completeness

Track: D

Tasks:
1. Add INV-20 (no contradictory same-domain modifiers)
2. Review all invariants against edge cases
3. Add fuzzing test: random candidate generation → validator should never crash
4. Document invariant rationale

Acceptance:
- Fuzz test passes (1000 random candidates, no crashes)
- All invariants have documented rationale

### Phase 3: LLM Creativity (Sprint 6-7)

**Objective:** LLM proposes creative beats and directives. The Refinery validates compositions.
The influence budget system is active. Causal chains are available.

**Track ownership:** Track D only (Java service LLM integration).

#### Sprint 6 — Epic S6-A: LLM Director Proposal Stage

Track: D

Tasks:
1. Implement `LlmDirectorPlanner` behind `REFINERY_LLM_ENABLED=true` flag
2. Build prompt from Design/Runtime layers using `DirectorPromptFactory`
3. Call OpenRouter via `OpenRouterClient`
4. Parse response via `DirectorCandidateParser`
5. Sanitize: trim fields, clamp values, handle malformed JSON gracefully
6. Default: LLM flag OFF (mock path remains default)

Acceptance:
- LLM flag toggleable without breaking pipeline
- LLM response parsed into candidate
- Malformed LLM output does not crash (graceful fallback)

#### Sprint 6 — Epic S6-B: LLM + Refinery Iterative Correction

Track: D

Tasks:
1. Wire LLM into retry loop: invalid → feedback → re-prompt → retry
2. Build feedback prompt with specific invariant violations
3. Test with intentionally bad prompt to verify retry behavior
4. Verify deterministic fallback after max retries
5. Log each iteration (prompt, response, errors) for debugging

Acceptance:
- LLM hallucinations caught by Refinery
- Feedback loop converges (typically 1-3 retries)
- Fallback always available

#### Sprint 6 — Epic S6-C: Influence Budget System

Track: D (Java) + B (runtime budget tracking)

Tasks:
1. Implement budget cost calculation in `DirectorModelValidator`
2. Add INV-15 (budget constraint) to validator
3. Include remaining budget in LLM prompt context
4. Track budget usage in `DirectorState` (C# runtime)
5. Include remaining budget in director snapshot
6. Add budget display to debug overlay (Track A request)

Acceptance:
- Budget prevents LLM from proposing too many/too strong effects
- Budget visible in debug overlay
- Budget resets at each checkpoint

#### Sprint 7 — Epic S7-A: Causal Chain Layer

Track: D

Tasks:
1. Add `CausalChainEntry` to v2 contracts (C# and Java)
2. Add INV-16 through INV-19 to validator
3. Implement causal chain monitoring in `DirectorState`
4. Runtime: evaluate conditions each tick, trigger follow-up if met
5. Include causal chain prompt guidance in LLM prompt
6. Add causal chain display to debug overlay

Acceptance:
- Causal chains trigger correctly based on world state
- Follow-up beats go through normal modifier engine
- Chains that never trigger expire cleanly after window

#### Sprint 7 — Epic S7-B: Operational UX

Track: D + A

Tasks:
1. Add local profile presets (e.g., `mock-safe`, `llm-creative`, `debug-verbose`)
2. Profile applies env vars in batch
3. Add in-game debug toggle: cycle output mode with a key
4. Reduce env var proliferation by grouping into profiles
5. Document all env vars and profiles

Acceptance:
- Developer can switch modes without editing env vars manually
- Profiles documented in plan or README

### Phase 4+: Expansion (Future, Not Sprint-Planned)

These items are architectural possibilities enabled by the generic engine design.
They are documented here to ensure the architecture does not box them out, but they are
NOT scheduled into sprints. They will be planned when prerequisites are met.

#### Entity Hint Vocabulary Layer

Prerequisites:
- Track C has a trait system for NPCs
- Formal trait vocabulary defined

Description:
- LLM can assign temporary traits to NPCs
- Refinery validates traits exist and are compatible
- NPC AI considers temporary traits in decisions

#### Spatial Hint Vocabulary Layer

Prerequisites:
- Track B has a tile condition system
- Track A has tile overlay rendering

Description:
- LLM can create temporary tile-region conditions
- Refinery validates region types and condition bounds
- Track A renders conditions visually

#### Faction Hint Vocabulary Layer

Prerequisites:
- Combat Master Plan Phase 1 Sprint 2 (FactionRelations system)

Description:
- LLM can shift faction relation values
- Refinery validates faction references and delta bounds
- Feeds into diplomacy/war state machine

#### Combat-Era Directive Taxonomy

Prerequisites:
- Track C has military/training/diplomatic goal categories
- Combat Master Plan Phase 1+

Description:
- New directives: FortifyDefenses, RallyMilitia, SeekDiplomacy, EvacuateOutskirts
- These are just goal bias compositions using new categories — no engine changes

#### Automated Checkpoint Triggers

Prerequisites:
- Stable Director pipeline (Phase 2+ proven)
- Runtime state-change event system

Description:
- Configurable tick interval triggers
- Season boundary triggers
- Event-driven triggers (crisis, milestone, war state change)

---

## 13. Gap Analysis

### 13.1 Java Gaps (Current → Target)

| File | Current State | Required Change | Phase |
|------|--------------|-----------------|-------|
| `PatchOp.java` | 3 records (AddTech, TweakTech, AddWorldEvent) | Add `AddStoryBeat`, `SetColonyDirective` records | 0 |
| `Goal.java` | 3 values (TECH_TREE_PATCH, WORLD_EVENT, NPC_POLICY) | Add `SEASON_DIRECTOR_CHECKPOINT` | 0 |
| `ComposedPatchPlanner.java` | Routes to Mock/Llm/Refinery, no Director path | Add Director goal routing to `DirectorRefineryPlanner` | 0 |
| `DirectorRefineryPlanner.java` | Exists (63 lines), not wired | Wire into `ComposedPatchPlanner`, add mock branch | 0 |
| `DirectorDesign.java` | 3 directives, basic constants | Extend with full vocabulary constants | 0 |
| `DirectorModelValidator.java` | 109 lines, basic validation | Extend with full invariant pack (INV-01 through INV-20) | 2 |
| `DirectorRuntimeFacts.java` | tick, colonyCount, beatCooldownTicks | Extend with active beats/directives, budget | 0-2 |
| `DirectorSnapshotMapper.java` | 11 lines, basic mapping | Extend for full snapshot fields | 0 |
| `DirectorCandidateParser.java` | 83 lines, exists | Extend for v2 effect vocabulary format | 3 |
| `DirectorPromptFactory.java` | 42 lines, exists | Extend with effect vocabulary prompt design | 3 |
| `OpenRouterClient.java` | 85 lines, fully implemented | No changes needed | - |
| `LlmPlanner.java` | Stub returning Optional.empty() | Implement actual LLM proposal flow | 3 |
| JSON fixtures | `patch-season-director-v1.expected.json` exists | Update for v2 format, add more fixtures | 0 |
| JSON schemas | In `examples/schema/` | Add director ops schema | 0 |

### 13.2 C# Gaps (Current → Target)

| File | Current State | Required Change | Phase |
|------|--------------|-----------------|-------|
| `WorldSim.Contracts/v1/PatchOp.cs` | 3 ops | NO CHANGE (v1 stays) | - |
| `WorldSim.Contracts/v2/` | Does not exist | Create: DirectorOps, DirectorGoals, EffectVocabulary, DirectorSnapshot | 0 |
| `PatchResponseParser.cs` | Hardcoded 3-op whitelist | Extend for v2 ops | 0 |
| `PatchApplier.cs` | 3 op cases | Add addStoryBeat/setColonyDirective cases | 0 |
| `PatchCommandTranslation.cs` | Only addTech→UnlockTechCommand | Add beat/directive translation | 0 |
| `RuntimeCommands.cs` | Only UnlockTech feature flag | Add beat/directive command types | 0 |
| `SimulationRuntime.cs` | Only UnlockTechForPrimaryColony | Add ApplyStoryBeat, ApplyColonyDirective | 1 |
| `DomainModifierEngine` | Does not exist | Create in WorldSim.Runtime | 1 |
| `GoalBiasEngine` | Does not exist | Create in WorldSim.Runtime | 1 |
| `DirectorState` | Does not exist | Create in WorldSim.Runtime | 1 |
| `WorldSnapshotBuilder.cs` | No director fields | Extend with DirectorRenderState | 1 |
| `WorldRenderSnapshot.cs` | No director fields | Extend with DirectorRenderState | 1 |
| `RefineryTriggerAdapter.cs` | Only TriggerPatch | Add TriggerDirectorCheckpoint or extend | 0 |
| `RefineryPatchRuntime.cs` | Only tech patch flow | Add director checkpoint flow | 0 |
| `RefineryRuntimeOptions.cs` | No director config | Add dampening, output mode, budget config | 0 |
| `GameHost.cs` | F6 → tech patch | Extend F6 or add separate director trigger | 0 |
| `EventFeedCategory.cs` | Has Director category | Already exists, extend if needed | 1 |

---

## 14. Cross-Track Integration Points

All cross-track work is requested through clean interfaces. Track D does not directly modify
Track A/B/C code — it provides specifications and the respective track implements them.

### 14.1 Track A (Graphics/UI) — Requested by Track D

| Request | Phase | Description |
|---------|-------|-------------|
| A-D1: Event feed beat rendering | 1 (Sprint 3) | Render beat narrative text with severity-based styling (minor=subtle, major=highlighted, epic=prominent) |
| A-D2: HUD directive indicator | 1 (Sprint 3) | Show active directive name, timer, target colony |
| A-D3: Debug overlay — director panel | 1 (Sprint 3) | Show: modifier values per domain, bias values per category, cooldowns, stage, output mode, budget |
| A-D4: Budget display | 3 (Sprint 6) | Add budget remaining to debug overlay |
| A-D5: Causal chain display | 3 (Sprint 7) | Show pending chains and their conditions in debug overlay |

### 14.2 Track B (Runtime) — Requested by Track D

| Request | Phase | Description |
|---------|-------|-------------|
| B-D1: DomainModifierEngine | 1 (Sprint 2) | Generic timed modifier engine consumed by ecology/economy/morale update methods |
| B-D2: GoalBiasEngine | 1 (Sprint 2) | Generic timed bias engine with interface for Track C |
| B-D3: DirectorState | 1 (Sprint 2) | Director operational state, ticked in main loop |
| B-D4: ApplyStoryBeat endpoint | 1 (Sprint 2) | Command endpoint in SimulationRuntime |
| B-D5: ApplyColonyDirective endpoint | 1 (Sprint 2) | Command endpoint in SimulationRuntime |
| B-D6: Director snapshot fields | 1 (Sprint 2) | Extend WorldSnapshotBuilder with DirectorRenderState |
| B-D7: Causal chain evaluation | 3 (Sprint 7) | Condition evaluation in tick loop for pending chains |

### 14.3 Track C (AI) — Requested by Track D

| Request | Phase | Description |
|---------|-------|-------------|
| C-D1: Goal bias query integration | 1 (Sprint 2) | NPC goal evaluation reads `GetEffectiveBias()` before scoring goals |
| C-D2: Job priority integration | 1 (Sprint 2) | Job assignment reads `IsJobPriorityActive()` to prioritize directive-aligned slots |

Both C-D1 and C-D2 are interface-level integrations. Track C implements how biases are consumed
in the NPC decision pipeline. Track D provides the `GoalBiasEngine` interface.

### 14.4 Track D Internal

All contract, adapter, client, and Java service work is Track D owned:
- v2 contract namespace creation
- Parser/applier extension
- Translation layer extension
- Java PatchOp/Goal expansion
- Mock planner implementation
- Refinery validation implementation
- LLM integration
- Output mode matrix
- Fixture/parity tests

---

## 15. Definition of Done

### Phase 0 (Contract & Plumbing)

- [ ] v2 contracts compile and serialize/deserialize correctly
- [ ] Java service accepts `SEASON_DIRECTOR_CHECKPOINT` goal
- [ ] Mock director returns valid response with minor beat + directive
- [ ] C# parser deserializes v2 ops
- [ ] Adapter translates to runtime commands
- [ ] v1 backward compatibility unbroken
- [ ] Output mode matrix works (all 4 modes)

### Phase 1 (Runtime Effects Core)

- [ ] Domain modifier engine applies and decays effects correctly
- [ ] Goal bias engine biases NPC decisions visibly
- [ ] Job priority kicks in at threshold
- [ ] Beat severity tiers produce correct gameplay impact
- [ ] Director state tracks active beats, directives, cooldowns
- [ ] HUD shows beat narrative in event feed
- [ ] HUD shows active directive name + timer
- [ ] Debug overlay shows modifier/bias values
- [ ] Dampening factor at 0.0 → zero gameplay effect
- [ ] Fixture parity: C# and Java agree on same input → same output

### Phase 2 (Refinery Gate)

- [ ] All Phase 0-2 invariants (INV-01 through INV-14, INV-20) implemented and tested
- [ ] Validation/repair loop works (mock path: 1 attempt, injected-invalid: retries + fallback)
- [ ] Fallback planner always produces valid output
- [ ] opId dedupe works for director ops
- [ ] Telemetry counters are functional

### Phase 3 (LLM Creativity)

- [ ] LLM proposes creative beats/directives with novel names and effect compositions
- [ ] Refinery catches hallucinations and triggers retry
- [ ] Feedback loop converges (typically 1-3 retries)
- [ ] Deterministic fallback works after max retries
- [ ] Influence budget prevents overreach
- [ ] Causal chains trigger correctly
- [ ] LLM flag toggleable without breaking pipeline

### Overall (All Phases)

- [ ] Checkpoint-only operation (not per-frame)
- [ ] Output mode matrix works (`both/story_only/nudge_only/off`)
- [ ] Formal gate mandatory when director pipeline active
- [ ] Deterministic fallback always available
- [ ] Fixture and live parity for deterministic mode
- [ ] Deterministic error handling for unknown/invalid ops
- [ ] HUD can verify stage/mode/beat/nudge status
- [ ] Generic engine design supports future vocabulary layers without code changes

---

## 16. Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Director over-controls simulation, kills emergence | High | Medium | Low-frequency checkpoints, soft effects, influence budget, dampening factor |
| LLM hallucinations pass through to runtime | Critical | Low (with Refinery) | Mandatory Refinery gate, bounded retries, deterministic fallback |
| LLM non-determinism reduces reproducibility | Medium | High (inherent) | Deterministic mock path for testing/CI, invariant parity checks |
| Generic modifier engine too abstract, hard to debug | Medium | Low | Comprehensive debug overlay, per-domain visibility, telemetry counters |
| Cross-track drift (Track B/C engine APIs change) | Medium | Medium | Interface-first design, Track D provides specs, implementation behind interfaces |
| Effect balance is wrong (too strong/too weak) | Medium | High (expected) | Global dampening factor for rapid tuning, per-effect intensity bounds, iterative balancing |
| v2 contract breaks v1 flow | High | Low | Separate namespace, no v1 modifications, integration tests |
| Java service response time too slow for LLM path | Medium | Medium | Async trigger (already non-blocking), timeout + fallback, mock path always available |
| Causal chains create unexpected cascading effects | Medium | Medium | Chain budget, max 1 trigger, window timeout, no loops (INV-16) |
| Phase 4+ vocabulary layers require runtime changes | Low | Low | Generic engine designed for extensibility, new layers = new effect types, not new engines |

---

## 17. Relationship to Existing Plans

### Track-D-Season-Director-Plan.md (Superseded)

The existing `WorldSim.RefineryAdapter/Docs/Plans/Track-D-Season-Director-Plan.md` (290 lines, 3 sprints)
was the original tactical sprint plan. This master plan supersedes it as the strategic reference.

Key differences:
- Master plan introduces the Effect Vocabulary system (generic engine vs. enum-based)
- Master plan expands from 3 sprints to 7+ sprints across 4 phases
- Master plan adds the creativity gradient concept
- Master plan details the influence tuning system (3 layers)
- Master plan includes formal invariant pack and gap analysis

The original sprint plan remains valid as a tactical reference for Sprint 1 epics, which
are largely aligned with Phase 0 of this master plan. When discrepancies exist, this master
plan takes precedence.

### Combat-Defense-Campaign-Master-Plan.md (Integration Points)

The Combat Master Plan (1958 lines, 8 phases) defines combat, defense, and campaign mechanics.
The Director system integrates with combat through:

1. **Faction Hint vocabulary** (Phase 4+): Director can shift faction relations, feeding into
   the diplomacy/war state machine (Combat Phase 1 Sprint 2)
2. **Combat-era directives** (Phase 4+): FortifyDefenses, RallyMilitia etc. become valid
   when Track C has military goal categories (Combat Phase 0+)
3. **War state triggers** (Phase 4+): ColonyWarState transitions as director checkpoint triggers
4. **Morale integration**: Director story beats affect morale, which influences combat readiness
   (Combat Master Plan Section 5.3 morale effects)

No combat-era features are implemented in Phases 0-3 of this plan. They are planned as Phase 4+
expansions gated on Combat Master Plan progress.

### OnlabRefinery Documentation

This master plan directly implements the pattern described in `OnlabRefineryDocumentation.txt`:
- Section 2.2 (responsibility separation) → Section 1.2 of this plan
- Section 2.3 (layered model) → Section 10 of this plan
- Section 2.4 (iterative loop) → Section 10.6 of this plan
- Section 3.1 (generalizability) → the Effect Vocabulary is exactly this: a generalizable
  composition pattern applied to game simulation

---

## Appendix A: Wire Format Examples

### A.1 Director Checkpoint Request

```json
{
  "goal": "SEASON_DIRECTOR_CHECKPOINT",
  "snapshot": {
    "currentTick": 480,
    "currentSeason": "summer",
    "colonyPopulation": 47,
    "foodReservesPct": 62.5,
    "moraleAvg": 0.71,
    "economyOutput": 34.2,
    "activeBeats": [],
    "activeDirectives": [
      {
        "directiveName": "PrioritizeFood",
        "remainingTicks": 3,
        "target": "colony:primary"
      }
    ],
    "beatCooldownRemainingTicks": 0,
    "remainingInfluenceBudget": 5.0
  },
  "constraints": {
    "outputMode": "both",
    "maxBudget": 5.0
  }
}
```

### A.2 Director Checkpoint Response (Mock, Phase 0)

```json
{
  "status": "OK",
  "ops": [
    {
      "op": "addStoryBeat",
      "opId": "beat-mock-tick480-abc123",
      "severity": "minor",
      "beatName": "Summer Breeze",
      "narrative": "A warm breeze carries the scent of wildflowers through the settlement.",
      "effects": []
    },
    {
      "op": "setColonyDirective",
      "opId": "dir-mock-tick480-def456",
      "directiveName": "BoostIndustry",
      "biases": [
        { "type": "goal_bias", "goalCategory": "crafting", "weight": 0.15 },
        { "type": "goal_bias", "goalCategory": "building", "weight": 0.10 }
      ],
      "durationTicks": 25,
      "target": "colony:primary"
    }
  ],
  "explain": {
    "directorStage": "mock",
    "directorOutputMode": "both",
    "retryCount": 0,
    "budgetUsed": 1.875,
    "warnings": []
  }
}
```

### A.3 Director Checkpoint Response (LLM, Phase 3)

```json
{
  "status": "OK",
  "ops": [
    {
      "op": "addStoryBeat",
      "opId": "beat-llm-tick960-ghi789",
      "severity": "major",
      "beatName": "The Whispering Blight",
      "narrative": "Dark tendrils spread across the eastern fields overnight. Crops touched by the blight wither within hours. The elders speak of an ancient curse awakened by the summer storms.",
      "effects": [
        { "type": "domain_modifier", "domain": "food", "modifier": -0.12, "durationTicks": 20 },
        { "type": "domain_modifier", "domain": "morale", "modifier": -0.05, "durationTicks": 15 }
      ]
    },
    {
      "op": "setColonyDirective",
      "opId": "dir-llm-tick960-jkl012",
      "directiveName": "Emergency Harvest and Containment",
      "biases": [
        { "type": "goal_bias", "goalCategory": "farming", "weight": 0.20 },
        { "type": "goal_bias", "goalCategory": "gathering", "weight": 0.25 }
      ],
      "durationTicks": 25,
      "target": "colony:primary"
    }
  ],
  "explain": {
    "directorStage": "llm",
    "directorOutputMode": "both",
    "retryCount": 1,
    "budgetUsed": 3.975,
    "warnings": ["Initial LLM proposal had modifier -0.45, clamped by retry"]
  }
}
```

### A.4 Director Checkpoint Response (Fallback)

```json
{
  "status": "OK",
  "ops": [
    {
      "op": "addStoryBeat",
      "opId": "beat-fallback-tick1440-mno345",
      "severity": "minor",
      "beatName": "A Quiet Day",
      "narrative": "The settlement goes about its routine. Nothing remarkable happens, yet there is comfort in the ordinary.",
      "effects": []
    }
  ],
  "explain": {
    "directorStage": "fallback",
    "directorOutputMode": "both",
    "retryCount": 5,
    "budgetUsed": 0,
    "warnings": ["LLM retries exhausted, using deterministic fallback"]
  }
}
```

---

## Appendix B: Effect Vocabulary Reference

### B.1 Phase 0-1 Vocabulary

| Effect Type | Fields | Bounds | Budget Cost |
|-------------|--------|--------|-------------|
| `domain_modifier` | domain, modifier, durationTicks | modifier: [-0.3, +0.3], duration: [5, 50] | abs(modifier) * duration * 0.5 |
| `goal_bias` | goalCategory, weight, durationTicks | weight: [0.0, 0.5], duration: [5, 50] | weight * duration * 0.3 |

### B.2 Phase 3+ Vocabulary Additions

| Effect Type | Fields | Bounds | Budget Cost |
|-------------|--------|--------|-------------|
| `causal_chain` | condition, followUpBeat, windowTicks, maxTriggers | window: [10, 100], maxTriggers: 1 | 2.0 flat + follow-up cost |

### B.3 Phase 4+ Vocabulary Additions (Planned, Not Implemented)

| Effect Type | Fields | Bounds | Budget Cost |
|-------------|--------|--------|-------------|
| `entity_hint` | targetSelection, trait, durationTicks | duration: [10, 50] | 1.0 flat |
| `spatial_hint` | regionType, condition, durationTicks, modifier | modifier: [-0.2, +0.2], duration: [10, 50] | 1.5 flat + modifier cost |
| `faction_hint` | factionA, factionB, relationDelta, durationTicks | delta: [-0.3, +0.3], duration: [10, 50] | abs(delta) * duration * 0.4 |

### B.4 Valid Domains

```
food        — food production, reserves, farming yield
morale      — colony happiness, social cohesion, unrest
economy     — crafting output, trade value, resource processing
military    — combat readiness, training speed, defense strength
research    — tech progress, discovery rate, knowledge gain
```

### B.5 Valid Goal Categories

```
farming     — crop planting, field tending, harvest
gathering   — foraging, berry picking, wild resource collection
crafting    — item creation, tool making, processing
building    — construction, repair, infrastructure
social      — socializing, entertaining, community events
military    — combat training, patrol, guard duty
research    — studying, experimenting, tech development
rest        — sleeping, relaxation, recovery
```

---

## Appendix C: Formal Model Constraint Reference

### C.1 Invariant Table

| ID | Constraint | Error Message Template | Phase |
|----|-----------|----------------------|-------|
| INV-01 | severity matches effect count | "Beat severity '{sev}' requires {expected} effects, got {actual}" | 0 |
| INV-02 | domains in VALID_DOMAINS | "Unknown domain '{dom}', valid: {list}" | 0 |
| INV-03 | modifier in [MIN, MAX] | "Modifier {val} out of bounds [{min}, {max}] for domain '{dom}'" | 0 |
| INV-04 | duration in [MIN, MAX] | "Duration {val} out of bounds [{min}, {max}]" | 0 |
| INV-05 | effects count <= MAX_EFFECTS_PER_BEAT | "Beat has {n} effects, max {max}" | 0 |
| INV-06 | major beat cooldown | "Major beat cooldown: {remaining} ticks remaining" | 0 |
| INV-07 | epic beat cooldown | "Epic beat cooldown: {remaining} ticks remaining" | 0 |
| INV-08 | max 1 active major beat | "Cannot add major beat: {existing} already active" | 0 |
| INV-09 | max 1 active epic beat | "Cannot add epic beat: {existing} already active" | 0 |
| INV-10 | domain stack <= MAX | "Domain '{dom}' stack {total} exceeds cap {max}" | 0 |
| INV-11 | goal categories in VALID_GOAL_CATEGORIES | "Unknown goal category '{cat}', valid: {list}" | 0 |
| INV-12 | bias weight in [MIN, MAX] | "Bias weight {val} out of bounds [{min}, {max}]" | 0 |
| INV-13 | biases count <= MAX_BIASES_PER_DIRECTIVE | "Directive has {n} biases, max {max}" | 0 |
| INV-14 | max 1 directive per colony | "Colony '{col}' already has active directive '{existing}'" | 0 |
| INV-15 | total budget within limit | "Budget cost {cost} exceeds limit {limit}" | 3 |
| INV-16 | causal chain: no loops | "Causal chain references parent beat, creating loop" | 3 |
| INV-17 | causal chain: combined budget | "Chain total cost {cost} exceeds limit {limit}" | 3 |
| INV-18 | causal condition metric exists | "Unknown condition metric '{metric}'" | 3 |
| INV-19 | causal chain window in bounds | "Chain window {val} out of bounds [{min}, {max}]" | 3 |
| INV-20 | no contradictory same-domain modifiers | "Contradictory modifiers on '{dom}': +{a} and -{b} in same checkpoint" | 2 |

### C.2 Constraint Application Matrix

| Constraint | addStoryBeat | setColonyDirective | causalChain |
|-----------|:---:|:---:|:---:|
| INV-01 | x | | |
| INV-02 | x | | |
| INV-03 | x | | x (follow-up) |
| INV-04 | x | x | x |
| INV-05 | x | | |
| INV-06 | x | | |
| INV-07 | x | | |
| INV-08 | x | | |
| INV-09 | x | | |
| INV-10 | x | | x |
| INV-11 | | x | |
| INV-12 | | x | |
| INV-13 | | x | |
| INV-14 | | x | |
| INV-15 | x | x | x |
| INV-16 | | | x |
| INV-17 | | | x |
| INV-18 | | | x |
| INV-19 | | | x |
| INV-20 | x | | x |

### C.3 Valid Condition Metrics (Phase 3+)

```
food_reserves_pct   — colony food reserves as percentage (0-100)
morale_avg          — colony average morale (0.0-1.0)
population          — colony population count
economy_output      — colony economy output value
military_strength   — colony military strength (future)
```

---

*End of Director Integration Master Plan*
*Document version: 1.0*
*Date: 2026-02-28*
*Prepared by: Meta Coordinator session*
