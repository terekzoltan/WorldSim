using System;

namespace WorldSim.Simulation.Military;

public sealed record CampaignRuntimeSnapshot(
    int CampaignId,
    int ArmyId,
    Faction OwnerFaction,
    Faction TargetFaction,
    int OriginColonyId,
    int TargetColonyId,
    CampaignPhase Phase,
    long CreatedTick,
    CampaignRouteIntent RouteIntent,
    CampaignRouteCountersSnapshot RouteCounters,
    CampaignSiegeRuntimeSnapshot Siege,
    CampaignResolutionRuntimeSnapshot Resolution,
    ArmyRuntimeSnapshot Army)
{
    public static CampaignRuntimeSnapshot From(CampaignState state)
        => new(
            state.CampaignId,
            state.ArmyId,
            state.OwnerFaction,
            state.TargetFaction,
            state.OriginColonyId,
            state.TargetColonyId,
            state.Phase,
            state.CreatedTick,
            state.RouteIntent,
            CampaignRouteCountersSnapshot.From(state.RouteCounters),
            CampaignSiegeRuntimeSnapshot.From(state.Siege),
            CampaignResolutionRuntimeSnapshot.From(state.Resolution),
            ArmyRuntimeSnapshot.From(state.Army));
}

public sealed record SiegeUnitRuntimeSnapshot(
    int SiegeUnitId,
    int CampaignId,
    int ArmyId,
    Faction OwnerFaction,
    SiegeUnitKind Kind,
    SiegeUnitPhase Phase,
    string InactiveReason,
    int X,
    int Y,
    int TargetStructureId,
    int TargetX,
    int TargetY,
    float Health,
    float MaxHealth,
    string RecentActionEffect,
    long LastActionTick)
{
    public static SiegeUnitRuntimeSnapshot From(SiegeUnitState state)
        => new(
            state.SiegeUnitId,
            state.CampaignId,
            state.ArmyId,
            state.OwnerFaction,
            state.Kind,
            state.Phase,
            state.InactiveReason,
            state.X,
            state.Y,
            state.TargetStructureId,
            state.TargetX,
            state.TargetY,
            state.Health,
            state.MaxHealth,
            state.RecentActionEffect,
            state.LastActionTick);
}

public sealed record CampaignSiegeRuntimeSnapshot(
    CampaignSiegeStatus Status,
    int TargetStructureId,
    int DefenderColonyId,
    int ObservedSiegeId,
    int BreachCount,
    long SiegeEnteredTick,
    long LastObservedTick,
    int SiegesEntered,
    int SiegePressureTicks,
    int BreachesObserved)
{
    public static CampaignSiegeRuntimeSnapshot From(CampaignSiegeState state)
        => new(
            state.Status,
            state.TargetStructureId,
            state.DefenderColonyId,
            state.ObservedSiegeId,
            state.BreachCount,
            state.SiegeEnteredTick,
            state.LastObservedTick,
            state.SiegesEntered,
            state.SiegePressureTicks,
            state.BreachesObserved);
}

public sealed record CampaignResolutionRuntimeSnapshot(
    bool IsResolved,
    CampaignResolutionKind Kind,
    string Reason,
    long ResolvedTick,
    Faction AttackerFaction,
    Faction DefenderFaction,
    int OriginColonyId,
    int TargetColonyId,
    int TargetStructureId,
    int LootFood,
    int LootWood,
    int LootStone,
    int LootGold,
    int WarScoreDelta,
    int CumulativeWarScore,
    bool PeaceEligible,
    bool PeaceApplied,
    string TreatyKind)
{
    public static CampaignResolutionRuntimeSnapshot From(CampaignResolutionState state)
        => new(
            state.IsResolved,
            state.Kind,
            state.Reason,
            state.ResolvedTick,
            state.AttackerFaction,
            state.DefenderFaction,
            state.OriginColonyId,
            state.TargetColonyId,
            state.TargetStructureId,
            state.LootFood,
            state.LootWood,
            state.LootStone,
            state.LootGold,
            state.WarScoreDelta,
            state.CumulativeWarScore,
            state.PeaceEligible,
            state.PeaceApplied,
            state.TreatyKind);
}

