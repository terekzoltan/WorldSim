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
            ArmyRuntimeSnapshot.From(state.Army));
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
