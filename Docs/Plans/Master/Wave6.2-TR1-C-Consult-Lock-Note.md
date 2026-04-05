# Wave 6.2 TR1-C Consult Lock Note (Track B + Track D)

Date: 2026-04-05

Purpose:
- Lock the TR1-C runtime-fact boundary decisions (D1-D5) so Wave 8.5 / TR2-A can start from an explicit, non-implicit baseline.

Scope:
- Consult lock only.
- No runtime/adapter behavior change.

## Locked decisions (D1-D5)

- D1 (canonical colony-count source): `snapshot.world.colonyCount` is canonical during TR1/TR2 transition.
- D2 (budget source priority): `constraints.maxBudget` -> `snapshot.director.remainingInfluenceBudget` -> configured default.
- D3 (cooldown source): `snapshot.director.beatCooldownRemainingTicks` is canonical; `snapshot.world.storyBeatCooldownTicks` remains transitional fallback only.
- D4 (constraint shape policy): root `constraints.*` is canonical; nested `constraints.director.*` remains compatibility fallback only.
- D5 (TR2-A minimum formal runtime-fact set): `tick`, `colonyCount`, `beatCooldownRemainingTicks`, `remainingInfluenceBudget`, `activeBeats`, `activeDirectives`.

## Ownership split (locked)

- C# (Track B): deterministic export of runtime facts in checkpoint snapshot + deterministic command execution/read-model visibility.
- Java (Track D): normalization and mapping authority from snapshot envelope to `DirectorRuntimeFacts`, then formal-model-side processing.
- Bridge policy: keep current C# wire boundary stable; do not promote prompt-context-only fields to formal runtime facts in TR1-C.

## Transitional-only fallback list (explicit)

- `snapshot.director.colonyPopulation` is not a canonical colony-count fact.
- `snapshot.world.storyBeatCooldownTicks` is fallback only.
- `constraints.director.maxBudget` is fallback only while root `constraints.maxBudget` remains canonical.

## TR2-A handoff assumptions

- TR2-A runtime snapshot -> runtime assertions mapping starts from the D5 minimum fact set above.
- Additional context fields may remain for prompt/debug/observability, but they are not part of the locked formal runtime-fact minimum.
