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
    float TimeSeconds,
    float TickAlpha,
    float FxIntensity
);
