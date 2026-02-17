using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class StructureRenderPass
{
    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings)
    {
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
    }
}
