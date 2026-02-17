using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class EcologyPanelRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, WorldRenderSnapshot snapshot, int startY, HudTheme theme)
    {
        var y = startY;

        var worldLine = $"Season: {snapshot.CurrentSeason} | Drought: {(snapshot.IsDroughtActive ? "ON" : "off")}";
        spriteBatch.DrawString(font, worldLine, new Vector2(10, y), theme.AccentText);
        y += 20;

        var ecoLine =
            $"Eco: Herb {snapshot.Ecology.Herbivores}, Pred {snapshot.Ecology.Predators}, FoodNodes {snapshot.Ecology.ActiveFoodNodes}, FoodRegrow {snapshot.Ecology.DepletedFoodNodes}, CriticalHungry {snapshot.Ecology.CriticalHungry}, StuckFix {snapshot.Ecology.AnimalStuckRecoveries}, PredDeaths {snapshot.Ecology.PredatorDeaths}, PredHits {snapshot.Ecology.PredatorHumanHits}";
        spriteBatch.DrawString(font, ecoLine, new Vector2(10, y), theme.SecondaryText);
        y += 20;

        return y;
    }
}
