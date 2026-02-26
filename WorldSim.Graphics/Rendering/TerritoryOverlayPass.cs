using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Rendering;

public sealed class TerritoryOverlayPass : IRenderPass
{
    public string Name => "TerritoryOverlay";
    public bool Enabled { get; set; }

    public void Draw(in RenderFrameContext context)
    {
        if (!Enabled)
            return;

        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var spriteBatch = context.SpriteBatch;
        var pixel = context.Textures.Pixel;

        var colorA = new Color(93, 145, 212) * 0.18f;
        var colorB = new Color(212, 155, 93) * 0.18f;

        int stripeStep = Math.Max(16, settings.TileSize * 10);
        int maxX = snapshot.Width * settings.TileSize;
        int maxY = snapshot.Height * settings.TileSize;

        for (int x = 0; x < maxX; x += stripeStep)
        {
            var color = ((x / stripeStep) % 2 == 0) ? colorA : colorB;
            spriteBatch.Draw(pixel, new Rectangle(x, 0, 2, maxY), color);
        }
    }
}
