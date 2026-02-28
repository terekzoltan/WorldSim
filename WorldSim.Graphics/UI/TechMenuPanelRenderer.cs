using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class TechMenuPanelRenderer
{
    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        TechMenuView menu,
        int viewportWidth,
        int viewportHeight,
        HudTheme theme)
    {
        int panelWidth = Math.Min(720, viewportWidth - 24);
        int panelHeight = Math.Min(viewportHeight - 24, 320);
        int x = 12;
        int y = 12;

        var rect = new Rectangle(x, y, panelWidth, panelHeight);
        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int lineY = y + 10;
        spriteBatch.DrawString(
            font,
            $"Tech Tree - Colony {menu.ColonyId} (Left/Right to change, F1 to close)",
            new Vector2(x + 10, lineY),
            theme.PrimaryText);
        lineY += 26;

        for (var i = 0; i < menu.LockedTechNames.Count && i < 9; i++)
        {
            spriteBatch.DrawString(font, $"{i + 1}. {menu.LockedTechNames[i]}", new Vector2(x + 10, lineY), theme.PrimaryText);
            lineY += 20;
        }

        if (menu.LockedTechNames.Count == 0)
            spriteBatch.DrawString(font, "All technologies unlocked.", new Vector2(x + 10, lineY), theme.SuccessText);
    }
}
