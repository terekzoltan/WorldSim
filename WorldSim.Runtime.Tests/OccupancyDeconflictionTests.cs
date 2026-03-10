using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class OccupancyDeconflictionTests
{
    [Fact]
    public void EndPositionDeconfliction_SplitsStackedPeopleIntoDistinctTiles()
    {
        var world = new World(width: 36, height: 24, initialPop: 8, randomSeed: 901);
        var stackTile = FindLandTile(world);

        foreach (var person in world._people)
            person.Pos = stackTile;

        world.Update(0.25f);

        var alive = world._people.Where(person => person.Health > 0f).ToList();
        var distinct = alive.Select(person => person.Pos).Distinct().Count();

        Assert.Equal(alive.Count, distinct);
    }

    [Fact]
    public void NoProgressBackoff_Activates_WhenMovementStallsRepeatedly()
    {
        var world = new World(
            width: 36,
            height: 24,
            initialPop: 8,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysFleeBrain()),
            randomSeed: 902)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        var actor = world._people[0];
        var ringCenter = FindLandTileWithLandNeighbors(world);
        actor.Pos = ringCenter;

        var blockerColony = world._colonies.First(colony => colony.Faction != actor.Home.Faction);
        foreach (var n in CardinalNeighbors(ringCenter))
            world.TryAddWoodWall(blockerColony, n);

        int observedBackoff = 0;
        int observedNoProgress = 0;
        for (int i = 0; i < 24; i++)
        {
            actor.Needs["Hunger"] = 0f;
            actor.Home.Stock[Resource.Food] = 999;
            world.Update(0.25f);
            observedBackoff = Math.Max(observedBackoff, actor.BackoffTicksRemaining);
            observedNoProgress = Math.Max(observedNoProgress, actor.NoProgressStreak);
        }

        Assert.True(observedBackoff > 0 || observedNoProgress > 0);
    }

    [Fact]
    public void FleeBehavior_UsesRefugeRing_AndSpreadsCivilians()
    {
        var world = new World(
            width: 40,
            height: 28,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysFleeBrain()),
            randomSeed: 903)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        var colony = world._colonies[0];
        var civilians = world._people.Where(person => person.Home == colony).Take(6).ToList();
        var hostile = world._people.First(person => person.Home.Faction != colony.Faction);
        world.SetFactionStance(colony.Faction, hostile.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.War);
        foreach (var person in civilians)
            person.Pos = colony.Origin;

        for (int i = 0; i < 12; i++)
        {
            hostile.Pos = (Math.Clamp(colony.Origin.x + 1, 0, world.Width - 1), colony.Origin.y);
            world.Update(0.25f);
        }

        var distinct = civilians.Select(person => person.Pos).Distinct().Count();
        var movedFromOrigin = civilians.Count(person => person.Pos != colony.Origin);

        Assert.True(distinct >= 2);
        Assert.True(movedFromOrigin >= 1);
    }

    [Fact]
    public void SoftReservation_ResourceTargets_SpreadAcrossEquivalentNodes()
    {
        var world = new World(
            width: 40,
            height: 28,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysGatherWoodBrain()),
            randomSeed: 904)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        var colony = world._colonies[0];
        var workers = world._people
            .Where(person => person.Home == colony && person.Age >= 16f)
            .Take(2)
            .ToList();
        Assert.Equal(2, workers.Count);
        world._people = workers;

        var center = FindLandTileWithHorizontalLandNeighbors(world);
        workers[0].Pos = center;
        workers[1].Pos = center;
        foreach (var worker in workers)
        {
            worker.Health = 100f;
            worker.Needs["Hunger"] = 0f;
        }
        colony.Stock[Resource.Food] = 200;

        for (int y = Math.Max(0, center.y - 3); y <= Math.Min(world.Height - 1, center.y + 3); y++)
        {
            for (int x = Math.Max(0, center.x - 3); x <= Math.Min(world.Width - 1, center.x + 3); x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        world.GetTile(center.x - 1, center.y).ReplaceNode(new ResourceNode(Resource.Wood, 8));
        world.GetTile(center.x + 1, center.y).ReplaceNode(new ResourceNode(Resource.Wood, 8));

        var leftKey = $"resource:Wood:{center.x - 1}:{center.y}";
        var rightKey = $"resource:Wood:{center.x + 1}:{center.y}";

        world.Update(0.25f);

        Assert.Equal(1, world.GetSoftReservationCount(leftKey));
        Assert.Equal(1, world.GetSoftReservationCount(rightKey));
    }

    [Fact]
    public void Snapshot_ExportsClusteringObservabilityFields()
    {
        var world = new World(
            width: 40,
            height: 28,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysFleeBrain()),
            randomSeed: 905)
        {
            EnableDiplomacy = true,
            EnableCombatPrimitives = true
        };

        var colony = world._colonies[0];
        var actor = world._people.First(person => person.Home == colony);
        var hostile = world._people.First(person => person.Home.Faction != colony.Faction);
        world.SetFactionStance(colony.Faction, hostile.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.War);
        actor.Pos = colony.Origin;
        hostile.Pos = (Math.Clamp(colony.Origin.x + 1, 0, world.Width - 1), colony.Origin.y);

        for (int i = 0; i < 8; i++)
            world.Update(0.25f);

        var snapshot = WorldSim.Runtime.ReadModel.WorldSnapshotBuilder.Build(world);
        var actorView = snapshot.People.First(person => person.X == actor.Pos.x && person.Y == actor.Pos.y && person.ColonyId == actor.Home.Id);
        var colonyView = snapshot.Colonies.First(entry => entry.Id == colony.Id);

        Assert.Equal(actor.NoProgressStreak, actorView.NoProgressStreak);
        Assert.Equal(actor.BackoffTicksRemaining, actorView.BackoffTicksRemaining);
        Assert.Equal(actor.DebugDecisionCause, actorView.DebugDecisionCause);
        Assert.Equal(actor.DebugTargetKey, actorView.DebugTargetKey);
        Assert.Equal(world.GetColonyWarState(colony.Id).ToString(), colonyView.WarState);
        Assert.Equal(world.GetColonyWarriorCount(colony.Id), colonyView.WarriorCount);
        Assert.Equal(world.ActiveSoftReservationCount, snapshot.Ecology.SoftReservationCount);
        Assert.Equal(world.TotalOverlapResolveMoves, snapshot.Ecology.OverlapResolveMoves);
        Assert.Equal(world.TotalCrowdDissipationMoves, snapshot.Ecology.CrowdDissipationMoves);
        Assert.Equal(world.TotalBirthFallbackToOccupiedCount, snapshot.Ecology.BirthFallbackToOccupied);
        Assert.Equal(world.TotalBirthFallbackToParentCount, snapshot.Ecology.BirthFallbackToParent);
        Assert.Equal(world.TotalBuildSiteResetCount, snapshot.Ecology.BuildSiteResetCount);
        Assert.Equal(world.TotalNoProgressBackoffResource, snapshot.Ecology.NoProgressBackoffResource);
        Assert.Equal(world.TotalNoProgressBackoffBuild, snapshot.Ecology.NoProgressBackoffBuild);
        Assert.Equal(world.TotalNoProgressBackoffFlee, snapshot.Ecology.NoProgressBackoffFlee);
        Assert.Equal(world.TotalNoProgressBackoffCombat, snapshot.Ecology.NoProgressBackoffCombat);
        Assert.Equal(world.DenseNeighborhoodTicks, snapshot.Ecology.DenseNeighborhoodTicks);
        Assert.Equal(world.LastTickDenseActors, snapshot.Ecology.LastTickDenseActors);
    }

    [Fact]
    public void LocalCrowdDissipation_ReducesNeighborhoodDensity()
    {
        var world = new World(
            width: 40,
            height: 28,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysIdleBrain()),
            randomSeed: 906)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f
        };

        var colony = world._colonies[0];
        var actors = world._people
            .Where(person => person.Home == colony)
            .Take(8)
            .ToList();
        world._people = actors;
        ClearAllResourceNodes(world);

        var center = FindLandTileWithLandNeighbors(world);
        var cluster = new[]
        {
            center,
            (center.x + 1, center.y),
            (center.x - 1, center.y),
            (center.x, center.y + 1),
            (center.x, center.y - 1),
            (center.x + 1, center.y + 1),
            (center.x - 1, center.y - 1),
            (center.x + 1, center.y - 1)
        };

        for (int i = 0; i < actors.Count; i++)
        {
            actors[i].Profession = Profession.Generalist;
            actors[i].Needs["Hunger"] = 0f;
            actors[i].Home.Stock[Resource.Food] = 200;
            actors[i].Pos = cluster[i];
        }

        float before = AverageNeighborDensity(actors, radius: 2);
        world.Update(0.25f);
        float after = AverageNeighborDensity(actors, radius: 2);

        Assert.True(after < before);
        Assert.True(world.TotalCrowdDissipationMoves > 0);
    }

    [Fact]
    public void LocalCrowdDissipation_DoesNotMoveAlreadySparseActors()
    {
        var world = new World(
            width: 44,
            height: 30,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysIdleBrain()),
            randomSeed: 907)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f
        };

        var colony = world._colonies[0];
        var actors = world._people
            .Where(person => person.Home == colony)
            .Take(4)
            .ToList();
        world._people = actors;
        ClearAllResourceNodes(world);

        var sparse = new[]
        {
            (x: 4, y: 4),
            (x: 16, y: 5),
            (x: 28, y: 6),
            (x: 36, y: 10)
        };

        for (int i = 0; i < actors.Count; i++)
        {
            actors[i].Profession = Profession.Generalist;
            actors[i].Needs["Hunger"] = 0f;
            actors[i].Home.Stock[Resource.Food] = 200;
            actors[i].Pos = sparse[i];
        }

        var before = actors.Select(person => person.Pos).ToArray();
        world.Update(0.25f);
        var after = actors.Select(person => person.Pos).ToArray();

        Assert.Equal(before, after);
    }

    [Fact]
    public void PeacefulResourceMove_NoProgress_TriggersBackoff()
    {
        var world = new World(
            width: 36,
            height: 24,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysGatherWoodBrain()),
            randomSeed: 908)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f
        };

        var colony = world._colonies[0];
        var enemy = world._colonies.First(entry => entry.Faction != colony.Faction);
        var actor = world._people.First(person => person.Home == colony);
        world._people = new() { actor };
        ClearAllResourceNodes(world);

        var center = FindLandTileWithLandNeighbors(world);
        actor.Pos = center;
        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 0f;
        colony.Stock[Resource.Food] = 200;

        world.GetTile(center.x + 1, center.y).ReplaceNode(new ResourceNode(Resource.Wood, 8));
        foreach (var pos in CardinalNeighbors(center))
            world.TryAddWoodWall(enemy, pos);

        bool triggered = false;
        for (int i = 0; i < 16; i++)
        {
            world.Update(0.25f);
            if (actor.BackoffTicksRemaining > 0 && actor.DebugDecisionCause.StartsWith("no_progress_backoff:resource", StringComparison.Ordinal))
            {
                triggered = true;
                break;
            }
        }

        Assert.True(triggered);
        Assert.True(world.TotalNoProgressBackoffResource > 0);
    }

    [Fact]
    public void PeacefulBuildMove_NoProgress_TriggersBackoff()
    {
        var world = new World(
            width: 36,
            height: 24,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysBuildHouseBrain()),
            randomSeed: 909)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f,
            StoneBuildingsEnabled = false
        };

        var colony = world._colonies[0];
        var enemy = world._colonies.First(entry => entry.Faction != colony.Faction);
        var actor = world._people.First(person => person.Home == colony);
        world._people = new() { actor };
        ClearAllResourceNodes(world);

        actor.Profession = Profession.Generalist;
        actor.Needs["Hunger"] = 0f;
        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost * 3;

        var center = FindLandTileWithLandNeighbors(world);
        actor.Pos = center;
        foreach (var pos in CardinalNeighbors(center))
            world.TryAddWoodWall(enemy, pos);

        bool triggered = false;
        for (int i = 0; i < 16; i++)
        {
            world.Update(0.25f);
            if (actor.BackoffTicksRemaining > 0 && actor.DebugDecisionCause.StartsWith("no_progress_backoff:build", StringComparison.Ordinal))
            {
                triggered = true;
                break;
            }
        }

        Assert.True(triggered);
        Assert.True(world.TotalNoProgressBackoffBuild > 0);
        Assert.True(world.TotalBuildSiteResetCount > 0);
    }

    [Fact]
    public void CrowdDissipation_DoesNotRelocate_ActiveGatherWorker()
    {
        var world = new World(
            width: 36,
            height: 24,
            initialPop: 20,
            brainFactory: _ => new RuntimeNpcBrain(new AlwaysGatherWoodBrain()),
            randomSeed: 910)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f
        };

        var colony = world._colonies[0];
        var actors = world._people.Where(person => person.Home == colony).Take(6).ToList();
        world._people = actors;
        ClearAllResourceNodes(world);

        var center = FindLandTileWithLandNeighbors(world);
        world.GetTile(center.x, center.y).ReplaceNode(new ResourceNode(Resource.Wood, 16));

        var ring = new[]
        {
            (center.x, center.y),
            (center.x + 1, center.y),
            (center.x - 1, center.y),
            (center.x, center.y + 1),
            (center.x, center.y - 1),
            (center.x + 1, center.y + 1)
        };

        for (int i = 0; i < actors.Count; i++)
        {
            actors[i].Profession = Profession.Generalist;
            actors[i].Needs["Hunger"] = 0f;
            actors[i].Home.Stock[Resource.Food] = 200;
            actors[i].Pos = ring[i];
        }

        var worker = actors[0];
        world.Update(0.25f);

        Assert.Equal(center, worker.Pos);
    }

    [Fact]
    public void OverlapResolution_PrefersMoving_NonProtectedActor()
    {
        var world = new World(
            width: 36,
            height: 24,
            initialPop: 20,
            brainFactory: colony => colony.Id == 0
                ? new RuntimeNpcBrain(new AlwaysBuildHouseBrain())
                : new RuntimeNpcBrain(new AlwaysIdleBrain()),
            randomSeed: 911)
        {
            EnableDiplomacy = false,
            EnableCombatPrimitives = false,
            BirthRateMultiplier = 0f
        };

        var protectedActor = world._people.First(person => person.Home.Id == 0);
        var idleActor = world._people.First(person => person.Home.Id != 0);
        world._people = new() { protectedActor, idleActor };
        ClearAllResourceNodes(world);

        var center = FindLandTileWithLandNeighbors(world);
        var enemy = world._colonies.First(colony => colony.Faction != protectedActor.Home.Faction);
        protectedActor.Pos = center;
        idleActor.Pos = center;
        protectedActor.Needs["Hunger"] = 0f;
        idleActor.Needs["Hunger"] = 0f;
        protectedActor.Home.Stock[Resource.Food] = 200;
        protectedActor.Home.Stock[Resource.Wood] = protectedActor.Home.HouseWoodCost * 2;
        idleActor.Home.Stock[Resource.Food] = 200;
        foreach (var pos in CardinalNeighbors(center))
            world.TryAddWoodWall(enemy, pos);

        world.Update(0.25f);

        Assert.Equal(center, protectedActor.Pos);
        Assert.NotEqual(center, idleActor.Pos);
        Assert.True(world.TotalOverlapResolveMoves > 0);
    }

    private static (int x, int y) FindLandTile(World world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("No land tile found.");
    }

    private static (int x, int y) FindLandTileWithLandNeighbors(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 1; x < world.Width - 1; x++)
            {
                var center = (x, y);
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;

                if (CardinalNeighbors(center).All(n => world.GetTile(n.x, n.y).Ground != Ground.Water))
                    return center;
            }
        }

        throw new InvalidOperationException("No tile with land neighbors found.");
    }

    private static (int x, int y) FindLandTileWithHorizontalLandNeighbors(World world)
    {
        for (int y = 1; y < world.Height - 1; y++)
        {
            for (int x = 2; x < world.Width - 2; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;
                if (world.GetTile(x - 1, y).Ground == Ground.Water)
                    continue;
                if (world.GetTile(x + 1, y).Ground == Ground.Water)
                    continue;

                return (x, y);
            }
        }

        throw new InvalidOperationException("No tile with horizontal land neighbors found.");
    }

    private static (int x, int y)[] CardinalNeighbors((int x, int y) center)
        =>
        [
            (center.x + 1, center.y),
            (center.x - 1, center.y),
            (center.x, center.y + 1),
            (center.x, center.y - 1)
        ];

    private static void ClearAllResourceNodes(World world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }
    }

    private static float AverageNeighborDensity(System.Collections.Generic.IReadOnlyList<Person> people, int radius)
    {
        if (people.Count == 0)
            return 0f;

        int total = 0;
        foreach (var person in people)
        {
            total += people.Count(other => other != person && Manhattan(person.Pos, other.Pos) <= radius);
        }

        return total / (float)people.Count;
    }

    private static int Manhattan((int x, int y) left, (int x, int y) right)
        => Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private sealed class AlwaysFleeBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            return new AiDecisionResult(
                Command: NpcCommand.Flee,
                Trace: new AiDecisionTrace(
                    SelectedGoal: "TestFlee",
                    PlannerName: "Test",
                    PolicyName: "Test",
                    PlanLength: 1,
                    PlanPreview: new[] { NpcCommand.Flee },
                    PlanCost: 1,
                    ReplanReason: "Test",
                    MethodName: "TestMethod",
                    GoalScores: Array.Empty<GoalScoreEntry>()));
        }
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

    private sealed class AlwaysGatherWoodBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            return new AiDecisionResult(
                Command: NpcCommand.GatherWood,
                Trace: new AiDecisionTrace(
                    SelectedGoal: "TestGatherWood",
                    PlannerName: "Test",
                    PolicyName: "Test",
                    PlanLength: 1,
                    PlanPreview: new[] { NpcCommand.GatherWood },
                    PlanCost: 1,
                    ReplanReason: "Test",
                    MethodName: "TestMethod",
                    GoalScores: Array.Empty<GoalScoreEntry>()));
        }
    }

    private sealed class AlwaysBuildHouseBrain : INpcDecisionBrain
    {
        public AiDecisionResult Think(in NpcAiContext context)
        {
            return new AiDecisionResult(
                Command: NpcCommand.BuildHouse,
                Trace: new AiDecisionTrace(
                    SelectedGoal: "TestBuildHouse",
                    PlannerName: "Test",
                    PolicyName: "Test",
                    PlanLength: 1,
                    PlanPreview: new[] { NpcCommand.BuildHouse },
                    PlanCost: 1,
                    ReplanReason: "Test",
                    MethodName: "TestMethod",
                    GoalScores: Array.Empty<GoalScoreEntry>()));
        }
    }
}
