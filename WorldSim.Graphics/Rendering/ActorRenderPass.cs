using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class ActorRenderPass : IRenderPass
{
    public string Name => "Actors";

    public void Draw(in RenderFrameContext context)
    {
        var spriteBatch = context.SpriteBatch;
        var snapshot = context.Snapshot;
        var textures = context.Textures;
        var settings = context.Settings;
        var theme = context.Theme;
        var visibleTiles = context.VisibleTileBounds;

        DrawPeople(spriteBatch, snapshot, textures, settings, theme, visibleTiles);
        DrawAnimals(spriteBatch, snapshot, textures, settings, theme, visibleTiles);
    }

    private static void DrawPeople(
        SpriteBatch spriteBatch,
        WorldRenderSnapshot snapshot,
        TextureCatalog textures,
        WorldRenderSettings settings,
        WorldRenderTheme theme,
        TileBounds visibleTiles)
    {
        foreach (var person in snapshot.People)
        {
            if (!RenderLayout.IsVisible(visibleTiles, person.X, person.Y))
                continue;

            var icon = textures.GetPersonIcon(person.ColonyId);
            var bounds = RenderLayout.BottomAnchoredInTile(person.X, person.Y, settings.TileSize, settings.PersonScale);
            RenderLayout.DrawGroundShadow(spriteBatch, textures.Pixel, bounds, settings.ActorShadowAlpha);

            if (icon != null)
            {
                spriteBatch.Draw(icon, bounds, Color.White);
            }
            else
            {
                spriteBatch.Draw(textures.Pixel, bounds, Color.White * 0.92f);
            }

            DrawHealthBar(spriteBatch, textures, person, bounds, settings, theme);
            DrawCombatMarker(spriteBatch, textures, person, bounds, settings, theme);
            DrawBattleMarkers(spriteBatch, textures, person, bounds, settings, theme);
        }

        DrawStackDebugMarkers(spriteBatch, textures, snapshot, settings, visibleTiles);
    }

    private static void DrawStackDebugMarkers(
        SpriteBatch spriteBatch,
        TextureCatalog textures,
        WorldRenderSnapshot snapshot,
        WorldRenderSettings settings,
        TileBounds visibleTiles)
    {
        var stacks = snapshot.People
            .GroupBy(person => (person.X, person.Y))
            .Where(group => RenderLayout.IsVisible(visibleTiles, group.Key.X, group.Key.Y))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.Y)
            .ThenBy(group => group.Key.X);

        int tile = settings.TileSize;
        int border = Math.Max(1, tile / 5);

        foreach (var stack in stacks)
        {
            int count = stack.Count();
            bool hasNoProgress = stack.Any(person => person.NoProgressStreak > 0 || person.BackoffTicksRemaining > 0);

            var baseColor = count switch
            {
                >= 5 => new Color(221, 79, 79),
                >= 3 => new Color(233, 160, 95),
                _ => new Color(231, 210, 112)
            };

            if (hasNoProgress)
                baseColor = new Color(232, 118, 224);

            var accent = baseColor * (hasNoProgress ? 0.88f : 0.72f);
            int x = stack.Key.X * tile;
            int y = stack.Key.Y * tile;
            var rect = new Rectangle(x, y, tile, tile);

            spriteBatch.Draw(textures.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, border), accent);
            spriteBatch.Draw(textures.Pixel, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), accent);
            spriteBatch.Draw(textures.Pixel, new Rectangle(rect.X, rect.Y, border, rect.Height), accent);
            spriteBatch.Draw(textures.Pixel, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), accent);

            int pips = Math.Clamp(count, 2, 6);
            int pipWidth = Math.Max(1, tile / 6);
            int pipHeight = Math.Max(2, tile / 3);
            int startX = rect.X + 1;
            int topY = Math.Max(0, rect.Y - pipHeight - 1);
            for (int i = 0; i < pips; i++)
            {
                int px = startX + (i * (pipWidth + 1));
                if (px >= rect.Right)
                    break;
                spriteBatch.Draw(textures.Pixel, new Rectangle(px, topY, pipWidth, pipHeight), accent);
            }
        }
    }

    private static void DrawHealthBar(
        SpriteBatch spriteBatch,
        TextureCatalog textures,
        PersonRenderData person,
        Rectangle actorBounds,
        WorldRenderSettings settings,
        WorldRenderTheme theme)
    {
        var normalizedHealth = Math.Clamp(person.Health / 100f, 0f, 1f);
        if (normalizedHealth >= 0.995f)
            return;

        int barWidth = Math.Max(settings.TileSize + 4, actorBounds.Width);
        int barHeight = Math.Max(2, settings.TileSize / 3);
        int x = actorBounds.X + (actorBounds.Width - barWidth) / 2;
        int y = actorBounds.Y - barHeight - 2;

        if (y < 0)
            y = actorBounds.Bottom + 1;

        var barRect = new Rectangle(x, y, barWidth, barHeight);
        spriteBatch.Draw(textures.Pixel, barRect, new Color(12, 16, 20, 205));

        int innerWidth = Math.Max(1, barWidth - 2);
        int filledWidth = Math.Clamp((int)MathF.Round(innerWidth * normalizedHealth), 1, innerWidth);
        var fillRect = new Rectangle(x + 1, y + 1, filledWidth, Math.Max(1, barHeight - 2));

        Color fillColor = normalizedHealth switch
        {
            < 0.35f => theme.Warning,
            < 0.7f => theme.Highlight,
            _ => theme.Success
        };

        spriteBatch.Draw(textures.Pixel, fillRect, fillColor);
    }

    private static void DrawCombatMarker(
        SpriteBatch spriteBatch,
        TextureCatalog textures,
        PersonRenderData person,
        Rectangle actorBounds,
        WorldRenderSettings settings,
        WorldRenderTheme theme)
    {
        if (!person.IsInCombat || person.IsRouting)
            return;

        int thickness = Math.Max(1, settings.TileSize / 4);
        var marker = new Rectangle(actorBounds.X - 1, actorBounds.Y - 1, actorBounds.Width + 2, actorBounds.Height + 2);
        var color = theme.Warning * 0.9f;

        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Y, marker.Width, thickness), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Bottom - thickness, marker.Width, thickness), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Y, thickness, marker.Height), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.Right - thickness, marker.Y, thickness, marker.Height), color);
    }

    private static void DrawBattleMarkers(
        SpriteBatch spriteBatch,
        TextureCatalog textures,
        PersonRenderData person,
        Rectangle actorBounds,
        WorldRenderSettings settings,
        WorldRenderTheme theme)
    {
        bool inBattleContext = person.ActiveBattleId >= 0 || person.IsRouting;
        if (!inBattleContext)
            return;

        var morale = Math.Clamp(person.CombatMorale / 100f, 0f, 1f);
        int barWidth = Math.Max(actorBounds.Width, settings.TileSize);
        int barHeight = Math.Max(1, settings.TileSize / 4);
        int x = actorBounds.X + (actorBounds.Width - barWidth) / 2;
        int y = actorBounds.Bottom + 1;

        spriteBatch.Draw(textures.Pixel, new Rectangle(x, y, barWidth, barHeight), new Color(14, 18, 24, 190));
        int innerWidth = Math.Max(1, barWidth - 2);
        int fillWidth = Math.Clamp((int)MathF.Round(innerWidth * morale), 1, innerWidth);
        var moraleColor = morale switch
        {
            < 0.30f => new Color(222, 92, 86),
            < 0.65f => new Color(236, 188, 106),
            _ => theme.Success
        };
        spriteBatch.Draw(textures.Pixel, new Rectangle(x + 1, y + 1, fillWidth, Math.Max(1, barHeight - 2)), moraleColor);

        if (person.IsRouting)
        {
            int markerSize = Math.Max(2, settings.TileSize / 3);
            var routeColor = new Color(235, 121, 205) * 0.92f;
            var centerX = actorBounds.X + actorBounds.Width / 2;
            var centerY = actorBounds.Y + actorBounds.Height / 2;
            spriteBatch.Draw(textures.Pixel, new Rectangle(centerX - markerSize / 2, centerY - markerSize / 2, markerSize, markerSize), routeColor);
            spriteBatch.Draw(textures.Pixel, new Rectangle(centerX + markerSize / 2, centerY - markerSize / 2, markerSize, markerSize), routeColor * 0.85f);
            spriteBatch.Draw(textures.Pixel, new Rectangle(centerX + markerSize - 1, centerY - markerSize / 2 + 1, markerSize, markerSize), routeColor * 0.7f);
        }

        if (person.IsCommander)
        {
            int badge = Math.Max(1, settings.TileSize / 3);
            var commanderColor = new Color(130, 214, 255) * 0.9f;
            spriteBatch.Draw(textures.Pixel, new Rectangle(actorBounds.Right - badge - 1, actorBounds.Y + 1, badge, badge), commanderColor);
        }
    }

    private static void DrawAnimals(
        SpriteBatch spriteBatch,
        WorldRenderSnapshot snapshot,
        TextureCatalog textures,
        WorldRenderSettings settings,
        WorldRenderTheme theme,
        TileBounds visibleTiles)
    {
        foreach (var animal in snapshot.Animals)
        {
            if (!RenderLayout.IsVisible(visibleTiles, animal.X, animal.Y))
                continue;

            var color = animal.Kind == AnimalKindView.Predator ? theme.Predator : theme.Herbivore;
            var bounds = RenderLayout.BottomAnchoredInTile(animal.X, animal.Y, settings.TileSize, settings.AnimalScale);
            RenderLayout.DrawGroundShadow(spriteBatch, textures.Pixel, bounds, settings.ActorShadowAlpha * 0.9f);

            var icon = textures.GetAnimalIcon(animal.Kind);
            if (icon != null)
            {
                spriteBatch.Draw(icon, bounds, Color.White);
                continue;
            }

            DrawAnimalFallback(spriteBatch, textures.Pixel, bounds, color, animal.Kind == AnimalKindView.Predator);
        }
    }

    private static void DrawAnimalFallback(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color baseColor,
        bool isPredator)
    {
        var body = Shrink(bounds, isPredator ? 0.9f : 0.84f, isPredator ? 0.72f : 0.78f, yBias: 0.08f);
        spriteBatch.Draw(pixel, body, baseColor);

        if (isPredator)
        {
            var head = Shrink(bounds, 0.46f, 0.4f, yBias: -0.08f);
            spriteBatch.Draw(pixel, head, Color.Lerp(baseColor, Color.Black, 0.25f));
            spriteBatch.Draw(pixel, new Rectangle(head.Right - 1, head.Y, 1, 1), new Color(255, 232, 214));
        }
        else
        {
            var ear = Shrink(bounds, 0.36f, 0.3f, yBias: -0.12f);
            spriteBatch.Draw(pixel, ear, Color.Lerp(baseColor, Color.White, 0.12f));
            spriteBatch.Draw(pixel, new Rectangle(ear.X, ear.Y, 1, 1), Color.White * 0.45f);
        }
    }

    private static Rectangle Shrink(Rectangle rect, float widthFactor, float heightFactor, float yBias = 0f)
    {
        int width = Math.Max(1, (int)MathF.Round(rect.Width * widthFactor));
        int height = Math.Max(1, (int)MathF.Round(rect.Height * heightFactor));
        int x = rect.Center.X - (width / 2);
        int y = rect.Center.Y - (height / 2) + (int)MathF.Round(rect.Height * yBias);
        return new Rectangle(x, y, width, height);
    }
}
