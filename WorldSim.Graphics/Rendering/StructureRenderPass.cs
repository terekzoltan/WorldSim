using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class StructureRenderPass : IRenderPass
{
    public string Name => "Structures";

    public void Draw(in RenderFrameContext context)
    {
        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var textures = context.Textures;
        var settings = context.Settings;

        foreach (var house in snapshot.Houses)
        {
            var hx = house.X * settings.TileSize;
            var hy = house.Y * settings.TileSize;

            var baseX = hx + (settings.TileSize - 3) / 2;
            var baseY = hy + (settings.TileSize - 3) / 2;
            spriteBatch.Draw(textures.Pixel, new Rectangle(baseX, baseY, 3, 3), Color.White);

            var icon = textures.GetHouseIcon(house.ColonyId);
            if (icon == null)
                continue;

            var iconSize = settings.TileSize * settings.HouseIconTiles * 2;
            var iconX = hx + (settings.TileSize - iconSize) / 2;
            var iconY = hy + (settings.TileSize - iconSize) / 2;
            spriteBatch.Draw(icon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
        }

        foreach (var building in snapshot.SpecializedBuildings)
        {
            var bx = building.X * settings.TileSize;
            var by = building.Y * settings.TileSize;
            var size = Math.Max(3, settings.TileSize / 2);
            var cx = bx + (settings.TileSize - size) / 2;
            var cy = by + (settings.TileSize - size) / 2;

            switch (building.Kind)
            {
                case SpecializedBuildingKindView.FarmPlot:
                    DrawFarmMarker(spriteBatch, textures.Pixel, cx, cy, size);
                    break;
                case SpecializedBuildingKindView.Workshop:
                    DrawWorkshopMarker(spriteBatch, textures.Pixel, cx, cy, size);
                    break;
                case SpecializedBuildingKindView.Storehouse:
                    DrawStorehouseMarker(spriteBatch, textures.Pixel, cx, cy, size);
                    break;
            }
        }
    }

    private static void DrawFarmMarker(SpriteBatch spriteBatch, Texture2D pixel, int x, int y, int size)
    {
        var accent = new Color(126, 214, 98);
        spriteBatch.Draw(pixel, new Rectangle(x, y, size, 1), accent);
        spriteBatch.Draw(pixel, new Rectangle(x, y + size - 1, size, 1), accent);
        spriteBatch.Draw(pixel, new Rectangle(x, y, 1, size), accent);
        spriteBatch.Draw(pixel, new Rectangle(x + size - 1, y, 1, size), accent);
        spriteBatch.Draw(pixel, new Rectangle(x + size / 2, y + size / 2, 1, 1), Color.White);
    }

    private static void DrawWorkshopMarker(SpriteBatch spriteBatch, Texture2D pixel, int x, int y, int size)
    {
        var accent = new Color(233, 139, 52);
        int mid = size / 2;
        spriteBatch.Draw(pixel, new Rectangle(x + mid, y, 1, size), accent);
        spriteBatch.Draw(pixel, new Rectangle(x, y + mid, size, 1), accent);
        spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 1, size - 2, size - 2), new Color(65, 47, 28));
    }

    private static void DrawStorehouseMarker(SpriteBatch spriteBatch, Texture2D pixel, int x, int y, int size)
    {
        var accent = new Color(76, 180, 196);
        spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), new Color(21, 56, 63));
        spriteBatch.Draw(pixel, new Rectangle(x, y, size, 1), accent);
        spriteBatch.Draw(pixel, new Rectangle(x, y + size - 1, size, 1), accent);
        for (int i = 1; i < size - 1; i += 2)
            spriteBatch.Draw(pixel, new Rectangle(x + i, y + 1, 1, size - 2), accent);
    }
}
