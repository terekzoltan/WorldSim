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
        DrawSiegeZones(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawSiegeUnits(spriteBatch, pixel, snapshot, settings.TileSize, context.VisibleTileBounds);
        DrawBreachMarkers(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawFormationMarkers(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawCombatActors(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawNoProgressActors(spriteBatch, pixel, snapshot, settings.TileSize);
        DrawColonyWarDiagnostics(spriteBatch, pixel, snapshot, settings.TileSize);
    }

    private static void DrawSiegeZones(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        foreach (var siege in snapshot.Sieges
                     .GroupBy(s => s.SiegeId)
                     .Select(group => group.OrderByDescending(s => s.ActiveAttackerCount).First())
                     .OrderBy(s => s.SiegeId))
        {
            int centerX = (siege.CenterX * tileSize) + (tileSize / 2);
            int centerY = (siege.CenterY * tileSize) + (tileSize / 2);
            int radius = Math.Max(tileSize, tileSize + (Math.Clamp(siege.ActiveAttackerCount, 1, 9) * Math.Max(1, tileSize / 3)));
            bool breached = string.Equals(siege.Status, "breached", StringComparison.OrdinalIgnoreCase) || siege.BreachCount > 0;

            var fill = breached
                ? new Color(190, 106, 76) * 0.16f
                : new Color(156, 126, 96) * 0.12f;
            var edge = breached
                ? new Color(239, 140, 104) * 0.74f
                : new Color(206, 178, 124) * 0.62f;

            DrawCircleFill(spriteBatch, pixel, centerX, centerY, radius, fill, step: Math.Max(2, tileSize / 2));
            DrawCircleOutline(spriteBatch, pixel, centerX, centerY, radius, edge, Math.Max(1, tileSize / 4));

            if (breached)
            {
                int breachHalo = Math.Max(1, tileSize / 2);
                DrawCircleOutline(spriteBatch, pixel, centerX, centerY, Math.Max(2, radius - breachHalo), new Color(245, 194, 120) * 0.78f, Math.Max(1, tileSize / 5));
            }
        }
    }

    private static void DrawBreachMarkers(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize)
    {
        int marker = Math.Max(2, tileSize);
        foreach (var breach in snapshot.Breaches.OrderBy(b => b.CreatedTick))
        {
            int cx = (breach.X * tileSize) + (tileSize / 2);
            int cy = (breach.Y * tileSize) + (tileSize / 2);
            var glow = new Color(246, 199, 110) * 0.66f;
            var slash = new Color(246, 138, 92) * 0.92f;

            spriteBatch.Draw(pixel, new Rectangle(cx - marker, cy - marker, marker * 2, marker * 2), glow * 0.35f);
            DrawLine(spriteBatch, pixel, new Vector2(cx - marker, cy - marker), new Vector2(cx + marker, cy + marker), slash, Math.Max(1, tileSize / 4));
            DrawLine(spriteBatch, pixel, new Vector2(cx - marker, cy + marker), new Vector2(cx + marker, cy - marker), slash, Math.Max(1, tileSize / 4));
        }
    }

    private static void DrawSiegeUnits(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.WorldRenderSnapshot snapshot, int tileSize, TileBounds visibleTiles)
    {
        foreach (var unit in snapshot.SiegeUnits.OrderBy(unit => unit.SiegeUnitId))
        {
            if (!IsSiegeUnitVisible(unit, visibleTiles, padding: 2))
                continue;

            bool active = string.Equals(unit.Phase, "active", StringComparison.OrdinalIgnoreCase);
            var baseColor = GetFactionColor(unit.OwnerFactionId);
            var color = active ? baseColor : Color.Lerp(baseColor, new Color(172, 172, 172), 0.68f);
            float alpha = active ? 0.86f : 0.38f;
            int thickness = Math.Max(1, tileSize / 5);
            int size = Math.Max(tileSize + 2, 8);
            var center = TileCenter(unit.X, unit.Y, tileSize);
            var body = CenteredRect(center, size);

            if (active)
            {
                var target = TileCenter(unit.TargetX, unit.TargetY, tileSize);
                DrawLine(spriteBatch, pixel, center, target, color * 0.36f, Math.Max(1, tileSize / 6));
                DrawRectOutline(spriteBatch, pixel, CenteredRect(target, Math.Max(4, tileSize / 2)), thickness, color * 0.58f);
            }

            DrawSiegeUnitGlyph(spriteBatch, pixel, unit.Kind, body, color * alpha, thickness);
            DrawSiegeUnitHealthCue(spriteBatch, pixel, unit, body, tileSize, active);

            if (active)
                DrawSiegeUnitActionCue(spriteBatch, pixel, unit, center, tileSize, color);
        }
    }

    private static bool IsSiegeUnitVisible(Runtime.ReadModel.SiegeUnitRenderData unit, TileBounds visibleTiles, int padding)
        => ContainsWithPadding(visibleTiles, unit.X, unit.Y, padding)
           || ContainsWithPadding(visibleTiles, unit.TargetX, unit.TargetY, padding);

    private static bool ContainsWithPadding(TileBounds bounds, int x, int y, int padding)
        => x >= bounds.MinX - padding
           && x <= bounds.MaxX + padding
           && y >= bounds.MinY - padding
           && y <= bounds.MaxY + padding;

    private static void DrawSiegeUnitGlyph(SpriteBatch spriteBatch, Texture2D pixel, string kind, Rectangle body, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, body, color * 0.22f);
        DrawRectOutline(spriteBatch, pixel, body, thickness, color);

        if (string.Equals(kind, "ram", StringComparison.OrdinalIgnoreCase))
        {
            int y = body.Y + (body.Height / 2);
            DrawLine(spriteBatch, pixel, new Vector2(body.X, y), new Vector2(body.Right, y), color * 0.96f, Math.Max(1, thickness + 1));
            spriteBatch.Draw(pixel, new Rectangle(body.Right - thickness - 1, body.Y + thickness, Math.Max(1, thickness + 1), Math.Max(1, body.Height - (thickness * 2))), color * 0.8f);
            return;
        }

        if (string.Equals(kind, "siege_tower", StringComparison.OrdinalIgnoreCase))
        {
            var tower = new Rectangle(body.X + (body.Width / 4), body.Y - Math.Max(2, body.Height / 3), Math.Max(2, body.Width / 2), body.Height + Math.Max(2, body.Height / 3));
            spriteBatch.Draw(pixel, tower, color * 0.18f);
            DrawRectOutline(spriteBatch, pixel, tower, thickness, color * 0.9f);
            DrawLine(spriteBatch, pixel, new Vector2(tower.X, tower.Y + Math.Max(2, tower.Height / 3)), new Vector2(tower.Right, tower.Y + Math.Max(2, tower.Height / 3)), color * 0.84f, thickness);
            return;
        }

        if (string.Equals(kind, "mobile_catapult", StringComparison.OrdinalIgnoreCase))
        {
            DrawLine(spriteBatch, pixel, new Vector2(body.X + thickness, body.Bottom - thickness), new Vector2(body.Right - thickness, body.Y + thickness), color * 0.95f, thickness);
            DrawLine(spriteBatch, pixel, new Vector2(body.X + thickness, body.Y + thickness), new Vector2(body.Right - thickness, body.Bottom - thickness), color * 0.54f, Math.Max(1, thickness));
            return;
        }

        DrawLine(spriteBatch, pixel, new Vector2(body.X, body.Y), new Vector2(body.Right, body.Bottom), color * 0.72f, thickness);
    }

    private static void DrawSiegeUnitHealthCue(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.SiegeUnitRenderData unit, Rectangle body, int tileSize, bool active)
    {
        if (unit.MaxHealth <= 0f)
            return;

        float healthRatio = Math.Clamp(unit.Health / unit.MaxHealth, 0f, 1f);
        if (healthRatio >= 0.995f)
            return;

        int barHeight = Math.Max(1, tileSize / 5);
        var back = new Rectangle(body.X, body.Bottom + 1, body.Width, barHeight);
        var fill = new Rectangle(back.X, back.Y, Math.Max(1, (int)MathF.Round(back.Width * healthRatio)), back.Height);
        var color = active ? new Color(242, 162, 96) : new Color(156, 156, 156);

        spriteBatch.Draw(pixel, back, new Color(10, 14, 18) * 0.72f);
        spriteBatch.Draw(pixel, fill, color * 0.82f);
    }

    private static void DrawSiegeUnitActionCue(SpriteBatch spriteBatch, Texture2D pixel, Runtime.ReadModel.SiegeUnitRenderData unit, Vector2 center, int tileSize, Color color)
    {
        if (string.IsNullOrWhiteSpace(unit.RecentActionEffect)
            || string.Equals(unit.RecentActionEffect, "ready", StringComparison.OrdinalIgnoreCase))
            return;

        int pulse = Math.Max(1, (int)(Math.Abs(unit.LastActionTick) % 3));
        int radius = Math.Max(tileSize, tileSize + (pulse * Math.Max(1, tileSize / 4)));
        int cx = (int)MathF.Round(center.X);
        int cy = (int)MathF.Round(center.Y);

        if (string.Equals(unit.RecentActionEffect, "ram_wall_gate_pressure", StringComparison.OrdinalIgnoreCase))
        {
            DrawCircleOutline(spriteBatch, pixel, cx, cy, radius, new Color(246, 164, 88) * 0.72f, Math.Max(1, tileSize / 5));
            return;
        }

        if (string.Equals(unit.RecentActionEffect, "siege_tower_access_pressure", StringComparison.OrdinalIgnoreCase))
        {
            DrawLine(spriteBatch, pixel, new Vector2(cx - radius, cy + radius), new Vector2(cx, cy - radius), new Color(222, 206, 148) * 0.76f, Math.Max(1, tileSize / 5));
            DrawLine(spriteBatch, pixel, new Vector2(cx, cy - radius), new Vector2(cx + radius, cy + radius), new Color(222, 206, 148) * 0.76f, Math.Max(1, tileSize / 5));
            return;
        }

        if (string.Equals(unit.RecentActionEffect, "mobile_catapult_ranged_pressure", StringComparison.OrdinalIgnoreCase))
        {
            DrawCircleOutline(spriteBatch, pixel, cx, cy, radius + Math.Max(1, tileSize / 3), new Color(246, 202, 108) * 0.58f, Math.Max(1, tileSize / 6));
            spriteBatch.Draw(pixel, CenteredRect(center, Math.Max(3, tileSize / 2)), new Color(246, 202, 108) * 0.55f);
            return;
        }

        DrawCircleOutline(spriteBatch, pixel, cx, cy, radius, color * 0.44f, Math.Max(1, tileSize / 6));
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

    private static Vector2 TileCenter(int x, int y, int tileSize)
        => new((x * tileSize) + (tileSize / 2f), (y * tileSize) + (tileSize / 2f));

    private static Rectangle CenteredRect(Vector2 center, int size)
        => new((int)MathF.Round(center.X - (size / 2f)), (int)MathF.Round(center.Y - (size / 2f)), size, size);

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            0 => new Color(94, 149, 214),
            1 => new Color(216, 131, 93),
            2 => new Color(120, 194, 128),
            3 => new Color(181, 140, 232),
            _ => new Color(180, 180, 180)
        };
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
