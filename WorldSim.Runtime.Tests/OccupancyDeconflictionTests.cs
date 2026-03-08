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
}