public sealed record ArmyRuntimeSnapshot(
    int ArmyId,
    Faction OwnerFaction,
    int HomeColonyId,
    int OriginX,
    int OriginY,
    int TargetX,
    int TargetY,
    int RequestedMemberCount,
    int AssignedMemberCount,
    IReadOnlyList<int> MemberActorIds,
    bool HasRallyPoint,
    int RallyX,
    int RallyY,
    bool IsAssembled,
    long AssemblyStartedTick,
    long AssemblyCompletedTick,
    string ForageConsumerKey,
    ArmySupplyRuntimeSnapshot Supply,
    ArmyRationPoolRuntimeSnapshot RationPool,
    ArmySupplyCarrierRuntimeSnapshot Carrier,
    ArmyForagingRuntimeSnapshot Foraging)
{
    public static ArmyRuntimeSnapshot From(ArmyState state)
        => new(
            state.ArmyId,
            state.OwnerFaction,
            state.HomeColonyId,
            state.OriginX,
            state.OriginY,
            state.TargetX,
            state.TargetY,
            state.RequestedMemberCount,
            state.MemberCount,
            state.MemberActorIds.ToArray(),
            state.HasRallyPoint,
            state.RallyX,
            state.RallyY,
            state.IsAssembled,
            state.AssemblyStartedTick,
            state.AssemblyCompletedTick,
            state.ForageConsumerKey,
            ArmySupplyRuntimeSnapshot.From(state.SupplyState),
            ArmyRationPoolRuntimeSnapshot.From(state.RationPoolState),
            ArmySupplyCarrierRuntimeSnapshot.From(state.CarrierState),
            ArmyForagingRuntimeSnapshot.From(state.ForagingState));
}

public sealed record CampaignRouteCountersSnapshot(
    int PathRequests,
    int PathCacheHits,
    int BlockedMovementChecks,
    int RouteRecomputes,
    int MarchProgressTicks,
    int EncounterTicks,
    int NoProgressTicks)
{
    public static CampaignRouteCountersSnapshot From(CampaignRouteCounters counters)
        => new(
            counters.PathRequests,
            counters.PathCacheHits,
            counters.BlockedMovementChecks,
            counters.RouteRecomputes,
            counters.MarchProgressTicks,
            counters.EncounterTicks,
            counters.NoProgressTicks);
}

public sealed record ArmySupplyRuntimeSnapshot(
    float FractionalFoodDemand,
    int SustainedOutOfSupplyTicks)
{
    public static ArmySupplyRuntimeSnapshot From(ArmySupplyState state)
        => new(state.FractionalFoodDemand, state.SustainedOutOfSupplyTicks);
}

public sealed record ArmyRationPoolRuntimeSnapshot(int RationPoolFood)
{
    public static ArmyRationPoolRuntimeSnapshot From(ArmyRationPoolState state)
        => new(state.RationPoolFood);
}

public sealed record ArmySupplyCarrierRuntimeSnapshot(
    int AssignedCarrierActorId,
    int LastSupplyTick,
    ArmySupplySourceMode LastSupplySource,
    bool HasAssignedCarrier)
{
    public static ArmySupplyCarrierRuntimeSnapshot From(ArmySupplyCarrierState state)
        => new(
            state.AssignedCarrierActorId,
            state.LastSupplyTick,
            state.LastSupplySource,
            state.HasAssignedCarrier);
}

public sealed record ArmyForagingRuntimeSnapshot(
    int Attempts,
    int Successes,
    int Failures,
    int FoodGained,
    int LastSourceX,
    int LastSourceY,
    string LastConsumerKey,
    ArmyForageStatus LastStatus,
    ArmyForageFailureReason LastFailureReason)
{
    public static ArmyForagingRuntimeSnapshot From(ArmyForagingState state)
        => new(
            state.Attempts,
            state.Successes,
            state.Failures,
            state.FoodGained,
            state.LastSourceX,
            state.LastSourceY,
            state.LastConsumerKey,
            state.LastStatus,
            state.LastFailureReason);
}

