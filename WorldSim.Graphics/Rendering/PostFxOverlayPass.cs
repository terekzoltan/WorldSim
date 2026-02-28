using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Rendering;

public sealed class PostFxOverlayPass : IRenderPass
{
    public string Name => "PostFxOverlay";

    public void Draw(in RenderFrameContext context)
    {
        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var pixel = context.Textures.Pixel;
        var fx = context.FxIntensity;

        int width = snapshot.Width * settings.TileSize;
        int height = snapshot.Height * settings.TileSize;
        if (width <= 0 || height <= 0)
            return;

        var tint = context.Theme.Highlight * (0.02f * fx);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), tint);

        int edge = Math.Max(8, settings.TileSize * 3);
        var shade = Color.Black * (0.08f * fx);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, width, edge), shade);
        spriteBatch.Draw(pixel, new Rectangle(0, height - edge, width, edge), shade);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, edge, height), shade);
        spriteBatch.Draw(pixel, new Rectangle(width - edge, 0, edge, height), shade);
    }
}
