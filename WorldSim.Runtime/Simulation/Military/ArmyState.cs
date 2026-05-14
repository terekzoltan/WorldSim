using System;
using System.Collections.Generic;

namespace WorldSim.Simulation.Military;

public sealed class ArmyState
{
    private readonly List<int> _memberActorIds = new();

    public ArmyState(
        int armyId,
        Faction ownerFaction,
        int homeColonyId,
        int originX,
        int originY,
        int targetX,
        int targetY,
        int requestedMemberCount)
    {
        ArmyId = Math.Max(0, armyId);
        OwnerFaction = ownerFaction;
        HomeColonyId = homeColonyId;
        OriginX = originX;
        OriginY = originY;
        TargetX = targetX;
        TargetY = targetY;
        RequestedMemberCount = Math.Max(0, requestedMemberCount);
        ForageConsumerKey = $"army:{ArmyId}";
    }

    public int ArmyId { get; }
    public Faction OwnerFaction { get; }
    public int HomeColonyId { get; }
    public int OriginX { get; }
    public int OriginY { get; }
    public int TargetX { get; }
    public int TargetY { get; }
    public int RequestedMemberCount { get; }
    public IReadOnlyList<int> MemberActorIds => _memberActorIds.ToArray();
    public string ForageConsumerKey { get; }
    public ArmySupplyState SupplyState { get; } = new();
    public ArmyRationPoolState RationPoolState { get; } = new();
    public ArmySupplyCarrierState CarrierState { get; } = new();
    public ArmyForagingState ForagingState { get; } = new();
}
