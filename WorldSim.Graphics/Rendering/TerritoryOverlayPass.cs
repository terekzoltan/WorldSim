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

        int tileSize = settings.TileSize;
        int contourThickness = Math.Max(1, tileSize / 4);
        foreach (var tile in snapshot.Tiles)
        {
            int x = tile.X * tileSize;
            int y = tile.Y * tileSize;
            var tileRect = new Rectangle(x, y, tileSize, tileSize);

            if (tile.OwnerFactionId >= 0)
                spriteBatch.Draw(pixel, tileRect, GetFactionColor(tile.OwnerFactionId));

            if (!tile.IsContested)
                continue;

            var contested = new Color(248, 214, 126) * 0.55f;
            spriteBatch.Draw(pixel, new Rectangle(x, y, tileSize, contourThickness), contested);
            spriteBatch.Draw(pixel, new Rectangle(x, y + tileSize - contourThickness, tileSize, contourThickness), contested);
            spriteBatch.Draw(pixel, new Rectangle(x, y, contourThickness, tileSize), contested);
            spriteBatch.Draw(pixel, new Rectangle(x + tileSize - contourThickness, y, contourThickness, tileSize), contested);
        }
    }

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            0 => new Color(94, 149, 214) * 0.2f,
            1 => new Color(216, 131, 93) * 0.2f,
            2 => new Color(120, 194, 128) * 0.2f,
            3 => new Color(181, 140, 232) * 0.2f,
            _ => new Color(160, 160, 160) * 0.16f
        };
    }
}
