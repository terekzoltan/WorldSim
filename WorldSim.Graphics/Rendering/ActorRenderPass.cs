using System;
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

        DrawPeople(spriteBatch, snapshot, textures, settings, theme);
        DrawAnimals(spriteBatch, snapshot, textures, settings, theme);
    }

    private static void DrawPeople(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings, WorldRenderTheme theme)
    {
        foreach (var person in snapshot.People)
        {
            var dx = person.X * settings.TileSize;
            var dy = person.Y * settings.TileSize;
            var icon = textures.GetPersonIcon(person.ColonyId);
            Rectangle bounds;

            if (icon != null)
            {
                var iconSize = (int)MathF.Round(settings.TileSize * settings.IconScale);
                var iconX = dx + (settings.TileSize - iconSize) / 2;
                var iconY = dy + (settings.TileSize - iconSize) / 2;
                bounds = new Rectangle(iconX, iconY, iconSize, iconSize);
                spriteBatch.Draw(icon, bounds, Color.White);
            }
            else
            {
                bounds = new Rectangle(dx, dy, settings.TileSize, settings.TileSize);
                spriteBatch.Draw(textures.Pixel, bounds, Color.White);
            }

            DrawHealthBar(spriteBatch, textures, person, bounds, settings, theme);
            DrawCombatMarker(spriteBatch, textures, person, bounds, settings, theme);
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
        if (!person.IsInCombat)
            return;

        int thickness = Math.Max(1, settings.TileSize / 4);
        var marker = new Rectangle(actorBounds.X - 1, actorBounds.Y - 1, actorBounds.Width + 2, actorBounds.Height + 2);
        var color = theme.Warning * 0.9f;

        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Y, marker.Width, thickness), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Bottom - thickness, marker.Width, thickness), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.X, marker.Y, thickness, marker.Height), color);
        spriteBatch.Draw(textures.Pixel, new Rectangle(marker.Right - thickness, marker.Y, thickness, marker.Height), color);
    }

    private static void DrawAnimals(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings, WorldRenderTheme theme)
    {
        foreach (var animal in snapshot.Animals)
        {
            var color = animal.Kind == AnimalKindView.Predator ? theme.Predator : theme.Herbivore;
            spriteBatch.Draw(
                textures.Pixel,
                new Rectangle(animal.X * settings.TileSize, animal.Y * settings.TileSize, settings.TileSize, settings.TileSize),
                color);
        }
    }
}
