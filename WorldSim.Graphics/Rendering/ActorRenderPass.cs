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

        DrawPeople(spriteBatch, snapshot, textures, settings);
        DrawAnimals(spriteBatch, snapshot, textures, settings, theme);
    }

    private static void DrawPeople(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings)
    {
        foreach (var person in snapshot.People)
        {
            var dx = person.X * settings.TileSize;
            var dy = person.Y * settings.TileSize;
            var icon = textures.GetPersonIcon(person.ColonyId);

            if (icon != null)
            {
                var iconSize = (int)MathF.Round(settings.TileSize * settings.IconScale);
                var iconX = dx + (settings.TileSize - iconSize) / 2;
                var iconY = dy + (settings.TileSize - iconSize) / 2;
                spriteBatch.Draw(icon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);

                DrawCombatIndicators(spriteBatch, textures.Pixel, person, iconX, iconY, iconSize);
            }
            else
            {
                spriteBatch.Draw(textures.Pixel, new Rectangle(dx, dy, settings.TileSize, settings.TileSize), Color.White);
                DrawCombatIndicators(spriteBatch, textures.Pixel, person, dx, dy, settings.TileSize);
            }
        }
    }

    private static void DrawCombatIndicators(SpriteBatch spriteBatch, Texture2D pixel, PersonRenderData person, int x, int y, int size)
    {
        if (person.Health < 100f)
        {
            int barWidth = Math.Max(2, size);
            int barHeight = 2;
            int barX = x;
            int barY = y - 3;
            float hpNorm = Math.Clamp(person.Health / 100f, 0f, 1f);
            int fill = Math.Max(1, (int)MathF.Round(barWidth * hpNorm));

            spriteBatch.Draw(pixel, new Rectangle(barX, barY, barWidth, barHeight), new Color(25, 25, 25, 200));
            var hpColor = hpNorm >= 0.5f ? new Color(102, 224, 120) : new Color(230, 94, 84);
            spriteBatch.Draw(pixel, new Rectangle(barX, barY, fill, barHeight), hpColor);
        }

        if (person.IsInCombat)
        {
            int marker = Math.Max(1, size / 4);
            spriteBatch.Draw(pixel, new Rectangle(x + size - marker, y, marker, marker), new Color(255, 196, 84));
        }
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
