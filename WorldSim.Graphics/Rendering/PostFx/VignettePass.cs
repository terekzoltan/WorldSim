using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering.PostFx;

public sealed class VignettePass : IPostProcessPass
{
    public string Name => "Vignette";

    public void Apply(in PostProcessFrameContext context, Texture2D source, RenderTarget2D destination)
    {
        context.GraphicsDevice.SetRenderTarget(destination);
        context.GraphicsDevice.Clear(Color.Black);

        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        context.SpriteBatch.Draw(source, destination.Bounds, Color.White);

        float opacity = context.Quality switch
        {
            PostFxQuality.Low => 0.07f,
            PostFxQuality.Medium => 0.12f,
            _ => 0.18f
        } * Math.Clamp(context.FxIntensity, 0f, 2f);

        int w = destination.Width;
        int h = destination.Height;
        int edge = Math.Max(12, Math.Min(w, h) / 12);
        var shade = Color.Black * opacity;

        context.SpriteBatch.Draw(context.Textures.Pixel, new Rectangle(0, 0, w, edge), shade);
        context.SpriteBatch.Draw(context.Textures.Pixel, new Rectangle(0, h - edge, w, edge), shade);
        context.SpriteBatch.Draw(context.Textures.Pixel, new Rectangle(0, 0, edge, h), shade);
        context.SpriteBatch.Draw(context.Textures.Pixel, new Rectangle(w - edge, 0, edge, h), shade);

        context.SpriteBatch.End();
    }
}
