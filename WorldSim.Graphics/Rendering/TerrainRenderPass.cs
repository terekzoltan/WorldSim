using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class TerrainRenderPass
{
    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings, WorldRenderTheme theme)
    {
        foreach (var tile in snapshot.Tiles)
        {
            var color = tile.Ground switch
            {
                TileGroundView.Water => theme.Water,
                TileGroundView.Grass => theme.Grass,
                _ => theme.Dirt
            };

            var x = tile.X * settings.TileSize;
            var y = tile.Y * settings.TileSize;
            spriteBatch.Draw(textures.Pixel, new Microsoft.Xna.Framework.Rectangle(x, y, settings.TileSize, settings.TileSize), color);
        }
    }
}
