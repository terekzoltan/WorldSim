using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using WorldSim.Simulation.Military;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave9ForagingRuntimeTests
{
    [Fact]
    public void Forage_SameTileFood_AddsExactYieldToRationPool()
    {
        var world = CreateWorld(seed: 9301, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 5));
        var pool = new ArmyRationPoolState();
        var state = new ArmyForagingState();

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            state,
            source.x,
            source.y,
            " campaign:alpha ",
            new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 8));

        Assert.Equal(ArmyForageStatus.Succeeded, result.Status);
        Assert.Equal("campaign:alpha", result.ConsumerKey);
        Assert.Equal(2, result.FoodGained);
        Assert.Equal(5, result.SourceFoodBefore);
        Assert.Equal(3, result.SourceFoodAfter);
        Assert.Equal(2, pool.RationPoolFood);
        Assert.Equal(result.FoodGained, result.RationPoolFoodAfter - result.RationPoolFoodBefore);
        Assert.Equal(result.FoodGained, result.SourceFoodBefore - result.SourceFoodAfter);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(1, state.Successes);
        Assert.Equal(0, state.Failures);
        Assert.Equal(2, state.FoodGained);
        Assert.Equal(2, state.GetFoodGainedForConsumer("campaign:alpha"));
    }

    [Fact]
    public void Forage_DiagonalAdjacentFood_IsAllowedByChebyshevDistance()
    {
        var world = CreateWorld(seed: 9302, initialPop: 1);
        var forager = world._people[0];
        var (actor, source) = PlaceForagerNearDiagonalGroundSource(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 4));

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            new ArmyRationPoolState(),
            new ArmyForagingState(),
            source.x,
            source.y,
            "army:diagonal");

        Assert.Equal(actor, forager.Pos);
        Assert.Equal(ArmyForageStatus.Succeeded, result.Status);
        Assert.Equal(2, result.FoodGained);
    }

    [Fact]
    public void Forage_DoesNotRequireSupplyCarrierRole()
    {
        var world = CreateWorld(seed: 9303, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 3));

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            new ArmyRationPoolState(),
            new ArmyForagingState(),
            source.x,
            source.y,
            "army:no-role");

        Assert.False(forager.HasRole(PersonRole.SupplyCarrier));
        Assert.Equal(ArmyForageStatus.Succeeded, result.Status);
    }

    [Fact]
    public void Forage_PartialNodeBelowAttemptCap_HarvestsOnlyAvailableFood()
    {
        var world = CreateWorld(seed: 9304, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 1));
        var pool = new ArmyRationPoolState();

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            new ArmyForagingState(),
            source.x,
            source.y,
            "army:partial-node",
            new ArmyForagingOptions(MaxFoodPerAttempt: 4, MaxFoodPerConsumer: 8));

        Assert.Equal(ArmyForageStatus.Succeeded, result.Status);
        Assert.Equal(1, result.FoodGained);
        Assert.Equal(0, result.SourceFoodAfter);
        Assert.Equal(1, pool.RationPoolFood);
    }

    [Fact]
    public void Forage_RemainingConsumerCapBelowNodeAmount_HarvestsOnlyRemainingCap()
    {
        var world = CreateWorld(seed: 9305, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 10));
        var pool = new ArmyRationPoolState();
        var state = new ArmyForagingState();
        var options = new ArmyForagingOptions(MaxFoodPerAttempt: 4, MaxFoodPerConsumer: 3);

        var first = ArmyForagingModel.TryForageToRationPool(world, forager, pool, state, source.x, source.y, "army:cap", options);
        var second = ArmyForagingModel.TryForageToRationPool(world, forager, pool, state, source.x, source.y, "army:cap", options);

        Assert.Equal(3, first.FoodGained);
        Assert.Equal(ArmyForageStatus.Failed, second.Status);
        Assert.Equal(ArmyForageFailureReason.ConsumerCapReached, second.FailureReason);
        Assert.Equal(3, pool.RationPoolFood);
        Assert.Equal(7, world.GetTile(source.x, source.y).Node?.Amount);
        Assert.Equal(3, state.GetFoodGainedForConsumer("army:cap"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Forage_InvalidConsumerKey_FailsWithoutMutation(string? consumerKey)
    {
        var world = CreateWorld(seed: 9306, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 3));

        AssertFailurePreservesSourceAndPool(
            world,
            forager,
            source,
            consumerKey!,
            ArmyForageFailureReason.InvalidConsumerKey);
    }

    [Fact]
    public void Forage_DeadActor_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9307, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 3));
        forager.Health = 0f;

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:dead", ArmyForageFailureReason.ForagerDead);
    }

    [Fact]
    public void Forage_OutOfBoundsSource_FailsWithoutThrowingOrMutation()
    {
        var world = CreateWorld(seed: 9308, initialPop: 1);
        var forager = world._people[0];
        _ = PlaceForagerOnGround(world, forager);
        var pool = new ArmyRationPoolState(2);
        var state = new ArmyForagingState();

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            state,
            sourceX: -1,
            sourceY: 0,
            "army:oob");

        Assert.Equal(ArmyForageStatus.Failed, result.Status);
        Assert.Equal(ArmyForageFailureReason.SourceOutOfBounds, result.FailureReason);
        Assert.Equal(2, pool.RationPoolFood);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(1, state.Failures);
    }

    [Fact]
    public void Forage_OutOfRangeSource_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9309, initialPop: 1);
        var forager = world._people[0];
        var (actor, source) = PlaceForagerNearFarGroundSource(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 3));
        Assert.Equal(actor, forager.Pos);

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:far", ArmyForageFailureReason.SourceOutOfRange);
    }

    [Fact]
    public void Forage_WaterTile_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9310, initialPop: 1, width: 64, height: 64);
        var forager = world._people[0];
        var source = PlaceForagerOnWaterTile(world, forager);

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:water", ArmyForageFailureReason.WaterTile);
    }

    [Fact]
    public void Forage_NoResourceNode_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9311, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(null);

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:no-node", ArmyForageFailureReason.NoResourceNode);
    }

    [Fact]
    public void Forage_WrongResource_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9312, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Wood, 3));

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:wrong", ArmyForageFailureReason.WrongResource);
    }

    [Fact]
    public void Forage_DepletedFood_FailsWithoutMutation()
    {
        var world = CreateWorld(seed: 9313, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 0));

        AssertFailurePreservesSourceAndPool(world, forager, source, "army:depleted", ArmyForageFailureReason.DepletedFood);
    }

    [Fact]
    public void Forage_RationPoolNearIntMax_DoesNotOverflowAndPreservesConservation()
    {
        var world = CreateWorld(seed: 9314, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 5));
        var pool = new ArmyRationPoolState(int.MaxValue - 1);

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            new ArmyForagingState(),
            source.x,
            source.y,
            "army:saturate",
            new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 8));

        Assert.Equal(ArmyForageStatus.Succeeded, result.Status);
        Assert.Equal(1, result.FoodGained);
        Assert.Equal(int.MaxValue, pool.RationPoolFood);
        Assert.Equal(result.FoodGained, result.RationPoolFoodAfter - result.RationPoolFoodBefore);
        Assert.Equal(result.FoodGained, result.SourceFoodBefore - result.SourceFoodAfter);
    }

    [Fact]
    public void Forage_FullRationPool_ReturnsNoYieldWithoutMutation()
    {
        var world = CreateWorld(seed: 9315, initialPop: 1);
        var forager = world._people[0];
        var source = PlaceForagerOnGround(world, forager);
        world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 5));
        var pool = new ArmyRationPoolState(int.MaxValue);
        var state = new ArmyForagingState();

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            state,
            source.x,
            source.y,
            "army:no-capacity",
            new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 8));

        Assert.Equal(ArmyForageStatus.Failed, result.Status);
        Assert.Equal(ArmyForageFailureReason.NoYield, result.FailureReason);
        Assert.Equal(0, result.FoodGained);
        Assert.Equal(5, result.SourceFoodBefore);
        Assert.Equal(5, result.SourceFoodAfter);
        Assert.Equal(int.MaxValue, result.RationPoolFoodBefore);
        Assert.Equal(int.MaxValue, result.RationPoolFoodAfter);
        Assert.Equal(int.MaxValue, pool.RationPoolFood);
        Assert.Equal(5, world.GetTile(source.x, source.y).Node?.Amount);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(0, state.Successes);
        Assert.Equal(1, state.Failures);
        Assert.Equal(0, state.FoodGained);
    }

    private static void AssertFailurePreservesSourceAndPool(
        World world,
        Person forager,
        (int x, int y) source,
        string consumerKey,
        ArmyForageFailureReason expectedReason)
    {
        var pool = new ArmyRationPoolState(7);
        var state = new ArmyForagingState();
        var nodeBefore = world.GetTile(source.x, source.y).Node;
        var typeBefore = nodeBefore?.Type;
        var amountBefore = nodeBefore?.Amount ?? 0;

        var result = ArmyForagingModel.TryForageToRationPool(
            world,
            forager,
            pool,
            state,
            source.x,
            source.y,
            consumerKey);

        var nodeAfter = world.GetTile(source.x, source.y).Node;
        Assert.Equal(ArmyForageStatus.Failed, result.Status);
        Assert.Equal(expectedReason, result.FailureReason);
        Assert.Equal(7, pool.RationPoolFood);
        Assert.Equal(typeBefore, nodeAfter?.Type);
        Assert.Equal(amountBefore, nodeAfter?.Amount ?? 0);
        Assert.Equal(0, state.FoodGained);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(0, state.Successes);
        Assert.Equal(1, state.Failures);
    }

    private static World CreateWorld(int seed, int initialPop, int width = 24, int height = 18)
    {
        var world = new World(
            width: width,
            height: height,
            initialPop: initialPop,
            brainFactory: _ => new RuntimeNpcBrain(new FixedBrain()),
            randomSeed: seed)
        {
            BirthRateMultiplier = 0f
        };

        foreach (var colony in world._colonies)
            colony.Stock[Resource.Food] = 0;

        foreach (var person in world._people)
        {
            person.Age = 30f;
            person.Health = 100f;
        }

        return world;
    }

    private static (int x, int y) PlaceForagerOnGround(World world, Person forager)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;

                forager.Pos = (x, y);
                return (x, y);
            }
        }

        throw new InvalidOperationException("Test world has no ground tile.");
    }

    private static ((int x, int y) actor, (int x, int y) source) PlaceForagerNearDiagonalGroundSource(World world, Person forager)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water || world.GetTile(x + 1, y + 1).Ground == Ground.Water)
                    continue;

                forager.Pos = (x, y);
                return ((x, y), (x + 1, y + 1));
            }
        }

        throw new InvalidOperationException("Test world has no diagonal ground pair.");
    }

    private static ((int x, int y) actor, (int x, int y) source) PlaceForagerNearFarGroundSource(World world, Person forager)
    {
        for (int y = 1; y < world.Height - 3; y++)
        {
            for (int x = 1; x < world.Width - 3; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water || world.GetTile(x + 2, y).Ground == Ground.Water)
                    continue;

                forager.Pos = (x, y);
                return ((x, y), (x + 2, y));
            }
        }

        throw new InvalidOperationException("Test world has no far ground pair.");
    }

    private static (int x, int y) PlaceForagerOnWaterTile(World world, Person forager)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water)
                    continue;

                forager.Pos = (x, y);
                return (x, y);
            }
        }

        throw new InvalidOperationException("Test world has no water tile.");
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
