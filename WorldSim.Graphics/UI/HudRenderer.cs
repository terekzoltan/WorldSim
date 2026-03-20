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
                              || director.ActiveDomainModifiers.Count > 0
                              || director.ActiveGoalBiases.Count > 0
                              || director.BeatCooldownRemainingTicks > 0
                              || !string.Equals(director.LastActionStatus, "No director action", StringComparison.OrdinalIgnoreCase);

        var directorExtraLines = 0;
        if (hasDirectorData)
        {
            directorExtraLines += 2;
            if (showAiDebug)
            {
                directorExtraLines += Math.Min(2, director.ActiveDomainModifiers.Count > 0 ? 1 : 0);
                directorExtraLines += Math.Min(2, director.ActiveGoalBiases.Count > 0 ? 1 : 0);
            }
        }

        if (showAiDebug && director.HasBudgetData)
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
        y = DrawDirectorStatus(spriteBatch, font, snapshot, leftX, y + 4, contentWidth, showAiDebug);
        y = _eventFeed.Draw(spriteBatch, font, snapshot.RecentEvents.Take(visibleEventCount).ToList(), leftX, y + 4, contentWidth, Theme);
        y = TextWrap.DrawWrapped(spriteBatch, font, refineryStatus, new Vector2(leftX, y + 10), Theme.StatusText, contentWidth, 20);
        y = TextWrap.DrawWrapped(spriteBatch, font, plannerStatus, new Vector2(leftX, y), Theme.StatusText, contentWidth, 20);

        if (renderStats != null)
        {
            var passSummary = string.Join(" | ", renderStats.PassSamples.Select(s => $"{s.PassName}:{s.Milliseconds:0.00}ms"));
            TextWrap.DrawWrapped(spriteBatch, font, $"Render {renderStats.LastFrameMilliseconds:0.00}ms | {passSummary}", new Vector2(leftX, y), Theme.SecondaryText, contentWidth, 20);
        }

        if (showAiDebug)
            _aiDebugPanel.Draw(spriteBatch, font, aiDebug, snapshot, viewportWidth, Theme, aiDebugCompact, aiScoreOffset, aiHistoryOffset);
    }

    private int DrawDirectorStatus(
        SpriteBatch spriteBatch,
        SpriteFont font,
        WorldRenderSnapshot snapshot,
        int startX,
        int startY,
        int maxWidth,
        bool showDebug)
    {
        var state = snapshot.Director;
        var directive = state.ActiveDirectives.FirstOrDefault();
        var directiveLabel = directive == null
            ? "none"
            : $"{directive.Directive} C{directive.ColonyId} ({directive.RemainingTicks}/{directive.TotalTicks}t)";
        var budgetLabel = state.HasBudgetData
            ? $"{state.RemainingInfluenceBudget:0.###}/{state.MaxInfluenceBudget:0.###} used={state.LastCheckpointBudgetUsed:0.###}"
            : "n/a";

        int y = TextWrap.DrawWrapped(
            spriteBatch,
            font,
            $"Director: stage={state.StageMarker} mode={state.OutputMode} src={state.OutputModeSource} cd={state.BeatCooldownRemainingTicks}t budget={budgetLabel}",
            new Vector2(startX, startY),
            Theme.DirectorEventText,
            maxWidth,
            18);

        y = TextWrap.DrawWrapped(
            spriteBatch,
            font,
            $"Directive: {directiveLabel}",
            new Vector2(startX, y),
            Theme.SecondaryText,
            maxWidth,
            18);

        if (!showDebug)
            return y;

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

        if (state.HasBudgetData)
        {
            var checkpointTickLabel = state.LastBudgetCheckpointTick >= 0
                ? state.LastBudgetCheckpointTick.ToString()
                : "n/a";
            var usedPct = state.MaxInfluenceBudget > 0d
                ? (state.LastCheckpointBudgetUsed / state.MaxInfluenceBudget) * 100d
                : 0d;
            y = TextWrap.DrawWrapped(
                spriteBatch,
                font,
                $"Dir budget: checkpoint={checkpointTickLabel} remaining={state.RemainingInfluenceBudget:0.###} used={state.LastCheckpointBudgetUsed:0.###} ({usedPct:0.#}%)",
                new Vector2(startX, y),
                Theme.SecondaryText,
                maxWidth,
                18);
        }

        return y;
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
