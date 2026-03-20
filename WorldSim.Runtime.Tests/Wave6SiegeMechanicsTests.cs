using System;
using System.Linq;
using System.Reflection;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave6SiegeMechanicsTests
{
    [Fact]
    public void SiegeState_TracksAttackers_AndRegistersBreach()
    {
        var attackerColonyId = -1;
        var world = new World(
            width: 30,
            height: 20,
            initialPop: 16,
            brainFactory: colony =>
            {
                if (attackerColonyId < 0)
                    attackerColonyId = colony.Id;

                var command = colony.Id == attackerColonyId ? NpcCommand.AttackStructure : NpcCommand.Idle;
                return new RuntimeNpcBrain(new FixedCommandBrain(command));
            },
            randomSeed: 9601)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true,
            EnableSiege = true,
            BirthRateMultiplier = 0f
        };

        world._animals.Clear();
        var attacker = world._colonies.First(colony => colony.Id == attackerColonyId);
        var defender = world._colonies.First(colony => colony.Faction != attacker.Faction);
        world.SetFactionStance(attacker.Faction, defender.Faction, WorldSim.Simulation.Diplomacy.Stance.War);

        var raider = world._people.First(person => person.Home == attacker);
        var wallPos = (x: Math.Clamp(raider.Pos.x + 1, 0, world.Width - 1), y: raider.Pos.y);
        if (!IsBuildable(world, wallPos))
            wallPos = FindBuildableTileNear(world, raider.Pos);

        Assert.True(world.TryAddWoodWall(defender, wallPos));
        raider.Pos = (Math.Clamp(wallPos.x - 1, 0, world.Width - 1), wallPos.y);
        raider.Current = Job.AttackStructure;

        var blockedBefore = world.IsMovementBlocked(wallPos.x, wallPos.y, raider.Home.Id);
        Assert.True(blockedBefore);

        for (int i = 0; i < 18; i++)
        {
            ForceAttackStructureTick(raider);
            raider.Needs["Hunger"] = 0f;
            attacker.Stock[Resource.Food] = 250;
            raider.Pos = (Math.Clamp(wallPos.x - 1, 0, world.Width - 1), wallPos.y);
            world.Update(0.25f);
        }

        Assert.True(world.TotalSiegesStarted > 0);
        Assert.True(world.TotalBreaches > 0);
        Assert.True(world.TotalStructuresDestroyed > 0);
        Assert.NotEmpty(world.GetRecentBreaches());

        var blockedAfter = world.IsMovementBlocked(wallPos.x, wallPos.y, raider.Home.Id);
        Assert.False(blockedAfter);
    }

    [Fact]
    public void SiegeCraft_IncreasesStructureDamageRate()
    {
        var baseSetup = CreateSiegeDamageWorld(randomSeed: 9602, siegeCraft: false);
        var techSetup = CreateSiegeDamageWorld(randomSeed: 9602, siegeCraft: true);

        var baseWorld = baseSetup.World;
        var techWorld = techSetup.World;
        var baseRaider = baseSetup.Raider;
        var techRaider = techSetup.Raider;
        var baseWallPos = baseSetup.WallPos;
        var techWallPos = techSetup.WallPos;

        var baseWall = baseWorld.DefensiveStructures.Single();
        var techWall = techWorld.DefensiveStructures.Single();

        for (int i = 0; i < 2; i++)
        {
            ForceAttackStructureTick(baseRaider);
            baseRaider.Needs["Hunger"] = 0f;
            baseRaider.Home.Stock[Resource.Food] = 250;
            baseRaider.Pos = (Math.Clamp(baseWallPos.x - 1, 0, baseWorld.Width - 1), baseWallPos.y);

            ForceAttackStructureTick(techRaider);
            techRaider.Needs["Hunger"] = 0f;
            techRaider.Home.Stock[Resource.Food] = 250;
            techRaider.Pos = (Math.Clamp(techWallPos.x - 1, 0, techWorld.Width - 1), techWallPos.y);

            baseWorld.Update(0.25f);
            techWorld.Update(0.25f);
        }

        Assert.True(techWall.Hp < baseWall.Hp);
    }

    private static (World World, Person Raider, (int x, int y) WallPos) CreateSiegeDamageWorld(int randomSeed, bool siegeCraft)
    {
        var attackerColonyId = -1;
        var world = new World(
            width: 30,
            height: 20,
            initialPop: 16,
            brainFactory: colony =>
            {
                if (attackerColonyId < 0)
                    attackerColonyId = colony.Id;

                var command = colony.Id == attackerColonyId ? NpcCommand.AttackStructure : NpcCommand.Idle;
                return new RuntimeNpcBrain(new FixedCommandBrain(command));
            },
            randomSeed: randomSeed)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true,
            EnableSiege = true,
            BirthRateMultiplier = 0f
        };

        world._animals.Clear();
        var attacker = world._colonies.First(colony => colony.Id == attackerColonyId);
        var defender = world._colonies.First(colony => colony.Faction != attacker.Faction);
        world.SetFactionStance(attacker.Faction, defender.Faction, WorldSim.Simulation.Diplomacy.Stance.War);

        if (siegeCraft)
            attacker.UnlockedTechs.Add("siege_craft");

        var raider = world._people.First(person => person.Home == attacker);
        var wallPos = (x: Math.Clamp(raider.Pos.x + 1, 0, world.Width - 1), y: raider.Pos.y);
        if (!IsBuildable(world, wallPos))
            wallPos = FindBuildableTileNear(world, raider.Pos);

        world.TryAddWoodWall(defender, wallPos);
        raider.Pos = (Math.Clamp(wallPos.x - 1, 0, world.Width - 1), wallPos.y);
        raider.Current = Job.AttackStructure;

        return (world, raider, wallPos);
    }

    private sealed class FixedCommandBrain : INpcDecisionBrain
    {
        private readonly NpcCommand _command;

        public FixedCommandBrain(NpcCommand command)
        {
            _command = command;
        }

        public AiDecisionResult Think(in NpcAiContext context)
        {
            var trace = new AiDecisionTrace(
                SelectedGoal: "Fixed",
                PlannerName: "Fixed",
                PolicyName: "Fixed",
                PlanLength: 1,
                PlanPreview: new[] { _command },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "Fixed",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }

    private static void ForceAttackStructureTick(Person raider)
    {
        raider.Current = Job.AttackStructure;
        typeof(Person)
            .GetField("_doingJob", BindingFlags.Instance | BindingFlags.NonPublic)?
            .SetValue(raider, 1);
    }

    private static (int x, int y) FindBuildableTileNear(World world, (int x, int y) center)
    {
        for (int radius = 0; radius <= 8; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var candidate = (x: center.x + dx, y: center.y + dy);
                    if (IsBuildable(world, candidate))
                        return candidate;
                }
            }
        }

        return center;
    }

    private static bool IsBuildable(World world, (int x, int y) pos)
    {
        if (pos.x < 0 || pos.y < 0 || pos.x >= world.Width || pos.y >= world.Height)
            return false;
        if (world.GetTile(pos.x, pos.y).Ground == Ground.Water)
            return false;
        if (world.Houses.Any(house => house.Pos == pos))
            return false;
        if (world.SpecializedBuildings.Any(building => building.Pos == pos))
            return false;
        if (world.DefensiveStructures.Any(structure => structure.Pos == pos && !structure.IsDestroyed))
            return false;
        return true;
    }
}
