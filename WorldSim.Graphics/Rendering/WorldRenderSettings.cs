namespace WorldSim.Graphics.Rendering;

public sealed record WorldRenderSettings(
    int TileSize = 7,
    float PersonScale = 1.575f,
    float AnimalScale = 1.43f,
    float ResourceScale = 2.6f,
    float HouseScale = 4.32f,
    float SpecializedBuildingScale = 2.88f,
    float DefensiveStructureScale = 4f,
    float ActorShadowAlpha = 0.24f,
    float StructureShadowAlpha = 0.2f
);
