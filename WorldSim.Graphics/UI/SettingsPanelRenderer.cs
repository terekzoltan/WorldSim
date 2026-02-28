using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class SettingsPanelRenderer
{
    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        int viewportWidth,
        int viewportHeight,
        HudTheme theme,
        string quality,
        string postFx,
        string hudScale,
        string cinematic,
        string captureStatus)
    {
        int width = Math.Min(520, viewportWidth - 24);
        int height = 280;
        int x = viewportWidth - width - 12;
        int y = 12;

        var rect = new Rectangle(x, y, width, height);
        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int lineY = y + 10;
        spriteBatch.DrawString(font, "Settings Overlay", new Vector2(x + 10, lineY), theme.AccentText);
        lineY += 26;
        spriteBatch.DrawString(font, $"Quality Profile: {quality} (G)", new Vector2(x + 10, lineY), theme.PrimaryText);
        lineY += 20;
        spriteBatch.DrawString(font, $"PostFx: {postFx} (D toggle, F quality)", new Vector2(x + 10, lineY), theme.PrimaryText);
        lineY += 20;
        spriteBatch.DrawString(font, $"HUD Scale: {hudScale} (H)", new Vector2(x + 10, lineY), theme.PrimaryText);
        lineY += 20;
        spriteBatch.DrawString(font, $"Cinematic Route: {cinematic} (L)", new Vector2(x + 10, lineY), theme.PrimaryText);
        lineY += 20;
        spriteBatch.DrawString(font, "Screenshot: P | CleanShot: F12", new Vector2(x + 10, lineY), theme.SecondaryText);
        lineY += 20;
        spriteBatch.DrawString(font, "Panels: A diplomacy, S campaign, O settings", new Vector2(x + 10, lineY), theme.SecondaryText);
        lineY += 20;
        spriteBatch.DrawString(font, "Overlays: J territory, K combat", new Vector2(x + 10, lineY), theme.SecondaryText);
        lineY += 20;
        spriteBatch.DrawString(font, $"Capture: {captureStatus}", new Vector2(x + 10, lineY), theme.SuccessText);
    }
}
