# Contract Ownership Notes

Current state (transitional):

- `WorldSim.Contracts` is the target home for shared cross-language contract definitions.
- `WorldSim.RefineryClient/Contracts` still contains active DTOs and parser-oriented contract classes used by the current adapter/client flow.

Migration plan:

1. Keep wire-compatible `v1` payload behavior unchanged.
2. Move canonical DTO definitions to `WorldSim.Contracts` in small PRs.
3. Update `WorldSim.RefineryClient` to consume `WorldSim.Contracts` types.
4. Leave only client-specific transport/parsing helpers in `WorldSim.RefineryClient`.

This file exists to make temporary ownership explicit until Track D fully finalizes the boundary.
