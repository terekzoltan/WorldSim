using System;
using System.Collections.Generic;

namespace WorldSim.Simulation.Military;

public enum ArmySupplySourceMode
{
    None,
    CarriedInventory,
    RationPool
}

public enum ArmySupplyCarrierTickStatus
{
    Processed,
    AlreadyProcessed,
    RejectedMixedSupplySource
}

public enum ArmySupplyCarrierAssignmentStatus
{
    Assigned,
    AlreadyAssigned,
    Cleared,
    IgnoredWrongCarrier,
    NoCarrierAssigned
}

public sealed class ArmySupplyCarrierState
{
    private Person? _assignedCarrier;

    public int AssignedCarrierActorId => _assignedCarrier?.Id ?? -1;
    public int LastSupplyTick { get; private set; } = -1;
    public ArmySupplySourceMode LastSupplySource { get; private set; } = ArmySupplySourceMode.None;

    public bool HasAssignedCarrier => AssignedCarrierActorId >= 0;

    internal Person? AssignedCarrier => _assignedCarrier;

    internal void AssignCarrier(Person carrier)
        => _assignedCarrier = carrier;

    internal void ClearCarrier()
        => _assignedCarrier = null;

    internal ArmySupplyCarrierTickStatus TryBeginSupplyTick(int tick, ArmySupplySourceMode source)
    {
        if (LastSupplyTick == tick)
        {
            return LastSupplySource == source
                ? ArmySupplyCarrierTickStatus.AlreadyProcessed
                : ArmySupplyCarrierTickStatus.RejectedMixedSupplySource;
        }

        LastSupplyTick = tick;
        LastSupplySource = source;
        return ArmySupplyCarrierTickStatus.Processed;
    }
}

public sealed record ArmySupplyCarrierAssignmentResult(
    ArmySupplyCarrierAssignmentStatus Status,
    int CarrierActorId,
    int AssignedCarrierActorId,
    bool IsAssigned,
    bool IsSupplyCarrier);

public sealed record ArmySupplyCarrierTickResult(
    ArmySupplyCarrierTickStatus Status,
    ArmySupplySourceMode RequestedSource,
    ArmySupplySourceMode ActiveSource,
    int Tick,
    ArmySupplyTickResult? CarriedInventoryResult,
    ArmyRationPoolSupplyTickResult? RationPoolResult);

public static class ArmySupplyCarrierModel
{
    public static ArmySupplyCarrierAssignmentResult AssignCarrier(Person carrier, ArmySupplyCarrierState state)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(state);

        if (state.AssignedCarrier?.Id == carrier.Id)
        {
            carrier.AssignRole(PersonRole.SupplyCarrier);
            return BuildAssignmentResult(
                ArmySupplyCarrierAssignmentStatus.AlreadyAssigned,
                carrier,
                state,
                isAssigned: true);
        }

        state.AssignedCarrier?.ClearRole(PersonRole.SupplyCarrier);
        carrier.AssignRole(PersonRole.SupplyCarrier);
        state.AssignCarrier(carrier);

        return BuildAssignmentResult(
            ArmySupplyCarrierAssignmentStatus.Assigned,
            carrier,
            state,
            isAssigned: true);
    }

    public static ArmySupplyCarrierAssignmentResult ClearCarrier(Person carrier, ArmySupplyCarrierState state)
    {
        ArgumentNullException.ThrowIfNull(carrier);
        ArgumentNullException.ThrowIfNull(state);

        if (state.AssignedCarrier == null)
        {
            return BuildAssignmentResult(
                ArmySupplyCarrierAssignmentStatus.NoCarrierAssigned,
                carrier,
                state,
                isAssigned: false);
        }

        if (state.AssignedCarrier.Id != carrier.Id)
        {
            return BuildAssignmentResult(
                ArmySupplyCarrierAssignmentStatus.IgnoredWrongCarrier,
                carrier,
                state,
                isAssigned: true);
        }

        carrier.ClearRole(PersonRole.SupplyCarrier);
        state.ClearCarrier();

        return BuildAssignmentResult(
            ArmySupplyCarrierAssignmentStatus.Cleared,
            carrier,
            state,
            isAssigned: false);
    }

    public static ArmySupplyCarrierTickResult TickCarriedInventory(
        IReadOnlyList<Person> members,
        ArmySupplyState supplyState,
        ArmySupplyCarrierState carrierState,
        int tick,
        float dt,
        ArmySupplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(supplyState);
        ArgumentNullException.ThrowIfNull(carrierState);

        var guard = carrierState.TryBeginSupplyTick(tick, ArmySupplySourceMode.CarriedInventory);
        if (guard != ArmySupplyCarrierTickStatus.Processed)
            return BuildNoOpResult(guard, ArmySupplySourceMode.CarriedInventory, carrierState.LastSupplySource, tick);

        var result = ArmySupplyModel.Tick(members, supplyState, dt, options);
        return new ArmySupplyCarrierTickResult(
            ArmySupplyCarrierTickStatus.Processed,
            ArmySupplySourceMode.CarriedInventory,
            ArmySupplySourceMode.CarriedInventory,
            tick,
            result,
            RationPoolResult: null);
    }

    public static ArmySupplyCarrierTickResult TickRationPool(
        IReadOnlyList<Person> members,
        ArmySupplyState supplyState,
        ArmySupplyCarrierState carrierState,
        ArmyRationPoolState rationPool,
        int tick,
        float dt,
        ArmySupplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(supplyState);
        ArgumentNullException.ThrowIfNull(carrierState);
        ArgumentNullException.ThrowIfNull(rationPool);

        var guard = carrierState.TryBeginSupplyTick(tick, ArmySupplySourceMode.RationPool);
        if (guard != ArmySupplyCarrierTickStatus.Processed)
            return BuildNoOpResult(guard, ArmySupplySourceMode.RationPool, carrierState.LastSupplySource, tick);

        var result = ArmyRationPoolSupplyModel.TickRationPool(members, supplyState, rationPool, dt, options);
        return new ArmySupplyCarrierTickResult(
            ArmySupplyCarrierTickStatus.Processed,
            ArmySupplySourceMode.RationPool,
            ArmySupplySourceMode.RationPool,
            tick,
            CarriedInventoryResult: null,
            result);
    }

    private static ArmySupplyCarrierTickResult BuildNoOpResult(
        ArmySupplyCarrierTickStatus status,
        ArmySupplySourceMode requestedSource,
        ArmySupplySourceMode activeSource,
        int tick)
        => new(
            status,
            requestedSource,
            activeSource,
            tick,
            CarriedInventoryResult: null,
            RationPoolResult: null);

    private static ArmySupplyCarrierAssignmentResult BuildAssignmentResult(
        ArmySupplyCarrierAssignmentStatus status,
        Person carrier,
        ArmySupplyCarrierState state,
        bool isAssigned)
        => new(
            status,
            carrier.Id,
            state.AssignedCarrierActorId,
            isAssigned,
            carrier.HasRole(PersonRole.SupplyCarrier));
}
