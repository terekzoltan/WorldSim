using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI.Panels;

public sealed class CampaignPanelRenderer
{
    private const int MaxVisibleCampaigns = 4;
    private const int MaxVisibleLogisticsRows = 3;
    private const int RowHeight = 60;
    private const int LogisticsRowHeight = 18;

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        int viewportWidth,
        int viewportHeight,
        HudTheme theme,
        WorldRenderSnapshot snapshot)
    {
        int width = Math.Min(620, Math.Max(320, viewportWidth - 28));
        int height = Math.Min(430, Math.Max(230, viewportHeight - 28));
        int x = viewportWidth - width - 14;
        int y = viewportHeight - height - 14;
        var rect = new Rectangle(x, y, width, height);

        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int pad = 10;
        int ty = y + 10;
        spriteBatch.DrawString(font, "Campaigns", new Vector2(x + pad, ty), theme.AccentText);
        ty += 24;

        int logisticsReserve = HasLogistics(snapshot) ? 78 : 22;
        int availableRows = Math.Max(1, Math.Min(MaxVisibleCampaigns, (rect.Bottom - ty - logisticsReserve) / RowHeight));
        var campaigns = snapshot.Campaigns
            .OrderBy(campaign => campaign.CampaignId)
            .Take(availableRows)
            .ToList();
        if (campaigns.Count == 0)
        {
            spriteBatch.DrawString(font, "No active campaigns.", new Vector2(x + pad, ty), theme.SecondaryText);
            ty += 22;
        }
        else
        {
            foreach (var campaign in campaigns)
            {
                DrawCampaignRow(spriteBatch, pixel, font, theme, campaign, x + pad, ty, width - (pad * 2));
                ty += RowHeight;
                if (ty > rect.Bottom - 28)
                    break;
            }

            int remaining = Math.Max(0, snapshot.Campaigns.Count - campaigns.Count);
            if (remaining > 0 && ty <= rect.Bottom - 22)
            {
                spriteBatch.DrawString(font, $"+{remaining} more campaigns", new Vector2(x + pad, ty), theme.SecondaryText);
                ty += 18;
            }
        }

        DrawLogisticsSection(spriteBatch, pixel, font, theme, snapshot, x + pad, ty + 4, width - (pad * 2), rect.Bottom - 10);
    }

    private static void DrawLogisticsSection(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        HudTheme theme,
        WorldRenderSnapshot snapshot,
        int x,
        int y,
        int width,
        int bottom)
    {
        if (y > bottom - 20)
            return;

        spriteBatch.DrawString(font, "Logistics", new Vector2(x, y), theme.AccentText);
        y += 20;

        string summary = $"Conv {CountPhase(snapshot.SupplyConvoys, "marching")}/{CountPhase(snapshot.SupplyConvoys, "delivered")}/{CountPhase(snapshot.SupplyConvoys, "failed")} act/del/fail  Base {CountPhase(snapshot.ForwardBases, "active")}/{CountPhase(snapshot.ForwardBases, "expired")}/{CountPhase(snapshot.ForwardBases, "abandoned")} act/exp/abn";
        spriteBatch.DrawString(font, TrimToWidth(font, summary, width), new Vector2(x, y), theme.SecondaryText);
        y += LogisticsRowHeight;

        int maxVisibleRows = Math.Max(0, Math.Min(MaxVisibleLogisticsRows, (bottom - y) / LogisticsRowHeight));
        int rowsDrawn = 0;
        foreach (var convoy in snapshot.SupplyConvoys.OrderBy(convoy => convoy.ConvoyId).Take(maxVisibleRows))
        {
            if (y > bottom - LogisticsRowHeight)
                return;

            var color = GetLogisticsPhaseColor(theme, convoy.Phase);
            spriteBatch.Draw(pixel, new Rectangle(x, y + 3, 3, 12), color * 0.85f);
            string line = $"Conv#{convoy.ConvoyId} {GetFactionAbbreviation(convoy.OwnerFactionId)} c:{convoy.TargetCampaignId} {FormatPhase(convoy.Phase)} f:{convoy.PayloadFood} {convoy.CurrentX},{convoy.CurrentY}->{convoy.TargetX},{convoy.TargetY}";
            spriteBatch.DrawString(font, TrimToWidth(font, line, width - 8), new Vector2(x + 8, y), color);
            y += LogisticsRowHeight;
            rowsDrawn++;
        }

        foreach (var forwardBase in snapshot.ForwardBases.OrderBy(forwardBase => forwardBase.BaseId).Take(Math.Max(0, maxVisibleRows - rowsDrawn)))
        {
            if (y > bottom - LogisticsRowHeight)
                return;

            var color = GetLogisticsPhaseColor(theme, forwardBase.Phase);
            spriteBatch.Draw(pixel, new Rectangle(x, y + 3, 3, 12), color * 0.85f);
            string line = $"Base#{forwardBase.BaseId} {GetFactionAbbreviation(forwardBase.OwnerFactionId)} c:{forwardBase.CampaignId} {FormatPhase(forwardBase.Phase)} r:{forwardBase.Radius} rest:{forwardBase.RestTicks} a:{forwardBase.RestedActorTicks}";
            spriteBatch.DrawString(font, TrimToWidth(font, line, width - 8), new Vector2(x + 8, y), color);
            y += LogisticsRowHeight;
            rowsDrawn++;
        }

        int hidden = Math.Max(0, snapshot.SupplyConvoys.Count + snapshot.ForwardBases.Count - rowsDrawn);
        if (hidden > 0 && y <= bottom - LogisticsRowHeight)
            spriteBatch.DrawString(font, $"+{hidden} more logistics records", new Vector2(x, y), theme.SecondaryText);
    }

    private static void DrawCampaignRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        HudTheme theme,
        CampaignRenderData campaign,
        int x,
        int y,
        int width)
    {
        var rowRect = new Rectangle(x, y, width, RowHeight - 4);
        spriteBatch.Draw(pixel, rowRect, theme.PanelBorder * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rowRect.X, rowRect.Y, 3, rowRect.Height), GetFactionColor(campaign.OwnerFactionId));

        string title = $"#{campaign.CampaignId} {GetFactionAbbreviation(campaign.OwnerFactionId)}->{GetFactionAbbreviation(campaign.TargetFactionId)} {FormatCampaignState(campaign.Phase, campaign.Status)}";
        string army = $"Army {campaign.Army.AssignedMemberCount}/{campaign.Army.RequestedMemberCount} {FormatAnchor(campaign.Army)} r:{FormatRally(campaign.Army)}";
        string route = $"Route {FormatRouteProgress(campaign.Route)} m:{campaign.Route.MarchProgressTicks} s:{campaign.Route.NoProgressTicks} e:{campaign.Encounters.Count}";
        string supply = $"Supply {FormatSupplySource(campaign.Supply.LastSupplySource)} rat:{campaign.Supply.RationPoolFood} oos:{campaign.Supply.SustainedOutOfSupplyTicks} car:{FormatCarrier(campaign.Supply)}";
        string result = FormatResolution(campaign.Resolution);
        var resultColor = GetResolutionColor(theme, campaign.Resolution);
        int textWidth = Math.Max(80, width - 16);
        int lowerRowWidth = Math.Max(60, (textWidth - 8) / 2);

        spriteBatch.DrawString(font, TrimToWidth(font, title, textWidth), new Vector2(x + 8, y + 4), theme.PrimaryText);
        spriteBatch.DrawString(font, TrimToWidth(font, army, textWidth), new Vector2(x + 8, y + 18), theme.SecondaryText);
        spriteBatch.DrawString(font, TrimToWidth(font, route, textWidth), new Vector2(x + 8, y + 32), theme.SecondaryText);
        spriteBatch.DrawString(font, TrimToWidth(font, supply, lowerRowWidth), new Vector2(x + 8, y + 46), theme.SecondaryText);
        if (!string.IsNullOrWhiteSpace(result))
            spriteBatch.DrawString(font, TrimToWidth(font, result, lowerRowWidth), new Vector2(x + 12 + lowerRowWidth, y + 46), resultColor);
    }

    private static string FormatRally(ArmyRenderData army)
        => army.HasRallyPoint ? $"({army.RallyX},{army.RallyY})" : "none";

    private static string FormatAnchor(ArmyRenderData army)
        => army.AnchorActorId < 0 ? "anchor:none" : $"anchor({army.AnchorX},{army.AnchorY})";

    private static string FormatCarrier(ArmySupplyRenderData supply)
        => supply.HasAssignedCarrier ? supply.AssignedCarrierActorId.ToString() : "none";

    private static string FormatRouteProgress(CampaignRouteRenderData route)
    {
        if (route.CachedWaypointCount <= 0)
            return "no path";

        int next = Math.Clamp(route.NextWaypointIndex + 1, 1, route.CachedWaypointCount);
        string fallback = route.UsesFallbackObjective ? " fallback" : string.Empty;
        return $"{next}/{route.CachedWaypointCount}{fallback}";
    }

    private static string FormatResolution(CampaignResolutionRenderData resolution)
    {
        if (!resolution.IsResolved)
            return "Result pending";

        string peace = resolution.PeaceApplied ? "yes" : "no";
        string scoreDelta = resolution.WarScoreDelta >= 0 ? $"+{resolution.WarScoreDelta}" : resolution.WarScoreDelta.ToString();
        return $"Res {FormatResolutionKind(resolution.Kind)} {FormatCloseReason(resolution.Reason)} sc:{scoreDelta}/{resolution.CumulativeWarScore} p:{peace} t:{FormatCloseReason(resolution.TreatyKind)}";
    }

    private static string FormatCampaignState(string phase, string status)
        => $"{CompactToken(phase)}/{CompactToken(status)}";

    private static string FormatSupplySource(string source)
        => $"src:{CompactToken(source)}";

    private static string FormatResolutionKind(string kind)
        => CompactToken(kind);

    private static string CompactToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "none";

        return value.Trim()
            .Replace("assembling_pending", "asm_pending", StringComparison.OrdinalIgnoreCase)
            .Replace("pending_assembly", "pending", StringComparison.OrdinalIgnoreCase)
            .Replace("attacker_victory", "atk_win", StringComparison.OrdinalIgnoreCase)
            .Replace("defender_held", "def_hold", StringComparison.OrdinalIgnoreCase)
            .Replace("consumer_cap_reached", "cap", StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.Ordinal);
    }

    private static bool HasLogistics(WorldRenderSnapshot snapshot)
        => snapshot.SupplyConvoys.Count > 0 || snapshot.ForwardBases.Count > 0;

    private static int CountPhase<T>(IEnumerable<T> records, string phase)
        => records.Count(record => string.Equals(GetPhase(record), phase, StringComparison.OrdinalIgnoreCase));

    private static string GetPhase<T>(T record)
        => record switch
        {
            SupplyConvoyRenderData convoy => convoy.Phase,
            ForwardBaseRenderData forwardBase => forwardBase.Phase,
            _ => string.Empty
        };

    private static string FormatPhase(string phase)
        => string.IsNullOrWhiteSpace(phase) ? "unknown" : phase.Trim();

    private static string FormatCloseReason(string closeReason)
        => string.IsNullOrWhiteSpace(closeReason) ? "none" : closeReason.Trim();

    private static Color GetLogisticsPhaseColor(HudTheme theme, string phase)
    {
        if (string.Equals(phase, "delivered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(phase, "active", StringComparison.OrdinalIgnoreCase))
            return theme.SuccessText;

        if (string.Equals(phase, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(phase, "abandoned", StringComparison.OrdinalIgnoreCase))
            return theme.WarningText;

        if (string.Equals(phase, "expired", StringComparison.OrdinalIgnoreCase))
            return theme.SecondaryText;

        if (string.Equals(phase, "pending", StringComparison.OrdinalIgnoreCase)
            || string.Equals(phase, "marching", StringComparison.OrdinalIgnoreCase))
            return theme.CampaignEventText;

        return theme.SecondaryText;
    }

    private static Color GetResolutionColor(HudTheme theme, CampaignResolutionRenderData resolution)
    {
        if (!resolution.IsResolved)
            return theme.SecondaryText;

        if (string.Equals(resolution.Kind, "attacker_victory", StringComparison.OrdinalIgnoreCase))
            return theme.SuccessText;

        if (string.Equals(resolution.Kind, "defender_held", StringComparison.OrdinalIgnoreCase))
            return theme.WarningText;

        return theme.CampaignEventText;
    }

    private static string TrimToWidth(SpriteFont font, string text, int maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
            return text;

        const string ellipsis = "...";
        int length = text.Length;
        while (length > ellipsis.Length && font.MeasureString(text[..length] + ellipsis).X > maxWidth)
            length--;

        return length <= ellipsis.Length ? ellipsis : text[..length] + ellipsis;
    }

    private static string GetFactionAbbreviation(int factionId)
    {
        return factionId switch
        {
            0 => "Syl",
            1 => "Obs",
            2 => "Aet",
            3 => "Chi",
            _ => $"F{factionId}"
        };
    }

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            0 => new Color(94, 149, 214) * 0.72f,
            1 => new Color(216, 131, 93) * 0.72f,
            2 => new Color(120, 194, 128) * 0.72f,
            3 => new Color(181, 140, 232) * 0.72f,
            _ => new Color(180, 180, 180) * 0.62f
        };
    }
}
