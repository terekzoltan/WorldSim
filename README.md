# WorldSim

MonoGame-based colony simulation project under modular migration (Tracks A-D).

## Active project structure

- `WorldSim.App/` - MonoGame host and input wiring
- `WorldSim.Graphics/` - rendering and HUD from read-only snapshot
- `WorldSim.Runtime/` - world simulation, ecology/economy updates, tech effects
- `WorldSim.AI/` - AI abstractions and future planner module extraction target
- `WorldSim.Contracts/` - shared C# contract ownership target
- `WorldSim.RefineryAdapter/` - runtime adapter boundary for refinery integration
- `WorldSim.RefineryClient/` - HTTP client + patch parser/applier

## Legacy note

The old monolithic `WorldSim/` project tree is being retired as part of the split.
Use the modular projects above for active development.

## Build and test

```bash
dotnet build WorldSim.sln
dotnet test WorldSim.ArchTests/WorldSim.ArchTests.csproj
```

## Boundary guardrails

- Runtime must not reference App/Graphics/RefineryClient directly.
- Graphics should consume only Runtime read-model/snapshot types.
- App should remain a thin host over Runtime APIs.
