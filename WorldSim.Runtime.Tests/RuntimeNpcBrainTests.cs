using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class RuntimeNpcBrainTests
{
    [Theory]
    [InlineData(NpcCommand.Idle, Job.Idle)]
    [InlineData(NpcCommand.GatherWood, Job.GatherWood)]
    [InlineData(NpcCommand.GatherStone, Job.GatherStone)]
    [InlineData(NpcCommand.GatherIron, Job.GatherIron)]
    [InlineData(NpcCommand.GatherGold, Job.GatherGold)]
    [InlineData(NpcCommand.GatherFood, Job.GatherFood)]
    [InlineData(NpcCommand.EatFood, Job.EatFood)]
    [InlineData(NpcCommand.Rest, Job.Rest)]
    [InlineData(NpcCommand.BuildHouse, Job.BuildHouse)]
    [InlineData(NpcCommand.CraftTools, Job.CraftTools)]
    [InlineData(NpcCommand.BuildWall, Job.BuildWall)]
    [InlineData(NpcCommand.BuildWatchtower, Job.BuildWatchtower)]
    [InlineData(NpcCommand.RaidBorder, Job.RaidBorder)]
    [InlineData(NpcCommand.AttackStructure, Job.AttackStructure)]
    [InlineData(NpcCommand.Fight, Job.Fight)]
    [InlineData(NpcCommand.Flee, Job.Flee)]
    public void Think_MapsAiCommandToRuntimeJob(NpcCommand command, Job expected)
    {
        var world = new World(16, 16, 10);
        var person = world._people[0];
        var brain = new RuntimeNpcBrain(new FixedBrain(command));

        var result = brain.Think(person, world, dt: 1f);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SimulationRuntime_UsesProvidedPlannerMode()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(
            width: 16,
            height: 16,
            initialPopulation: 10,
            technologyFilePath: techPath,
            aiOptions: new RuntimeAiOptions { PlannerMode = NpcPlannerMode.Simple });

        Assert.Equal(NpcPlannerMode.Simple, runtime.PlannerMode);
        Assert.Equal(NpcPolicyMode.GlobalPlanner, runtime.PolicyMode);
    }

    [Fact]
    public void SimulationRuntime_LoadsKnownTechs()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(16, 16, 10, techPath);

        Assert.True(runtime.LoadedTechCount > 0);
        Assert.True(runtime.IsKnownTech("agriculture"));
    }

    [Fact]
    public void RuntimeNpcBrain_StoresLastDecisionTrace()
    {
        var world = new World(16, 16, 10);
        var person = world._people[0];
        var brain = new RuntimeNpcBrain(new FixedBrain(NpcCommand.Rest));

        var result = brain.Think(person, world, dt: 1f);

        Assert.Equal(Job.Rest, result);
        Assert.NotNull(brain.LastDecision);
        Assert.Equal("Fixed", brain.LastDecision!.Trace.SelectedGoal);
        Assert.Equal("Test", brain.LastDecision.Trace.PolicyName);
    }

    [Fact]
    public void SimulationRuntime_AiDebugSnapshotContainsPolicyAndScores()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(
            width: 16,
            height: 16,
            initialPopulation: 10,
            technologyFilePath: techPath,
            aiOptions: new RuntimeAiOptions { PlannerMode = NpcPlannerMode.Goap, PolicyMode = NpcPolicyMode.FactionMix });

        runtime.AdvanceTick(0.25f);
        var snapshot = runtime.GetAiDebugSnapshot();

        Assert.True(snapshot.HasData);
        Assert.NotEmpty(snapshot.PolicyMode);
        Assert.NotEmpty(snapshot.GoalScores);
    }

    [Fact]
    public void SimulationRuntime_CycleTrackedNpc_ChangesTrackingModeAndIndex()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(16, 16, 10, techPath);

        runtime.AdvanceTick(0.25f);
        var before = runtime.GetAiDebugSnapshot();

        runtime.CycleTrackedNpc(1);
        var after = runtime.GetAiDebugSnapshot();

        Assert.True(after.HasData);
        Assert.Equal("Manual", after.TrackingMode);
        Assert.True(after.TrackedNpcIndex >= 1);
        Assert.True(after.TrackedNpcCount >= 1);
        Assert.NotEqual(before.TrackingMode, after.TrackingMode);
    }

    [Fact]
    public void SimulationRuntime_ResetTrackedNpc_ReturnsToLatestMode()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(16, 16, 10, techPath);

        runtime.AdvanceTick(0.25f);
        runtime.CycleTrackedNpc(1);
        runtime.ResetTrackedNpc();
        var snapshot = runtime.GetAiDebugSnapshot();

        Assert.Equal("Latest", snapshot.TrackingMode);
        Assert.True(snapshot.TrackedNpcIndex <= 1);
    }

    [Fact]
    public void RuntimeAiOptions_FromEnvironment_AppliesFactionPolicyTable()
    {
        const string key = "WORLDSIM_AI_POLICY_TABLE";
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "Sylvars=Htn;Obsidari=Goap;default=Simple");
            var options = RuntimeAiOptions.FromEnvironment();

            Assert.Equal(NpcPlannerMode.Htn, options.ResolveFactionPlanner(Faction.Sylvars));
            Assert.Equal(NpcPlannerMode.Goap, options.ResolveFactionPlanner(Faction.Obsidari));
            Assert.Equal(NpcPlannerMode.Simple, options.ResolveFactionPlanner(Faction.Chirita));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }

    [Fact]
    public void Think_BuildsThreatAndCombatContextFields()
    {
        var world = new World(16, 16, 10, randomSeed: 42)
        {
            EnableCombatPrimitives = true
        };

        world._animals.Clear();
        world._animals.Add(new Predator((5, 5), new Random(41)));
        int colonyId = world._people[0].Home.Id;
        world.RegisterGoalBiases(
            sourceId: "dir-food",
            colonyId: colonyId,
            biases: new[]
            {
                new WorldSim.Simulation.Effects.GoalBiasSpec(GoalBiasCategories.Gathering, 0.35),
                new WorldSim.Simulation.Effects.GoalBiasSpec(GoalBiasCategories.Building, 0.2)
            },
            durationTicks: 20,
            dampeningFactor: 1.0);

        var actor = world._people[0];
        actor.Pos = (5, 5);
        actor.Health = 77f;
        actor.Strength = 14;
        actor.Defense = 12;

        var hostile = world._people.First(person => person != actor && person.Home.Faction != actor.Home.Faction);
        world.SetFactionStance(actor.Home.Faction, hostile.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.Hostile);
        hostile.Pos = (6, 5);

        var brain = new RuntimeNpcBrain(new CapturingBrain());
        _ = brain.Think(actor, world, dt: 1f);

        Assert.NotNull(brain.LastDecision);
        Assert.NotNull(CapturingBrain.LastContext);
        Assert.Equal(77f, CapturingBrain.LastContext!.Value.Health);
        Assert.Equal(14, CapturingBrain.LastContext.Value.Strength);
        Assert.Equal(12, CapturingBrain.LastContext.Value.Defense);
        Assert.True(CapturingBrain.LastContext.Value.NearbyPredators >= 1);
        Assert.True(CapturingBrain.LastContext.Value.NearbyHostilePeople >= 1);
        Assert.True(CapturingBrain.LastContext.Value.BiasGathering > 0.30f);
        Assert.True(CapturingBrain.LastContext.Value.BiasBuilding > 0.15f);
    }

    [Fact]
    public void Think_EnemyCountUsesStanceFilteredHostiles()
    {
        var world = new World(20, 20, 12, randomSeed: 12)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        var actor = world._people[0];
        var otherFaction = world._people.First(person => person.Home.Faction != actor.Home.Faction);
        actor.Pos = (7, 7);
        otherFaction.Pos = (8, 7);

        world.SetFactionStance(actor.Home.Faction, otherFaction.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.Neutral);

        var brain = new RuntimeNpcBrain(new CapturingBrain());
        _ = brain.Think(actor, world, dt: 1f);
        Assert.Equal(0, CapturingBrain.LastContext!.Value.NearbyEnemyCount);

        world.SetFactionStance(actor.Home.Faction, otherFaction.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.Hostile);
        _ = brain.Think(actor, world, dt: 1f);
        Assert.True(CapturingBrain.LastContext!.Value.NearbyEnemyCount >= 1);
    }

    [Fact]
    public void Think_PopulatesContestedSignals_WhenTerritoryContestedNearby()
    {
        var world = new World(24, 24, 16, randomSeed: 13)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        var actor = world._people[0];
        var hostile = world._people.First(person => person.Home.Faction != actor.Home.Faction);
        world.SetFactionStance(actor.Home.Faction, hostile.Home.Faction, WorldSim.Simulation.Diplomacy.Stance.War);
        actor.Pos = (10, 10);
        hostile.Pos = (11, 10);

        foreach (var ally in world._people.Where(person => person.Home == actor.Home).Take(4))
            ally.Pos = (10, 10);
        foreach (var enemy in world._people.Where(person => person.Home.Faction == hostile.Home.Faction).Take(4))
            enemy.Pos = (11, 10);

        world.Update(0.2f);
        world.Update(0.2f);

        var brain = new RuntimeNpcBrain(new CapturingBrain());
        _ = brain.Think(actor, world, dt: 1f);

        Assert.NotNull(CapturingBrain.LastContext);
        Assert.True(CapturingBrain.LastContext!.Value.IsWarStance);
        Assert.True(CapturingBrain.LastContext.Value.IsContestedTile || CapturingBrain.LastContext.Value.HasContestedTilesNearby);
        Assert.True(CapturingBrain.LastContext.Value.LocalThreatScore > 0f);
    }

    private sealed class FixedBrain : INpcDecisionBrain
    {
        private readonly NpcCommand _command;

        public FixedBrain(NpcCommand command)
        {
            _command = command;
        }

        public AiDecisionResult Think(in NpcAiContext context)
        {
        var trace = new AiDecisionTrace(
            SelectedGoal: "Fixed",
            PlannerName: "Fixed",
            PolicyName: "Test",
            PlanLength: 1,
            PlanPreview: new[] { _command },
            PlanCost: 1,
            ReplanReason: "Fixed",
            MethodName: "FixedMethod",
            GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }

    private sealed class CapturingBrain : INpcDecisionBrain
    {
        public static NpcAiContext? LastContext { get; private set; }

        public AiDecisionResult Think(in NpcAiContext context)
        {
            LastContext = context;
            var trace = new AiDecisionTrace(
                SelectedGoal: "Capture",
                PlannerName: "Capture",
                PolicyName: "Capture",
                PlanLength: 1,
                PlanPreview: new[] { NpcCommand.Idle },
                PlanCost: 1,
                ReplanReason: "Capture",
                MethodName: "CaptureMethod",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(NpcCommand.Idle, trace);
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var techPath = Path.Combine(current.FullName, "Tech", "technologies.json");
            if (File.Exists(techPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
    }
}
