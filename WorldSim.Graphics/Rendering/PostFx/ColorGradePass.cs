using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering.PostFx;

public sealed class ColorGradePass : IPostProcessPass
{
    public string Name => "ColorGrade";

    public void Apply(in PostProcessFrameContext context, Texture2D source, RenderTarget2D destination)
    {
        context.GraphicsDevice.SetRenderTarget(destination);
        context.GraphicsDevice.Clear(Color.Black);

        float intensity = context.Quality switch
        {
            PostFxQuality.Low => 0.08f,
            PostFxQuality.Medium => 0.14f,
            _ => 0.2f
        } * Math.Clamp(context.FxIntensity, 0f, 2f);

        var tint = Color.Lerp(Color.White, context.Theme.Highlight, intensity * 0.35f);
        tint = Color.Lerp(tint, context.Theme.Dirt, intensity * 0.1f);

        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        context.SpriteBatch.Draw(source, destination.Bounds, tint);
        context.SpriteBatch.End();
    }
}
