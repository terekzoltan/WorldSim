using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering.PostFx;

public interface IPostProcessPass
{
    string Name { get; }

    void Apply(in PostProcessFrameContext context, Texture2D source, RenderTarget2D destination);
}
