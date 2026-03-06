using System;
using System.Collections.Generic;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class DefenseRaidAiTests
{
    [Fact]
    public void BuilderInWar_ConstructsFortifications()
    {
        var world = new World(width: 28, height: 20, initialPop: 16, randomSeed: 741)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = true
        };

        var colony = world._colonies[0];
        var enemyColony = world._colonies.First(c => c.Faction != colony.Faction);
        world.SetFactionStance(colony.Faction, enemyColony.Faction, WorldSim.Simulation.Diplomacy.Stance.Hostile);

        var builder = world._people.First(person => person.Home == colony);
        builder.Profession = Profession.Builder;
        builder.Needs["Hunger"] = 8f;
        colony.Stock[Resource.Wood] = 64;
        colony.Stock[Resource.Stone] = 24;

        int before = world.DefensiveStructures.Count(structure => structure.Owner == colony && !structure.IsDestroyed);

        for (int i = 0; i < 36; i++)
            world.Update(0.25f);

        int after = world.DefensiveStructures.Count(structure => structure.Owner == colony && !structure.IsDestroyed);
        Assert.True(after > before);
    }

    [Fact]
    public void RaidAttackStructure_DestroysWall_AndAppliesLootAndStanceImpact()
    {
        var attackerColonyId = -1;
        var world = new World(
            width: 28,
            height: 20,
            initialPop: 16,
            brainFactory: colony =>
            {
                if (attackerColonyId < 0)
                    attackerColonyId = colony.Id;

                var command = colony.Id == attackerColonyId ? NpcCommand.RaidBorder : NpcCommand.Idle;
                return new RuntimeNpcBrain(new FixedCommandBrain(command));
            },
            randomSeed: 742)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        var attackerColony = world._colonies.First(c => c.Id == attackerColonyId);
        var defenderColony = world._colonies.First(c => c.Faction != attackerColony.Faction);

        var raider = world._people.First(person => person.Home == attackerColony);
        raider.Profession = Profession.Hunter;
        raider.Pos = FindBuildableTileNear(world, attackerColony.Origin);
        raider.Needs["Hunger"] = 10f;

        var wallPos = (x: Math.Clamp(raider.Pos.x + 1, 0, world.Width - 1), y: raider.Pos.y);
        if (!IsBuildable(world, wallPos))
            wallPos = FindBuildableTileNear(world, (raider.Pos.x + 2, raider.Pos.y));

        Assert.True(world.TryAddWoodWall(defenderColony, wallPos));
        world.TryDamageDefensiveStructure(wallPos, 105f); // leave low HP so raid structure attack can finish it quickly

        defenderColony.Stock[Resource.Food] = 12;
        defenderColony.Stock[Resource.Wood] = 9;

        world._animals.Add(new Predator((raider.Pos.x, Math.Clamp(raider.Pos.y + 1, 0, world.Height - 1)), new Random(91)));

        for (int i = 0; i < 4; i++)
            world.Update(0.25f);

        Assert.DoesNotContain(world.DefensiveStructures, structure => structure.Pos == wallPos && !structure.IsDestroyed);
        Assert.True(attackerColony.Stock[Resource.Food] > 0 || attackerColony.Stock[Resource.Wood] > 0);
        Assert.True(world.GetFactionStance(attackerColony.Faction, defenderColony.Faction) >= WorldSim.Simulation.Diplomacy.Stance.Hostile);
        Assert.Contains(world.RecentEvents, entry => entry.Contains("raid", StringComparison.OrdinalIgnoreCase));
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

    private static (int x, int y) FindBuildableTileNear(World world, (int x, int y) center)
    {
        for (int radius = 0; radius <= 8; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var candidate = (x: center.x + dx, y: center.y + dy);
                    if (!IsBuildable(world, candidate))
                        continue;
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
