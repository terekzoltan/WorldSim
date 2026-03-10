using System;
using System.Collections.Generic;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave35BuildSiteTargetingTests
{
    [Fact]
    public void BuildHouse_UsesExplicitBuildSiteBeforeWork()
    {
        var world = CreateForcedBuildWorld(seed: 1301, NpcCommand.BuildHouse);
        var colony = world._colonies[0];
        var actor = world._people[0];

        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost + 30;
        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 8f;
        actor.Pos = colony.Origin;
        ClearAllResourceNodes(world);

        for (int i = 0; i < 10 && actor.DebugDecisionCause != "build_site_move"; i++)
            world.Update(0.25f);

        Assert.Equal("build_site_move", actor.DebugDecisionCause);
        var target = ParseBuildTarget(actor.DebugTargetKey);
        Assert.NotNull(target);

        int before = colony.HouseCount;
        for (int i = 0; i < 48; i++)
            world.Update(0.25f);

        var house = Assert.Single(world.Houses, h => h.Owner == colony);
        Assert.True(colony.HouseCount > before);
        Assert.Equal(target!.Value, house.Pos);
    }

    [Fact]
    public void BuildWall_UsesExplicitBuildSiteBeforeWork()
    {
        var world = CreateForcedBuildWorld(seed: 1302, NpcCommand.BuildWall);
        var colony = world._colonies[0];
        var actor = world._people[0];

        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = 64;
        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 8f;
        actor.Pos = colony.Origin;
        ClearAllResourceNodes(world);

        for (int i = 0; i < 10 && actor.DebugDecisionCause != "build_site_move"; i++)
            world.Update(0.25f);

        Assert.Equal("build_site_move", actor.DebugDecisionCause);
        var target = ParseBuildTarget(actor.DebugTargetKey);
        Assert.NotNull(target);

        for (int i = 0; i < 40; i++)
            world.Update(0.25f);

        Assert.Contains(world.DefensiveStructures, s => s.Owner == colony && !s.IsDestroyed && s.Pos == target!.Value);
    }

    [Fact]
    public void BuildSite_PrefersActorFreeCandidate_WhenAvailable()
    {
        var world = CreateForcedBuildWorld(seed: 1303, NpcCommand.BuildHouse);
        var colony = world._colonies[0];
        var actor = world._people[0];
        var blocker = Person.Spawn(colony, colony.Origin, new RuntimeNpcBrain(new FixedCommandBrain(NpcCommand.Idle)), new Random(42), world.AllocatePersonId());
        world._people.Add(blocker);

        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost + 40;
        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 8f;
        actor.Pos = colony.Origin;
        ClearAllResourceNodes(world);

        var occupiedCandidate = (colony.Origin.x + 2, colony.Origin.y);
        var actorFreeCandidate = (colony.Origin.x + 3, colony.Origin.y);
        blocker.Pos = occupiedCandidate;
        BlockBuildableTilesExcept(world, colony, colony.Origin, radius: 6, keep: occupiedCandidate, keep2: actorFreeCandidate);

        for (int i = 0; i < 12 && actor.DebugDecisionCause != "build_site_move"; i++)
            world.Update(0.25f);

        Assert.Equal("build_site_move", actor.DebugDecisionCause);
        var target = ParseBuildTarget(actor.DebugTargetKey);
        Assert.Equal(actorFreeCandidate, target);
    }

    [Fact]
    public void BuildSite_UsesOccupiedFallback_WhenNoActorFreeCandidateExists()
    {
        var world = CreateForcedBuildWorld(seed: 1304, NpcCommand.BuildHouse);
        var colony = world._colonies[0];
        var actor = world._people[0];
        var blocker = Person.Spawn(colony, colony.Origin, new RuntimeNpcBrain(new FixedCommandBrain(NpcCommand.Idle)), new Random(43), world.AllocatePersonId());
        world._people.Add(blocker);

        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost + 40;
        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 8f;
        actor.Pos = colony.Origin;
        ClearAllResourceNodes(world);

        var occupiedCandidate = (colony.Origin.x + 2, colony.Origin.y);
        blocker.Pos = occupiedCandidate;
        BlockBuildableTilesExcept(world, colony, colony.Origin, radius: 6, keep: occupiedCandidate);

        for (int i = 0; i < 12 && actor.DebugDecisionCause != "build_site_move"; i++)
            world.Update(0.25f);

        Assert.Equal("build_site_move", actor.DebugDecisionCause);
        var target = ParseBuildTarget(actor.DebugTargetKey);
        Assert.Equal(occupiedCandidate, target);
    }

    private static World CreateForcedBuildWorld(int seed, NpcCommand command)
    {
        var world = new World(
            width: 28,
            height: 20,
            initialPop: 12,
            brainFactory: _ => new RuntimeNpcBrain(new FixedCommandBrain(command)),
            randomSeed: seed)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            StoneBuildingsEnabled = false
        };

        var colony = world._colonies[0];
        var actor = world._people.First(person => person.Home == colony);
        world._people = new List<Person> { actor };
        return world;
    }

    private static (int x, int y)? ParseBuildTarget(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("build:", StringComparison.Ordinal))
            return null;

        var parts = key.Split(':');
        if (parts.Length != 3)
            return null;
        if (!int.TryParse(parts[1], out var x))
            return null;
        if (!int.TryParse(parts[2], out var y))
            return null;
        return (x, y);
    }

    private static void ClearAllResourceNodes(World world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }
    }

    private static void BlockBuildableTilesExcept(World world, Colony colony, (int x, int y) center, int radius, (int x, int y) keep, (int x, int y)? keep2 = null)
    {
        var blockerColony = world._colonies.First(entry => entry.Faction != colony.Faction);
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int md = Math.Abs(dx) + Math.Abs(dy);
                if (md < 2 || md > radius)
                    continue;

                int x = center.x + dx;
                int y = center.y + dy;
                if ((x, y) == keep || (keep2.HasValue && (x, y) == keep2.Value))
                    continue;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                    continue;
                if (!world.CanPlaceStructureAt(x, y))
                    continue;

                world.TryAddWoodWall(blockerColony, (x, y));
            }
        }
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
}
