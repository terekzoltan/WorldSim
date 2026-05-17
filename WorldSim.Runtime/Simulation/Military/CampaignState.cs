using System;

namespace WorldSim.Simulation.Military;

public enum CampaignPhase
{
    AssemblingPending,
    Assembling,
    Marching,
    Encounter,
    Resolved
}

public enum CampaignCreationStatus
{
    Created,
    CampaignRuntimeUnavailable,
    InvalidOwnerFaction,
    InvalidTargetFaction,
    SameFaction,
    OwnerColonyNotFound,
    TargetColonyNotFound,
    InvalidRequestedMemberCount
}

public sealed record CampaignRouteIntent(
    int OriginColonyId,
    int TargetColonyId,
    int OriginX,
    int OriginY,
    int TargetX,
    int TargetY);

public sealed class CampaignRouteCounters
{
    public int PathRequests { get; private set; }
    public int PathCacheHits { get; private set; }
    public int BlockedMovementChecks { get; private set; }
    public int RouteRecomputes { get; private set; }
    public int MarchProgressTicks { get; private set; }
    public int EncounterTicks { get; private set; }
    public int NoProgressTicks { get; private set; }
}

public sealed class CampaignState
{
    public CampaignState(
        int campaignId,
        Faction ownerFaction,
        Faction targetFaction,
        int originColonyId,
        int targetColonyId,
        long createdTick,
        CampaignRouteIntent routeIntent,
        ArmyState army)
    {
        CampaignId = Math.Max(0, campaignId);
        ArmyId = army.ArmyId;
        OwnerFaction = ownerFaction;
        TargetFaction = targetFaction;
        OriginColonyId = originColonyId;
        TargetColonyId = targetColonyId;
        CreatedTick = Math.Max(0, createdTick);
        RouteIntent = routeIntent;
        Army = army;
    }

    public int CampaignId { get; }
    public int ArmyId { get; }
    public Faction OwnerFaction { get; }
    public Faction TargetFaction { get; }
    public int OriginColonyId { get; }
    public int TargetColonyId { get; }
    public CampaignPhase Phase { get; private set; } = CampaignPhase.AssemblingPending;
    public long CreatedTick { get; }
    public CampaignRouteIntent RouteIntent { get; }
    public CampaignRouteCounters RouteCounters { get; } = new();
    public ArmyState Army { get; }

    internal void BeginAssembly(long tick)
    {
        if (Phase == CampaignPhase.AssemblingPending)
            Phase = CampaignPhase.Assembling;

        if (Phase == CampaignPhase.Assembling)
            Army.BeginAssembly(tick);
    }

    internal void MarkAssemblyComplete(long tick)
    {
        if (Phase is not CampaignPhase.AssemblingPending and not CampaignPhase.Assembling)
            return;

        Army.MarkAssemblyComplete(tick);
        Phase = CampaignPhase.Marching;
    }
}

public sealed record CampaignCreationResult(
    bool Success,
    CampaignCreationStatus Status,
    int? CampaignId,
    int? ArmyId,
    string Message)
{
    public static CampaignCreationResult Created(int campaignId, int armyId)
        => new(
            Success: true,
            CampaignCreationStatus.Created,
            campaignId,
            armyId,
            $"Created campaign {campaignId} with army {armyId}.");

    public static CampaignCreationResult Failed(CampaignCreationStatus status, string message)
        => new(
            Success: false,
            status,
            CampaignId: null,
            ArmyId: null,
            message);
}
