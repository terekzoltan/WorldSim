using System.Linq;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class RelationDynamicsTests
{
    [Fact]
    public void SustainedBorderPressure_DegradesStance_OverTime()
    {
        var world = new World(width: 36, height: 24, initialPop: 20, randomSeed: 201)
        {
            EnableDiplomacy = true
        };

        var a = world._colonies[0].Faction;
        var b = world._colonies[1].Faction;

        Assert.Equal(Stance.Neutral, world.GetFactionStance(a, b));

        for (int i = 0; i < 120; i++)
            world.Update(0.25f);

        var stance = world.GetFactionStance(a, b);
        Assert.True(stance is Stance.Hostile or Stance.War);
    }

    [Fact]
    public void PeaceCooldown_PreventsInstantWarRetrigger()
    {
        var world = new World(width: 36, height: 24, initialPop: 20, randomSeed: 202)
        {
            EnableDiplomacy = true
        };

        var c0 = world._colonies[0];
        var c1 = world._colonies[1];
        var p0 = world._people.First(p => p.Home == c0);
        var p1 = world._people.First(p => p.Home == c1);

        p0.Pos = (18, 12);
        p1.Pos = (19, 12);

        for (int i = 0; i < 160; i++)
            world.Update(0.25f);

        world.SetFactionStance(c0.Faction, c1.Faction, Stance.Neutral);

        for (int i = 0; i < 20; i++)
            world.Update(0.25f);

        Assert.Equal(Stance.Neutral, world.GetFactionStance(c0.Faction, c1.Faction));
    }
}
