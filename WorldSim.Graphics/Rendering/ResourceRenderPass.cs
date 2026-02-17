using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;

namespace WorldSim.Graphics.Rendering;

public sealed class ResourceRenderPass
{
    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings, WorldRenderTheme theme)
    {
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

            if (tile.NodeType == Resource.Wood)
                spriteBatch.Draw(textures.Tree, iconRect, Color.White);
            else if (tile.NodeType == Resource.Stone)
                spriteBatch.Draw(textures.Rock, iconRect, Color.White);
            else if (tile.NodeType == Resource.Iron)
                spriteBatch.Draw(textures.Iron, iconRect, Color.White);
            else if (tile.NodeType == Resource.Gold)
                spriteBatch.Draw(textures.Gold, iconRect, Color.White);
            else if (tile.NodeType == Resource.Food)
                spriteBatch.Draw(textures.Pixel, iconRect, theme.FoodNode);
        }
    }
}
