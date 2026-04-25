using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class ScenarioEcologyTelemetryTests
{
    [Fact]
    public void BuildScenarioEcologyTelemetrySnapshot_EmptyWorld_ReturnsZeroSnapshot()
    {
        var world = new World(16, 16, 8, randomSeed: 42);
        world._people.Clear();
        world._animals.Clear();

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();

        Assert.Equal(0, telemetry.Herbivores);
        Assert.Equal(0, telemetry.Predators);
        Assert.True(telemetry.ActiveFoodNodes >= 0);
        Assert.True(telemetry.DepletedFoodNodes >= 0);
        Assert.Equal(0, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(0, telemetry.TicksWithZeroHerbivores);
        Assert.Equal(0, telemetry.TicksWithZeroPredators);
        Assert.Null(telemetry.FirstZeroHerbivoreTick);
        Assert.Null(telemetry.FirstZeroPredatorTick);
        Assert.Equal(0, telemetry.PredatorDeaths);
        Assert.Equal(0, telemetry.PredatorHumanHits);
    }

    [Fact]
    public void ZeroSpeciesCounters_TrackWorldTicks_PreReplenishment()
    {
        var world = new World(16, 16, 8, randomSeed: 84);
        world._animals.Clear();

        world.Update(0f);
        world.Update(0f);
        world.Update(0f);

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(3, telemetry.TicksWithZeroHerbivores);
        Assert.Equal(3, telemetry.TicksWithZeroPredators);
        Assert.Equal(1, telemetry.FirstZeroHerbivoreTick);
        Assert.Equal(1, telemetry.FirstZeroPredatorTick);
    }

    [Fact]
    public void ReplenishmentCounters_IncrementOnlyOnReplenishmentSpawn()
    {
        var world = new World(16, 16, 8, randomSeed: 91);
        world._animals.Clear();

        var method = typeof(World).GetMethod("UpdateAnimalPopulation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        method!.Invoke(world, new object[] { 200f });
        var afterSpawn = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, afterSpawn.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, afterSpawn.PredatorReplenishmentSpawns);

        method.Invoke(world, new object[] { 0f });
        var afterNoCheck = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, afterNoCheck.HerbivoreReplenishmentSpawns);
        Assert.Equal(0, afterNoCheck.PredatorReplenishmentSpawns);
    }

    [Fact]
    public void EcologyFoodCounts_MatchSnapshotEcologySemantics()
    {
        var world = new World(24, 16, 10, randomSeed: 137);
        DepleteSingleFoodNode(world);

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Equal(snapshot.Ecology.ActiveFoodNodes, telemetry.ActiveFoodNodes);
        Assert.Equal(snapshot.Ecology.DepletedFoodNodes, telemetry.DepletedFoodNodes);
    }

    [Fact]
    public void ToTimelineSnapshot_MapsEcologyFields()
    {
        var snapshot = new WorldSim.Runtime.Diagnostics.ScenarioEcologyTelemetrySnapshot(
            Herbivores: 4,
            Predators: 2,
            ActiveFoodNodes: 18,
            DepletedFoodNodes: 3,
            HerbivoreReplenishmentSpawns: 5,
            PredatorReplenishmentSpawns: 1,
            TicksWithZeroHerbivores: 2,
            TicksWithZeroPredators: 7,
            FirstZeroHerbivoreTick: 11,
            FirstZeroPredatorTick: 6,
            PredatorDeaths: 9,
            PredatorHumanHits: 12);

        var timeline = snapshot.ToTimelineSnapshot();

        Assert.Equal(4, timeline.Herbivores);
        Assert.Equal(2, timeline.Predators);
        Assert.Equal(18, timeline.ActiveFoodNodes);
        Assert.Equal(3, timeline.DepletedFoodNodes);
        Assert.Equal(5, timeline.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, timeline.PredatorReplenishmentSpawns);
        Assert.Equal(2, timeline.TicksWithZeroHerbivores);
        Assert.Equal(7, timeline.TicksWithZeroPredators);
    }

    [Fact]
    public void EcologyBalanceDefaults_AreTunedPw8B2Values()
    {
        var world = new World(16, 16, 8, randomSeed: 181);

        Assert.Equal(World.DefaultAnimalReplenishmentChancePerSecond, world.AnimalReplenishmentChancePerSecond);
        Assert.Equal(World.DefaultPredatorReplenishmentChance, world.PredatorReplenishmentChance);
        Assert.Equal(World.DefaultFoodRegrowthMinSeconds, world.FoodRegrowthMinSeconds);
        Assert.Equal(World.DefaultFoodRegrowthJitterSeconds, world.FoodRegrowthJitterSeconds);

        var balance = world.BuildScenarioEcologyBalanceSnapshot();
        Assert.Equal(0.04f, balance.AnimalReplenishmentChancePerSecond);
        Assert.Equal(1.0f, balance.PredatorReplenishmentChance);
        Assert.Equal(18f, balance.FoodRegrowthMinSeconds);
        Assert.Equal(18f, balance.FoodRegrowthJitterSeconds);
    }

    [Fact]
    public void EcologyBalanceKnobs_ClampInvalidValues()
    {
        var world = new World(16, 16, 8, randomSeed: 182)
        {
            AnimalReplenishmentChancePerSecond = 3f,
            PredatorReplenishmentChance = -1f,
            FoodRegrowthMinSeconds = float.NaN,
            FoodRegrowthJitterSeconds = float.PositiveInfinity
        };

        Assert.Equal(1f, world.AnimalReplenishmentChancePerSecond);
        Assert.Equal(0f, world.PredatorReplenishmentChance);
        Assert.True(world.FoodRegrowthMinSeconds > 0f);
        Assert.Equal(3600f, world.FoodRegrowthJitterSeconds);
    }

    [Fact]
    public void ConfiguredReplenishmentChanceAndPredatorChance_CanSpawnPredatorWhenEligible()
    {
        var world = new World(16, 16, 8, randomSeed: 183)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f
        };
        world._animals.Clear();
        for (var i = 0; i < 8; i++)
            world._animals.Add(new Herbivore((i, 1), new Random(200 + i)));

        var method = typeof(World).GetMethod("UpdateAnimalPopulation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void ReplenishmentTick_CanRunHerbivoreAndPredatorBranchesTogether()
    {
        var world = new World(16, 16, 8, randomSeed: 185)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f
        };
        world._animals.Clear();
        for (var i = 0; i < 7; i++)
            world._animals.Add(new Herbivore((i, 2), new Random(300 + i)));

        var method = typeof(World).GetMethod("UpdateAnimalPopulation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(8, telemetry.Herbivores);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void PredatorRescue_CanRunBelowHerbivoreFloor_WhenPredatorsAreExtinctAndPreyExists()
    {
        var world = new World(16, 16, 8, randomSeed: 186)
        {
            AnimalReplenishmentChancePerSecond = 1f,
            PredatorReplenishmentChance = 1f
        };
        world._animals.Clear();
        for (var i = 0; i < 4; i++)
            world._animals.Add(new Herbivore((i, 3), new Random(400 + i)));

        var method = typeof(World).GetMethod("UpdateAnimalPopulation", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { 1f });

        var telemetry = world.BuildScenarioEcologyTelemetrySnapshot();
        Assert.Equal(1, telemetry.HerbivoreReplenishmentSpawns);
        Assert.Equal(1, telemetry.PredatorReplenishmentSpawns);
        Assert.Equal(5, telemetry.Herbivores);
        Assert.Equal(1, telemetry.Predators);
    }

    [Fact]
    public void ConfiguredFoodRegrowthDelay_IsHonored()
    {
        var world = new World(24, 16, 10, randomSeed: 184)
        {
            FoodRegrowthMinSeconds = 0.1f,
            FoodRegrowthJitterSeconds = 0f
        };
        var pos = DepleteSingleFoodNode(world);

        InvokeFoodRegrowthTick(world, 0.05f);
        Assert.Equal(0, world.GetTile(pos.x, pos.y).Node?.Amount ?? 0);

        InvokeFoodRegrowthTick(world, 0.05f);
        Assert.True(world.GetTile(pos.x, pos.y).Node?.Amount > 0);
    }

    private static (int x, int y) DepleteSingleFoodNode(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.Node is { Type: Resource.Food, Amount: > 0 })
                {
                    var amount = tile.Node.Amount;
                    Assert.True(world.TryHarvest((x, y), Resource.Food, amount));
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("No harvestable food tile found for depletion path.");
    }

    private static void InvokeFoodRegrowthTick(World world, float dt)
    {
        var method = typeof(World).GetMethod("UpdateFoodRegrowth", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { dt });
    }
}
