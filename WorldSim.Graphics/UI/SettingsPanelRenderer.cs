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
        string captureStatus,
        string directorProfile,
        string directorProfileSource,
        string directorLane,
        string directorRequestedMode,
        string directorRequestedSource,
        string directorEffectiveMode,
        string directorEffectiveSource,
        string directorStage,
        string directorApply,
        bool directorHasBudgetData,
        double directorMaxBudget,
        double directorLastBudgetUsed,
        string directorFailureDetail)
    {
        int width = Math.Min(520, viewportWidth - 24);
        int height = Math.Min(viewportHeight - 24, 470);
        int x = viewportWidth - width - 12;
        int y = 12;
        int contentX = x + 10;
        int contentWidth = width - 20;

        var rect = new Rectangle(x, y, width, height);
        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int lineY = y + 10;
        spriteBatch.DrawString(font, "Settings Overlay", new Vector2(contentX, lineY), theme.AccentText);
        lineY += 26;

        spriteBatch.DrawString(font, "General", new Vector2(contentX, lineY), theme.AccentText);
        lineY += 20;
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Requested visual lane: {quality} (Ctrl+F5)", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"PostFx: {postFx} (Ctrl+F3, Ctrl+F4)", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"HUD scale: {hudScale} (locked)", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Cinematic route: {cinematic} (Ctrl+F9)", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, "Screenshot: Ctrl+F10 | CleanShot: F12", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, "Panels: Ctrl+F1 diplomacy, Ctrl+F2 campaign, Ctrl+F12 settings", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, "Overlays: Ctrl+F7 territory, Ctrl+F8 combat", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, "Debug: F8 AI panel | F6 trigger refinery", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);

        lineY += 4;
        spriteBatch.DrawString(font, "Director Control State", new Vector2(contentX, lineY), theme.AccentText);
        lineY += 20;
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Profile: {directorProfile} ({directorProfileSource})", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Lane: {directorLane} | Preset cycle: Ctrl+Shift+F6", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Requested mode: {directorRequestedMode} ({directorRequestedSource}) | Ctrl+F6", new Vector2(contentX, lineY), theme.PrimaryText, contentWidth, 18);

        lineY += 4;
        spriteBatch.DrawString(font, "Director Effective State", new Vector2(contentX, lineY), theme.AccentText);
        lineY += 20;
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Effective mode: {directorEffectiveMode} ({directorEffectiveSource})", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);
        lineY = TextWrap.DrawWrapped(spriteBatch, font, $"Stage: {directorStage} | Apply: {directorApply}", new Vector2(contentX, lineY), theme.SecondaryText, contentWidth, 18);

        lineY += 2;
        lineY = DrawChip(spriteBatch, pixel, font, $"apply:{directorApply}", GetApplyChipColor(theme, directorApply), contentX, lineY);
        if (directorHasBudgetData && directorMaxBudget > 0d)
        {
            var usedRatio = Math.Clamp((float)(directorLastBudgetUsed / directorMaxBudget), 0f, 1f);
            lineY = DrawMiniBar(
                spriteBatch,
                pixel,
                font,
                $"Budget used: {directorLastBudgetUsed:0.###}/{directorMaxBudget:0.###}",
                usedRatio,
                theme.WarningText,
                contentX,
                lineY,
                contentWidth);
        }

        if (!string.IsNullOrWhiteSpace(directorFailureDetail))
        {
            lineY += 4;
            spriteBatch.DrawString(font, "Failure / Diagnostics", new Vector2(contentX, lineY), theme.AccentText);
            lineY += 20;
            lineY = TextWrap.DrawWrapped(spriteBatch, font, directorFailureDetail, new Vector2(contentX, lineY), theme.WarningText, contentWidth, 18);
        }

        lineY += 4;
        TextWrap.DrawWrapped(spriteBatch, font, $"Capture: {captureStatus}", new Vector2(contentX, lineY), theme.SuccessText, contentWidth, 18);
    }

    private static int DrawChip(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        string label,
        Color color,
        int x,
        int y)
    {
        var chipWidth = Math.Max(42, (int)font.MeasureString(label).X + 12);
        var rect = new Rectangle(x, y + 2, chipWidth, 16);
        var background = color * 0.22f;
        spriteBatch.Draw(pixel, rect, background);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        spriteBatch.DrawString(font, label, new Vector2(x + 6, y), Color.White);
        return y + 20;
    }

    private static int DrawMiniBar(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        string label,
        float ratio,
        Color fill,
        int x,
        int y,
        int width)
    {
        var nextY = TextWrap.DrawWrapped(spriteBatch, font, label, new Vector2(x, y), Color.LightGray, width, 18);
        const int barHeight = 7;
        var barY = nextY + 2;
        var barWidth = Math.Min(180, Math.Max(100, width - 12));
        var rect = new Rectangle(x, barY, barWidth, barHeight);
        spriteBatch.Draw(pixel, rect, Color.LightGray * 0.2f);
        var fillWidth = Math.Clamp((int)Math.Round(barWidth * Math.Clamp(ratio, 0f, 1f)), 0, barWidth);
        if (fillWidth > 0)
            spriteBatch.Draw(pixel, new Rectangle(x, barY, fillWidth, barHeight), fill);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.LightGray * 0.8f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.LightGray * 0.8f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.LightGray * 0.8f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.LightGray * 0.8f);
        return barY + barHeight + 6;
    }

    private static Color GetApplyChipColor(HudTheme theme, string apply)
    {
        return apply switch
        {
            "applied" => theme.SuccessText,
            "apply_failed" => theme.WarningText,
            "request_failed" => theme.WarningText,
            _ => theme.StatusText
        };
    }
}
