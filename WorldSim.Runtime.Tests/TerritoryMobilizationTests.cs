using WorldSim.Simulation;
using WorldSim.Runtime.ReadModel;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class TerritoryMobilizationTests
{
    [Fact]
    public void TerritoryOwnership_AssignsNonWaterTiles()
    {
        var world = new World(width: 40, height: 24, initialPop: 16, randomSeed: 1337);

        for (int i = 0; i < 12; i++)
            world.Update(0.25f);

        bool foundOwnedLand = false;
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                int owner = world.GetTileOwnerColonyId(x, y);
                if (tile.Ground == Ground.Water)
                    Assert.Equal(-1, owner);
                else if (owner >= 0)
                    foundOwnedLand = true;
            }
        }

        Assert.True(foundOwnedLand);
    }

    [Fact]
    public void CombatAndDiplomacyFlags_EnableMobilization_AssignsWarriors()
    {
        var world = new World(width: 36, height: 24, initialPop: 20, randomSeed: 99)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        var c0 = world._colonies[0];
        var c1 = world._colonies[1];

        var p0 = world._people.First(p => p.Home == c0);
        var p1 = world._people.First(p => p.Home == c1);
        p0.Pos = (18, 12);
        p1.Pos = (19, 12);

        for (int i = 0; i < 8; i++)
            world.Update(0.25f);

        var state0 = world.GetColonyWarState(c0.Id);
        var warriors0 = world.GetColonyWarriorCount(c0.Id);

        Assert.True(state0 is ColonyWarState.Tense or ColonyWarState.War);
        Assert.True(warriors0 >= 1);
    }

    [Fact]
    public void MobilizationState_Accessors_ReturnStableValues()
    {
        var world = new World(width: 36, height: 24, initialPop: 20, randomSeed: 55)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        for (int i = 0; i < 12; i++)
            world.Update(0.25f);

        foreach (var colony in world._colonies)
        {
            var warriors = world.GetColonyWarriorCount(colony.Id);
            var state = world.GetColonyWarState(colony.Id);
            Assert.True(warriors >= 0);
            Assert.True(state is ColonyWarState.Peace or ColonyWarState.Tense or ColonyWarState.War);
        }
    }
}
