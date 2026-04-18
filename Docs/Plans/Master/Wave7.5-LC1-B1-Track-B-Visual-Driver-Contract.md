# Wave 7.5 LC1-B1 Track B Visual-Driver Contract

Date: 2026-04-18
Owner: Track B
Scope: minimal additive runtime snapshot contract lock for low-cost state-driven rendering.

## Purpose

- Lock a minimal tile-level visual-driver contract before profile plumbing (`LC1-B2`) and Track A render follow-ups.
- Keep Graphics snapshot-driven; no gameplay logic migration into renderer in this slice.

## Authority and boundaries

- Sequencing authority: `Docs/Plans/Master/Combined-Execution-Sequencing-Plan.md` (Wave 7.5).
- Low-cost policy reference: `Docs/Plans/Master/world_sim_low_cost_2_d_docs.md`.
- This slice does not modify profile plumbing or Graphics passes.

## Locked additive fields (TileRenderData)

- `OwnershipStrength` (`float`, `0..1`)
- `FoodRegrowthProgress` (`float`, `0..1`)

No new top-level snapshot block is introduced.

## OwnershipStrength policy

Runtime-owned normalization. Builder consumes world accessor only.

- Water tile -> `0`
- Ownerless tile (`ownerColonyId < 0`) -> `0`
- Owned tile with no meaningful runner-up (`secondId < 0`) -> `1.0` (explicit fallback lock)
- Owned tile with runner-up:
  - `margin = max(0, bestScore - secondScore)`
  - `OwnershipStrength = clamp(margin / TerritoryContestedThreshold, 0, 1)`

Important:
- Existing contested threshold semantics stay unchanged.
- This field is read-model confidence/intensity only; no gameplay branching depends on it.

## FoodRegrowthProgress policy

Runtime-owned progress exported from regrowth lifecycle.

- No active regrowth slot on tile -> `0`
- Active regrowth slot -> `clamp(timer / target, 0, 1)`
- On completion (node restored), regrowth slot removed and exported value returns to `0`

## Builder rule

- `WorldSnapshotBuilder` must not recompute territory or regrowth state.
- Builder only consumes accessors (`GetTileOwnershipStrength`, `GetFoodRegrowthProgress`).

## Test lock (LC1-B1)

- Range/default snapshot test (`0..1`, water/ownerless defaults, secondId fallback coverage)
- Determinism test (same seed + tick sequence)
- Controlled regrowth progression test (explicit `TryHarvest(... Food ...)` depletion path)
- Controlled ownership ordering test (stable-owned strength > contested strength)

## Note-only boundary smells (out of scope in LC1-B1)

- `WorldSim.Graphics/Rendering/StructureRenderPass.cs` tower beam target inference from event strings.
- `WorldSim.Graphics/Rendering/CombatOverlayPass.cs` colony-center / contested-nearby inference in Graphics.
