using System;
using System.Collections.Generic;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave35BirthSpawnTests
{
    [Fact]
    public void Birth_SpawnsOnNearbyFreeTile_InsteadOfParentTile()
    {
        var world = new World(width: 28, height: 20, initialPop: 12, randomSeed: 1401)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            BirthRateMultiplier = 100_000f
        };

        var colony = world._colonies[0];
        var adults = world._people.Where(person => person.Home == colony).Take(2).ToList();
        Assert.Equal(2, adults.Count);
        world._people = new List<Person> { adults[0], adults[1] };

        var parent = adults[0];
        var supportAdult = adults[1];
        parent.Age = 30f;
        parent.Needs["Hunger"] = 6f;
        supportAdult.Age = 70f;
        supportAdult.Needs["Hunger"] = 6f;

        var parentPos = FindLandTileWithNeighbor(world);
        parent.Pos = parentPos;
        supportAdult.Pos = (Math.Clamp(parentPos.x + 2, 0, world.Width - 1), parentPos.y);

        colony.HouseCount = 3;
        colony.Stock[Resource.Food] = 200;

        int before = world._people.Count;
        world.Update(0.25f);

        Assert.Equal(before + 1, world._people.Count);
        var newborn = world._people.Last();
        Assert.Equal(0f, newborn.Age);
        Assert.NotEqual(parentPos, newborn.Pos);
        Assert.True(Manhattan(parentPos, newborn.Pos) <= 4);
        Assert.Equal(0, world.TotalBirthFallbackToOccupiedCount);
        Assert.Equal(0, world.TotalBirthFallbackToParentCount);
    }

    [Fact]
    public void Birth_FallsBackToParentTile_WhenNearbyFreeTilesAreUnavailable()
    {
        var world = new World(width: 28, height: 20, initialPop: 12, randomSeed: 1402)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            BirthRateMultiplier = 100_000f
        };

        var colony = world._colonies[0];
        var adults = world._people.Where(person => person.Home == colony).Take(2).ToList();
        Assert.Equal(2, adults.Count);
        world._people = new List<Person> { adults[0], adults[1] };

        var parent = adults[0];
        var supportAdult = adults[1];
        parent.Age = 30f;
        supportAdult.Age = 70f;
        parent.Needs["Hunger"] = 6f;
        supportAdult.Needs["Hunger"] = 6f;

        var parentPos = FindLandTileWithNeighbor(world);
        parent.Pos = parentPos;
        supportAdult.Pos = (Math.Clamp(parentPos.x + 2, 0, world.Width - 1), parentPos.y);

        colony.HouseCount = 3;
        colony.Stock[Resource.Food] = 200;

        BlockNearbyBuildableTiles(world, colony, parentPos, radius: 4);

        int before = world._people.Count;
        world.Update(0.25f);

        Assert.Equal(before + 1, world._people.Count);
        var newborn = world._people.Last();
        Assert.Equal(parentPos, newborn.Pos);
        Assert.True(world.TotalBirthFallbackToParentCount > 0);
    }

    [Fact]
    public void Birth_PrefersActorFreeTile_WhenOccupiedAlternativeExists()
    {
        var world = new World(width: 28, height: 20, initialPop: 12, randomSeed: 1403)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            BirthRateMultiplier = 100_000f
        };

        var colony = world._colonies[0];
        var adults = world._people.Where(person => person.Home == colony).Take(3).ToList();
        Assert.Equal(3, adults.Count);
        world._people = new List<Person> { adults[0], adults[1], adults[2] };

        var parent = adults[0];
        var supportAdult = adults[1];
        var blocker = adults[2];
        parent.Age = 30f;
        supportAdult.Age = 70f;
        blocker.Age = 70f;
        parent.Needs["Hunger"] = 6f;
        supportAdult.Needs["Hunger"] = 6f;
        blocker.Needs["Hunger"] = 6f;

        var parentPos = FindLandTileWithHorizontalNeighbors(world);
        parent.Pos = parentPos;
        supportAdult.Pos = (Math.Clamp(parentPos.x + 3, 0, world.Width - 1), parentPos.y);

        var occupiedCandidate = (parentPos.x - 1, parentPos.y);
        var freeCandidate = (parentPos.x + 1, parentPos.y);
        blocker.Pos = occupiedCandidate;
        BlockNearbyBuildableTilesExcept(world, colony, parentPos, radius: 4, keep: occupiedCandidate, keep2: freeCandidate);

        colony.HouseCount = 3;
        colony.Stock[Resource.Food] = 200;

        int before = world._people.Count;
        world.Update(0.25f);

        Assert.Equal(before + 1, world._people.Count);
        var newborn = world._people.Last();
        Assert.NotEqual(occupiedCandidate, newborn.Pos);
        Assert.NotEqual(parentPos, newborn.Pos);
        Assert.Equal(0, world.TotalBirthFallbackToOccupiedCount);
    }

    [Fact]
    public void Birth_UsesOccupiedFallback_WhenNoActorFreeCandidateExists()
    {
        var world = new World(
            width: 28,
            height: 20,
            initialPop: 12,
            brainFactory: _ => new RuntimeNpcBrain(new FixedIdleBrain()),
            randomSeed: 1404)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            BirthRateMultiplier = 100_000f
        };

        var colony = world._colonies[0];
        var adults = world._people.Where(person => person.Home == colony).Take(3).ToList();
        Assert.Equal(3, adults.Count);
        world._people = new List<Person> { adults[0], adults[1], adults[2] };

        var parent = adults[0];
        var supportAdult = adults[1];
        var blocker = adults[2];
        parent.Age = 30f;
        supportAdult.Age = 70f;
        blocker.Age = 70f;
        parent.Needs["Hunger"] = 6f;
        supportAdult.Needs["Hunger"] = 6f;
        blocker.Needs["Hunger"] = 6f;

        var parentPos = FindLandTileWithHorizontalNeighbors(world);
        parent.Pos = parentPos;
        supportAdult.Pos = (Math.Clamp(parentPos.x + 3, 0, world.Width - 1), parentPos.y);

        var occupiedCandidate = (parentPos.x - 1, parentPos.y);
        blocker.Pos = occupiedCandidate;
        BlockNearbyBuildableTilesExcept(world, colony, parentPos, radius: 4, keep: occupiedCandidate);

        colony.HouseCount = 3;
        colony.Stock[Resource.Food] = 200;

        var spawn = world.GetBirthSpawnPosition(colony, parentPos);

        Assert.Equal(occupiedCandidate, spawn);
        Assert.True(world.TotalBirthFallbackToOccupiedCount > 0);
    }

    private sealed class FixedIdleBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            var trace = new AiDecisionTrace(
                SelectedGoal: "Idle",
                PlannerName: "Fixed",
                PolicyName: "Fixed",
                PlanLength: 1,
                PlanPreview: new[] { NpcCommand.Idle },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "Fixed",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(NpcCommand.Idle, trace);
        }
    }

    private static void BlockNearbyBuildableTiles(World world, Colony colony, (int x, int y) center, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int md = Math.Abs(dx) + Math.Abs(dy);
                if (md == 0 || md > radius)
                    continue;

                int x = center.x + dx;
                int y = center.y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                    continue;
                if (!world.CanPlaceStructureAt(x, y))
                    continue;

                world.TryAddWoodWall(colony, (x, y));
            }
        }
    }

    private static void BlockNearbyBuildableTilesExcept(World world, Colony colony, (int x, int y) center, int radius, (int x, int y) keep, (int x, int y)? keep2 = null)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int md = Math.Abs(dx) + Math.Abs(dy);
                if (md == 0 || md > radius)
                    continue;

                int x = center.x + dx;
                int y = center.y + dy;
                if ((x, y) == keep || (keep2.HasValue && (x, y) == keep2.Value))
                    continue;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                    continue;
                if (!world.CanPlaceStructureAt(x, y))
                    continue;

                world.TryAddWoodWall(colony, (x, y));
            }
        }
    }

    private static (int x, int y) FindLandTileWithHorizontalNeighbors(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 2; x < world.Width - 2; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;
                if (world.GetTile(x - 1, y).Ground == Ground.Water)
                    continue;
                if (world.GetTile(x + 1, y).Ground == Ground.Water)
                    continue;
                return (x, y);
            }
        }

        throw new InvalidOperationException("No suitable horizontal land tile found.");
    }

    private static (int x, int y) FindLandTileWithNeighbor(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;
                if (world.GetTile(x + 1, y).Ground == Ground.Water)
                    continue;
                return (x, y);
            }
        }

        throw new InvalidOperationException("No suitable land tile found.");
    }

    private static int Manhattan((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);
}
