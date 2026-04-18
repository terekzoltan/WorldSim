using System;
using Microsoft.Xna.Framework;
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
        var visibleTiles = context.VisibleTileBounds;

        foreach (var tile in snapshot.Tiles)
        {
            if (!visibleTiles.Contains(tile.X, tile.Y))
                continue;

            var baseColor = tile.Ground switch
            {
                TileGroundView.Water => theme.Water,
                TileGroundView.Grass => theme.Grass,
                _ => theme.Dirt
            };

            var color = ApplyLowCostVariation(baseColor, tile, theme);

            var x = tile.X * settings.TileSize;
            var y = tile.Y * settings.TileSize;
            spriteBatch.Draw(textures.Pixel, new Microsoft.Xna.Framework.Rectangle(x, y, settings.TileSize, settings.TileSize), color);
        }
    }

    private static Color ApplyLowCostVariation(Color baseColor, TileRenderData tile, WorldRenderTheme theme)
    {
        var variationNoise = Hash01(tile.X, tile.Y) * 2f - 1f;
        var variationFactor = 1f + (variationNoise * 0.055f);
        var result = ScaleRgb(baseColor, variationFactor);

        if (tile.OwnerFactionId >= 0 && tile.OwnershipStrength > 0f)
        {
            var ownershipTint = tile.IsContested
                ? Color.Lerp(theme.Warning, theme.Highlight, 0.35f)
                : theme.Highlight;
            var ownershipWeight = 0.04f + (tile.OwnershipStrength * 0.16f);
            result = Color.Lerp(result, ownershipTint, Math.Clamp(ownershipWeight, 0f, 0.2f));
        }

        if (tile.NodeType == ResourceView.Food && tile.NodeAmount <= 0 && tile.FoodRegrowthProgress > 0f)
        {
            var recoveryWeight = Math.Clamp(tile.FoodRegrowthProgress * 0.14f, 0f, 0.14f);
            result = Color.Lerp(result, theme.Success, recoveryWeight);
        }

        return result;
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            var hash = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u;
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static Color ScaleRgb(Color color, float factor)
    {
        var r = (byte)Math.Clamp((int)MathF.Round(color.R * factor), 0, 255);
        var g = (byte)Math.Clamp((int)MathF.Round(color.G * factor), 0, 255);
        var b = (byte)Math.Clamp((int)MathF.Round(color.B * factor), 0, 255);
        return new Color(r, g, b, color.A);
    }
}
