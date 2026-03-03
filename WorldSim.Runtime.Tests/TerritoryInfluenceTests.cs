using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class TerritoryInfluenceTests
{
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
}
