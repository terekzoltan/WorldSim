using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class CampaignOverlayPass : IRenderPass
{
    public string Name => "CampaignOverlay";
    public bool Enabled { get; set; }

    public void Draw(in RenderFrameContext context)
    {
        if (!Enabled)
            return;

        var spriteBatch = context.SpriteBatch;
        var pixel = context.Textures.Pixel;
        int tileSize = context.Settings.TileSize;

        foreach (var campaign in context.Snapshot.Campaigns.OrderBy(campaign => campaign.CampaignId))
        {
            DrawRoute(spriteBatch, pixel, campaign, tileSize);
            DrawWaypoints(spriteBatch, pixel, campaign, tileSize);
            DrawArmyMarker(spriteBatch, pixel, campaign, tileSize);
            DrawEncounterMarkers(spriteBatch, pixel, campaign, tileSize);
            DrawEncounterActivityCue(spriteBatch, pixel, campaign, tileSize);
            DrawResolutionMarker(spriteBatch, pixel, campaign, tileSize);
        }

        DrawSupplyConvoys(spriteBatch, pixel, context.Snapshot, tileSize);
        DrawForwardBases(spriteBatch, pixel, context.Snapshot, tileSize);
    }

    private static void DrawSupplyConvoys(SpriteBatch spriteBatch, Texture2D pixel, WorldRenderSnapshot snapshot, int tileSize)
    {
        foreach (var convoy in snapshot.SupplyConvoys.OrderBy(convoy => convoy.ConvoyId))
        {
            var current = TileCenter(convoy.CurrentX, convoy.CurrentY, tileSize);
            var target = TileCenter(convoy.TargetX, convoy.TargetY, tileSize);
            var color = GetSupplyConvoyColor(convoy);
            int thickness = Math.Max(1, tileSize / 5);

            DrawLine(spriteBatch, pixel, current, target, color * 0.48f, thickness);

            int markerSize = Math.Max(tileSize, 7) + Math.Clamp(convoy.PayloadFood, 0, 4);
            var marker = CenteredRect(current, markerSize);
            spriteBatch.Draw(pixel, marker, color * 0.34f);
            DrawRectOutline(spriteBatch, pixel, marker, Math.Max(1, tileSize / 5), color * 0.9f);

            int targetSize = Math.Max(4, tileSize / 2);
            DrawRectOutline(spriteBatch, pixel, CenteredRect(target, targetSize), Math.Max(1, tileSize / 6), color * 0.72f);

            if (convoy.PayloadFood > 0)
            {
                int badge = Math.Max(2, tileSize / 3);
                spriteBatch.Draw(pixel, new Rectangle(marker.Right - badge, marker.Bottom - badge, badge, badge), new Color(245, 211, 112) * 0.92f);
            }
        }
    }

    private static void DrawForwardBases(SpriteBatch spriteBatch, Texture2D pixel, WorldRenderSnapshot snapshot, int tileSize)
    {
        foreach (var forwardBase in snapshot.ForwardBases.OrderBy(forwardBase => forwardBase.BaseId))
        {
            var center = TileCenter(forwardBase.X, forwardBase.Y, tileSize);
            var color = GetForwardBaseColor(forwardBase);
            int markerSize = Math.Max(tileSize + 2, 8);
            int radius = Math.Max(markerSize, Math.Max(1, forwardBase.Radius) * tileSize);
            int thickness = Math.Max(1, tileSize / 5);

            var radiusRect = CenteredRect(center, radius * 2);
            DrawRectOutline(spriteBatch, pixel, radiusRect, thickness, color * 0.34f);

            var marker = CenteredRect(center, markerSize);
            spriteBatch.Draw(pixel, marker, color * 0.26f);
            DrawRectOutline(spriteBatch, pixel, marker, Math.Max(1, tileSize / 4), color * 0.86f);
            DrawLine(spriteBatch, pixel, new Vector2(marker.X, marker.Y + (marker.Height / 2f)), new Vector2(marker.Right, marker.Y + (marker.Height / 2f)), color * 0.82f, thickness);
            DrawLine(spriteBatch, pixel, new Vector2(marker.X + (marker.Width / 2f), marker.Y), new Vector2(marker.X + (marker.Width / 2f), marker.Bottom), color * 0.82f, thickness);
        }
    }

    private static void DrawRoute(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        var route = campaign.Route;
        var origin = TileCenter(route.OriginX, route.OriginY, tileSize);
        var target = route.HasResolvedObjective
            ? TileCenter(route.ResolvedObjectiveX, route.ResolvedObjectiveY, tileSize)
            : TileCenter(route.TargetX, route.TargetY, tileSize);

        var baseColor = GetFactionColor(campaign.OwnerFactionId);
        DrawLine(spriteBatch, pixel, origin, target, baseColor * 0.44f, Math.Max(1, tileSize / 5));

        int marker = Math.Max(2, tileSize / 2);
        spriteBatch.Draw(pixel, CenteredRect(origin, marker), baseColor * 0.64f);
        DrawRectOutline(spriteBatch, pixel, CenteredRect(target, Math.Max(marker + 2, tileSize)), Math.Max(1, tileSize / 5), baseColor * 0.78f);
    }

    private static void DrawWaypoints(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        int dot = Math.Max(2, tileSize / 3);
        int nextDot = Math.Max(dot + 2, tileSize / 2);
        var baseColor = GetFactionColor(campaign.OwnerFactionId);

        foreach (var waypoint in campaign.RouteWaypoints.OrderBy(waypoint => waypoint.Index))
        {
            var center = TileCenter(waypoint.X, waypoint.Y, tileSize);
            var color = waypoint.IsNext ? new Color(250, 230, 126) * 0.92f : baseColor * 0.68f;
            spriteBatch.Draw(pixel, CenteredRect(center, waypoint.IsNext ? nextDot : dot), color);
        }
    }

    private static void DrawArmyMarker(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        var army = campaign.Army;
        var baseColor = GetFactionColor(campaign.OwnerFactionId);

        if (army.AnchorActorId < 0)
        {
            DrawRallyMarker(spriteBatch, pixel, army, tileSize);
            return;
        }

        var supply = campaign.Supply;
        int size = Math.Max(tileSize, 8);
        var anchor = CenteredRect(TileCenter(army.AnchorX, army.AnchorY, tileSize), size);

        spriteBatch.Draw(pixel, anchor, baseColor * 0.52f);
        DrawRectOutline(spriteBatch, pixel, anchor, Math.Max(1, tileSize / 4), baseColor * 0.95f);

        DrawRallyMarker(spriteBatch, pixel, army, tileSize);

        if (supply.HasAssignedCarrier)
        {
            int badge = Math.Max(3, tileSize / 2);
            var badgeRect = new Rectangle(anchor.Right - badge, anchor.Y - badge, badge, badge);
            spriteBatch.Draw(pixel, badgeRect, new Color(245, 211, 112) * 0.92f);
        }

        if (supply.SustainedOutOfSupplyTicks > 0)
        {
            DrawRectOutline(spriteBatch, pixel, Inflate(anchor, Math.Max(2, tileSize / 3)), Math.Max(1, tileSize / 4), new Color(236, 88, 80) * 0.9f);
        }
    }

    private static void DrawRallyMarker(SpriteBatch spriteBatch, Texture2D pixel, ArmyRenderData army, int tileSize)
    {
        if (!army.HasRallyPoint)
            return;

        var rally = CenteredRect(TileCenter(army.RallyX, army.RallyY, tileSize), Math.Max(4, tileSize / 2));
        DrawRectOutline(spriteBatch, pixel, rally, Math.Max(1, tileSize / 5), new Color(128, 218, 246) * 0.84f);
    }

    private static void DrawEncounterMarkers(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        foreach (var encounter in campaign.Encounters.OrderBy(encounter => encounter.EncounterTicks))
        {
            var source = TileCenter(encounter.SourceX, encounter.SourceY, tileSize);
            var target = TileCenter(encounter.TargetX, encounter.TargetY, tileSize);
            var color = new Color(239, 142, 98) * 0.78f;
            DrawLine(spriteBatch, pixel, source, target, color * 0.62f, Math.Max(1, tileSize / 5));
            DrawRectOutline(spriteBatch, pixel, CenteredRect(target, Math.Max(tileSize, 6)), Math.Max(1, tileSize / 4), color);
        }
    }

    private static void DrawEncounterActivityCue(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        if (campaign.Resolution.IsResolved)
            return;

        int encounterTicks = Math.Max(campaign.Route.EncounterTicks, campaign.Encounters.Select(e => e.EncounterTicks).DefaultIfEmpty(0).Max());
        if (encounterTicks <= 0)
            return;

        var center = ResolveObjectiveCenter(campaign.Route, tileSize);
        int pulse = Math.Max(tileSize + 2, tileSize + Math.Clamp(encounterTicks / 12, 1, 5) * Math.Max(1, tileSize / 3));
        var cue = CenteredRect(center, pulse);
        var color = new Color(239, 174, 92) * 0.68f;
        DrawRectOutline(spriteBatch, pixel, cue, Math.Max(1, tileSize / 5), color);
    }

    private static void DrawResolutionMarker(SpriteBatch spriteBatch, Texture2D pixel, CampaignRenderData campaign, int tileSize)
    {
        var resolution = campaign.Resolution;
        if (!resolution.IsResolved)
            return;

        var center = ResolveObjectiveCenter(campaign.Route, tileSize);
        int size = Math.Max(tileSize + 4, tileSize * 2);
        var marker = CenteredRect(center, size);
        var color = GetResolutionColor(resolution);

        DrawRectOutline(spriteBatch, pixel, marker, Math.Max(1, tileSize / 4), color * 0.92f);
        if (string.Equals(resolution.Kind, "attacker_victory", StringComparison.OrdinalIgnoreCase))
        {
            DrawLine(spriteBatch, pixel, new Vector2(marker.X, marker.Bottom), new Vector2(marker.X + (marker.Width / 2f), marker.Y), color * 0.85f, Math.Max(1, tileSize / 5));
            DrawLine(spriteBatch, pixel, new Vector2(marker.X + (marker.Width / 2f), marker.Y), new Vector2(marker.Right, marker.Bottom), color * 0.85f, Math.Max(1, tileSize / 5));
        }
        else
        {
            DrawLine(spriteBatch, pixel, new Vector2(marker.X, marker.Y), new Vector2(marker.Right, marker.Bottom), color * 0.82f, Math.Max(1, tileSize / 5));
            DrawLine(spriteBatch, pixel, new Vector2(marker.X, marker.Bottom), new Vector2(marker.Right, marker.Y), color * 0.82f, Math.Max(1, tileSize / 5));
        }
    }

    private static Vector2 ResolveObjectiveCenter(CampaignRouteRenderData route, int tileSize)
        => route.HasResolvedObjective
            ? TileCenter(route.ResolvedObjectiveX, route.ResolvedObjectiveY, tileSize)
            : TileCenter(route.TargetX, route.TargetY, tileSize);

    private static Vector2 TileCenter(int x, int y, int tileSize)
        => new((x * tileSize) + (tileSize / 2f), (y * tileSize) + (tileSize / 2f));

    private static Rectangle CenteredRect(Vector2 center, int size)
        => new((int)MathF.Round(center.X - (size / 2f)), (int)MathF.Round(center.Y - (size / 2f)), size, size);

    private static Rectangle Inflate(Rectangle rect, int amount)
        => new(rect.X - amount, rect.Y - amount, rect.Width + (amount * 2), rect.Height + (amount * 2));

    private static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int thickness, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        var delta = end - start;
        float length = delta.Length();
        if (length <= 0.1f)
            return;

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private static Color GetFactionColor(int factionId)
    {
        return factionId switch
        {
            0 => new Color(94, 149, 214),
            1 => new Color(216, 131, 93),
            2 => new Color(120, 194, 128),
            3 => new Color(181, 140, 232),
            _ => new Color(180, 180, 180)
        };
    }

    private static Color GetResolutionColor(CampaignResolutionRenderData resolution)
    {
        if (string.Equals(resolution.Kind, "attacker_victory", StringComparison.OrdinalIgnoreCase))
            return new Color(135, 232, 144);

        if (string.Equals(resolution.Kind, "defender_held", StringComparison.OrdinalIgnoreCase))
            return new Color(236, 158, 92);

        return new Color(180, 180, 180);
    }

    private static Color GetSupplyConvoyColor(SupplyConvoyRenderData convoy)
    {
        if (string.Equals(convoy.Phase, "marching", StringComparison.OrdinalIgnoreCase))
            return GetFactionColor(convoy.OwnerFactionId);

        if (string.Equals(convoy.Phase, "delivered", StringComparison.OrdinalIgnoreCase))
            return new Color(132, 220, 142);

        if (string.Equals(convoy.Phase, "failed", StringComparison.OrdinalIgnoreCase))
            return new Color(236, 158, 92);

        var factionColor = GetFactionColor(convoy.OwnerFactionId);
        return string.Equals(convoy.Phase, "pending", StringComparison.OrdinalIgnoreCase)
            ? factionColor * 0.72f
            : new Color(180, 180, 180);
    }

    private static Color GetForwardBaseColor(ForwardBaseRenderData forwardBase)
    {
        if (string.Equals(forwardBase.Phase, "expired", StringComparison.OrdinalIgnoreCase))
            return new Color(156, 178, 190);

        if (string.Equals(forwardBase.Phase, "abandoned", StringComparison.OrdinalIgnoreCase))
            return new Color(236, 158, 92);

        return string.Equals(forwardBase.Phase, "active", StringComparison.OrdinalIgnoreCase)
            ? GetFactionColor(forwardBase.OwnerFactionId)
            : new Color(180, 180, 180);
    }
}
