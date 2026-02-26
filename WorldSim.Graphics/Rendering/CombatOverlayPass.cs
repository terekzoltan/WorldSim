using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Rendering;

public sealed class CombatOverlayPass : IRenderPass
{
    public string Name => "CombatOverlay";
    public bool Enabled { get; set; }

    public void Draw(in RenderFrameContext context)
    {
        if (!Enabled)
            return;

        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var spriteBatch = context.SpriteBatch;
        var pixel = context.Textures.Pixel;

        var warning = new Color(224, 92, 86) * 0.22f;
        foreach (var colony in snapshot.Colonies)
        {
            int hash = colony.Id * 9973;
            int x = Math.Abs(hash % Math.Max(1, snapshot.Width)) * settings.TileSize;
            int y = Math.Abs((hash / 17) % Math.Max(1, snapshot.Height)) * settings.TileSize;
            int size = Math.Max(settings.TileSize * 5, 24);
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, 2), warning);
            spriteBatch.Draw(pixel, new Rectangle(x, y + size, size, 2), warning);
            spriteBatch.Draw(pixel, new Rectangle(x, y, 2, size), warning);
            spriteBatch.Draw(pixel, new Rectangle(x + size, y, 2, size), warning);
        }
    }
}
