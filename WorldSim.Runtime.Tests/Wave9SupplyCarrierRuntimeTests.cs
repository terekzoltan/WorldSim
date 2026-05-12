using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave9SupplyCarrierRuntimeTests
{
    [Fact]
    public void TickCarriedInventory_ConsumesMemberFoodWithoutMutatingRationPool()
    {
        var world = CreateWorld(seed: 9201, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 2));
        var pool = new ArmyRationPoolState(rationPoolFood: 5);

        var result = ArmySupplyCarrierModel.TickCarriedInventory(
            members,
            new ArmySupplyState(),
            new ArmySupplyCarrierState(),
            tick: 10,
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, result.Status);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, result.ActiveSource);
        Assert.NotNull(result.CarriedInventoryResult);
        Assert.Null(result.RationPoolResult);
        Assert.Equal(2, result.CarriedInventoryResult.FoodConsumed);
        Assert.Equal(2, members.Sum(member => member.Inventory.GetCount(ItemType.Food)));
        Assert.Equal(5, pool.RationPoolFood);
    }

    [Fact]
    public void TickRationPool_ConsumesPoolWithoutMutatingMemberFood()
    {
        var world = CreateWorld(seed: 9202, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 2));
        var pool = new ArmyRationPoolState(rationPoolFood: 5);

        var result = ArmySupplyCarrierModel.TickRationPool(
            members,
            new ArmySupplyState(),
            new ArmySupplyCarrierState(),
            pool,
            tick: 10,
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, result.Status);
        Assert.Equal(ArmySupplySourceMode.RationPool, result.ActiveSource);
        Assert.Null(result.CarriedInventoryResult);
        Assert.NotNull(result.RationPoolResult);
        Assert.Equal(2, result.RationPoolResult.FoodConsumed);
        Assert.Equal(4, members.Sum(member => member.Inventory.GetCount(ItemType.Food)));
        Assert.Equal(3, pool.RationPoolFood);
    }

    [Fact]
    public void MixedSourceTick_CarriedThenRationPool_IsRejectedWithoutMutation()
    {
        var world = CreateWorld(seed: 9203, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 3));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 3));
        var supplyState = new ArmySupplyState();
        var carrierState = new ArmySupplyCarrierState();
        var pool = new ArmyRationPoolState(rationPoolFood: 5);
        var options = new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f);

        var first = ArmySupplyCarrierModel.TickCarriedInventory(members, supplyState, carrierState, tick: 11, dt: 1f, options);
        var beforeRejected = CaptureState(members, supplyState, pool);
        var rejected = ArmySupplyCarrierModel.TickRationPool(members, supplyState, carrierState, pool, tick: 11, dt: 1f, options);
        var afterRejected = CaptureState(members, supplyState, pool);

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, first.Status);
        Assert.Equal(ArmySupplyCarrierTickStatus.RejectedMixedSupplySource, rejected.Status);
        Assert.Equal(ArmySupplySourceMode.RationPool, rejected.RequestedSource);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, rejected.ActiveSource);
        Assert.Null(rejected.CarriedInventoryResult);
        Assert.Null(rejected.RationPoolResult);
        Assert.Equal(beforeRejected, afterRejected);
    }

    [Fact]
    public void MixedSourceTick_RationPoolThenCarried_IsRejectedWithoutMutation()
    {
        var world = CreateWorld(seed: 9204, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 3));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 3));
        var supplyState = new ArmySupplyState();
        var carrierState = new ArmySupplyCarrierState();
        var pool = new ArmyRationPoolState(rationPoolFood: 5);
        var options = new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f);

        var first = ArmySupplyCarrierModel.TickRationPool(members, supplyState, carrierState, pool, tick: 12, dt: 1f, options);
        var beforeRejected = CaptureState(members, supplyState, pool);
        var rejected = ArmySupplyCarrierModel.TickCarriedInventory(members, supplyState, carrierState, tick: 12, dt: 1f, options);
        var afterRejected = CaptureState(members, supplyState, pool);

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, first.Status);
        Assert.Equal(ArmySupplyCarrierTickStatus.RejectedMixedSupplySource, rejected.Status);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, rejected.RequestedSource);
        Assert.Equal(ArmySupplySourceMode.RationPool, rejected.ActiveSource);
        Assert.Null(rejected.CarriedInventoryResult);
        Assert.Null(rejected.RationPoolResult);
        Assert.Equal(beforeRejected, afterRejected);
    }

    [Fact]
    public void SameSourceTick_CarriedInventoryRepeat_IsAlreadyProcessedNoOp()
    {
        var world = CreateWorld(seed: 9205, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 3));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 3));
        var supplyState = new ArmySupplyState();
        var carrierState = new ArmySupplyCarrierState();
        var pool = new ArmyRationPoolState(rationPoolFood: 5);
        var options = new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f);

        var first = ArmySupplyCarrierModel.TickCarriedInventory(members, supplyState, carrierState, tick: 13, dt: 1f, options);
        var beforeRepeat = CaptureState(members, supplyState, pool);
        var repeat = ArmySupplyCarrierModel.TickCarriedInventory(members, supplyState, carrierState, tick: 13, dt: 1f, options);
        var afterRepeat = CaptureState(members, supplyState, pool);

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, first.Status);
        Assert.Equal(ArmySupplyCarrierTickStatus.AlreadyProcessed, repeat.Status);
        Assert.Equal(ArmySupplySourceMode.CarriedInventory, repeat.ActiveSource);
        Assert.Null(repeat.CarriedInventoryResult);
        Assert.Null(repeat.RationPoolResult);
        Assert.Equal(beforeRepeat, afterRepeat);
    }

    [Fact]
    public void SameSourceTick_RationPoolRepeat_IsAlreadyProcessedNoOp()
    {
        var world = CreateWorld(seed: 9206, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 3));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 3));
        var supplyState = new ArmySupplyState();
        var carrierState = new ArmySupplyCarrierState();
        var pool = new ArmyRationPoolState(rationPoolFood: 5);
        var options = new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f);

        var first = ArmySupplyCarrierModel.TickRationPool(members, supplyState, carrierState, pool, tick: 14, dt: 1f, options);
        var beforeRepeat = CaptureState(members, supplyState, pool);
        var repeat = ArmySupplyCarrierModel.TickRationPool(members, supplyState, carrierState, pool, tick: 14, dt: 1f, options);
        var afterRepeat = CaptureState(members, supplyState, pool);

        Assert.Equal(ArmySupplyCarrierTickStatus.Processed, first.Status);
        Assert.Equal(ArmySupplyCarrierTickStatus.AlreadyProcessed, repeat.Status);
        Assert.Equal(ArmySupplySourceMode.RationPool, repeat.ActiveSource);
        Assert.Null(repeat.CarriedInventoryResult);
        Assert.Null(repeat.RationPoolResult);
        Assert.Equal(beforeRepeat, afterRepeat);
    }

    [Fact]
    public void SupplyCarrierRole_CanBeAssignedAndReadBack()
    {
        var world = CreateWorld(seed: 9207, initialPop: 1);
        var carrier = world._people[0];
        var state = new ArmySupplyCarrierState();

        var assigned = ArmySupplyCarrierModel.AssignCarrier(carrier, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.Assigned, assigned.Status);
        Assert.True(assigned.IsAssigned);
        Assert.Equal(carrier.Id, assigned.CarrierActorId);
        Assert.Equal(carrier.Id, assigned.AssignedCarrierActorId);
        Assert.True(assigned.IsSupplyCarrier);
        Assert.True(carrier.HasRole(PersonRole.SupplyCarrier));
        Assert.True(state.HasAssignedCarrier);
        Assert.Equal(carrier.Id, state.AssignedCarrierActorId);
    }

    [Fact]
    public void SupplyCarrierRole_IsVisibleInSnapshot()
    {
        var world = CreateWorld(seed: 9208, initialPop: 2);
        var carrier = world._people.OrderBy(person => person.Id).First();
        var nonCarrier = world._people.OrderBy(person => person.Id).Skip(1).First();

        _ = ArmySupplyCarrierModel.AssignCarrier(carrier, new ArmySupplyCarrierState());
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.True(snapshot.People.First(person => person.ActorId == carrier.Id).IsSupplyCarrier);
        Assert.False(snapshot.People.First(person => person.ActorId == nonCarrier.Id).IsSupplyCarrier);
        AssertSnapshotCarrierIds(world, carrier.Id);
    }

    [Fact]
    public void AssignCarrier_ReassignmentClearsPreviousCarrierRoleAndMarksOnlyNewCarrier()
    {
        var world = CreateWorld(seed: 9209, initialPop: 2);
        var firstCarrier = world._people.OrderBy(person => person.Id).First();
        var secondCarrier = world._people.OrderBy(person => person.Id).Skip(1).First();
        var state = new ArmySupplyCarrierState();

        var first = ArmySupplyCarrierModel.AssignCarrier(firstCarrier, state);
        var second = ArmySupplyCarrierModel.AssignCarrier(secondCarrier, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.Assigned, first.Status);
        Assert.Equal(ArmySupplyCarrierAssignmentStatus.Assigned, second.Status);
        Assert.False(firstCarrier.HasRole(PersonRole.SupplyCarrier));
        Assert.True(secondCarrier.HasRole(PersonRole.SupplyCarrier));
        Assert.True(state.HasAssignedCarrier);
        Assert.Equal(secondCarrier.Id, state.AssignedCarrierActorId);
        Assert.Equal(secondCarrier.Id, second.AssignedCarrierActorId);
        AssertSnapshotCarrierIds(world, secondCarrier.Id);
    }

    [Fact]
    public void ClearCarrier_WrongActorDoesNotClearStateOrAssignedRole()
    {
        var world = CreateWorld(seed: 9210, initialPop: 2);
        var assignedCarrier = world._people.OrderBy(person => person.Id).First();
        var wrongActor = world._people.OrderBy(person => person.Id).Skip(1).First();
        var state = new ArmySupplyCarrierState();
        _ = ArmySupplyCarrierModel.AssignCarrier(assignedCarrier, state);

        var result = ArmySupplyCarrierModel.ClearCarrier(wrongActor, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.IgnoredWrongCarrier, result.Status);
        Assert.True(result.IsAssigned);
        Assert.Equal(wrongActor.Id, result.CarrierActorId);
        Assert.Equal(assignedCarrier.Id, result.AssignedCarrierActorId);
        Assert.True(assignedCarrier.HasRole(PersonRole.SupplyCarrier));
        Assert.False(wrongActor.HasRole(PersonRole.SupplyCarrier));
        Assert.True(state.HasAssignedCarrier);
        Assert.Equal(assignedCarrier.Id, state.AssignedCarrierActorId);
        AssertSnapshotCarrierIds(world, assignedCarrier.Id);
    }

    [Fact]
    public void ClearCarrier_AssignedActorClearsRoleAndState()
    {
        var world = CreateWorld(seed: 9211, initialPop: 1);
        var carrier = world._people[0];
        var state = new ArmySupplyCarrierState();
        _ = ArmySupplyCarrierModel.AssignCarrier(carrier, state);

        var result = ArmySupplyCarrierModel.ClearCarrier(carrier, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.Cleared, result.Status);
        Assert.False(result.IsAssigned);
        Assert.Equal(carrier.Id, result.CarrierActorId);
        Assert.Equal(-1, result.AssignedCarrierActorId);
        Assert.False(result.IsSupplyCarrier);
        Assert.False(carrier.HasRole(PersonRole.SupplyCarrier));
        Assert.False(state.HasAssignedCarrier);
        Assert.Equal(-1, state.AssignedCarrierActorId);
        AssertSnapshotCarrierIds(world);
    }

    [Fact]
    public void AssignCarrier_SameActorReassignmentIsIdempotent()
    {
        var world = CreateWorld(seed: 9212, initialPop: 1);
        var carrier = world._people[0];
        var state = new ArmySupplyCarrierState();
        var first = ArmySupplyCarrierModel.AssignCarrier(carrier, state);

        var second = ArmySupplyCarrierModel.AssignCarrier(carrier, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.Assigned, first.Status);
        Assert.Equal(ArmySupplyCarrierAssignmentStatus.AlreadyAssigned, second.Status);
        Assert.True(second.IsAssigned);
        Assert.Equal(carrier.Id, second.CarrierActorId);
        Assert.Equal(carrier.Id, second.AssignedCarrierActorId);
        Assert.True(carrier.HasRole(PersonRole.SupplyCarrier));
        Assert.Equal(carrier.Id, state.AssignedCarrierActorId);
        AssertSnapshotCarrierIds(world, carrier.Id);
    }

    [Fact]
    public void ClearCarrier_NoAssignedCarrierIsSafeNoOp()
    {
        var world = CreateWorld(seed: 9213, initialPop: 1);
        var carrier = world._people[0];
        var state = new ArmySupplyCarrierState();

        var result = ArmySupplyCarrierModel.ClearCarrier(carrier, state);

        Assert.Equal(ArmySupplyCarrierAssignmentStatus.NoCarrierAssigned, result.Status);
        Assert.False(result.IsAssigned);
        Assert.Equal(carrier.Id, result.CarrierActorId);
        Assert.Equal(-1, result.AssignedCarrierActorId);
        Assert.False(carrier.HasRole(PersonRole.SupplyCarrier));
        Assert.False(state.HasAssignedCarrier);
        Assert.Equal(-1, state.AssignedCarrierActorId);
        AssertSnapshotCarrierIds(world);
    }

    private static SupplySideEffectSnapshot CaptureState(
        Person[] members,
        ArmySupplyState supplyState,
        ArmyRationPoolState pool)
        => new(
            CarriedFood: members.Sum(member => member.Inventory.GetCount(ItemType.Food)),
            RationPoolFood: pool.RationPoolFood,
            FractionalFoodDemand: supplyState.FractionalFoodDemand,
            SustainedOutOfSupplyTicks: supplyState.SustainedOutOfSupplyTicks,
            Morale: members.Sum(member => member.CombatMorale),
            Stamina: members.Sum(member => member.Stamina),
            RoutingCount: members.Count(member => member.IsRouting));

    private static World CreateWorld(int seed, int initialPop)
    {
        var world = new World(
            width: 24,
            height: 18,
            initialPop: initialPop,
            brainFactory: _ => new RuntimeNpcBrain(new FixedBrain()),
            randomSeed: seed)
        {
            BirthRateMultiplier = 0f
        };

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        foreach (var colony in world._colonies)
            colony.Stock[Resource.Food] = 0;

        foreach (var person in world._people)
        {
            person.Age = 30f;
            person.Health = 100f;
        }

        return world;
    }

    private static void AssertSnapshotCarrierIds(World world, params int[] expectedActorIds)
    {
        var actual = WorldSnapshotBuilder.Build(world)
            .People
            .Where(person => person.IsSupplyCarrier)
            .Select(person => person.ActorId)
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(expectedActorIds.OrderBy(id => id).ToArray(), actual);
    }

    private sealed record SupplySideEffectSnapshot(
        int CarriedFood,
        int RationPoolFood,
        float FractionalFoodDemand,
        int SustainedOutOfSupplyTicks,
        float Morale,
        float Stamina,
        int RoutingCount);

    private sealed class FixedBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            var trace = new AiDecisionTrace(
                SelectedGoal: "Fixed",
                PlannerName: "Fixed",
                PolicyName: "Test",
                PlanLength: 1,
                PlanPreview: new[] { NpcCommand.Idle },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "FixedMethod",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(NpcCommand.Idle, trace);
        }
    }
}
