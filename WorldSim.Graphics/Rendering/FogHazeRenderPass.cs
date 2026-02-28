using System;
using Microsoft.Xna.Framework;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class FogHazeRenderPass : IRenderPass
{
    public string Name => "FogHaze";

    public void Draw(in RenderFrameContext context)
    {
        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var textures = context.Textures;
        var time = context.TimeSeconds;
        var fx = context.FxIntensity;

        var mapWidthPx = snapshot.Width * settings.TileSize;
        var mapHeightPx = snapshot.Height * settings.TileSize;
        if (mapWidthPx <= 0 || mapHeightPx <= 0)
            return;

        float seasonFactor = snapshot.CurrentSeason switch
        {
            SeasonView.Winter => 0.2f,
            SeasonView.Autumn => 0.14f,
            SeasonView.Summer => 0.08f,
            _ => 0.1f
        };

        float droughtFactor = snapshot.IsDroughtActive ? 0.08f : 0f;
        float pulse = 0.03f * (0.5f + 0.5f * MathF.Sin(time * 0.5f));
        float opacity = Math.Clamp((seasonFactor + droughtFactor + pulse) * fx, 0.03f, 0.35f);

        var hazeColor = snapshot.IsDroughtActive
            ? new Color(198, 173, 134) * opacity
            : new Color(194, 210, 225) * opacity;

        spriteBatch.Draw(textures.Pixel, new Rectangle(0, 0, mapWidthPx, mapHeightPx), hazeColor);
    }
}
