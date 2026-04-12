using System;
using System.Collections.Generic;
using System.Reflection;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class MovementSpeedMultiplierTests
{
    [Fact]
    public void WorldMovementSpeedMultiplier_PropagatesToExistingColonies()
    {
        var world = new World(16, 16, 8, randomSeed: 123);

        world.MovementSpeedMultiplier = 1.35f;

        Assert.All(world._colonies, colony => Assert.Equal(1.35f, colony.MovementSpeedMultiplier));
    }

    [Fact]
    public void FractionalMovementSpeedMultiplier_IncreasesMoveTowardsDistanceOverRepeatedTicks()
    {
        var baselineWorld = CreateFlatWorld();
        baselineWorld.MovementSpeedMultiplier = 1.0f;

        var fastWorld = CreateFlatWorld();
        fastWorld.MovementSpeedMultiplier = 1.35f;

        var baselineX = AdvanceTowardsTarget(baselineWorld, ticks: 4);
        var fastX = AdvanceTowardsTarget(fastWorld, ticks: 4);

        Assert.True(fastX > baselineX, $"Expected fast move to outpace baseline, got baselineX={baselineX}, fastX={fastX}.");
    }

    private static World CreateFlatWorld()
    {
        var world = new World(16, 16, 8, randomSeed: 123);
        SetFlatGrassMap(world);
        world._animals.Clear();
        world._people = new List<Person> { world._people[0] };
        world._people[0].Pos = (1, 1);
        return world;
    }

    private static int AdvanceTowardsTarget(World world, int ticks)
    {
        var actor = world._people[0];
        var moveTowards = typeof(Person).GetMethod("MoveTowards", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(moveTowards);

        for (var i = 0; i < ticks; i++)
            moveTowards!.Invoke(actor, new object[] { world, (8, 1), 1 });

        return actor.Pos.x;
    }

    private static void SetFlatGrassMap(World world)
    {
        var mapField = typeof(World).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(mapField);

        var flatMap = new Tile[world.Width, world.Height];
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            flatMap[x, y] = new Tile(Ground.Grass);

        mapField!.SetValue(world, flatMap);
    }
}
