## Summary
- 

## Boundary Check
- [ ] Runtime does not reference App/Graphics/RefineryClient directly
- [ ] Graphics consumes only Runtime read-model/snapshot types
- [ ] App remains thin host (no direct mutable domain state manipulation)
- [ ] If AI-related changes were made, they are behind interfaces for Track C migration

## Validation
- [ ] `dotnet build WorldSim.sln`
- [ ] Relevant tests passed (include `WorldSim.ArchTests`)
