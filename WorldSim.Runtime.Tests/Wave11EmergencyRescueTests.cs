using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11EmergencyRescueTests
{
    [Fact]
    public void DefaultDisabledPolicy_MakesNonzeroChanceKnobsInert()
    {
        var world = CreateControlledWorld(seed: 11301);
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 1f;
        world._animals.Clear();

        InvokeEmergencyRescue(world, 200f);

        Assert.Equal(EmergencyRescuePolicy.Disabled, world.EmergencyRescuePolicy);
        Assert.Empty(world._animals);
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(0, world.TotalPredatorReplenishmentSpawns);
        Assert.Equal(0, world.BuildEcologyLifecycleCounters().EmergencyRescues);
        Assert.Equal("none", world.LastEmergencyRescueReason);
    }

    [Fact]
    public void DisabledPolicy_DoesNotConsumeWorldRngOrChangeLaterRescueOutcome()
    {
        var withDisabledProbe = CreateControlledWorld(seed: 11306);
        var withoutDisabledProbe = CreateControlledWorld(seed: 11306);
        ConfigureGuaranteedHerbivoreRescue(withDisabledProbe);
        ConfigureGuaranteedHerbivoreRescue(withoutDisabledProbe);

        InvokeEmergencyRescue(withDisabledProbe, 200f);
        Assert.Empty(withDisabledProbe._animals);
        Assert.Equal(0, withDisabledProbe.BuildEcologyLifecycleCounters().EmergencyRescues);

        withDisabledProbe.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        withoutDisabledProbe.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        InvokeEmergencyRescue(withDisabledProbe, 1f);
        InvokeEmergencyRescue(withoutDisabledProbe, 1f);

        Assert.Equal(withoutDisabledProbe.TotalHerbivoreReplenishmentSpawns, withDisabledProbe.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(withoutDisabledProbe.TotalPredatorReplenishmentSpawns, withDisabledProbe.TotalPredatorReplenishmentSpawns);
        Assert.Equal(withoutDisabledProbe.BuildEcologyLifecycleCounters().EmergencyRescues, withDisabledProbe.BuildEcologyLifecycleCounters().EmergencyRescues);
        Assert.Equal(withoutDisabledProbe.LastEmergencyRescueReason, withDisabledProbe.LastEmergencyRescueReason);
        Assert.Equal(
            withoutDisabledProbe._animals.Select(animal => (animal.GetType().Name, animal.Pos)).ToArray(),
            withDisabledProbe._animals.Select(animal => (animal.GetType().Name, animal.Pos)).ToArray());
    }

    [Fact]
    public void EnabledPolicy_AllowsHerbivoreFloorRescueAndCountsItSeparately()
    {
        var world = CreateControlledWorld(seed: 11302);
        world.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 0f;
        world._animals.Clear();

        InvokeEmergencyRescue(world, 1f);

        Assert.Single(world._animals.OfType<Herbivore>());
        Assert.Equal(1, world.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(0, world.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().EmergencyRescues);
        Assert.Equal("herbivore_floor", world.LastEmergencyRescueReason);
    }

    [Fact]
    public void EnabledPolicy_PredatorExtinctWithPreyRescueUsesBoundedReason()
    {
        var world = CreateControlledWorld(seed: 11303);
        world.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 1f;
        world._animals.Clear();
        for (var i = 0; i < 8; i++)
            world._animals.Add(new Herbivore((i, 1), world.CreateEntityRng(), energy: 50f, reproductionCooldown: 30f));

        InvokeEmergencyRescue(world, 1f);

        Assert.Single(world._animals.OfType<Predator>());
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(1, world.TotalPredatorReplenishmentSpawns);
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().EmergencyRescues);
        Assert.Equal("predator_extinct_with_prey", world.LastEmergencyRescueReason);
    }

    [Fact]
    public void LifecycleBirths_DoNotIncrementEmergencyRescueCounter()
    {
        var world = CreateControlledWorld(seed: 11304, width: 16, height: 16);
        var herbivorePos = (x: 2, y: 2);
        world.GetTile(herbivorePos.x, herbivorePos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        world._animals.Add(new Herbivore(herbivorePos, world.CreateEntityRng(), energy: AnimalLifecycleModel.HerbivoreMaxEnergy));
        world._animals.Add(new Predator((x: 0, y: 0), world.CreateEntityRng(), energy: AnimalLifecycleModel.PredatorMaxEnergy));
        for (var i = 0; i < 8; i++)
            world._animals.Add(new Herbivore((x: 8 + i % 4, y: 8 + i / 4), world.CreateEntityRng(), energy: 50f, reproductionCooldown: 30f));

        world.Update(0f);

        var counters = world.BuildEcologyLifecycleCounters();
        Assert.True(counters.HerbivoreBirths > 0);
        Assert.True(counters.PredatorBirths > 0);
        Assert.Equal(0, counters.EmergencyRescues);
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(0, world.TotalPredatorReplenishmentSpawns);
    }

    [Fact]
    public void SnapshotLifecycleCounters_ExposeEmergencyRescueParity()
    {
        var world = CreateControlledWorld(seed: 11305);
        world.EmergencyRescuePolicy = EmergencyRescuePolicy.Enabled;
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 0f;
        world._animals.Clear();

        InvokeEmergencyRescue(world, 1f);

        var runtimeCounters = world.BuildEcologyLifecycleCounters();
        var snapshotCounters = WorldSnapshotBuilder.Build(world).EcologyDetails.LifecycleCounters;
        Assert.Equal(runtimeCounters.EmergencyRescues, snapshotCounters.EmergencyRescues);
    }

    static void InvokeEmergencyRescue(World world, float dt)
    {
        var method = typeof(World).GetMethod("UpdateEmergencyAnimalRescue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(world, new object[] { dt });
    }

    static void ConfigureGuaranteedHerbivoreRescue(World world)
    {
        world.AnimalReplenishmentChancePerSecond = 1f;
        world.PredatorReplenishmentChance = 0f;
        world._animals.Clear();
    }

    static World CreateControlledWorld(int seed, int width = 16, int height = 16)
    {
        var world = new World(width: width, height: height, initialPop: 0, randomSeed: seed)
        {
            EmergencyRescuePolicy = EmergencyRescuePolicy.Disabled
        };
        ResetGround(world, Ground.Dirt);
        world._animals.Clear();
        return world;
    }

    static void ResetGround(World world, Ground ground)
    {
        var map = new Tile[world.Width, world.Height];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
                map[x, y] = new Tile(ground);
        }

        typeof(World).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, map);
        typeof(World).GetField("_ecologyState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            world,
            EcologyState.Create(map, world.Width, world.Height));
    }
}
