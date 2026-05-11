using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave9ArmyRationPoolSupplyModelTests
{
    [Fact]
    public void ReserveRations_RemovesColonyFoodIntoPool()
    {
        var world = CreateWorld(seed: 9101, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 10,
            armySize: 8,
            pool);

        Assert.Equal(ArmyRationReservationStatus.Reserved, result.Status);
        Assert.Equal(100, result.ColonyFoodBefore);
        Assert.Equal(76, result.ColonyFoodAfter);
        Assert.Equal(20, result.MinHomeReserveFood);
        Assert.Equal(25, result.MaxReserveByFraction);
        Assert.Equal(24, result.DesiredFood);
        Assert.Equal(24, result.ReservedFood);
        Assert.Equal(24, result.RationPoolFoodAfter);
        Assert.Equal(76, colony.Stock[Resource.Food]);
        Assert.Equal(24, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_RespectsHomeReserve()
    {
        var world = CreateWorld(seed: 9102, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 20;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 10,
            armySize: 5,
            pool);

        Assert.Equal(ArmyRationReservationStatus.NoEligibleFood, result.Status);
        Assert.Equal(0, result.ReservedFood);
        Assert.Equal(20, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_RespectsMaxReserveFraction()
    {
        var world = CreateWorld(seed: 9103, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 0,
            armySize: 100,
            pool);

        Assert.Equal(ArmyRationReservationStatus.Reserved, result.Status);
        Assert.Equal(25, result.MaxReserveByFraction);
        Assert.Equal(25, result.ReservedFood);
        Assert.Equal(75, colony.Stock[Resource.Food]);
        Assert.Equal(25, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_RespectsDesiredCampaignBudget()
    {
        var world = CreateWorld(seed: 9104, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 0,
            armySize: 4,
            pool);

        Assert.Equal(ArmyRationReservationStatus.Reserved, result.Status);
        Assert.Equal(12, result.DesiredFood);
        Assert.Equal(12, result.ReservedFood);
        Assert.Equal(88, colony.Stock[Resource.Food]);
        Assert.Equal(12, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_InvalidArmySizeDoesNotMutateStockOrPool()
    {
        var world = CreateWorld(seed: 9105, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 0,
            armySize: 0,
            pool);

        Assert.Equal(ArmyRationReservationStatus.InvalidArmySize, result.Status);
        Assert.Equal(0, result.ReservedFood);
        Assert.Equal(100, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_NonEmptyPoolDoesNotTopUpOrMutateColonyStock()
    {
        var world = CreateWorld(seed: 9106, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState(rationPoolFood: 5);

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 0,
            armySize: 10,
            pool);

        Assert.Equal(ArmyRationReservationStatus.AlreadyReserved, result.Status);
        Assert.Equal(0, result.ReservedFood);
        Assert.Equal(100, colony.Stock[Resource.Food]);
        Assert.Equal(5, pool.RationPoolFood);
    }

    [Fact]
    public void ReserveRations_SaturatesExtremeHomeReserveArithmetic()
    {
        var world = CreateWorld(seed: 9113, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var pool = new ArmyRationPoolState();

        var result = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: int.MaxValue,
            armySize: 10,
            pool,
            new ArmyRationReservationOptions(
                MinHomeReserveFoodPerPerson: int.MaxValue,
                MaxReserveFraction: 1f,
                CampaignDaysBudget: 3,
                FoodPerWarriorPerDay: 1));

        Assert.Equal(ArmyRationReservationStatus.NoEligibleFood, result.Status);
        Assert.Equal(int.MaxValue, result.MinHomeReserveFood);
        Assert.True(result.MinHomeReserveFood >= 0);
        Assert.Equal(0, result.ReservedFood);
        Assert.Equal(100, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
    }

    [Fact]
    public void TickRationPool_ConsumesPoolWithoutMutatingMemberInventory()
    {
        var world = CreateWorld(seed: 9107, initialPop: 2);
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 1));
        var pool = new ArmyRationPoolState(rationPoolFood: 5);

        var result = ArmyRationPoolSupplyModel.TickRationPool(
            members,
            new ArmySupplyState(),
            pool,
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));

        Assert.Equal(5, result.RationPoolFoodBefore);
        Assert.Equal(3, result.RationPoolFoodAfter);
        Assert.Equal(2, result.FoodDemandUnits);
        Assert.Equal(2, result.FoodConsumed);
        Assert.Equal(0, result.UnmetFoodDemandUnits);
        Assert.False(result.IsOutOfSupply);
        Assert.Equal(2, members[0].Inventory.GetCount(ItemType.Food));
        Assert.Equal(1, members[1].Inventory.GetCount(ItemType.Food));
        Assert.Equal(3, pool.RationPoolFood);
    }

    [Fact]
    public void TickRationPool_FractionalDemandBelowWholeUnitDoesNotCreatePressure()
    {
        var world = CreateWorld(seed: 9108, initialPop: 1);
        var member = world._people[0];
        var moraleBefore = member.CombatMorale;
        var staminaBefore = member.Stamina;

        var result = ArmyRationPoolSupplyModel.TickRationPool(
            new[] { member },
            new ArmySupplyState(),
            new ArmyRationPoolState(),
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
        Assert.Equal(moraleBefore, member.CombatMorale);
        Assert.Equal(staminaBefore, member.Stamina);
    }

    [Fact]
    public void TickRationPool_WholeUnmetUnitCreatesPressure()
    {
        var world = CreateWorld(seed: 9109, initialPop: 1);
        var member = world._people[0];
        var state = new ArmySupplyState();
        var pool = new ArmyRationPoolState();
        var options = new ArmySupplyOptions(
            FoodConsumedPerPersonPerSecond: 0.5f,
            OutOfSupplyMoraleLossPerSecond: 5f,
            OutOfSupplyStaminaLossPerSecond: 7f,
            RouteAfterOutOfSupplyTicks: 99);

        var first = ArmyRationPoolSupplyModel.TickRationPool(new[] { member }, state, pool, dt: 1f, options);
        var second = ArmyRationPoolSupplyModel.TickRationPool(new[] { member }, state, pool, dt: 1f, options);

        Assert.Equal(0, first.FoodDemandUnits);
        Assert.False(first.IsOutOfSupply);
        Assert.Equal(1, second.FoodDemandUnits);
        Assert.Equal(1, second.UnmetFoodDemandUnits);
        Assert.True(second.IsOutOfSupply);
        Assert.Equal(1, second.SustainedOutOfSupplyTicks);
        Assert.Equal(1, second.AttritionEventCount);
        Assert.Equal(95f, member.CombatMorale);
        Assert.Equal(93f, member.Stamina);
    }

    [Fact]
    public void TickRationPool_SustainedOutOfSupplyCanRouteMembers()
    {
        var world = CreateWorld(seed: 9110, initialPop: 2);
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

        var first = ArmyRationPoolSupplyModel.TickRationPool(members, state, new ArmyRationPoolState(), dt: 1f, options);
        var second = ArmyRationPoolSupplyModel.TickRationPool(members, state, new ArmyRationPoolState(), dt: 1f, options);

        Assert.True(first.IsOutOfSupply);
        Assert.Equal(0, first.RoutedMemberCount);
        Assert.True(second.IsOutOfSupply);
        Assert.Equal(2, second.SustainedOutOfSupplyTicks);
        Assert.Equal(2, second.RoutedMemberCount);
        Assert.All(members, member => Assert.True(member.IsRouting));
    }

    [Fact]
    public void ReturnRemainingRations_RestoresColonyFoodAndClearsPool()
    {
        var world = CreateWorld(seed: 9111, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 50;
        var pool = new ArmyRationPoolState(rationPoolFood: 7);

        var result = ArmyRationPoolSupplyModel.ReturnRemainingRations(colony, pool);

        Assert.Equal(7, result.ReturnedFood);
        Assert.Equal(50, result.ColonyFoodBefore);
        Assert.Equal(57, result.ColonyFoodAfter);
        Assert.Equal(0, result.RationPoolFoodAfter);
        Assert.Equal(57, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
    }

    [Fact]
    public void ReturnRemainingRations_IsIdempotentAndDoesNotDuplicateFood()
    {
        var world = CreateWorld(seed: 9112, initialPop: 4);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 50;
        var pool = new ArmyRationPoolState(rationPoolFood: 7);

        var first = ArmyRationPoolSupplyModel.ReturnRemainingRations(colony, pool);
        var second = ArmyRationPoolSupplyModel.ReturnRemainingRations(colony, pool);

        Assert.Equal(7, first.ReturnedFood);
        Assert.Equal(0, second.ReturnedFood);
        Assert.Equal(57, second.ColonyFoodBefore);
        Assert.Equal(57, second.ColonyFoodAfter);
        Assert.Equal(57, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
    }

    [Fact]
    public void RationPoolLifecycle_ConservesFoodAcrossReserveConsumeAndReturn()
    {
        var world = CreateWorld(seed: 9114, initialPop: 2);
        var colony = world._colonies[0];
        colony.Stock[Resource.Food] = 100;
        var members = world._people.Take(2).ToArray();
        Assert.True(members[0].Inventory.TryAdd(ItemType.Food, 2));
        Assert.True(members[1].Inventory.TryAdd(ItemType.Food, 1));
        var memberInventoryBefore = members.Sum(member => member.Inventory.GetCount(ItemType.Food));
        var originalColonyFood = colony.Stock[Resource.Food];
        var pool = new ArmyRationPoolState();

        var reservation = ArmyRationPoolSupplyModel.ReserveRations(
            colony,
            homePopulation: 0,
            armySize: 4,
            pool);
        var consumption = ArmyRationPoolSupplyModel.TickRationPool(
            members,
            new ArmySupplyState(),
            pool,
            dt: 1f,
            new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));
        var returned = ArmyRationPoolSupplyModel.ReturnRemainingRations(colony, pool);
        var memberInventoryAfter = members.Sum(member => member.Inventory.GetCount(ItemType.Food));

        Assert.Equal(ArmyRationReservationStatus.Reserved, reservation.Status);
        Assert.Equal(12, reservation.ReservedFood);
        Assert.Equal(2, consumption.FoodConsumed);
        Assert.Equal(10, returned.ReturnedFood);
        Assert.Equal(originalColonyFood - consumption.FoodConsumed, colony.Stock[Resource.Food]);
        Assert.Equal(0, pool.RationPoolFood);
        Assert.Equal(memberInventoryBefore, memberInventoryAfter);
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
