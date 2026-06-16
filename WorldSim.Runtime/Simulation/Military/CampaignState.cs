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

public enum CampaignSiegeStatus
{
    None,
    SeekingTarget,
    Active,
    Breached,
    NoTarget
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
    public CampaignSiegeState Siege { get; } = new();
    public CampaignResolutionState Resolution { get; } = new();
    public CampaignWave9EvidenceCounters Wave9Evidence { get; } = new();
    public long EncounterStartedTick { get; private set; } = -1;
    public long ResolvedTick { get; private set; } = -1;

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
        if (EncounterStartedTick < 0)
            EncounterStartedTick = Math.Max(0, tick);
        Wave9Evidence.RecordMarchCompleted();
        Wave9Evidence.RecordEncounterStarted();
    }

    internal void RecordWave9PhaseTick()
        => Wave9Evidence.RecordPhaseTick(Phase);

    internal bool Resolve(CampaignResolutionApplication application)
    {
        if (!Resolution.TryApply(application))
            return false;

        RouteCache.Invalidate();
        Phase = CampaignPhase.Resolved;
        ResolvedTick = Math.Max(0, application.ResolvedTick);
        return true;
    }
}

public sealed class CampaignSiegeState
{
    public CampaignSiegeStatus Status { get; private set; } = CampaignSiegeStatus.None;
    public int TargetStructureId { get; private set; } = -1;
    public int DefenderColonyId { get; private set; } = -1;
    public int ObservedSiegeId { get; private set; } = -1;
    public int BreachCount { get; private set; }
    public long SiegeEnteredTick { get; private set; } = -1;
    public long LastObservedTick { get; private set; } = -1;
    internal long LastPressureTick { get; private set; } = -1;
    public int SiegesEntered { get; private set; }
    public int SiegePressureTicks { get; private set; }
    public int BreachesObserved { get; private set; }
    public int LastObservedBreachStructureId { get; private set; } = -1;
    public long LastObservedBreachTick { get; private set; } = -1;

    internal void MarkNoTarget(long tick)
    {
        if (Status == CampaignSiegeStatus.Breached)
            return;

        Status = CampaignSiegeStatus.NoTarget;
        LastObservedTick = Math.Max(0, tick);
    }

    internal void SuppressActivePressure(long tick)
    {
        if (Status == CampaignSiegeStatus.Breached)
            return;

        Status = CampaignSiegeStatus.None;
        TargetStructureId = -1;
        DefenderColonyId = -1;
        ObservedSiegeId = -1;
        LastObservedTick = Math.Max(0, tick);
    }

    internal void RecordPressure(int targetStructureId, int defenderColonyId, long tick)
    {
        if (Status == CampaignSiegeStatus.Breached)
            return;

        TargetStructureId = Math.Max(0, targetStructureId);
        DefenderColonyId = Math.Max(0, defenderColonyId);
        LastPressureTick = Math.Max(0, tick);
        LastObservedTick = LastPressureTick;
        SiegePressureTicks++;

        if (SiegesEntered == 0)
        {
            SiegeEnteredTick = Math.Max(0, tick);
            SiegesEntered = 1;
        }

        if (Status is CampaignSiegeStatus.None or CampaignSiegeStatus.NoTarget)
            Status = CampaignSiegeStatus.SeekingTarget;
    }

    internal void ObserveActiveSiege(int siegeId, int breachCount, long tick)
    {
        if (Status == CampaignSiegeStatus.Breached)
            return;

        ObservedSiegeId = Math.Max(0, siegeId);
        LastObservedTick = Math.Max(0, tick);
        Status = CampaignSiegeStatus.Active;
    }

    internal void ObserveBreach(int structureId, long breachTick, long tick)
    {
        if (LastObservedBreachStructureId != structureId || LastObservedBreachTick != breachTick)
        {
            LastObservedBreachStructureId = structureId;
            LastObservedBreachTick = breachTick;
            BreachesObserved++;
        }

        BreachCount = Math.Max(BreachCount, BreachesObserved);
        LastObservedTick = Math.Max(0, tick);
        Status = CampaignSiegeStatus.Breached;
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
