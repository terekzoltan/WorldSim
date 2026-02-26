using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class ResourceRenderPass : IRenderPass
{
    public string Name => "Resources";

    public void Draw(in RenderFrameContext context)
    {
        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var textures = context.Textures;
        var settings = context.Settings;
        var theme = context.Theme;

        var (minX, maxX, minY, maxY) = ComputeVisibleTileBounds(context, snapshot.Width, snapshot.Height, settings.TileSize);

        for (int y = minY; y <= maxY; y++)
        {
            int row = y * snapshot.Width;
            for (int x = minX; x <= maxX; x++)
            {
                var tile = snapshot.Tiles[row + x];
                if (tile.NodeAmount <= 0)
                    continue;

                var bx = tile.X * settings.TileSize;
                var by = tile.Y * settings.TileSize;
                var iconSize = (int)MathF.Round(settings.TileSize * settings.IconScale);
                var iconX = bx + (settings.TileSize - iconSize) / 2;
                var iconY = by + (settings.TileSize - iconSize) / 2;
                var iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

                if (tile.NodeType == ResourceView.Wood)
                    spriteBatch.Draw(textures.Tree, iconRect, Color.White);
                else if (tile.NodeType == ResourceView.Stone)
                    spriteBatch.Draw(textures.Rock, iconRect, Color.White);
                else if (tile.NodeType == ResourceView.Iron)
                    spriteBatch.Draw(textures.Iron, iconRect, Color.White);
                else if (tile.NodeType == ResourceView.Gold)
                    spriteBatch.Draw(textures.Gold, iconRect, Color.White);
                else if (tile.NodeType == ResourceView.Food)
                    spriteBatch.Draw(textures.Pixel, iconRect, theme.FoodNode);
            }
        }
    }

    private static (int minX, int maxX, int minY, int maxY) ComputeVisibleTileBounds(
        in RenderFrameContext context,
        int worldWidth,
        int worldHeight,
        int tileSize)
    {
        float viewWorldW = context.ViewportWidth / context.CameraZoom;
        float viewWorldH = context.ViewportHeight / context.CameraZoom;

        int minX = Math.Max(0, (int)MathF.Floor(context.CameraX / tileSize) - 2);
        int minY = Math.Max(0, (int)MathF.Floor(context.CameraY / tileSize) - 2);
        int maxX = Math.Min(worldWidth - 1, (int)MathF.Ceiling((context.CameraX + viewWorldW) / tileSize) + 2);
        int maxY = Math.Min(worldHeight - 1, (int)MathF.Ceiling((context.CameraY + viewWorldH) / tileSize) + 2);

        return (minX, maxX, minY, maxY);
    }
}
