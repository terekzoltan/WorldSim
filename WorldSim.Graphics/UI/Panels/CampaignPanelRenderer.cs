using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI.Panels;

public sealed class CampaignPanelRenderer
{
    private const int MaxVisibleCampaigns = 4;

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
        int height = Math.Min(330, Math.Max(190, viewportHeight - 28));
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

        var campaigns = snapshot.Campaigns
            .OrderBy(campaign => campaign.CampaignId)
            .Take(MaxVisibleCampaigns)
            .ToList();
        if (campaigns.Count == 0)
        {
            spriteBatch.DrawString(font, "No active campaigns.", new Vector2(x + pad, ty), theme.SecondaryText);
            return;
        }

        foreach (var campaign in campaigns)
        {
            DrawCampaignRow(spriteBatch, pixel, font, theme, campaign, x + pad, ty, width - (pad * 2));
            ty += 66;
            if (ty > rect.Bottom - 28)
                break;
        }

        int remaining = Math.Max(0, snapshot.Campaigns.Count - campaigns.Count);
        if (remaining > 0 && ty <= rect.Bottom - 22)
            spriteBatch.DrawString(font, $"+{remaining} more campaigns", new Vector2(x + pad, ty), theme.SecondaryText);
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
        var rowRect = new Rectangle(x, y, width, 58);
        spriteBatch.Draw(pixel, rowRect, theme.PanelBorder * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rowRect.X, rowRect.Y, 3, rowRect.Height), GetFactionColor(campaign.OwnerFactionId));

        string title = $"#{campaign.CampaignId} {GetFactionAbbreviation(campaign.OwnerFactionId)}->{GetFactionAbbreviation(campaign.TargetFactionId)} {campaign.Phase}/{campaign.Status}";
        string army = $"Army {campaign.Army.AssignedMemberCount}/{campaign.Army.RequestedMemberCount} {FormatAnchor(campaign.Army)} rally:{FormatRally(campaign.Army)}";
        string route = $"Route {FormatRouteProgress(campaign.Route)} march:{campaign.Route.MarchProgressTicks} stuck:{campaign.Route.NoProgressTicks} enc:{campaign.Encounters.Count}";
        string supply = $"Supply src:{campaign.Supply.LastSupplySource} ration:{campaign.Supply.RationPoolFood} demand:{campaign.Supply.FractionalFoodDemand:0.##} oos:{campaign.Supply.SustainedOutOfSupplyTicks} carrier:{FormatCarrier(campaign.Supply)} forage:{campaign.Supply.ForageSuccesses}/{campaign.Supply.ForageAttempts}+{campaign.Supply.ForageFoodGained}";

        spriteBatch.DrawString(font, title, new Vector2(x + 8, y + 4), theme.PrimaryText);
        spriteBatch.DrawString(font, army, new Vector2(x + 8, y + 20), theme.SecondaryText);
        spriteBatch.DrawString(font, route, new Vector2(x + 8, y + 34), theme.SecondaryText);
        spriteBatch.DrawString(font, supply, new Vector2(x + 8, y + 48), theme.SecondaryText);
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
