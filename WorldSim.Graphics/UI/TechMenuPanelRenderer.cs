using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class TechMenuPanelRenderer
{
    public void Draw(SpriteBatch spriteBatch, SpriteFont font, TechMenuView menu, HudTheme theme)
    {
        var y = 100;
        spriteBatch.DrawString(
            font,
            $"-- Tech Tree for Colony {menu.ColonyId} (Left/Right to change, F1 to close) --",
            new Vector2(0, y),
            theme.PrimaryText);
        y += 20;

        for (var i = 0; i < menu.LockedTechNames.Count && i < 9; i++)
        {
            spriteBatch.DrawString(font, $"{i + 1}. {menu.LockedTechNames[i]}", new Vector2(0, y), theme.PrimaryText);
            y += 20;
        }
    }
}
