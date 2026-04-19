using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public readonly record struct RenderFrameContext(
    SpriteBatch SpriteBatch,
    WorldRenderSnapshot Snapshot,
    TextureCatalog Textures,
    WorldRenderSettings Settings,
    WorldRenderTheme Theme,
    RenderStats Stats,
    TileBounds VisibleTileBounds,
    LowCostVisualPolicy VisualPolicy
);

public readonly record struct LowCostVisualPolicy(
    bool HazeEnabled,
    float HazeIntensityMultiplier,
    float TerrainAmbientMultiplier
);

public readonly record struct TileBounds(int MinX, int MinY, int MaxX, int MaxY)
{
    public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}
