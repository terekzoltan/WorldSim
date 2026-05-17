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
    public int MemberCount => _memberActorIds.Count;
    public IReadOnlyList<int> MemberActorIds => _memberActorIds.ToArray();
    public bool HasRallyPoint { get; private set; }
    public int RallyX { get; private set; } = -1;
    public int RallyY { get; private set; } = -1;
    public bool IsAssembled { get; private set; }
    public long AssemblyStartedTick { get; private set; } = -1;
    public long AssemblyCompletedTick { get; private set; } = -1;
    public string ForageConsumerKey { get; }
    public ArmySupplyState SupplyState { get; } = new();
    public ArmyRationPoolState RationPoolState { get; } = new();
    public ArmySupplyCarrierState CarrierState { get; } = new();
    public ArmyForagingState ForagingState { get; } = new();

    internal bool HasMemberActorId(int actorId)
        => _memberActorIds.Contains(actorId);

    internal bool TryAddMemberActorId(int actorId)
    {
        if (actorId < 0 || _memberActorIds.Contains(actorId) || _memberActorIds.Count >= RequestedMemberCount)
            return false;

        _memberActorIds.Add(actorId);
        return true;
    }

    internal bool RemoveMemberActorId(int actorId)
        => _memberActorIds.Remove(actorId);

    internal void SetRallyPoint(int x, int y)
    {
        HasRallyPoint = true;
        RallyX = x;
        RallyY = y;
    }

    internal void BeginAssembly(long tick)
    {
        if (AssemblyStartedTick < 0)
            AssemblyStartedTick = Math.Max(0, tick);
    }

    internal void MarkAssemblyComplete(long tick)
    {
        IsAssembled = true;
        if (AssemblyCompletedTick < 0)
            AssemblyCompletedTick = Math.Max(0, tick);
    }
}
