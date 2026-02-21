using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class EcologyPanelRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, WorldRenderSnapshot snapshot, int startX, int startY, int maxWidth, HudTheme theme)
    {
        var y = startY;

        var worldLine = $"Season: {snapshot.CurrentSeason} | Drought: {(snapshot.IsDroughtActive ? "ON" : "off")}";
        y = TextWrap.DrawWrapped(spriteBatch, font, worldLine, new Vector2(startX, y), theme.AccentText, maxWidth, 20);

        var ecoLine =
            $"Eco: Herb {snapshot.Ecology.Herbivores}, Pred {snapshot.Ecology.Predators}, FoodNodes {snapshot.Ecology.ActiveFoodNodes}, FoodRegrow {snapshot.Ecology.DepletedFoodNodes}, CriticalHungry {snapshot.Ecology.CriticalHungry}, AvgFoodPerPerson {snapshot.Ecology.AverageFoodPerPerson:0.0}, EmergencyCols {snapshot.Ecology.ColoniesInFoodEmergency}, StuckFix {snapshot.Ecology.AnimalStuckRecoveries}, PredDeaths {snapshot.Ecology.PredatorDeaths}, PredHits {snapshot.Ecology.PredatorHumanHits}";
        y = TextWrap.DrawWrapped(spriteBatch, font, ecoLine, new Vector2(startX, y), theme.SecondaryText, maxWidth, 20);

        var deathLine =
            $"Deaths: Age {snapshot.Ecology.DeathsOldAge}, Starv {snapshot.Ecology.DeathsStarvation}, Pred {snapshot.Ecology.DeathsPredator}, Other {snapshot.Ecology.DeathsOther} | Pred->Human {(snapshot.Ecology.PredatorHumanAttacksEnabled ? "ON" : "off")}";
        y = TextWrap.DrawWrapped(spriteBatch, font, deathLine, new Vector2(startX, y), theme.SecondaryText, maxWidth, 20);

        return y;
    }
}
