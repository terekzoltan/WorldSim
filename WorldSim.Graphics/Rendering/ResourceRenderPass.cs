using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
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
        var visibleTiles = context.VisibleTileBounds;

        foreach (var tile in snapshot.Tiles)
        {
            if (!visibleTiles.Contains(tile.X, tile.Y))
                continue;

            if (tile.NodeAmount <= 0)
                continue;

            var iconRect = RenderLayout.BottomAnchoredInTile(tile.X, tile.Y, settings.TileSize, settings.ResourceScale);
            RenderLayout.DrawGroundShadow(spriteBatch, textures.Pixel, iconRect, settings.StructureShadowAlpha * 0.7f);

            if (tile.NodeType == ResourceView.Wood)
                spriteBatch.Draw(textures.Tree, iconRect, Color.White);
            else if (tile.NodeType == ResourceView.Stone)
                spriteBatch.Draw(textures.Rock, iconRect, Color.White);
            else if (tile.NodeType == ResourceView.Iron)
                spriteBatch.Draw(textures.Iron, iconRect, Color.White);
            else if (tile.NodeType == ResourceView.Gold)
                spriteBatch.Draw(textures.Gold, iconRect, Color.White);
            else if (tile.NodeType == ResourceView.Food)
                DrawFoodNode(spriteBatch, textures, theme, iconRect);
        }
    }

    private static void DrawFoodNode(
        SpriteBatch spriteBatch,
        TextureCatalog textures,
        WorldRenderTheme theme,
        Rectangle iconRect)
    {
        if (textures.Food != null)
        {
            spriteBatch.Draw(textures.Food, iconRect, Color.White);
            return;
        }

        var clump = Shrink(iconRect, 0.64f);
        var stem = Shrink(iconRect, 0.42f);
        spriteBatch.Draw(textures.Pixel, clump, theme.FoodNode);
        spriteBatch.Draw(textures.Pixel, stem, Color.Lerp(theme.FoodNode, theme.Success, 0.35f));
        spriteBatch.Draw(textures.Pixel, new Rectangle(clump.X, clump.Y, Math.Max(1, clump.Width / 3), Math.Max(1, clump.Height / 3)), Color.White * 0.5f);
    }

    private static Rectangle Shrink(Rectangle rect, float factor)
    {
        int width = Math.Max(1, (int)MathF.Round(rect.Width * factor));
        int height = Math.Max(1, (int)MathF.Round(rect.Height * factor));
        int x = rect.Center.X - (width / 2);
        int y = rect.Center.Y - (height / 2);
        return new Rectangle(x, y, width, height);
    }
}
