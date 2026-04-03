# Refinery Artifact Layout (TR1-B)

This folder is the canonical classpath root for WorldSim `tools.refinery` artifacts.

## Family split

- `common/` shared, family-agnostic artifact policy/vocabulary space
- `director/` Season Director family artifacts
- `combat/` reserved namespace (future family)
- `campaign/` reserved namespace (future family)

## Scope rule

TR1-B defines layout and ownership boundaries only.

- It does not implement the full director family artifact set yet.
- Production Java director planning flow remains unchanged in TR1-B.

## TR1-A historical proof

- Historical spike artifact: `director/tr1a-spike.problem`
- This proves local artifact load + solve/generation path and remains intentionally separate from canonical family targets.

## Canonical TR1-C target paths (reserved now)

- `director/design.problem`
- `director/model.problem`
- `director/runtime.problem`
- `director/output.problem`

## TR1-C layer boundaries

- `director/design.problem`: director vocabulary and structural skeleton (read-only layer)
- `director/model.problem`: constraint/legal-combination layer (formal rule ownership)
- `director/runtime.problem`: normalized checkpoint-context fact shape
- `director/output.problem`: designated output area boundary only

Designated output area note:
- Only the output layer is intended as editable proposal space.
- Design/model/runtime layers are treated as read-only context.

## TR1-D ingest boundary

- Canonical candidate ingest targets `designatedOutput.storyBeatSlot` and `designatedOutput.directiveSlot`.
- Slot semantics are presence-driven: object present means proposed slot, missing/null means no slot.
- Runtime-fact authority for ingest-side normalization is `DirectorSnapshotMapper` -> `DirectorRuntimeFacts`.

## TR1-E bridge boundary policy

- Internal director proposal/assertion handling is separate from wire-level bridge DTOs.
- `PatchOp` / `PatchResponse` are bridge-output forms, not primary internal formal ontology.
- Transitional note: validator/fallback paths that still operate directly on `PatchOp` remain transitional until later TR2/TR3 convergence.
