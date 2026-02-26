using System;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class TerrainRenderPass : IRenderPass
{
    public string Name => "Terrain";

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
                var color = tile.Ground switch
                {
                    TileGroundView.Water => theme.Water,
                    TileGroundView.Grass => theme.Grass,
                    _ => theme.Dirt
                };

                var px = tile.X * settings.TileSize;
                var py = tile.Y * settings.TileSize;
                spriteBatch.Draw(textures.Pixel, new Microsoft.Xna.Framework.Rectangle(px, py, settings.TileSize, settings.TileSize), color);
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
