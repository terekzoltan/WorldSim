# Track C Prep Cross-Track Handshake

Status: active prep
Owner: Track C
Date: 2026-02-26

## Purpose

Lock the minimum contracts that Track C needs before Combat/Fortification/Campaign phase work scales up.

## Track B handshake (Runtime -> AI context contract)

- Confirm the P0/P1 AI context field list and ownership:
  - `Health`, `Strength`, `Defense`
  - `NearbyPredators`, `NearbyHostilePeople`
  - `WarState`, `TileContestedNearby`
  - `IsWarrior`, `ColonyWarriorCount`
- Confirm update cadence:
  - threat/combat fields per tick
  - heavier strategic fields can be periodic
- Confirm determinism policy:
  - seeded world random path
  - stable iteration order in aggregations
  - clamped numeric transforms for AI inputs

## Track A handshake (HUD vs debug panel budget)

- Keep AI HUD line compact (planner/policy/tracking hints only).
- Keep detailed AI data in debug panel:
  - selected goal, command, plan length/cost
  - replan reason
  - method winner + runner-up scores
  - goal score list and decision history paging
- Preserve input consistency:
  - `F8` panel toggle
  - `PgUp/PgDn` tracked NPC cycle
  - `Home` reset to latest

## Track D handshake (soft directive taxonomy)

- Minimum directive taxonomy for Track C soft bias hooks:
  - `Fortify`
  - `Mobilize`
  - `ConserveFood`
  - `RaidPressure`
  - `Deescalate`
- Directive policy:
  - soft weighting only (no hard action override)
  - intensity range `0..1`
  - expiry mandatory

## Communication protocol in this repo

- Cross-track communication is done through `AGENTS.md` entries.
- Any track can ask its agent to read `AGENTS.md` and react to new notes.
- If a note needs direct owner follow-up, keep it short and include:
  - impact
  - next step
  - owning track
