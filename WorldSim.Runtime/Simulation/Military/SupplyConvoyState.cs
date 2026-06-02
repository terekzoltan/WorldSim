using System;
using WorldSim.Simulation.Navigation;

namespace WorldSim.Simulation.Military;

public enum SupplyConvoyPhase
{
    Pending,
    Marching,
    Delivered,
    Failed
}

public sealed class SupplyConvoyRouteCounters
{
    public int PathRequests { get; private set; }
    public int PathCacheHits { get; private set; }
    public int RouteRecomputes { get; private set; }
    public int ProgressTicks { get; private set; }
    public int NoProgressTicks { get; private set; }

    internal void RecordPathRequest() => PathRequests++;
    internal void RecordPathCacheHit() => PathCacheHits++;
    internal void RecordRouteRecompute() => RouteRecomputes++;
    internal void RecordProgress() => ProgressTicks++;
    internal void RecordNoProgress() => NoProgressTicks++;
}

public sealed class SupplyConvoyState
{
    public SupplyConvoyState(
        int convoyId,
        Faction ownerFaction,
        int homeColonyId,
        int targetCampaignId,
        int targetArmyId,
        long createdTick,
        int originX,
        int originY,
        int targetX,
        int targetY,
        int payloadFood)
    {
        ConvoyId = Math.Max(0, convoyId);
        OwnerFaction = ownerFaction;
        HomeColonyId = Math.Max(0, homeColonyId);
        TargetCampaignId = Math.Max(0, targetCampaignId);
        TargetArmyId = Math.Max(0, targetArmyId);
        CreatedTick = Math.Max(0, createdTick);
        OriginX = originX;
        OriginY = originY;
        CurrentX = originX;
        CurrentY = originY;
        TargetX = targetX;
        TargetY = targetY;
        PayloadFood = Math.Max(0, payloadFood);
    }

    public int ConvoyId { get; }
    public Faction OwnerFaction { get; }
    public int HomeColonyId { get; }
    public int TargetCampaignId { get; }
    public int TargetArmyId { get; }
    public long CreatedTick { get; }
    public long CompletedTick { get; private set; } = -1;
    public int OriginX { get; }
    public int OriginY { get; }
    public int CurrentX { get; private set; }
    public int CurrentY { get; private set; }
    public int TargetX { get; }
    public int TargetY { get; }
    public int PayloadFood { get; }
    public SupplyConvoyPhase Phase { get; private set; } = SupplyConvoyPhase.Pending;
    public SupplyConvoyRouteCounters RouteCounters { get; } = new();
    internal NavigationPathCache RouteCache { get; } = new();

    public bool IsActive => Phase is SupplyConvoyPhase.Pending or SupplyConvoyPhase.Marching;

    internal void BeginMarch()
    {
        if (Phase == SupplyConvoyPhase.Pending)
            Phase = SupplyConvoyPhase.Marching;
    }

    internal void MoveTo(int x, int y)
    {
        CurrentX = x;
        CurrentY = y;
    }

    internal void MarkDelivered(long tick)
    {
        if (!IsActive)
            return;

        Phase = SupplyConvoyPhase.Delivered;
        CompletedTick = Math.Max(0, tick);
        RouteCache.Invalidate();
    }

    internal void MarkFailed(long tick)
    {
        if (!IsActive)
            return;

        Phase = SupplyConvoyPhase.Failed;
        CompletedTick = Math.Max(0, tick);
        RouteCache.Invalidate();
    }
}
