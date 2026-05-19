using System;
using WorldSim.Simulation.Navigation;

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

    internal void RecordPathRequest() => PathRequests++;
    internal void RecordPathCacheHit() => PathCacheHits++;
    internal void RecordBlockedMovementCheck() => BlockedMovementChecks++;
    internal void RecordRouteRecompute() => RouteRecomputes++;
    internal void RecordMarchProgress() => MarchProgressTicks++;
    internal void RecordEncounterTick() => EncounterTicks++;
    internal void RecordNoProgress() => NoProgressTicks++;
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
    internal NavigationPathCache RouteCache { get; } = new();
    public ArmyState Army { get; }
    public CampaignWave9EvidenceCounters Wave9Evidence { get; } = new();

    internal void BeginAssembly(long tick)
    {
        if (Phase == CampaignPhase.AssemblingPending)
        {
            Phase = CampaignPhase.Assembling;
            Wave9Evidence.RecordAssemblyStarted();
        }

        if (Phase == CampaignPhase.Assembling)
            Army.BeginAssembly(tick);
    }

    internal void MarkAssemblyComplete(long tick)
    {
        if (Phase is not CampaignPhase.AssemblingPending and not CampaignPhase.Assembling)
            return;

        Army.MarkAssemblyComplete(tick);
        Phase = CampaignPhase.Marching;
        Wave9Evidence.RecordAssemblyCompleted();
        Wave9Evidence.RecordMarchStarted();
    }

    internal void ReturnToAssemblyAfterRosterInvalidation(long tick)
    {
        if (Phase is not CampaignPhase.Marching)
            return;

        RouteCache.Invalidate();
        Army.MarkAssemblyInvalidatedForReassembly();
        Phase = CampaignPhase.Assembling;
        Army.BeginAssembly(tick);
        Wave9Evidence.RecordReturnedOrAborted();
    }

    internal void BeginEncounter(long tick)
    {
        if (Phase is not CampaignPhase.Marching)
            return;

        RouteCache.Invalidate();
        Phase = CampaignPhase.Encounter;
        Wave9Evidence.RecordMarchCompleted();
        Wave9Evidence.RecordEncounterStarted();
    }

    internal void RecordWave9PhaseTick()
        => Wave9Evidence.RecordPhaseTick(Phase);
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

public sealed class CampaignWave9EvidenceCounters
{
    public int AssemblyStartedCount { get; private set; }
    public int AssemblyCompletedCount { get; private set; }
    public int MarchStartedCount { get; private set; }
    public int MarchCompletedCount { get; private set; }
    public int CampaignsReturnedOrAborted { get; private set; }
    public int EncounterCount { get; private set; }
    public int AssemblingTicks { get; private set; }
    public int MarchingTicks { get; private set; }
    public int EncounterTicks { get; private set; }

    internal void RecordAssemblyStarted() => AssemblyStartedCount++;
    internal void RecordAssemblyCompleted() => AssemblyCompletedCount++;
    internal void RecordMarchStarted() => MarchStartedCount++;
    internal void RecordMarchCompleted() => MarchCompletedCount++;
    internal void RecordReturnedOrAborted() => CampaignsReturnedOrAborted++;
    internal void RecordEncounterStarted() => EncounterCount++;

    internal void RecordPhaseTick(CampaignPhase phase)
    {
        switch (phase)
        {
            case CampaignPhase.AssemblingPending:
            case CampaignPhase.Assembling:
                AssemblingTicks++;
                break;

            case CampaignPhase.Marching:
                MarchingTicks++;
                break;

            case CampaignPhase.Encounter:
                EncounterTicks++;
                break;
        }
    }
}
