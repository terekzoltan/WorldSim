namespace WorldSim.Graphics.Rendering;

public sealed record WorldRenderSettings(
    int TileSize = 7,
    float IconScale = 3f,
    int HouseIconTiles = 3
);
