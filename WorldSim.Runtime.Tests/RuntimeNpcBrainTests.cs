using System;
using System.Collections.Generic;
using System.IO;
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
    public void RuntimeNpcBrain_PeriodicContextFields_RespectCadence()
    {
        var world = new World(16, 16, 10);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = false;

        var actor = world._people[0];
        var hostile = world._people.First(person => person.Home != actor.Home);
        hostile.Pos = actor.Pos;

        var spy = new CaptureBrain();
        var brain = new RuntimeNpcBrain(spy);

        for (var i = 0; i < 4; i++)
            brain.Think(actor, world, dt: 0.1f);

        Assert.Equal(4, spy.Contexts.Count);
        Assert.All(spy.Contexts, context => Assert.Equal(spy.Contexts[0].WarState, context.WarState));
        Assert.All(spy.Contexts, context => Assert.Equal(spy.Contexts[0].TileContestedNearby, context.TileContestedNearby));
        Assert.All(spy.Contexts, context => Assert.Equal(spy.Contexts[0].ColonyWarriorCount, context.ColonyWarriorCount));

        brain.Think(actor, world, dt: 0.1f); // tick 5, warrior count cadence refresh
        Assert.Equal(spy.Contexts[0].WarState, spy.Contexts[4].WarState);
        Assert.Equal(spy.Contexts[0].TileContestedNearby, spy.Contexts[4].TileContestedNearby);
    }

    [Fact]
    public void RuntimeNpcBrain_UsesRealFallbackSources_ForWarTerritoryAndRole()
    {
        var world = new World(16, 16, 10);
        world.EnableDiplomacy = true;
        world.EnableCombatPrimitives = true;

        var actor = world._people[0];
        actor.Roles = PersonRole.Warrior;
        var hostile = world._people.First(person => person.Home != actor.Home);
        hostile.Pos = actor.Pos;

        // Place actor near border-like distance between colony origins.
        actor.Pos = (
            (actor.Home.Origin.x + hostile.Home.Origin.x) / 2,
            (actor.Home.Origin.y + hostile.Home.Origin.y) / 2);

        var spy = new CaptureBrain();
        var brain = new RuntimeNpcBrain(spy);

        brain.Think(actor, world, dt: 0.1f);
        var context = spy.Contexts.Last();

        Assert.Equal(NpcWarState.War, context.WarState);
        Assert.True(context.TileContestedNearby);
        Assert.True(context.IsWarrior);
        Assert.True(context.ColonyWarriorCount >= 1);
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

        for (var i = 0; i < 6 && !runtime.GetAiDebugSnapshot().HasData; i++)
            runtime.AdvanceTick(0.25f);
        var before = runtime.GetAiDebugSnapshot();

        Assert.True(before.HasData);

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
    public void SimulationRuntime_FeatureFlagScaffold_CanBeSetAndRead()
    {
        var repoRoot = FindRepoRoot();
        var techPath = Path.Combine(repoRoot, "Tech", "technologies.json");
        var runtime = new SimulationRuntime(16, 16, 10, techPath);

        var flags = new RuntimeFeatureFlags(
            EnableCombatPrimitives: true,
            EnableDiplomacy: true,
            EnableFortifications: false,
            EnableSiege: false,
            EnableSupply: false,
            EnableCampaigns: false,
            EnablePredatorHumanAttacks: false);

        var result = runtime.SetFeatureFlags(flags);
        var read = runtime.GetFeatureFlags();

        Assert.True(result.Success);
        Assert.True(read.EnableCombatPrimitives);
        Assert.True(read.EnableDiplomacy);
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
            MethodScore: 1f,
            RunnerUpMethod: "None",
            RunnerUpScore: 0f,
            GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }

    private sealed class CaptureBrain : INpcDecisionBrain
    {
        public List<NpcAiContext> Contexts { get; } = new();

        public AiDecisionResult Think(in NpcAiContext context)
        {
            Contexts.Add(context);
            return new AiDecisionResult(NpcCommand.Idle, FixedTrace());
        }

        private static AiDecisionTrace FixedTrace()
        {
            return new AiDecisionTrace(
                SelectedGoal: "Capture",
                PlannerName: "Capture",
                PolicyName: "Capture",
                PlanLength: 0,
                PlanPreview: Array.Empty<NpcCommand>(),
                PlanCost: 0,
                ReplanReason: "Capture",
                MethodName: "Capture",
                MethodScore: 0f,
                RunnerUpMethod: "None",
                RunnerUpScore: 0f,
                GoalScores: Array.Empty<GoalScoreEntry>());
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
