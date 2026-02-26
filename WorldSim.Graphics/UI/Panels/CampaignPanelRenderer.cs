using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI.Panels;

public sealed class CampaignPanelRenderer
{
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int viewportWidth, int viewportHeight, HudTheme theme)
    {
        int width = Math.Min(460, viewportWidth - 28);
        int height = 170;
        int x = viewportWidth - width - 14;
        int y = viewportHeight - height - 14;
        var rect = new Rectangle(x, y, width, height);

        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int ty = y + 10;
        spriteBatch.DrawString(font, "Campaign Panel (Track A scaffold)", new Vector2(x + 10, ty), theme.AccentText);
        ty += 24;
        spriteBatch.DrawString(font, "- Active campaign list hook: READY", new Vector2(x + 10, ty), theme.PrimaryText);
        ty += 20;
        spriteBatch.DrawString(font, "- Army supply/progress hook: READY", new Vector2(x + 10, ty), theme.PrimaryText);
        ty += 20;
        spriteBatch.DrawString(font, "- Waiting for runtime campaign snapshot", new Vector2(x + 10, ty), theme.SecondaryText);
    }
}
