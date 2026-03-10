using System;
using System.IO;
using System.Linq;
using WorldSim.AI;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave4AdvancedDefenseTests
{
    [Fact]
    public void StoneWall_RequiresFortificationTech_BlockedWithout()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9001);
        var colony = world._colonies[0];
        var pos = FindBuildableTile(world, colony.Origin);

        Assert.False(world.TryAddStoneWall(colony, pos));
    }

    [Fact]
    public void StoneWall_AllowedAfterFortificationTech()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9002)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];
        var pos = FindBuildableTile(world, colony.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, colony).Success);
        Assert.True(world.TryAddStoneWall(colony, pos));
    }

    [Fact]
    public void ReinforcedWall_RequiresAdvancedFortification()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9003)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];
        var pos = FindBuildableTile(world, colony.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, colony).Success);
        Assert.False(world.TryAddReinforcedWall(colony, pos));

        Assert.True(TechTree.TryUnlock("advanced_fortification", world, colony).Success);
        Assert.True(world.TryAddReinforcedWall(colony, pos));
    }

    [Fact]
    public void CatapultTower_RequiresSiegeCraft()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9004)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];
        var pos = FindBuildableTile(world, colony.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, colony).Success);
        Assert.False(world.TryAddCatapultTower(colony, pos));

        Assert.True(TechTree.TryUnlock("siege_craft", world, colony).Success);
        Assert.True(world.TryAddCatapultTower(colony, pos));
    }

    [Fact]
    public void Gate_BlocksHostileMovement_AllowsFriendlyMovement()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9005)
        {
            AllowFreeTechUnlocks = true,
            EnableDiplomacy = true
        };
        var owner = world._colonies[0];
        var hostile = world._colonies.First(colony => colony.Faction != owner.Faction);
        var pos = FindBuildableTile(world, owner.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, owner).Success);
        Assert.True(world.TryAddGate(owner, pos));
        world.SetFactionStance(owner.Faction, hostile.Faction, Stance.Hostile);

        Assert.False(world.IsMovementBlocked(pos.x, pos.y, owner.Id));
        Assert.True(world.IsMovementBlocked(pos.x, pos.y, hostile.Id));
    }

    [Fact]
    public void Tower_BecomesInactiveWhenUpkeepUnpaid()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9006)
        {
            AllowFreeTechUnlocks = true,
            EnableDiplomacy = true
        };
        var owner = world._colonies[0];
        var pos = FindBuildableTile(world, owner.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, owner).Success);
        Assert.True(world.TryAddArrowTower(owner, pos));
        owner.Stock[Resource.Wood] = 0;

        world.Update(0.25f);

        var tower = Assert.Single(world.DefensiveStructures, structure => structure.Pos == pos);
        Assert.False(tower.IsActive);
    }

    [Fact]
    public void Tower_ReactivatesWhenUpkeepPaid()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9007)
        {
            AllowFreeTechUnlocks = true,
            EnableDiplomacy = true
        };
        var owner = world._colonies[0];
        var pos = FindBuildableTile(world, owner.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, owner).Success);
        Assert.True(world.TryAddArrowTower(owner, pos));
        owner.Stock[Resource.Wood] = 0;
        world.Update(0.25f);

        owner.Stock[Resource.Wood] = 6;
        world.Update(0.25f);

        var tower = Assert.Single(world.DefensiveStructures, structure => structure.Pos == pos);
        Assert.True(tower.IsActive);
    }

    [Fact]
    public void CatapultTower_AoE_DamagesNearbyHostiles()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 36, height: 26, initialPop: 16, brainFactory: _ => new RuntimeNpcBrain(new AlwaysIdleBrain()), randomSeed: 9008)
        {
            AllowFreeTechUnlocks = true,
            EnableDiplomacy = true
        };
        var owner = world._colonies[0];
        var enemyFaction = world._colonies.First(colony => colony.Faction != owner.Faction).Faction;
        var towerPos = FindBuildableTile(world, owner.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, owner).Success);
        Assert.True(TechTree.TryUnlock("siege_craft", world, owner).Success);
        owner.Stock[Resource.Wood] = 200;
        owner.Stock[Resource.Stone] = 200;
        owner.Stock[Resource.Iron] = 200;
        Assert.True(world.TryAddCatapultTower(owner, towerPos));

        var targets = world._people.Where(person => person.Home.Faction == enemyFaction).Take(2).ToList();
        var tx = Math.Clamp(towerPos.x + 4, 0, world.Width - 1);
        var ty = Math.Clamp(towerPos.y, 0, world.Height - 2);
        targets[0].Pos = (tx, ty);
        targets[1].Pos = (tx, ty + 1);
        targets[0].Profession = Profession.Hunter;
        targets[1].Profession = Profession.Hunter;
        targets[0].Needs["Hunger"] = 0f;
        targets[1].Needs["Hunger"] = 0f;
        targets[0].Home.Stock[Resource.Food] = 200;
        targets[1].Home.Stock[Resource.Food] = 200;

        world.SetFactionStance(owner.Faction, enemyFaction, Stance.Hostile);
        var h0 = targets[0].Health;
        var h1 = targets[1].Health;

        for (int i = 0; i < 8; i++)
            world.Update(0.25f);

        Assert.True(targets[0].Health < h0);
        Assert.True(targets[1].Health < h1);
    }

    [Fact]
    public void StoneWall_HpScaled_WhenAdvancedFortificationUnlocked()
    {
        TechTree.Load(GetTechPath());
        var world = new World(width: 34, height: 24, initialPop: 14, randomSeed: 9009)
        {
            AllowFreeTechUnlocks = true
        };
        var colony = world._colonies[0];
        var pos = FindBuildableTile(world, colony.Origin);

        Assert.True(TechTree.TryUnlock("fortification", world, colony).Success);
        Assert.True(TechTree.TryUnlock("advanced_fortification", world, colony).Success);
        Assert.True(world.TryAddStoneWall(colony, pos));

        var wall = Assert.Single(world.DefensiveStructures, structure => structure.Pos == pos);
        Assert.Equal(StoneWallSegment.DefaultHp * 1.2f, wall.MaxHp);
    }

    private static (int x, int y) FindBuildableTile(World world, (int x, int y) origin)
    {
        for (int radius = 0; radius <= 12; radius++)
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
                    if (world.DefensiveStructures.Any(structure => !structure.IsDestroyed && structure.Pos == (x, y)))
                        continue;
                    return (x, y);
                }
            }
        }

        return origin;
    }

    private static string GetTechPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
                return techPath;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }

    private sealed class AlwaysIdleBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            return new AiDecisionResult(
                Command: NpcCommand.Idle,
                Trace: new AiDecisionTrace(
                    SelectedGoal: "TestIdle",
                    PlannerName: "Test",
                    PolicyName: "Test",
                    PlanLength: 1,
                    PlanPreview: new[] { NpcCommand.Idle },
                    PlanCost: 1,
                    ReplanReason: "Test",
                    MethodName: "TestMethod",
                    GoalScores: Array.Empty<GoalScoreEntry>()));
        }
    }
}
