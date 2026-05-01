using System;
using System.Linq;
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
        var visibleTiles = context.VisibleTileBounds;

        foreach (var house in snapshot.Houses)
        {
            if (!RenderLayout.IsVisible(visibleTiles, house.X, house.Y))
                continue;

            var footprint = RenderLayout.CenteredInTile(house.X, house.Y, settings.TileSize, 0.95f);
            var houseRect = RenderLayout.BottomAnchoredInTile(house.X, house.Y, settings.TileSize, settings.HouseScale);
            RenderLayout.DrawGroundShadow(spriteBatch, textures.Pixel, footprint, settings.StructureShadowAlpha);

            var icon = textures.GetHouseIcon(house.ColonyId);
            if (icon != null)
            {
                spriteBatch.Draw(icon, houseRect, Color.White);
            }
            else
            {
                spriteBatch.Draw(textures.Pixel, houseRect, new Color(202, 196, 168));
                spriteBatch.Draw(textures.Pixel, new Rectangle(houseRect.X, houseRect.Y, houseRect.Width, Math.Max(1, houseRect.Height / 4)), new Color(150, 112, 83));
            }
        }

        foreach (var building in snapshot.SpecializedBuildings)
        {
            if (!RenderLayout.IsVisible(visibleTiles, building.X, building.Y))
                continue;

            var iconRect = RenderLayout.BottomAnchoredInTile(building.X, building.Y, settings.TileSize, settings.SpecializedBuildingScale);
            RenderLayout.DrawGroundShadow(spriteBatch, textures.Pixel, iconRect, settings.StructureShadowAlpha * 0.8f);

            var icon = textures.GetSpecializedBuildingIcon(building.Kind);
            if (icon != null)
            {
                spriteBatch.Draw(icon, iconRect, Color.White);
                continue;
            }

            int size = Math.Max(3, iconRect.Width);
            int cx = iconRect.Center.X - (size / 2);
            int cy = iconRect.Center.Y - (size / 2);

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

        DrawDefensiveStructures(spriteBatch, snapshot, textures, settings, visibleTiles);
        DrawTowerProjectiles(spriteBatch, snapshot, textures, settings, visibleTiles);
    }

    private static void DrawDefensiveStructures(
        SpriteBatch spriteBatch,
        WorldRenderSnapshot snapshot,
        TextureCatalog textures,
        WorldRenderSettings settings,
        in TileBounds visibleTiles)
    {
        foreach (var structure in snapshot.DefensiveStructures)
        {
            if (!RenderLayout.IsVisible(visibleTiles, structure.X, structure.Y))
                continue;

            int tileX = structure.X * settings.TileSize;
            int tileY = structure.Y * settings.TileSize;
            var tileRect = new Rectangle(tileX, tileY, settings.TileSize, settings.TileSize);
            if (Math.Abs(settings.DefensiveStructureScale - 1f) > 0.001f)
                tileRect = RenderLayout.CenteredInTile(structure.X, structure.Y, settings.TileSize, settings.DefensiveStructureScale);

            var icon = textures.GetDefensiveStructureIcon(structure.Kind);
            if (icon != null)
            {
                spriteBatch.Draw(icon, tileRect, Color.White);
            }
            else
            {
                switch (structure.Kind)
                {
                    case DefensiveStructureKindView.WoodWall:
                        DrawWoodWall(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.StoneWall:
                        DrawStoneWall(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.ReinforcedWall:
                        DrawReinforcedWall(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.Gate:
                        DrawGate(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.Watchtower:
                        DrawWatchtower(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.ArrowTower:
                        DrawArrowTower(spriteBatch, textures.Pixel, tileRect);
                        break;
                    case DefensiveStructureKindView.CatapultTower:
                        DrawCatapultTower(spriteBatch, textures.Pixel, tileRect);
                        break;
                }
            }

            if (!structure.IsActive)
                DrawInactiveMarker(spriteBatch, textures.Pixel, tileRect);

            DrawStructureHpBar(spriteBatch, textures.Pixel, settings, tileRect, structure.Hp, structure.MaxHp);
        }
    }

    private static void DrawWoodWall(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var fill = new Color(125, 91, 61);
        var edge = new Color(79, 55, 36);
        int height = Math.Max(2, tileRect.Height / 2);
        int y = tileRect.Y + (tileRect.Height - height) / 2;
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y, tileRect.Width, height), fill);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y, tileRect.Width, 1), edge);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y + height - 1, tileRect.Width, 1), edge);
        for (int i = 1; i < tileRect.Width - 1; i += 2)
            spriteBatch.Draw(pixel, new Rectangle(tileRect.X + i, y + 1, 1, Math.Max(1, height - 2)), edge * 0.7f);
    }

    private static void DrawWatchtower(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var body = new Color(85, 94, 112);
        var top = new Color(136, 149, 176);
        int inset = Math.Max(1, tileRect.Width / 5);
        var bodyRect = new Rectangle(tileRect.X + inset, tileRect.Y + inset, tileRect.Width - (2 * inset), tileRect.Height - inset);
        spriteBatch.Draw(pixel, bodyRect, body);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, Math.Max(1, tileRect.Height / 3)), top);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, 1), Color.White * 0.35f);
    }

    private static void DrawStoneWall(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var fill = new Color(140, 140, 150);
        var edge = new Color(102, 102, 112);
        int height = Math.Max(2, (tileRect.Height * 2) / 3);
        int y = tileRect.Y + (tileRect.Height - height) / 2;
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y, tileRect.Width, height), fill);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y, tileRect.Width, 1), edge);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y + height - 1, tileRect.Width, 1), edge);
        for (int i = 1; i < tileRect.Width - 1; i += 3)
            spriteBatch.Draw(pixel, new Rectangle(tileRect.X + i, y + 1, 1, Math.Max(1, height - 2)), edge * 0.7f);
    }

    private static void DrawReinforcedWall(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        DrawStoneWall(spriteBatch, pixel, tileRect);
        var steel = new Color(170, 180, 195);
        int height = Math.Max(2, (tileRect.Height * 2) / 3);
        int y = tileRect.Y + (tileRect.Height - height) / 2;
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y + 1, tileRect.Width, 1), steel * 0.9f);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, y + height - 2, tileRect.Width, 1), steel * 0.9f);
    }

    private static void DrawGate(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var stone = new Color(138, 138, 148);
        var darkGap = new Color(40, 44, 52);
        int pillarWidth = Math.Max(1, tileRect.Width / 3);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y, pillarWidth, tileRect.Height), stone);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.Right - pillarWidth, tileRect.Y, pillarWidth, tileRect.Height), stone);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X + pillarWidth, tileRect.Y + 1, tileRect.Width - (pillarWidth * 2), tileRect.Height - 2), darkGap);
    }

    private static void DrawArrowTower(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var body = new Color(92, 105, 126);
        var parapet = new Color(150, 165, 185);
        int inset = Math.Max(1, tileRect.Width / 6);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X + inset, tileRect.Y + inset, tileRect.Width - (2 * inset), tileRect.Height - inset), body);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, Math.Max(2, tileRect.Height / 3)), parapet);
        for (int x = tileRect.X + 1; x < tileRect.Right - 1; x += 2)
            spriteBatch.Draw(pixel, new Rectangle(x, tileRect.Y + 1, 1, 1), new Color(60, 70, 84));
    }

    private static void DrawCatapultTower(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var body = new Color(60, 65, 80);
        var cap = new Color(44, 48, 60);
        var accent = new Color(230, 130, 60);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y + 1, tileRect.Width, tileRect.Height - 1), body);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, Math.Max(2, tileRect.Height / 2)), cap);
        int dot = Math.Max(1, tileRect.Width / 4);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.Center.X - (dot / 2), tileRect.Center.Y - (dot / 2), dot, dot), accent);
    }

    private static void DrawInactiveMarker(SpriteBatch spriteBatch, Texture2D pixel, Rectangle tileRect)
    {
        var tint = new Color(180, 62, 62) * 0.32f;
        spriteBatch.Draw(pixel, tileRect, tint);
        int marker = Math.Max(1, tileRect.Width / 4);
        spriteBatch.Draw(pixel, new Rectangle(tileRect.X + 1, tileRect.Y + 1, marker, marker), new Color(236, 118, 118) * 0.85f);
    }

    private static void DrawStructureHpBar(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        WorldRenderSettings settings,
        Rectangle tileRect,
        float hp,
        float maxHp)
    {
        if (maxHp <= 0f)
            return;

        float normalized = Math.Clamp(hp / maxHp, 0f, 1f);
        if (normalized >= 0.995f)
            return;

        int width = Math.Max(settings.TileSize + 2, tileRect.Width);
        int height = Math.Max(2, settings.TileSize / 3);
        int x = tileRect.X + (tileRect.Width - width) / 2;
        int y = tileRect.Y - height - 1;

        spriteBatch.Draw(pixel, new Rectangle(x, y, width, height), new Color(16, 20, 26, 210));
        int innerWidth = Math.Max(1, width - 2);
        int fillWidth = Math.Clamp((int)MathF.Round(innerWidth * normalized), 1, innerWidth);
        var fillColor = normalized switch
        {
            < 0.35f => new Color(214, 101, 88),
            < 0.7f => new Color(235, 196, 109),
            _ => new Color(136, 214, 147)
        };
        spriteBatch.Draw(pixel, new Rectangle(x + 1, y + 1, fillWidth, Math.Max(1, height - 2)), fillColor);
    }

    private static void DrawTowerProjectiles(
        SpriteBatch spriteBatch,
        WorldRenderSnapshot snapshot,
        TextureCatalog textures,
        WorldRenderSettings settings,
        in TileBounds visibleTiles)
    {
        var towerEvents = snapshot.RecentEvents
            .Where(evt => evt.Contains("watchtower fired", StringComparison.OrdinalIgnoreCase)
                       || evt.Contains("arrow tower fired", StringComparison.OrdinalIgnoreCase)
                       || evt.Contains("catapult tower fired", StringComparison.OrdinalIgnoreCase)
                       || evt.Contains("tower fired", StringComparison.OrdinalIgnoreCase)
                       || evt.Contains("tower hit predator", StringComparison.OrdinalIgnoreCase))
            .TakeLast(3)
            .ToList();

        if (towerEvents.Count == 0)
            return;

        foreach (var evt in towerEvents)
        {
            bool predatorShot = evt.Contains("tower hit predator", StringComparison.OrdinalIgnoreCase);
            bool catapultShot = evt.Contains("catapult fired", StringComparison.OrdinalIgnoreCase);
            bool arrowTowerShot = evt.Contains("arrow tower fired", StringComparison.OrdinalIgnoreCase);
            string suffix = ResolveTowerEventSuffix(evt, predatorShot, catapultShot, arrowTowerShot);
            int suffixIdx = evt.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (suffixIdx <= 0)
                continue;

            string colonyName = evt[..suffixIdx].Trim();
            var colony = snapshot.Colonies.FirstOrDefault(c => string.Equals(c.Name, colonyName, StringComparison.OrdinalIgnoreCase));
            if (colony == null)
                continue;

            var sourceTower = snapshot.DefensiveStructures
                .Where(s => s.ColonyId == colony.Id && IsMatchingTowerKind(s.Kind, catapultShot, arrowTowerShot))
                .FirstOrDefault();
            if (sourceTower == null)
                continue;
            if (!RenderLayout.IsVisible(visibleTiles, sourceTower.X, sourceTower.Y))
                continue;

            var source = TileCenter(sourceTower.X, sourceTower.Y, settings.TileSize);
            var target = predatorShot
                ? FindNearestPredator(snapshot, sourceTower.X, sourceTower.Y, settings.TileSize)
                : FindNearestHostile(snapshot, colony.FactionId, sourceTower.X, sourceTower.Y, settings.TileSize);

            if (target == null)
                continue;

            var beamColor = predatorShot
                ? new Color(248, 217, 114)
                : catapultShot
                    ? new Color(230, 130, 60)
                    : new Color(236, 142, 121);
            int thickness = Math.Max(1, settings.TileSize / (catapultShot ? 2 : 3));
            DrawBeam(spriteBatch, textures.Pixel, source, target.Value, beamColor, thickness);

            if (catapultShot)
            {
                int splash = Math.Max(2, settings.TileSize / 2);
                spriteBatch.Draw(textures.Pixel, new Rectangle((int)target.Value.X - splash, (int)target.Value.Y - splash, splash * 2, splash * 2), beamColor * 0.45f);
            }
        }
    }

    private static string ResolveTowerEventSuffix(string evt, bool predatorShot, bool catapultShot, bool arrowTowerShot)
    {
        if (predatorShot)
            return " tower hit predator";
        if (catapultShot)
            return " catapult fired";
        if (arrowTowerShot)
            return " arrow tower fired";
        if (evt.Contains("watchtower fired", StringComparison.OrdinalIgnoreCase))
            return " watchtower fired";
        return " tower fired";
    }

    private static bool IsMatchingTowerKind(DefensiveStructureKindView kind, bool catapultShot, bool arrowTowerShot)
    {
        if (catapultShot)
            return kind == DefensiveStructureKindView.CatapultTower;
        if (arrowTowerShot)
            return kind == DefensiveStructureKindView.ArrowTower;
        return kind == DefensiveStructureKindView.Watchtower || kind == DefensiveStructureKindView.ArrowTower;
    }

    private static Vector2? FindNearestPredator(WorldRenderSnapshot snapshot, int towerX, int towerY, int tileSize)
    {
        var predator = snapshot.Animals
            .Where(a => a.Kind == AnimalKindView.Predator)
            .Select(a => new { Animal = a, Dist = Math.Abs(a.X - towerX) + Math.Abs(a.Y - towerY) })
            .OrderBy(e => e.Dist)
            .FirstOrDefault();
        return predator == null ? null : TileCenter(predator.Animal.X, predator.Animal.Y, tileSize);
    }

    private static Vector2? FindNearestHostile(WorldRenderSnapshot snapshot, int factionId, int towerX, int towerY, int tileSize)
    {
        var factionByColonyId = snapshot.Colonies
            .ToDictionary(colony => colony.Id, colony => colony.FactionId);

        var target = snapshot.People
            .Where(person => factionByColonyId.TryGetValue(person.ColonyId, out var targetFactionId)
                             && IsHostileStance(snapshot, factionId, targetFactionId))
            .Select(p => new { Person = p, Dist = Math.Abs(p.X - towerX) + Math.Abs(p.Y - towerY) })
            .OrderBy(e => e.Dist)
            .FirstOrDefault();
        return target == null ? null : TileCenter(target.Person.X, target.Person.Y, tileSize);
    }

    private static bool IsHostileStance(WorldRenderSnapshot snapshot, int sourceFactionId, int targetFactionId)
    {
        if (sourceFactionId == targetFactionId)
            return false;

        var direct = snapshot.FactionStances
            .FirstOrDefault(s => s.LeftFactionId == sourceFactionId && s.RightFactionId == targetFactionId);
        var stance = direct?.Stance;
        if (string.IsNullOrWhiteSpace(stance))
        {
            var reverse = snapshot.FactionStances
                .FirstOrDefault(s => s.LeftFactionId == targetFactionId && s.RightFactionId == sourceFactionId);
            stance = reverse?.Stance;
        }

        return string.Equals(stance, "Hostile", StringComparison.OrdinalIgnoreCase)
               || string.Equals(stance, "War", StringComparison.OrdinalIgnoreCase);
    }

    private static Vector2 TileCenter(int tileX, int tileY, int tileSize)
        => new((tileX * tileSize) + (tileSize * 0.5f), (tileY * tileSize) + (tileSize * 0.5f));

    private static void DrawBeam(SpriteBatch spriteBatch, Texture2D pixel, Vector2 source, Vector2 target, Color color, int thickness)
    {
        var delta = target - source;
        float length = delta.Length();
        if (length < 1f)
            return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        var rect = new Rectangle((int)source.X, (int)source.Y, (int)length, thickness);
        spriteBatch.Draw(pixel, rect, null, color, angle, new Vector2(0f, thickness * 0.5f), SpriteEffects.None, 0f);
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
