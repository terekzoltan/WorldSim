using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Rendering;

public sealed class CombatOverlayPass : IRenderPass
{
    public string Name => "CombatOverlay";
    public bool Enabled { get; set; }

    public void Draw(in RenderFrameContext context)
    {
        if (!Enabled)
            return;

        var snapshot = context.Snapshot;
        var settings = context.Settings;
        var spriteBatch = context.SpriteBatch;
        var pixel = context.Textures.Pixel;

        DrawContestedTiles(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawBattleZones(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawFormationMarkers(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawCombatActors(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawNoProgressActors(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawColonyWarDiagnostics(spriteBatch, pixel, snapshot, settings.TileSize);
    }

    private static void DrawBattleZones(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        foreach (var battle in snapshot.Battles.OrderBy(b => b.BattleId))
        {
            int centerX = (battle.CenterX * tileSize) + (tileSize / 2);
            int centerY = (battle.CenterY * tileSize) + (tileSize / 2);
            int radius = Math.Max(tileSize, battle.Radius * tileSize);

            float intensityAlpha = Math.Clamp(0.10f + (battle.Intensity * 0.02f), 0.10f, 0.28f);
            var fill = new Color(226, 124, 96) * intensityAlpha;
            var edge = (battle.LeftIsRouting || battle.RightIsRouting)
                ? new Color(236, 124, 208) * 0.65f
                : new Color(232, 162, 112) * 0.58f;

            DrawCircleFill(spriteBatch, pixel, centerX, centerY, radius, fill, step: Math.Max(2, tileSize / 2));
            DrawCircleOutline(spriteBatch, pixel, centerX, centerY, radius, edge, Math.Max(1, tileSize / 4));
        }
    }

    private static void DrawFormationMarkers(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
            int glyph = Math.Max(2, tileSize);
            foreach (var group in snapshot.CombatGroups.OrderBy(g => g.GroupId))
            {
                int cx = (group.AnchorX * tileSize) + (tileSize / 2);
                int cy = (group.AnchorY * tileSize) + (tileSize / 2);
            var color = group.IsRouting
                ? new Color(236, 125, 206) * 0.9f
                : new Color(129, 206, 250) * 0.82f;

            switch (group.Formation)
            {
                case "Wedge":
                    DrawLine(spriteBatch, pixel, new Vector2(cx - glyph, cy + glyph), new Vector2(cx, cy - glyph), color, 1);
                    DrawLine(spriteBatch, pixel, new Vector2(cx + glyph, cy + glyph), new Vector2(cx, cy - glyph), color, 1);
                    break;
                case "DefensiveCircle":
                    DrawCircleOutline(spriteBatch, pixel, cx, cy, glyph, color, 1);
                    break;
                case "Skirmish":
                    spriteBatch.Draw(pixel, new Rectangle(cx - glyph, cy - 1, 2, 2), color);
                    spriteBatch.Draw(pixel, new Rectangle(cx, cy - glyph, 2, 2), color);
                    spriteBatch.Draw(pixel, new Rectangle(cx + glyph - 1, cy, 2, 2), color);
                    spriteBatch.Draw(pixel, new Rectangle(cx, cy + glyph - 1, 2, 2), color);
                    break;
                default:
                    spriteBatch.Draw(pixel, new Rectangle(cx - glyph, cy, glyph * 2, 1), color);
                    break;
            }

                if (group.CommanderActorId >= 0)
                {
                    var commander = new Color(108, 234, 201) * 0.95f;
                    spriteBatch.Draw(pixel, new Rectangle(cx - 2, cy - glyph - 4, 5, 5), new Color(10, 16, 22, 180));
                    spriteBatch.Draw(pixel, new Rectangle(cx - 1, cy - glyph - 3, 3, 3), commander);
                }
            }
        }

    private static void DrawContestedTiles(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        var contestedColor = new Color(242, 186, 112) * 0.22f;
        foreach (var tile in snapshot.Tiles.Where(tile => tile.IsContested))
        {
            spriteBatch.Draw(pixel, new Rectangle(tile.X * tileSize, tile.Y * tileSize, tileSize, tileSize), contestedColor);
        }
    }

    private static void DrawCombatActors(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        int border = Math.Max(1, tileSize / 4);
        var color = new Color(230, 90, 82) * 0.86f;
        foreach (var person in snapshot.People.Where(person => person.IsInCombat))
        {
            var rect = new Rectangle(person.X * tileSize, person.Y * tileSize, tileSize, tileSize);
            DrawRectOutline(spriteBatch, pixel, rect, border, color);
        }
    }

    private static void DrawNoProgressActors(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        int dot = Math.Max(1, tileSize / 3);
        foreach (var person in snapshot.People.Where(person => person.NoProgressStreak > 0 || person.BackoffTicksRemaining > 0))
        {
            if (person.ActiveBattleId >= 0 || person.IsRouting)
                continue;

            var severity = Math.Max(person.NoProgressStreak, person.BackoffTicksRemaining);
            var color = severity >= 6
                ? new Color(240, 88, 212) * 0.85f
                : new Color(206, 133, 244) * 0.75f;

            int x = person.X * tileSize;
            int y = person.Y * tileSize;
            spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 1, dot, dot), color);
        }
    }

    private static void DrawColonyWarDiagnostics(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        foreach (var colony in snapshot.Colonies)
        {
            var centerTile = GetColonyCenterTile(snapshot, colony.Id);
            if (centerTile == null)
                continue;

            var (cx, cy) = centerTile.Value;
            int px = cx * tileSize;
            int py = cy * tileSize;

            var warColor = colony.WarState.ToLowerInvariant() switch
            {
                "war" => new Color(232, 86, 78),
                "hostile" => new Color(232, 141, 86),
                "tense" => new Color(226, 194, 104),
                _ => new Color(112, 176, 216)
            };

            int size = Math.Max(tileSize * 2, 12);
            int thickness = Math.Max(1, tileSize / 5);
            var anchor = new Rectangle(px - (size / 2), py - (size / 2), size, size);
            DrawRectOutline(spriteBatch, pixel, anchor, thickness, warColor * 0.72f);

            int bars = Math.Clamp((colony.WarriorCount + 2) / 3, 0, 6);
            int barW = Math.Max(1, tileSize / 4);
            int barH = Math.Max(2, tileSize / 2);
            for (int i = 0; i < bars; i++)
            {
                int bx = anchor.X + (i * (barW + 1));
                int by = anchor.Y - barH - 1;
                spriteBatch.Draw(pixel, new Rectangle(bx, by, barW, barH), warColor * 0.8f);
            }

            int contestedNearby = CountNearbyContestedTiles(snapshot, cx, cy, radius: 10);
            if (contestedNearby > 0)
            {
                int pressureW = Math.Clamp(contestedNearby, 1, 12) * Math.Max(1, tileSize / 3);
                int pressureH = Math.Max(1, tileSize / 4);
                spriteBatch.Draw(pixel, new Rectangle(anchor.X, anchor.Bottom + 2, pressureW, pressureH), new Color(244, 194, 102) * 0.75f);
            }
        }
    }

    private static (int X, int Y)? GetColonyCenterTile(Runtime.ReadModel.WorldRenderSnapshot snapshot, int colonyId)
    {
        var people = snapshot.People.Where(person => person.ColonyId == colonyId).ToList();
        if (people.Count > 0)
        {
            int px = (int)Math.Round(people.Average(person => person.X));
            int py = (int)Math.Round(people.Average(person => person.Y));
            return (px, py);
        }

        var houses = snapshot.Houses.Where(house => house.ColonyId == colonyId).ToList();
        if (houses.Count > 0)
        {
            int hx = (int)Math.Round(houses.Average(house => house.X));
            int hy = (int)Math.Round(houses.Average(house => house.Y));
            return (hx, hy);
        }

        return null;
    }

    private static int CountNearbyContestedTiles(Runtime.ReadModel.WorldRenderSnapshot snapshot, int centerX, int centerY, int radius)
    {
        return snapshot.Tiles.Count(tile =>
            tile.IsContested
            && Math.Abs(tile.X - centerX) <= radius
            && Math.Abs(tile.Y - centerY) <= radius);
    }

    private static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private static void DrawCircleOutline(SpriteBatch spriteBatch, Texture2D pixel, int cx, int cy, int radius, Color color, int thickness)
    {
        const int segments = 20;
        Vector2? previous = null;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = MathF.Tau * t;
            var point = new Vector2(
                cx + (MathF.Cos(angle) * radius),
                cy + (MathF.Sin(angle) * radius));

            if (previous.HasValue)
                DrawLine(spriteBatch, pixel, previous.Value, point, color, thickness);
            previous = point;
        }
    }

    private static void DrawCircleFill(SpriteBatch spriteBatch, Texture2D pixel, int cx, int cy, int radius, Color color, int step)
    {
        for (int y = -radius; y <= radius; y += Math.Max(1, step))
        {
            float ratio = 1f - ((y * y) / (float)(radius * radius));
            if (ratio <= 0f)
                continue;
            int halfWidth = (int)(MathF.Sqrt(ratio) * radius);
            spriteBatch.Draw(pixel, new Rectangle(cx - halfWidth, cy + y, halfWidth * 2, Math.Max(1, step)), color);
        }
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, int thickness)
    {
        var delta = to - from;
        float length = delta.Length();
        if (length < 1f)
            return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            new Rectangle((int)from.X, (int)from.Y, (int)length, Math.Max(1, thickness)),
            null,
            color,
            angle,
            new Vector2(0f, Math.Max(1, thickness) * 0.5f),
            SpriteEffects.None,
            0f);
    }
}
