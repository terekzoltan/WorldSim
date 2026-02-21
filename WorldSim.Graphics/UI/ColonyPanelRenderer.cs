using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class ColonyPanelRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, IReadOnlyList<ColonyHudData> colonies, int startX, int startY, int maxWidth, HudTheme theme)
    {
        var y = startY;
        foreach (var colony in colonies)
        {
            var line =
                $"{colony.Name}({colony.Id}) Morale {colony.Morale:0}: Food {colony.Food}, Wood {colony.Wood}, Stone {colony.Stone}, Iron {colony.Iron}, Gold {colony.Gold}, Houses {colony.Houses}, Farm {colony.FarmPlots}, Work {colony.Workshops}, Store {colony.Storehouses}, Tools {colony.ToolCharges}, People {colony.People}, Fpp {colony.FoodPerPerson:0.0}, D[A:{colony.DeathsOldAge}/S:{colony.DeathsStarvation}/P:{colony.DeathsPredator}/O:{colony.DeathsOther}], AvgHun {colony.AverageHunger:0}, AvgSta {colony.AverageStamina:0}, Prof[{colony.ProfessionSummary}]";
            y = TextWrap.DrawWrapped(spriteBatch, font, line, new Vector2(startX, y), theme.PrimaryText, maxWidth, 20);
        }

        return y;
    }
}
