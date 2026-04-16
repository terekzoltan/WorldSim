using System;
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
        string operatorSummary,
        string operatorProfile,
        string operatorDebugDetail,
        string operatorFailureDetail,
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
        float hudOpacity = 1f)
    {
        if (techMenu != null)
        {
            _techMenuPanel.Draw(spriteBatch, font, techMenu.Value, Theme);
            if (showAiDebug)
                _aiDebugPanel.Draw(spriteBatch, font, aiDebug, snapshot, viewportWidth, Theme, aiDebugCompact, aiScoreOffset, aiHistoryOffset);
            return;
        }

        var margin = 14;
        var leftX = margin;
        var panelWidth = Math.Max(420, (int)(viewportWidth * 0.58f));
        var contentWidth = Math.Max(220, panelWidth - 26);

        var visibleEventCount = Math.Min(snapshot.RecentEvents.Count, 8);
        var director = snapshot.Director;
        var hasDirectorData = director.ActiveDirectives.Count > 0
                              || director.PendingChains.Count > 0
                              || director.ActiveDomainModifiers.Count > 0
                              || director.ActiveGoalBiases.Count > 0
                              || director.BeatCooldownRemainingTicks > 0
                              || !string.Equals(director.LastActionStatus, "No director action", StringComparison.OrdinalIgnoreCase);

        var directorExtraLines = 0;
        if (hasDirectorData)
            directorExtraLines += 1;

        if (showAiDebug)
        {
            directorExtraLines += 1;

            if (director.PendingChains.Count > 0)
            {
                directorExtraLines += 1 + Math.Min(3, director.PendingChains.Count);
                directorExtraLines += director.PendingChains
                    .Take(3)
                    .Count(chain => !string.IsNullOrWhiteSpace(chain.LastFailureMessage));
            }

            if (director.HasBudgetData)
                directorExtraLines += 1;

            directorExtraLines += Math.Min(2, director.ActiveDomainModifiers.Count > 0 ? 1 : 0);
            directorExtraLines += Math.Min(2, director.ActiveGoalBiases.Count > 0 ? 1 : 0);
        }

        if (!string.IsNullOrWhiteSpace(operatorSummary))
            directorExtraLines += 1;

        if (showAiDebug && !string.IsNullOrWhiteSpace(operatorDebugDetail))
            directorExtraLines += 1;

        if (!string.IsNullOrWhiteSpace(operatorFailureDetail))
            directorExtraLines += 1;

        if (showAiDebug && !string.IsNullOrWhiteSpace(plannerStatus))
            directorExtraLines += 1;

        var baseHeight = 120 + (snapshot.Colonies.Count * 44) + 84 + (visibleEventCount * 26);
        baseHeight += directorExtraLines * 20;
        if (renderStats != null)
            baseHeight += 24;
        var panelHeight = Math.Clamp(baseHeight, 220, Math.Max(220, viewportHeight - 40));

        DrawPanel(spriteBatch, pixel, new Rectangle(leftX - 8, 8, panelWidth, panelHeight));

        var y = 18;
        y = _colonyPanel.Draw(spriteBatch, font, snapshot.Colonies, leftX, y, contentWidth, Theme);
        y = _ecologyPanel.Draw(spriteBatch, font, snapshot, leftX, y + 4, contentWidth, Theme);
        y = DrawSiegeStatus(spriteBatch, font, snapshot, leftX, y + 4, contentWidth);
        y = DrawDirectorStatus(spriteBatch, pixel, font, snapshot, leftX, y + 4, contentWidth, showAiDebug, operatorFailureDetail);
        y = _eventFeed.Draw(spriteBatch, font, snapshot.RecentEvents.Take(visibleEventCount).ToList(), leftX, y + 4, contentWidth, Theme);
        y = TextWrap.DrawWrapped(spriteBatch, font, operatorSummary, new Vector2(leftX, y + 10), Theme.StatusText, contentWidth, 20);
        y = DrawOperatorStatusChips(spriteBatch, pixel, font, snapshot.Director, operatorProfile, leftX, y + 2, contentWidth);

        if (showAiDebug && !string.IsNullOrWhiteSpace(operatorDebugDetail))
            y = TextWrap.DrawWrapped(spriteBatch, font, operatorDebugDetail, new Vector2(leftX, y), Theme.SecondaryText, contentWidth, 20);

        if (showAiDebug && !string.IsNullOrWhiteSpace(plannerStatus))
            y = TextWrap.DrawWrapped(spriteBatch, font, plannerStatus, new Vector2(leftX, y), Theme.StatusText, contentWidth, 20);

        if (renderStats != null)
        {
            var passSummary = string.Join(" | ", renderStats.PassSamples.Select(s => $"{s.PassName}:{s.Milliseconds:0.00}ms"));
            TextWrap.DrawWrapped(spriteBatch, font, $"Render {renderStats.LastFrameMilliseconds:0.00}ms | {passSummary}", new Vector2(leftX, y), Theme.SecondaryText, contentWidth, 20);
        }

        if (showAiDebug)
            _aiDebugPanel.Draw(spriteBatch, font, aiDebug, snapshot, viewportWidth, Theme, aiDebugCompact, aiScoreOffset, aiHistoryOffset);
    }

    private int DrawOperatorStatusChips(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        DirectorRenderState state,
        string operatorProfile,
        int startX,
        int startY,
        int maxWidth)
    {
        var chips = new[]
        {
            ($"apply:{state.ApplyStatus}", GetApplyChipColor(state.ApplyStatus)),
            ($"mode:{state.OutputMode}", Theme.DirectorEventText),
            ($"profile:{CompactId(operatorProfile, 18)}", Theme.PanelBorder)
        };

        var x = startX;
        var y = startY;
        var lineHeight = 20;
        var rightEdge = startX + maxWidth;

        foreach (var (label, color) in chips)
        {
            var chipWidth = Math.Max(42, (int)font.MeasureString(label).X + 12);
            if (x + chipWidth > rightEdge)
            {
                x = startX;
                y += lineHeight;
            }

            var rect = new Rectangle(x, y + 2, chipWidth, 16);
            var background = color * 0.22f;
            spriteBatch.Draw(pixel, rect, background);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
            spriteBatch.DrawString(font, label, new Vector2(x + 6, y), Color.White);

            x += chipWidth + 6;
        }

        return y + lineHeight;
    }

    private int DrawProgressBar(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        string label,
        float ratio,
        Color fill,
        int startX,
        int startY,
        int maxWidth)
    {
        var clampedRatio = Math.Clamp(ratio, 0f, 1f);
        var y = TextWrap.DrawWrapped(spriteBatch, font, label, new Vector2(startX, startY), Theme.SecondaryText, maxWidth, 18);

        const int barHeight = 8;
        var barY = y + 2;
        var barWidth = Math.Min(240, Math.Max(120, maxWidth - 10));
        var rect = new Rectangle(startX, barY, barWidth, barHeight);
        spriteBatch.Draw(pixel, rect, Theme.PanelBorder * 0.25f);

        var fillWidth = Math.Clamp((int)Math.Round(barWidth * clampedRatio), 0, barWidth);
        if (fillWidth > 0)
            spriteBatch.Draw(pixel, new Rectangle(startX, barY, fillWidth, barHeight), fill);

        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Theme.PanelBorder);

        return barY + barHeight + 6;
    }

    private Color GetApplyChipColor(string applyStatus)
    {
        return applyStatus switch
        {
            "applied" => Theme.SuccessText,
            "apply_failed" => Theme.WarningText,
            "request_failed" => Theme.WarningText,
            _ => Theme.StatusText
        };
    }

    private int DrawDirectorStatus(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        WorldRenderSnapshot snapshot,
        int startX,
        int startY,
        int maxWidth,
        bool showDebug,
        string operatorFailureDetail)
    {
        var state = snapshot.Director;
        var directive = state.ActiveDirectives.FirstOrDefault();
        var directiveLabel = directive == null
            ? "none"
            : $"{directive.Directive} C{directive.ColonyId} ({directive.RemainingTicks}/{directive.TotalTicks}t)";

        int y = TextWrap.DrawWrapped(
            spriteBatch,
            font,
            $"Directive: {directiveLabel}",
            new Vector2(startX, startY),
            Theme.SecondaryText,
            maxWidth,
            18);

        if (directive != null && directive.TotalTicks > 0)
        {
            var directiveRatio = directive.RemainingTicks / (float)Math.Max(1, directive.TotalTicks);
            y = DrawProgressBar(
                spriteBatch,
                pixel,
                font,
                $"Directive remaining: {directive.RemainingTicks}/{directive.TotalTicks}t",
                directiveRatio,
                Theme.DirectorEventText,
                startX,
                y,
                maxWidth);
        }

        if (!string.IsNullOrWhiteSpace(operatorFailureDetail))
        {
            y = TextWrap.DrawWrapped(
                spriteBatch,
                font,
                operatorFailureDetail,
                new Vector2(startX, y),
                Theme.WarningText,
                maxWidth,
                18);
        }

        if (!showDebug)
            return y;

        if (state.PendingChains.Count > 0)
        {
            y = TextWrap.DrawWrapped(
                spriteBatch,
                font,
                $"Dir chains: {state.PendingChains.Count}",
                new Vector2(startX, y),
                Theme.SecondaryText,
                maxWidth,
                18);

            foreach (var chain in state.PendingChains.Take(3))
            {
                var parent = CompactId(chain.ParentBeatId, 16);
                var followUp = CompactId(chain.FollowUpBeatId, 16);
                y = TextWrap.DrawWrapped(
                    spriteBatch,
                    font,
                    $"Chain: {parent} {chain.Status} win={chain.RemainingWindowTicks} -> {followUp}",
                    new Vector2(startX, y),
                    Theme.SecondaryText,
                    maxWidth,
                    18);

                if (!string.IsNullOrWhiteSpace(chain.LastFailureMessage))
                {
                    y = TextWrap.DrawWrapped(
                        spriteBatch,
                        font,
                        $"Chain fail: {CompactId(chain.LastFailureMessage, 72)}",
                        new Vector2(startX, y),
                        Theme.WarningText,
                        maxWidth,
                        18);
                }
            }
        }

        if (state.HasBudgetData)
        {
            var checkpointTickLabel = state.LastBudgetCheckpointTick >= 0
                ? state.LastBudgetCheckpointTick.ToString()
                : "n/a";
            var usedPct = state.MaxInfluenceBudget > 0d
                ? (state.LastCheckpointBudgetUsed / state.MaxInfluenceBudget) * 100d
                : 0d;
            var budgetRatio = state.MaxInfluenceBudget > 0d
                ? (float)(state.LastCheckpointBudgetUsed / state.MaxInfluenceBudget)
                : 0f;
            y = DrawProgressBar(
                spriteBatch,
                pixel,
                font,
                $"Budget used: {state.LastCheckpointBudgetUsed:0.###}/{state.MaxInfluenceBudget:0.###} ({usedPct:0.#}%) checkpoint={checkpointTickLabel}",
                budgetRatio,
                Theme.WarningText,
                startX,
                y,
                maxWidth);
            y = TextWrap.DrawWrapped(
                spriteBatch,
                font,
                $"Dir budget/cd: cd={state.BeatCooldownRemainingTicks}t remaining={state.RemainingInfluenceBudget:0.###}",
                new Vector2(startX, y),
                Theme.SecondaryText,
                maxWidth,
                18);
        }

        y = TextWrap.DrawWrapped(
            spriteBatch,
            font,
            $"Dir detail: stage={state.StageMarker} src={state.OutputModeSource}",
            new Vector2(startX, y),
            Theme.SecondaryText,
            maxWidth,
            18);

        if (state.ActiveDomainModifiers.Count > 0)
        {
            var mods = string.Join(", ", state.ActiveDomainModifiers
                .Take(3)
                .Select(mod => $"{mod.Domain}:{mod.EffectiveModifier:+0.00;-0.00}({mod.RemainingTicks}t)"));
            y = TextWrap.DrawWrapped(spriteBatch, font, $"Dir mods: {mods}", new Vector2(startX, y), Theme.SecondaryText, maxWidth, 18);
        }

        if (state.ActiveGoalBiases.Count > 0)
        {
            var biases = string.Join(", ", state.ActiveGoalBiases
                .Take(3)
                .Select(bias => $"C{bias.ColonyId}.{bias.GoalCategory}:{bias.EffectiveWeight:0.00}"));
            y = TextWrap.DrawWrapped(spriteBatch, font, $"Dir bias: {biases}", new Vector2(startX, y), Theme.SecondaryText, maxWidth, 18);
        }

        return y;
    }

    private static string CompactId(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "n/a";

        var trimmed = value.Trim();
        if (trimmed.Length <= maxChars)
            return trimmed;

        if (maxChars <= 3)
            return trimmed[..maxChars];

        return trimmed[..(maxChars - 3)] + "...";
    }

    private int DrawSiegeStatus(
        SpriteBatch spriteBatch,
        SpriteFont font,
        WorldRenderSnapshot snapshot,
        int startX,
        int startY,
        int maxWidth)
    {
        if (snapshot.Sieges.Count == 0 && snapshot.Breaches.Count == 0)
            return startY;

        int breachedCount = snapshot.Sieges.Count(s => string.Equals(s.Status, "breached", StringComparison.OrdinalIgnoreCase) || s.BreachCount > 0);
        int activeCount = snapshot.Sieges.Count;
        int breachMarkers = snapshot.Breaches.Count;

        var primary = $"Siege: active={activeCount} breached={breachedCount} breaches={breachMarkers}";
        int y = TextWrap.DrawWrapped(spriteBatch, font, primary, new Vector2(startX, startY), Theme.SiegeEventText, maxWidth, 18);

        var hotSiege = snapshot.Sieges
            .OrderByDescending(s => s.ActiveAttackerCount)
            .ThenByDescending(s => s.LastActiveTick)
            .FirstOrDefault();
        if (hotSiege != null)
        {
            var secondary = $"Hot siege: C{hotSiege.AttackerColonyId}->C{hotSiege.DefenderColonyId} at ({hotSiege.CenterX},{hotSiege.CenterY}) attackers={hotSiege.ActiveAttackerCount} status={hotSiege.Status}";
            y = TextWrap.DrawWrapped(spriteBatch, font, secondary, new Vector2(startX, y), Theme.SecondaryText, maxWidth, 18);
        }

        return y;
    }

    private void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        spriteBatch.Draw(pixel, rect, Theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Theme.PanelBorder);
    }
}

public readonly record struct TechMenuView(int ColonyId, IReadOnlyList<string> LockedTechNames);
