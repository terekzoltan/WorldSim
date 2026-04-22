using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering;

internal static class RenderLayout
{
    public static TilePadding CalculateVisibleTilePadding(WorldRenderSettings settings)
    {
        float maxHorizontalScale = MathF.Max(
            settings.PersonScale,
            MathF.Max(
                settings.AnimalScale,
                MathF.Max(
                    settings.ResourceScale,
                    MathF.Max(settings.HouseScale, MathF.Max(settings.SpecializedBuildingScale, settings.DefensiveStructureScale)))));

        float maxBottomAnchoredScale = MathF.Max(
            settings.PersonScale,
            MathF.Max(settings.AnimalScale, MathF.Max(settings.ResourceScale, MathF.Max(settings.HouseScale, settings.SpecializedBuildingScale))));

        int horizontal = Math.Max(2, (int)MathF.Ceiling(MathF.Max(0f, (maxHorizontalScale - 1f) * 0.5f)) + 1);
        int top = Math.Max(2, (int)MathF.Ceiling(MathF.Max(0f, maxBottomAnchoredScale - 1f)) + 1);

        float shadowBottomReach = MathF.Max(settings.ActorShadowAlpha, settings.StructureShadowAlpha) > 0f
            ? maxBottomAnchoredScale * 0.1f
            : 0f;
        int bottom = Math.Max(2, (int)MathF.Ceiling(shadowBottomReach) + 1);

        return new TilePadding(horizontal, top, bottom);
    }

    public static bool IsVisible(in TileBounds visibleTiles, int tileX, int tileY)
        => visibleTiles.Contains(tileX, tileY);

    public static Rectangle CenteredInTile(int tileX, int tileY, int tileSize, float scale)
    {
        int size = Math.Max(1, (int)MathF.Round(tileSize * scale));
        int px = tileX * tileSize + ((tileSize - size) / 2);
        int py = tileY * tileSize + ((tileSize - size) / 2);
        return new Rectangle(px, py, size, size);
    }

    public static Rectangle BottomAnchoredInTile(int tileX, int tileY, int tileSize, float scale, int yLift = 0)
    {
        int size = Math.Max(1, (int)MathF.Round(tileSize * scale));
        int px = tileX * tileSize + ((tileSize - size) / 2);
        int py = (tileY * tileSize) + tileSize - size - yLift;
        return new Rectangle(px, py, size, size);
    }

    public static void DrawGroundShadow(SpriteBatch spriteBatch, Texture2D pixel, Rectangle spriteRect, float alpha)
    {
        int shadowWidth = Math.Max(2, (int)MathF.Round(spriteRect.Width * 0.78f));
        int shadowHeight = Math.Max(1, (int)MathF.Round(spriteRect.Height * 0.18f));
        int shadowX = spriteRect.Center.X - (shadowWidth / 2);
        int shadowY = spriteRect.Bottom - Math.Max(1, shadowHeight / 2);
        var shadow = new Color(10, 12, 16) * Math.Clamp(alpha, 0f, 1f);
        spriteBatch.Draw(pixel, new Rectangle(shadowX, shadowY, shadowWidth, shadowHeight), shadow);
    }
}

internal readonly record struct TilePadding(int Horizontal, int Top, int Bottom);
