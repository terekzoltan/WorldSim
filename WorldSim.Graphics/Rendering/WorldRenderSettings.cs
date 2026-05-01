namespace WorldSim.Graphics.Rendering;

public sealed record WorldRenderSettings(
    int TileSize = 7,
    float PersonScale = 2.25f,
    float AnimalScale = 1.43f,
    float ResourceScale = 2.6f,
    float HouseScale = 3.6f,
    float SpecializedBuildingScale = 0.72f,
    float DefensiveStructureScale = 1f,
    float ActorShadowAlpha = 0.24f,
    float StructureShadowAlpha = 0.2f
);
