using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class DiplomacyStanceMatrixTests
{
    [Fact]
    public void FactionStanceMatrix_DefaultsToNeutral()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 11);

        foreach (Faction left in System.Enum.GetValues(typeof(Faction)))
        {
            foreach (Faction right in System.Enum.GetValues(typeof(Faction)))
                Assert.Equal(Stance.Neutral, world.GetFactionStance(left, right));
        }
    }

    [Fact]
    public void FactionStanceMatrix_SetPersistsSymmetrically()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 12);

        world.SetFactionStance(Faction.Sylvars, Faction.Obsidari, Stance.Hostile);

        for (int i = 0; i < 20; i++)
            world.Update(0.25f);

        Assert.Equal(Stance.Hostile, world.GetFactionStance(Faction.Sylvars, Faction.Obsidari));
        Assert.Equal(Stance.Hostile, world.GetFactionStance(Faction.Obsidari, Faction.Sylvars));
    }

    [Fact]
    public void Snapshot_ExposesFactionStanceMatrix()
    {
        var world = new World(width: 24, height: 16, initialPop: 8, randomSeed: 13);
        world.SetFactionStance(Faction.Aetheri, Faction.Chirita, Stance.War);

        var snapshot = WorldSnapshotBuilder.Build(world);

        var match = snapshot.FactionStances.FirstOrDefault(s =>
            s.LeftFactionId == (int)Faction.Aetheri
            && s.RightFactionId == (int)Faction.Chirita);

        Assert.NotNull(match);
        Assert.Equal("War", match!.Stance);
    }
}