public sealed record SupplyConvoyRuntimeSnapshot(
    int ConvoyId,
    Faction OwnerFaction,
    int HomeColonyId,
    int TargetCampaignId,
    int TargetArmyId,
    SupplyConvoyPhase Phase,
    long CreatedTick,
    long CompletedTick,
    int OriginX,
    int OriginY,
    int CurrentX,
    int CurrentY,
    int TargetX,
    int TargetY,
    int PayloadFood,
    SupplyConvoyRouteCountersSnapshot RouteCounters)
{
    public static SupplyConvoyRuntimeSnapshot From(SupplyConvoyState state)
        => new(
            state.ConvoyId,
            state.OwnerFaction,
            state.HomeColonyId,
            state.TargetCampaignId,
            state.TargetArmyId,
            state.Phase,
            state.CreatedTick,
            state.CompletedTick,
            state.OriginX,
            state.OriginY,
            state.CurrentX,
            state.CurrentY,
            state.TargetX,
            state.TargetY,
            state.PayloadFood,
            SupplyConvoyRouteCountersSnapshot.From(state.RouteCounters));
}

public sealed record SupplyConvoyRouteCountersSnapshot(
    int PathRequests,
    int PathCacheHits,
    int RouteRecomputes,
    int ProgressTicks,
    int NoProgressTicks)
{
    public static SupplyConvoyRouteCountersSnapshot From(SupplyConvoyRouteCounters counters)
        => new(
            counters.PathRequests,
            counters.PathCacheHits,
            counters.RouteRecomputes,
            counters.ProgressTicks,
            counters.NoProgressTicks);
}

public sealed record ForwardBaseRuntimeSnapshot(
    int BaseId,
    Faction OwnerFaction,
    int HomeColonyId,
    int CampaignId,
    int ArmyId,
    ForwardBasePhase Phase,
    long CreatedTick,
    long EndedTick,
    int X,
    int Y,
    int Radius,
    string CloseReason,
    long LastLiveMemberNearTick,
    int RestTicks,
    int RestedActorTicks)
{
    public static ForwardBaseRuntimeSnapshot From(ForwardBaseState state)
        => new(
            state.BaseId,
            state.OwnerFaction,
            state.HomeColonyId,
            state.CampaignId,
            state.ArmyId,
            state.Phase,
            state.CreatedTick,
            state.EndedTick,
            state.X,
            state.Y,
            state.Radius,
            state.CloseReason,
            state.LastLiveMemberNearTick,
            state.RestTicks,
            state.RestedActorTicks);
}

public sealed record ScoutIntelRuntimeSnapshot(
    int IntelId,
    Faction OwnerFaction,
    Faction ObservedFaction,
    int ObservedColonyId,
    ScoutIntelObservationKind ObservationKind,
    int X,
    int Y,
    int SourceActorId,
    long CreatedTick,
    long LastRefreshTick,
    long ExpirationTick,
    int TicksSinceRefresh,
    float Confidence)
{
    public static ScoutIntelRuntimeSnapshot From(ScoutIntelState state, long currentTick)
        => new(
            state.IntelId,
            state.OwnerFaction,
            state.ObservedFaction,
            state.ObservedColonyId,
            state.ObservationKind,
            state.X,
            state.Y,
            state.SourceActorId,
            state.CreatedTick,
            state.LastRefreshTick,
            state.ExpirationTick,
            CalculateTicksSinceRefresh(state, currentTick),
            state.Confidence);

    private static int CalculateTicksSinceRefresh(ScoutIntelState state, long currentTick)
        => (int)Math.Min(int.MaxValue, Math.Max(0, currentTick - state.LastRefreshTick));
}
