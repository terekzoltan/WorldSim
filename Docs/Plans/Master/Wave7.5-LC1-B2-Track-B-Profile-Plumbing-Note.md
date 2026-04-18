# Wave 7.5 LC1-B2 Track B Profile Plumbing Note

Date: 2026-04-18
Owner: Track B
Scope: minimal runtime/profile plumbing for canonical visual lanes.

## Purpose

- Introduce canonical visual lane naming and default-resolution policy for low-cost baseline execution.
- Keep scope plumbing-only; do not pull Track A render-feature/UI-polish work into this slice.

## Canonical lanes

- `Showcase`
- `DevLite`
- `Headless`

## Resolution policy (locked)

- App default lane: `DevLite`
- App interactive cycle: `DevLite <-> Showcase`
- App does not expose a fake/no-render `Headless` mode in this slice.
- ScenarioRunner default lane: `Headless`
- ScenarioRunner optional override via env is supported.

## Shared seam

- A minimal shared resolver surface provides requested/effective/source data.
- The seam is naming/default-resolution only.
- Render-cost behavior ownership remains App/Graphics side.

## Artifact metadata (runner)

- Effective visual lane metadata is exported on:
  - run-level entries
  - summary runs
  - manifest

## Explicit out-of-scope for LC1-B2

- Render-pass logic changes
- Atmosphere/culling/terrain-feature implementation
- TileSize/render-scale policy package
- Settings/HUD polish pass
- Smoke checklist refresh
