using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave9ArmySupplyModelTests
{
    [Fact]
    public void AggregateSupply_CountsLivingMembersOnly()
    {
        var world = CreateWorld(seed: 9001, initialPop: 3);
        var members = world._people.OrderBy(person => person.Id).Take(3).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 1));
        Assert.True(members[2].Inventory.TryAdd(ItemType.Food, 3));
        members[2].Health = 0f;

        var result = ArmySupplyModel.Tick(
            members,
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 0f));

        Assert.Equal(2, result.ActiveMemberCount);
        Assert.Equal(3, result.CarriedFoodBefore);
        Assert.Equal(3, result.CarriedFoodAfter);
        Assert.Equal(0, result.FoodConsumed);
    }

    [Fact]
    public void MarchingConsumption_RemovesFoodDeterministicallyByPersonId()
    {
        var world = CreateWorld(seed: 9002, initialPop: 3);
        var membersById = world._people.OrderBy(person => person.Id).Take(3).ToArray();
        Assert.True(membersById[0].Inventory.TryAdd(ItemType.Food, 1));
        Assert.True(membersById[1].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(membersById[2].Inventory.TryAdd(ItemType.Food, 3));
        var reversedInput = membersById.Reverse().ToArray();

        var result = ArmySupplyModel.Tick(
            reversedInput,
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));

        Assert.Equal(6, result.CarriedFoodBefore);
        Assert.Equal(3, result.FoodDemandUnits);
        Assert.Equal(3, result.FoodConsumed);
        Assert.Equal(3, result.CarriedFoodAfter);
        Assert.Equal(0, membersById[0].Inventory.GetCount(ItemType.Food));
        Assert.Equal(0, membersById[1].Inventory.GetCount(ItemType.Food));
        Assert.Equal(3, membersById[2].Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void FractionalDemand_AccumulatesDeterministicallyAcrossTicks()
    {
        var world = CreateWorld(seed: 9003, initialPop: 1);
        var member = world._people[0];
        Assert.True(member.Inventory.TryAdd(ItemType.Food, 2));
        var state = new ArmySupplyState();
        var options = new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 0.5f);

        var first = ArmySupplyModel.Tick(new[] { member }, state, dt: 1f, options);
        var second = ArmySupplyModel.Tick(new[] { member }, state, dt: 1f, options);

        Assert.Equal(0, first.FoodDemandUnits);
        Assert.Equal(0.5f, first.FractionalFoodDemand, precision: 3);
        Assert.Equal(1, second.FoodDemandUnits);
        Assert.Equal(1, second.FoodConsumed);
        Assert.Equal(0f, second.FractionalFoodDemand, precision: 3);
        Assert.Equal(1, member.Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void FractionalDemand_ZeroFoodBelowWholeUnit_IsNotOutOfSupplyYet()
    {
        var world = CreateWorld(seed: 9010, initialPop: 1);
        var member = world._people[0];
        var moraleBefore = member.CombatMorale;
        var staminaBefore = member.Stamina;

        var result = ArmySupplyModel.Tick(
            new[] { member },
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(
                FoodConsumedPerPersonPerSecond: 0.5f,
                OutOfSupplyMoraleLossPerSecond: 50f,
                OutOfSupplyStaminaLossPerSecond: 50f,
                RouteAfterOutOfSupplyTicks: 1));

        Assert.Equal(0, result.FoodDemandUnits);
        Assert.Equal(0, result.UnmetFoodDemandUnits);
        Assert.False(result.IsOutOfSupply);
        Assert.Equal(0, result.SustainedOutOfSupplyTicks);
        Assert.Equal(0, result.AttritionEventCount);
        Assert.Equal(0, result.RoutedMemberCount);
        Assert.Equal(moraleBefore, member.CombatMorale);
        Assert.Equal(staminaBefore, member.Stamina);
        Assert.False(member.IsRouting);
    }

    [Fact]
    public void FractionalDemand_ZeroFoodStartsPressureWhenWholeUnitIsUnmet()
    {
        var world = CreateWorld(seed: 9011, initialPop: 1);
        var member = world._people[0];
        var state = new ArmySupplyState();
        var options = new ArmySupplyOptions(
            FoodConsumedPerPersonPerSecond: 0.5f,
            OutOfSupplyMoraleLossPerSecond: 5f,
            OutOfSupplyStaminaLossPerSecond: 7f,
            RouteAfterOutOfSupplyTicks: 99);

        var first = ArmySupplyModel.Tick(new[] { member }, state, dt: 1f, options);
        var second = ArmySupplyModel.Tick(new[] { member }, state, dt: 1f, options);

        Assert.Equal(0, first.FoodDemandUnits);
        Assert.Equal(0, first.UnmetFoodDemandUnits);
        Assert.False(first.IsOutOfSupply);
        Assert.Equal(0, first.SustainedOutOfSupplyTicks);
        Assert.Equal(0, first.AttritionEventCount);

        Assert.Equal(1, second.FoodDemandUnits);
        Assert.Equal(1, second.UnmetFoodDemandUnits);
        Assert.True(second.IsOutOfSupply);
        Assert.Equal(1, second.SustainedOutOfSupplyTicks);
        Assert.Equal(1, second.AttritionEventCount);
        Assert.Equal(95f, member.CombatMorale);
        Assert.Equal(93f, member.Stamina);
        Assert.False(member.IsRouting);
    }

    [Fact]
    public void FractionalDemand_NonEmptySupplyBelowWholeUnit_IsNotOutOfSupply()
    {
        var world = CreateWorld(seed: 9012, initialPop: 1);
        var member = world._people[0];
        Assert.True(member.Inventory.TryAdd(ItemType.Food, 1));
        var moraleBefore = member.CombatMorale;
        var staminaBefore = member.Stamina;

        var result = ArmySupplyModel.Tick(
            new[] { member },
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(
                FoodConsumedPerPersonPerSecond: 0.5f,
                OutOfSupplyMoraleLossPerSecond: 50f,
                OutOfSupplyStaminaLossPerSecond: 50f,
                RouteAfterOutOfSupplyTicks: 1));

        Assert.Equal(0, result.FoodDemandUnits);
        Assert.Equal(0, result.UnmetFoodDemandUnits);
        Assert.False(result.IsOutOfSupply);
        Assert.Equal(0, result.SustainedOutOfSupplyTicks);
        Assert.Equal(0, result.AttritionEventCount);
        Assert.Equal(0, result.RoutedMemberCount);
        Assert.Equal(1, member.Inventory.GetCount(ItemType.Food));
        Assert.Equal(moraleBefore, member.CombatMorale);
        Assert.Equal(staminaBefore, member.Stamina);
        Assert.False(member.IsRouting);
    }

    [Fact]
    public void NoOpCases_DoNotConsumeOrMutatePressureState()
    {
        var world = CreateWorld(seed: 9004, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        var state = new ArmySupplyState();
        _ = ArmySupplyModel.Tick(
            new[] { members[0] },
            state,
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 0.5f));

        var dtNoOp = ArmySupplyModel.Tick(members, state, dt: 0f);
        members[0].Health = 0f;
        members[1].Health = 0f;
        var allDeadNoOp = ArmySupplyModel.Tick(members, state, dt: 1f);
        var emptyNoOp = ArmySupplyModel.Tick(Array.Empty<Person>(), state, dt: 1f);

        Assert.Equal(0, dtNoOp.FoodConsumed);
        Assert.Equal(0, allDeadNoOp.FoodConsumed);
        Assert.Equal(0, emptyNoOp.FoodConsumed);
        Assert.Equal(0.5f, state.FractionalFoodDemand, precision: 3);
        Assert.Equal(0, state.SustainedOutOfSupplyTicks);
        Assert.Equal(2, members[0].Inventory.GetCount(ItemType.Food));
    }

    [Fact]
    public void LowSupply_IsObservableWithoutDefaultAttrition()
    {
        var world = CreateWorld(seed: 9005, initialPop: 3);
        var members = world._people.Take(3).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 1));
        var moraleBefore = members[0].CombatMorale;
        var staminaBefore = members[0].Stamina;

        var result = ArmySupplyModel.Tick(
            members,
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 0f));

        Assert.True(result.IsLowSupply);
        Assert.False(result.IsOutOfSupply);
        Assert.Equal(0, result.AttritionEventCount);
        Assert.Equal(moraleBefore, members[0].CombatMorale);
        Assert.Equal(staminaBefore, members[0].Stamina);
    }

    [Fact]
    public void OutOfSupply_AppliesMoraleAndStaminaAttrition()
    {
        var world = CreateWorld(seed: 9006, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        var options = new ArmySupplyOptions(
            FoodConsumedPerPersonPerSecond: 1f,
            OutOfSupplyMoraleLossPerSecond: 5f,
            OutOfSupplyStaminaLossPerSecond: 7f,
            RouteAfterOutOfSupplyTicks: 99);

        var result = ArmySupplyModel.Tick(members, new ArmySupplyState(), dt: 1f, options);

        Assert.True(result.IsOutOfSupply);
        Assert.Equal(2, result.UnmetFoodDemandUnits);
        Assert.Equal(2, result.AttritionEventCount);
        Assert.Equal(-5f, result.MoraleDeltaApplied);
        Assert.Equal(-7f, result.StaminaDeltaApplied);
        Assert.Equal(95f, members[0].CombatMorale);
        Assert.Equal(93f, members[0].Stamina);
    }

    [Fact]
    public void SustainedOutOfSupply_TriggersRoutingAfterConservativeThreshold()
    {
        var world = CreateWorld(seed: 9007, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        var state = new ArmySupplyState();
        var options = new ArmySupplyOptions(
            FoodConsumedPerPersonPerSecond: 1f,
            OutOfSupplyMoraleLossPerSecond: 35f,
            OutOfSupplyStaminaLossPerSecond: 45f,
            RouteAfterOutOfSupplyTicks: 2,
            RouteMoraleThreshold: 80f,
            RouteStaminaThreshold: 80f,
            RoutingTicks: 4);

        var first = ArmySupplyModel.Tick(members, state, dt: 1f, options);
        var second = ArmySupplyModel.Tick(members, state, dt: 1f, options);

        Assert.True(first.IsOutOfSupply);
        Assert.Equal(0, first.RoutedMemberCount);
        Assert.True(second.IsOutOfSupply);
        Assert.Equal(2, second.SustainedOutOfSupplyTicks);
        Assert.Equal(2, second.RoutedMemberCount);
        Assert.All(members, member => Assert.True(member.IsRouting));
    }

    [Fact]
    public void StaminaDelta_IsClamped()
    {
        var world = CreateWorld(seed: 9008, initialPop: 1);
        var member = world._people[0];

        member.ApplyStaminaDelta(-500f);
        Assert.Equal(0f, member.Stamina);

        member.ApplyStaminaDelta(700f);
        Assert.Equal(100f, member.Stamina);
    }

    [Fact]
    public void Consumption_PreservesNoDupingInvariant()
    {
        var world = CreateWorld(seed: 9009, initialPop: 3);
        var members = world._people.Take(3).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[2].Inventory.TryAdd(ItemType.Food, 2));
        var before = members.Sum(member => member.Inventory.GetCount(ItemType.Food));

        var result = ArmySupplyModel.Tick(
            members,
            new ArmySupplyState(),
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));
        var after = members.Sum(member => member.Inventory.GetCount(ItemType.Food));

        Assert.Equal(before - result.FoodConsumed, after);
        Assert.Equal(3, result.FoodConsumed);
        Assert.Equal(3, after);
    }

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
