using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;

namespace WorldSim.Graphics.Rendering.PostFx;

public readonly record struct PostProcessFrameContext(
    GraphicsDevice GraphicsDevice,
    SpriteBatch SpriteBatch,
    TextureCatalog Textures,
    WorldRenderTheme Theme,
    PostFxQuality Quality,
    float TimeSeconds,
    float FxIntensity
);
