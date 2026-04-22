using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class LowCostVisualDriverSnapshotTests
{
    [Fact]
    public void TileVisualDrivers_DefaultAndRange_AreStable()
    {
        var world = new World(width: 36, height: 24, initialPop: 0, randomSeed: 901);

        // Force single-colony ownership to validate second-runner fallback policy.
        while (world._colonies.Count > 1)
            world._colonies.RemoveAt(world._colonies.Count - 1);

        AdvanceTicks(world, 6);
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.NotEmpty(snapshot.Tiles);
        foreach (var tile in snapshot.Tiles)
        {
            Assert.InRange(tile.OwnershipStrength, 0f, 1f);
            Assert.InRange(tile.FoodRegrowthProgress, 0f, 1f);

            if (tile.Ground == TileGroundView.Water || tile.OwnerFactionId < 0)
                Assert.Equal(0f, tile.OwnershipStrength);
        }

        var ownedLand = snapshot.Tiles.FirstOrDefault(tile => tile.Ground != TileGroundView.Water && tile.OwnerFactionId >= 0);
        Assert.NotNull(ownedLand);
        Assert.Equal(1f, ownedLand!.OwnershipStrength);
    }

    [Fact]
    public void VisualDriverExport_IsDeterministic_ForSameSeed()
    {
        var first = BuildVisualDriverSnapshot(seed: 902);
        var second = BuildVisualDriverSnapshot(seed: 902);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].x, second[i].x);
            Assert.Equal(first[i].y, second[i].y);
            Assert.Equal(first[i].ownerFactionId, second[i].ownerFactionId);
            Assert.Equal(first[i].contested, second[i].contested);
            Assert.Equal(first[i].ownershipStrength, second[i].ownershipStrength, 6);
            Assert.Equal(first[i].foodRegrowthProgress, second[i].foodRegrowthProgress, 6);
        }
    }

    [Fact]
    public void FoodRegrowthProgress_AdvancesAndResets_WithControlledDepletionPath()
    {
        var world = new World(width: 40, height: 26, initialPop: 0, randomSeed: 903);

        var foodTile = FindFoodTile(world);
        DepleteFoodNode(world, foodTile);

        InvokeFoodRegrowthTicks(world, tickCount: 2, dt: 0.25f);
        var inProgress = WorldSnapshotBuilder.Build(world)
            .Tiles
            .First(tile => tile.X == foodTile.x && tile.Y == foodTile.y)
            .FoodRegrowthProgress;
        Assert.True(inProgress > 0f, "Expected food regrowth progress to become positive after depletion.");
        Assert.True(inProgress < 1f, "Expected food regrowth progress to stay below 1 before completion.");

        var guard = 0;
        while (world.GetTile(foodTile.x, foodTile.y).Node?.Amount == 0)
        {
            InvokeFoodRegrowthTicks(world, tickCount: 1, dt: 0.25f);
            guard++;
            if (guard > 400)
                throw new InvalidOperationException("Food regrowth did not complete within guard window.");
        }

        var completedSnapshot = WorldSnapshotBuilder.Build(world);
        var completedTile = completedSnapshot.Tiles.First(tile => tile.X == foodTile.x && tile.Y == foodTile.y);
        Assert.Equal(0f, completedTile.FoodRegrowthProgress);
        Assert.Equal(ResourceView.Food, completedTile.NodeType);
        Assert.True(completedTile.NodeAmount > 0, "Expected depleted food node to regrow by completion window.");
    }

    [Fact]
    public void OwnershipStrength_OrdersStableOverContested_WithControlledOrigins()
    {
        var world = new World(width: 48, height: 30, initialPop: 0, randomSeed: 904);

        var nearA = FindNearestLand(world, (12, 15));
        var nearB = FindNearestLand(world, (15, 15));
        var farC = FindNearestLand(world, (2, 2));
        var farD = FindNearestLand(world, (45, 27));

        world._colonies[0].Origin = nearA;
        world._colonies[1].Origin = nearB;
        world._colonies[2].Origin = farC;
        world._colonies[3].Origin = farD;

        AdvanceTicks(world, 6);
        var snapshot = WorldSnapshotBuilder.Build(world);

        int ownerFactionId = (int)world._colonies[0].Faction;
        var contested = snapshot.Tiles
            .Where(tile => tile.OwnerFactionId == ownerFactionId && tile.IsContested)
            .OrderBy(tile => tile.OwnershipStrength)
            .FirstOrDefault();
        var stable = snapshot.Tiles
            .Where(tile => tile.OwnerFactionId == ownerFactionId && !tile.IsContested)
            .OrderByDescending(tile => tile.OwnershipStrength)
            .FirstOrDefault();

        Assert.NotNull(contested);
        Assert.NotNull(stable);

        Assert.True(contested!.OwnershipStrength < stable!.OwnershipStrength,
            $"Expected stable ownership strength > contested (stable={stable.OwnershipStrength}, contested={contested.OwnershipStrength}).");
    }

    [Fact]
    public void SameFactionRunnerUp_DoesNotLowerFactionOwnershipStrengthOrContested()
    {
        var world = new World(width: 52, height: 30, initialPop: 0, randomSeed: 905);

        // Keep two factions in world, but add one extra same-faction colony near the winner.
        while (world._colonies.Count > 2)
            world._colonies.RemoveAt(world._colonies.Count - 1);

        var sameFactionColony = new Colony(4, (0, 0));
        world._colonies.Add(sameFactionColony);

        var winnerOrigin = FindNearestLand(world, (14, 15));
        var sameFactionRunnerUpOrigin = FindNearestLand(world, (16, 15));
        var opposingOrigin = FindNearestLand(world, (38, 15));

        world._colonies[0].Origin = winnerOrigin;
        world._colonies[1].Origin = opposingOrigin;
        sameFactionColony.Origin = sameFactionRunnerUpOrigin;

        AdvanceTicks(world, 6);
        var snapshot = WorldSnapshotBuilder.Build(world);

        var tile = snapshot.Tiles.First(t => t.X == winnerOrigin.x && t.Y == winnerOrigin.y);
        Assert.Equal((int)Faction.Sylvars, tile.OwnerFactionId);
        Assert.False(tile.IsContested);
        Assert.Equal(1f, tile.OwnershipStrength);
    }

    private static List<(int x, int y, int ownerFactionId, bool contested, float ownershipStrength, float foodRegrowthProgress)> BuildVisualDriverSnapshot(int seed)
    {
        var world = new World(width: 36, height: 24, initialPop: 0, randomSeed: seed);
        AdvanceTicks(world, 12);
        var snapshot = WorldSnapshotBuilder.Build(world);
        return snapshot.Tiles
            .Select(tile => (tile.X, tile.Y, tile.OwnerFactionId, tile.IsContested, tile.OwnershipStrength, tile.FoodRegrowthProgress))
            .ToList();
    }

    private static (int x, int y) FindFoodTile(World world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.Ground != Ground.Water && tile.Node?.Type == Resource.Food && tile.Node.Amount > 0)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected at least one food tile for controlled regrowth test.");
    }

    private static void DepleteFoodNode(World world, (int x, int y) pos)
    {
        var guard = 0;
        while (world.GetTile(pos.x, pos.y).Node?.Amount > 0)
        {
            var harvested = world.TryHarvest(pos, Resource.Food, 1);
            Assert.True(harvested, "Expected controlled depletion harvest to succeed.");
            guard++;
            if (guard > 256)
                throw new InvalidOperationException("Guard tripped while depleting food node.");
        }
    }

    private static (int x, int y) FindNearestLand(World world, (int x, int y) preferred)
    {
        const int maxRadius = 20;
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var x = Math.Clamp(preferred.x + dx, 0, world.Width - 1);
                    var y = Math.Clamp(preferred.y + dy, 0, world.Height - 1);
                    if (world.GetTile(x, y).Ground != Ground.Water)
                        return (x, y);
                }
            }
        }

        throw new InvalidOperationException("Unable to find non-water tile near preferred origin.");
    }

    private static void AdvanceTicks(World world, int ticks)
    {
        for (int i = 0; i < ticks; i++)
            world.Update(0.25f);
    }

    private static void AdvanceTicksWithoutAnimals(World world, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            world._animals.Clear();
            world.Update(0.25f);
            world._animals.Clear();
        }
    }

    private static void InvokeFoodRegrowthTicks(World world, int tickCount, float dt)
    {
        var method = typeof(World).GetMethod("UpdateFoodRegrowth", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        for (int i = 0; i < tickCount; i++)
            method!.Invoke(world, new object[] { dt });
    }
}
