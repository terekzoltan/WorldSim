using System;
using Microsoft.Xna.Framework;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class FogHazeRenderPass : IRenderPass
{
    public string Name => "FogHaze";

    public void Draw(in RenderFrameContext context)
    {
        var visualPolicy = context.VisualPolicy;
        if (!visualPolicy.HazeEnabled || visualPolicy.HazeIntensityMultiplier <= 0f)
            return;

        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var textures = context.Textures;
        var visibleTiles = context.VisibleTileBounds;

        var widthTiles = (visibleTiles.MaxX - visibleTiles.MinX) + 1;
        var heightTiles = (visibleTiles.MaxY - visibleTiles.MinY) + 1;
        if (widthTiles <= 0 || heightTiles <= 0)
            return;

        var drawX = visibleTiles.MinX * settings.TileSize;
        var drawY = visibleTiles.MinY * settings.TileSize;
        var drawWidth = widthTiles * settings.TileSize;
        var drawHeight = heightTiles * settings.TileSize;

        float seasonFactor = snapshot.CurrentSeason switch
        {
            SeasonView.Winter => 0.22f,
            SeasonView.Autumn => 0.16f,
            SeasonView.Summer => 0.1f,
            _ => 0.12f
        };

        float droughtFactor = snapshot.IsDroughtActive ? 0.08f : 0f;
        float opacity = Math.Clamp((seasonFactor + droughtFactor) * visualPolicy.HazeIntensityMultiplier, 0.015f, 0.28f);

        var hazeColor = snapshot.IsDroughtActive
            ? new Color(198, 173, 134) * opacity
            : new Color(194, 210, 225) * opacity;

        spriteBatch.Draw(textures.Pixel, new Rectangle(drawX, drawY, drawWidth, drawHeight), hazeColor);
    }
}
