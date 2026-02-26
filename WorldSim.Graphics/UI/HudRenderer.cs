using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Rendering;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class HudRenderer
{
    private readonly ColonyPanelRenderer _colonyPanel = new();
    private readonly EcologyPanelRenderer _ecologyPanel = new();
    private readonly EventFeedRenderer _eventFeed = new();
    private readonly TechMenuPanelRenderer _techMenuPanel = new();
    private readonly AiDebugPanelRenderer _aiDebugPanel = new();

    public HudTheme Theme { get; private set; }

    public HudRenderer(HudTheme? theme = null)
    {
        Theme = theme ?? HudTheme.Default;
    }

    public void SetTheme(HudTheme theme)
    {
        Theme = theme;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        WorldRenderSnapshot snapshot,
        string refineryStatus,
        string plannerStatus,
        TechMenuView? techMenu,
        AiDebugSnapshot aiDebug,
        bool showAiDebug,
        bool aiDebugCompact,
        int aiScoreOffset,
        int aiHistoryOffset,
        int viewportWidth,
        int viewportHeight,
        RenderStats? renderStats = null,
        float opacity = 1f)
    {
        var drawTheme = ApplyOpacity(Theme, opacity);
        var margin = 14;
        var leftX = margin;
        var panelWidth = Math.Max(420, (int)(viewportWidth * 0.58f));
        var contentWidth = Math.Max(220, panelWidth - 26);

        var visibleEventCount = Math.Min(snapshot.RecentEvents.Count, 8);
        var baseHeight = 120 + (snapshot.Colonies.Count * 44) + 84 + (visibleEventCount * 26);
        if (renderStats != null)
            baseHeight += 24;
        var panelHeight = Math.Clamp(baseHeight, 220, Math.Max(220, viewportHeight - 40));

        DrawPanel(spriteBatch, pixel, new Rectangle(leftX - 8, 8, panelWidth, panelHeight), drawTheme);

        var y = 18;
        y = _colonyPanel.Draw(spriteBatch, font, snapshot.Colonies, leftX, y, contentWidth, drawTheme);
        y = _ecologyPanel.Draw(spriteBatch, font, snapshot, leftX, y + 4, contentWidth, drawTheme);
        y = _eventFeed.Draw(spriteBatch, font, snapshot.RecentEvents.Take(visibleEventCount).ToList(), leftX, y + 4, contentWidth, drawTheme);
        y = TextWrap.DrawWrapped(spriteBatch, font, refineryStatus, new Vector2(leftX, y + 10), drawTheme.StatusText, contentWidth, 20);
        y = TextWrap.DrawWrapped(spriteBatch, font, plannerStatus, new Vector2(leftX, y), drawTheme.StatusText, contentWidth, 20);

        if (renderStats != null)
        {
            var passSummary = string.Join(" | ", renderStats.PassSamples.Select(s => $"{s.PassName}:{s.Milliseconds:0.00}ms"));
            TextWrap.DrawWrapped(
                spriteBatch,
                font,
                $"Frame {renderStats.LastFrameMilliseconds:0.00}ms | Avg {renderStats.AverageFrameMilliseconds:0.00}ms | P99 {renderStats.P99FrameMilliseconds:0.00}ms | {passSummary}",
                new Vector2(leftX, y),
                drawTheme.SecondaryText,
                contentWidth,
                20);
        }

        if (techMenu == null)
        {
            if (showAiDebug)
                _aiDebugPanel.Draw(spriteBatch, font, aiDebug, viewportWidth, drawTheme, aiDebugCompact, aiScoreOffset, aiHistoryOffset);
            return;
        }

        _techMenuPanel.Draw(spriteBatch, font, techMenu.Value, drawTheme);

        if (showAiDebug)
            _aiDebugPanel.Draw(spriteBatch, font, aiDebug, viewportWidth, drawTheme, aiDebugCompact, aiScoreOffset, aiHistoryOffset);
    }

    private void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, HudTheme theme)
    {
        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);
    }

    private static HudTheme ApplyOpacity(HudTheme theme, float opacity)
    {
        opacity = Math.Clamp(opacity, 0f, 1f);
        return theme with
        {
            PrimaryText = theme.PrimaryText * opacity,
            SecondaryText = theme.SecondaryText * opacity,
            AccentText = theme.AccentText * opacity,
            EventText = theme.EventText * opacity,
            StatusText = theme.StatusText * opacity,
            PanelBackground = theme.PanelBackground * opacity,
            PanelBorder = theme.PanelBorder * opacity,
            WarningText = theme.WarningText * opacity,
            SuccessText = theme.SuccessText * opacity
        };
    }
}

public readonly record struct TechMenuView(int ColonyId, IReadOnlyList<string> LockedTechNames);
