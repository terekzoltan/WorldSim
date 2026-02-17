using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class ActorRenderPass
{
    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, TextureCatalog textures, WorldRenderSettings settings, WorldRenderTheme theme)
    {
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
            }
            else
            {
                spriteBatch.Draw(textures.Pixel, new Rectangle(dx, dy, settings.TileSize, settings.TileSize), Color.White);
            }
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
