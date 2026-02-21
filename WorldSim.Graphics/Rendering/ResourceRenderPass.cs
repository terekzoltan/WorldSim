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

        foreach (var tile in snapshot.Tiles)
        {
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
