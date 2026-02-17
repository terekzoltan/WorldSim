using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class ColonyPanelRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, IReadOnlyList<ColonyHudData> colonies, int startY, HudTheme theme)
    {
        var y = startY;
        foreach (var colony in colonies)
        {
            var line =
                $"{colony.Name}({colony.Id}) Morale {colony.Morale:0}: Food {colony.Food}, Wood {colony.Wood}, Stone {colony.Stone}, Iron {colony.Iron}, Gold {colony.Gold}, Houses {colony.Houses}, People {colony.People}, AvgHun {colony.AverageHunger:0}, AvgSta {colony.AverageStamina:0}, Prof[{colony.ProfessionSummary}]";
            spriteBatch.DrawString(font, line, new Vector2(10, y), theme.PrimaryText);
            y += 20;
        }

        return y;
    }
}
