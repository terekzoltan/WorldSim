using System.Linq;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class DefenseScaffoldTests
{
    [Fact]
    public void WoodWall_BlocksHostileMovement_ButNotOwnerFaction()
    {
        var world = new World(width: 28, height: 20, initialPop: 12, randomSeed: 501);
        var ownerColony = world._colonies[0];
        var otherColony = world._colonies.First(colony => colony.Faction != ownerColony.Faction);
        var wallPos = FindBuildableTile(world, ownerColony.Origin);

        var placed = world.TryAddWoodWall(ownerColony, wallPos);

        Assert.True(placed);
        Assert.True(world.IsMovementBlocked(wallPos.x, wallPos.y, otherColony.Id));
        Assert.False(world.IsMovementBlocked(wallPos.x, wallPos.y, ownerColony.Id));
    }

    [Fact]
    public void Watchtower_AutoFires_OnHostiles()
    {
        var world = new World(width: 30, height: 22, initialPop: 14, randomSeed: 502)
        {
            EnableDiplomacy = true
        };

        var owner = world._colonies[0];
        var enemy = world._people.First(person => person.Home.Faction != owner.Faction);
        var towerPos = FindBuildableTile(world, owner.Origin);
        enemy.Pos = (towerPos.x + 2, towerPos.y);

        world.SetFactionStance(owner.Faction, enemy.Home.Faction, Stance.Hostile);
        var placed = world.TryAddWatchtower(owner, towerPos);
        var healthBefore = enemy.Health;

        for (int i = 0; i < 8; i++)
            world.Update(0.25f);

        Assert.True(placed);
        Assert.True(enemy.Health < healthBefore);
    }

    [Fact]
    public void Snapshot_ContainsDefensiveStructures()
    {
        var world = new World(width: 28, height: 20, initialPop: 12, randomSeed: 503);
        var owner = world._colonies[0];
        var wallPos = FindBuildableTile(world, owner.Origin);

        world.TryAddWoodWall(owner, wallPos);
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Contains(snapshot.DefensiveStructures, structure =>
            structure.X == wallPos.x && structure.Y == wallPos.y && structure.Kind == DefensiveStructureKindView.WoodWall);
    }

    private static (int x, int y) FindBuildableTile(World world, (int x, int y) origin)
    {
        for (int radius = 0; radius <= 10; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = origin.x + dx;
                    int y = origin.y + dy;
                    if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                        continue;
                    if (world.GetTile(x, y).Ground == Ground.Water)
                        continue;
                    if (world.Houses.Any(house => house.Pos == (x, y)))
                        continue;
                    if (world.SpecializedBuildings.Any(building => building.Pos == (x, y)))
                        continue;
                    return (x, y);
                }
            }
        }

        return origin;
    }
}
