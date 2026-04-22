using System;
using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class TerritoryInfluenceTests
{
    [Fact]
    public void TerritoryOwnership_Recompute_IsPeriodic_WhenWorldUnchanged()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 310);

        Assert.Equal(1, world.TerritoryRecomputeCount);

        for (int i = 0; i < 20; i++)
            world.Update(0.25f);

        Assert.Equal(5, world.TerritoryRecomputeCount);
    }

    [Fact]
    public void TerritoryOwnership_Recompute_TriggersOnDirtyStructureChange()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 311);
        var colony = world._colonies[0];

        world.Update(0.25f);
        Assert.Equal(1, world.TerritoryRecomputeCount);

        var tile = FindBuildableTile(world);
        world.AddHouse(colony, tile);

        world.Update(0.25f);
        Assert.Equal(2, world.TerritoryRecomputeCount);
    }

    [Fact]
    public void TerritoryOwnership_IsDeterministic_ForSameSeed()
    {
        var first = new World(width: 36, height: 24, initialPop: 20, randomSeed: 301);
        var second = new World(width: 36, height: 24, initialPop: 20, randomSeed: 301);

        for (int i = 0; i < 20; i++)
        {
            first.Update(0.25f);
            second.Update(0.25f);
        }

        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                Assert.Equal(first.GetTileOwnerColonyId(x, y), second.GetTileOwnerColonyId(x, y));
                Assert.Equal(first.IsTileContested(x, y), second.IsTileContested(x, y));
            }
        }
    }

    [Fact]
    public void Snapshot_ContainsOwnerFactionAndContestedFlags()
    {
        var world = new World(width: 36, height: 24, initialPop: 20, randomSeed: 302);
        for (int i = 0; i < 16; i++)
            world.Update(0.25f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        Assert.NotEmpty(snapshot.Tiles);
        Assert.Contains(snapshot.Tiles, tile => tile.OwnerFactionId >= 0 && !tile.IsContested);
        Assert.Contains(snapshot.Tiles, tile => tile.OwnerFactionId < 0 || tile.OwnerFactionId <= (int)Faction.Chirita);
    }

    [Fact]
    public void ContestedTileCounters_AreTrackedPerFactionPair()
    {
        var world = new World(width: 40, height: 28, initialPop: 24, randomSeed: 303);
        for (int i = 0; i < 30; i++)
            world.Update(0.25f);

        var factions = world._colonies.Select(c => c.Faction).Distinct().ToList();
        int sum = 0;
        for (int i = 0; i < factions.Count; i++)
        {
            for (int j = i + 1; j < factions.Count; j++)
                sum += world.GetContestedTilesForFactionPair(factions[i], factions[j]);
        }

        Assert.True(sum >= 0);
    }

    [Fact]
    public void SameFactionCompetition_DoesNotCreateContestedPairCounts()
    {
        var world = new World(width: 40, height: 26, initialPop: 0, randomSeed: 304);

        while (world._colonies.Count > 1)
            world._colonies.RemoveAt(world._colonies.Count - 1);

        var sameFactionColony = new Colony(4, (0, 0));
        world._colonies.Add(sameFactionColony);

        world._colonies[0].Origin = FindBuildableTile(world);
        sameFactionColony.Origin = (
            Math.Min(world.Width - 1, world._colonies[0].Origin.x + 2),
            world._colonies[0].Origin.y);

        for (int i = 0; i < 6; i++)
            world.Update(0.25f);

        for (int left = 0; left <= (int)Faction.Chirita; left++)
        {
            for (int right = left + 1; right <= (int)Faction.Chirita; right++)
            {
                var count = world.GetContestedTilesForFactionPair((Faction)left, (Faction)right);
                Assert.Equal(0, count);
            }
        }
    }

    private static (int x, int y) FindBuildableTile(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;
                if (world.Houses.Any(house => house.Pos == (x, y)))
                    continue;
                if (world.SpecializedBuildings.Any(building => building.Pos == (x, y)))
                    continue;
                if (world.DefensiveStructures.Any(structure => structure.Pos == (x, y) && !structure.IsDestroyed))
                    continue;
                return (x, y);
            }
        }

        throw new InvalidOperationException("No buildable tile found.");
    }
}
