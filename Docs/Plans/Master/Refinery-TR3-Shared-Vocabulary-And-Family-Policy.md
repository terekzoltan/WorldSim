# Refinery TR3 Shared Vocabulary And Family Policy

Status: TR3-C implementation artifact
Owner: Track D
Scope: Wave 10.5 TR3-C

## Purpose

TR3-C narrows shared vocabulary and family expansion prep to safe, reviewable boundaries while the project continues migrating from imperative Java validation toward real `tools.refinery` artifacts.

This slice does not change runtime solver routing, retire validator guards, modify ScenarioRunner evidence artifacts, or add runtime behavior.

## Shared Vocabulary Policy

`RefineryVocabulary` is a symbolic bridge/refinery vocabulary surface only.

It may contain stable symbols such as:

- output modes: `both`, `story_only`, `nudge_only`, `off`
- severities: `minor`, `major`, `epic`
- effect and bias types: `domain_modifier`, `goal_bias`
- effect domains and goal categories
- treaty kinds: `ceasefire`, `peace_talks`

It must not own:

- numeric bounds,
- duration or budget policy,
- runtime behavior,
- paid/live guardrails,
- fallback marker vocabulary,
- or operator-local requested-mode values.

`auto` remains adapter/operator-local requested-mode vocabulary. It is not part of shared Java/C# wire or effective output-mode vocabulary.

Faction identity remains numeric stable ID policy in this slice. TR3-C does not introduce new runtime-facing faction enum behavior.

Directive names remain director-local vocabulary unless a future step proves a broader shared surface is needed.

## Family Skeleton Policy

The new `common`, `combat`, and `campaign` `.problem` files are skeleton artifacts only.

They are classpath-resolvable parse/load anchors for future family work. They do not model combat or campaign rules, do not claim formal validation, and are not routed into the runtime solver path.

Do not add marker-only shared predicates. A `shared-predicates.problem` file should exist only when it contains a real predicate/error-predicate with replacement evidence.

## Evidence Schema Policy

Existing director markers remain stable compatibility truth:

- `directorSolver*`
- `directorStage:fallback-deterministic`
- `directorFallback`

Family-neutral evidence concepts are documented as future policy only in TR3-C. ScenarioRunner implementation and artifact-shape changes remain Track B-owned and require explicit Meta + Track B consult.

## Out Of Scope

- `WorldSim.Runtime`, `WorldSim.AI`, `WorldSim.App`, `WorldSim.Graphics` changes
- `WorldSim.ScenarioRunner` changes without explicit consult
- Java validator retirement
- `.problem` semantic migration beyond skeleton prep
- paid runs or paid evidence
- new fallback marker vocabulary
- runtime solver routing for combat or campaign families
