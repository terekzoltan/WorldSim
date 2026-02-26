using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering.PostFx;

public sealed class BloomLitePass : IPostProcessPass
{
    public string Name => "BloomLite";

    public void Apply(in PostProcessFrameContext context, Texture2D source, RenderTarget2D destination)
    {
        context.GraphicsDevice.SetRenderTarget(destination);
        context.GraphicsDevice.Clear(Color.Black);

        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        context.SpriteBatch.Draw(source, destination.Bounds, Color.White);
        context.SpriteBatch.End();

        if (context.Quality == PostFxQuality.Low)
            return;

        float intensity = context.Quality == PostFxQuality.Medium ? 0.07f : 0.1f;
        intensity *= Math.Clamp(context.FxIntensity, 0f, 2f);

        context.SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive);

        var bounds = destination.Bounds;
        var bloomColor = Color.White * intensity;
        context.SpriteBatch.Draw(source, new Rectangle(bounds.X - 1, bounds.Y, bounds.Width, bounds.Height), bloomColor);
        context.SpriteBatch.Draw(source, new Rectangle(bounds.X + 1, bounds.Y, bounds.Width, bounds.Height), bloomColor);
        context.SpriteBatch.Draw(source, new Rectangle(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height), bloomColor);
        context.SpriteBatch.Draw(source, new Rectangle(bounds.X, bounds.Y + 1, bounds.Width, bounds.Height), bloomColor);

        if (context.Quality == PostFxQuality.High)
        {
            var softColor = Color.White * (intensity * 0.55f);
            context.SpriteBatch.Draw(source, new Rectangle(bounds.X - 2, bounds.Y - 2, bounds.Width, bounds.Height), softColor);
            context.SpriteBatch.Draw(source, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width, bounds.Height), softColor);
        }

        context.SpriteBatch.End();
    }
}
