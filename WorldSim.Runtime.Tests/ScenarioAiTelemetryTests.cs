using System.Reflection;
using WorldSim.AI;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class ScenarioAiTelemetryTests
{
    [Fact]
    public void ScenarioAiTargetKindClassifier_NormalizesKnownPrefixes()
    {
        Assert.Equal("none", ScenarioAiTargetKindClassifier.Normalize(null));
        Assert.Equal("none", ScenarioAiTargetKindClassifier.Normalize("none"));
        Assert.Equal("build", ScenarioAiTargetKindClassifier.Normalize("build:4:7"));
        Assert.Equal("resource", ScenarioAiTargetKindClassifier.Normalize("resource:Food:3:9"));
        Assert.Equal("retreat", ScenarioAiTargetKindClassifier.Normalize("retreat:6:2"));
        Assert.Equal("move", ScenarioAiTargetKindClassifier.Normalize("move:11:13"));
        Assert.Equal("other", ScenarioAiTargetKindClassifier.Normalize("enemy:17"));
    }

    [Fact]
    public void BuildScenarioAiTelemetrySnapshot_EmptyWorld_ReturnsEmptySnapshot()
    {
        var world = new World(16, 16, 8, randomSeed: 42);
        world._people.Clear();

        var telemetry = world.BuildScenarioAiTelemetrySnapshot();

        Assert.Equal(0, telemetry.DecisionCount);
        Assert.Empty(telemetry.GoalCounts);
        Assert.Empty(telemetry.CommandCounts);
        Assert.Empty(telemetry.ReplanReasonCounts);
        Assert.Empty(telemetry.MethodCounts);
        Assert.Empty(telemetry.DebugCauseCounts);
        Assert.Empty(telemetry.TargetKindCounts);
        Assert.Empty(telemetry.TopGoals);
        Assert.Empty(telemetry.TopDebugCauses);
        Assert.Null(telemetry.LatestDecision);
    }

    [Fact]
    public void BuildScenarioAiTelemetrySnapshot_AggregatesDecisionMetadataAndLatestSample()
    {
        var world = new World(
            width: 16,
            height: 16,
            initialPop: 16,
            brainFactory: colony => new RuntimeNpcBrain(new FixedDecisionBrain(BuildDecisionForColony(colony.Id))),
            randomSeed: 42);

        world.Update(0.25f);

        foreach (var person in world._people)
        {
            switch (person.Home.Id)
            {
                case 0:
                case 1:
                    SetDebugState(person, "retreat_refuge", "retreat:4:4");
                    break;
                case 2:
                    SetDebugState(person, "move_to_resource", "resource:Food:6:8");
                    break;
                default:
                    SetDebugState(person, "no_progress_backoff:combat", "move:9:3");
                    break;
            }
        }

        var telemetry = world.BuildScenarioAiTelemetrySnapshot();

        Assert.Equal(16, telemetry.DecisionCount);
        Assert.Collection(
            telemetry.GoalCounts,
            entry => Assert.Equal(("DefendSelf", 12), (entry.Name, entry.Count)),
            entry => Assert.Equal(("GatherFood", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.CommandCounts,
            entry => Assert.Equal(("Fight", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("Flee", 4), (entry.Name, entry.Count)),
            entry => Assert.Equal(("GatherFood", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.ReplanReasonCounts,
            entry => Assert.Equal(("ThreatResponse", 12), (entry.Name, entry.Count)),
            entry => Assert.Equal(("HarvestDemand", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.MethodCounts,
            entry => Assert.Equal(("EmergencyFight", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("EmergencyRetreat", 4), (entry.Name, entry.Count)),
            entry => Assert.Equal(("HarvestFood", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.DebugCauseCounts,
            entry => Assert.Equal(("retreat_refuge", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("move_to_resource", 4), (entry.Name, entry.Count)),
            entry => Assert.Equal(("no_progress_backoff:combat", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.TargetKindCounts,
            entry => Assert.Equal(("retreat", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("move", 4), (entry.Name, entry.Count)),
            entry => Assert.Equal(("resource", 4), (entry.Name, entry.Count)));

        Assert.Collection(
            telemetry.TopGoals,
            entry => Assert.Equal(("DefendSelf", 12), (entry.Name, entry.Count)),
            entry => Assert.Equal(("GatherFood", 4), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.TopDebugCauses,
            entry => Assert.Equal(("retreat_refuge", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("move_to_resource", 4), (entry.Name, entry.Count)),
            entry => Assert.Equal(("no_progress_backoff:combat", 4), (entry.Name, entry.Count)));

        Assert.NotNull(telemetry.LatestDecision);
        Assert.Equal(world._people.Min(person => person.Id), telemetry.LatestDecision!.ActorId);
        Assert.Equal(0, telemetry.LatestDecision.ColonyId);
        Assert.Equal("DefendSelf", telemetry.LatestDecision.SelectedGoal);
        Assert.Equal("Fight", telemetry.LatestDecision.NextCommand);
        Assert.Equal(1, telemetry.LatestDecision.PlanLength);
        Assert.Equal(3, telemetry.LatestDecision.PlanCost);
        Assert.Equal("ThreatResponse", telemetry.LatestDecision.ReplanReason);
        Assert.Equal("EmergencyFight", telemetry.LatestDecision.MethodName);
        Assert.Equal("retreat_refuge", telemetry.LatestDecision.DebugDecisionCause);
        Assert.Equal("retreat:4:4", telemetry.LatestDecision.DebugTargetKey);
        Assert.Equal("retreat", telemetry.LatestDecision.TargetKind);
    }

    [Fact]
    public void BuildScenarioAiTelemetrySnapshot_SortsTiedCountsByName()
    {
        var world = new World(
            width: 16,
            height: 16,
            initialPop: 16,
            brainFactory: colony => new RuntimeNpcBrain(new FixedDecisionBrain(BuildTieDecisionForColony(colony.Id))),
            randomSeed: 99);

        world.Update(0.25f);
        foreach (var person in world._people)
            SetDebugState(person, "none", "none");

        var telemetry = world.BuildScenarioAiTelemetrySnapshot();

        Assert.Collection(
            telemetry.GoalCounts,
            entry => Assert.Equal(("AlphaGoal", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("BetaGoal", 8), (entry.Name, entry.Count)));
        Assert.Collection(
            telemetry.CommandCounts,
            entry => Assert.Equal(("BuildHouse", 8), (entry.Name, entry.Count)),
            entry => Assert.Equal(("CraftTools", 8), (entry.Name, entry.Count)));
    }

    private static AiDecisionResult BuildDecisionForColony(int colonyId)
    {
        return colonyId switch
        {
            0 or 1 => BuildDecision(
                command: NpcCommand.Fight,
                goal: "DefendSelf",
                planner: "Fixed",
                policy: "ScenarioAiTelemetry",
                planLength: 1,
                planCost: 3,
                replanReason: "ThreatResponse",
                methodName: "EmergencyFight"),
            2 => BuildDecision(
                command: NpcCommand.GatherFood,
                goal: "GatherFood",
                planner: "Fixed",
                policy: "ScenarioAiTelemetry",
                planLength: 2,
                planCost: 4,
                replanReason: "HarvestDemand",
                methodName: "HarvestFood"),
            _ => BuildDecision(
                command: NpcCommand.Flee,
                goal: "DefendSelf",
                planner: "Fixed",
                policy: "ScenarioAiTelemetry",
                planLength: 2,
                planCost: 5,
                replanReason: "ThreatResponse",
                methodName: "EmergencyRetreat")
        };
    }

    private static AiDecisionResult BuildTieDecisionForColony(int colonyId)
    {
        return colonyId switch
        {
            0 or 1 => BuildDecision(
                command: NpcCommand.BuildHouse,
                goal: "AlphaGoal",
                planner: "Fixed",
                policy: "ScenarioAiTelemetry",
                planLength: 1,
                planCost: 1,
                replanReason: "AlphaReason",
                methodName: "AlphaMethod"),
            _ => BuildDecision(
                command: NpcCommand.CraftTools,
                goal: "BetaGoal",
                planner: "Fixed",
                policy: "ScenarioAiTelemetry",
                planLength: 1,
                planCost: 1,
                replanReason: "BetaReason",
                methodName: "BetaMethod")
        };
    }

    private static AiDecisionResult BuildDecision(
        NpcCommand command,
        string goal,
        string planner,
        string policy,
        int planLength,
        int planCost,
        string replanReason,
        string methodName)
    {
        return new AiDecisionResult(
            Command: command,
            Trace: new AiDecisionTrace(
                SelectedGoal: goal,
                PlannerName: planner,
                PolicyName: policy,
                PlanLength: planLength,
                PlanPreview: new[] { command },
                PlanCost: planCost,
                ReplanReason: replanReason,
                MethodName: methodName,
                GoalScores: Array.Empty<GoalScoreEntry>()));
    }

    private static void SetDebugState(Person person, string cause, string targetKey)
    {
        SetPrivateSetterProperty(person, nameof(Person.DebugDecisionCause), cause);
        SetPrivateSetterProperty(person, nameof(Person.DebugTargetKey), targetKey);
    }

    private static void SetPrivateSetterProperty(Person person, string propertyName, string value)
    {
        var property = typeof(Person).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        var setter = property!.GetSetMethod(nonPublic: true);
        Assert.NotNull(setter);
        setter!.Invoke(person, new object[] { value });
    }

    private sealed class FixedDecisionBrain : INpcDecisionBrain
    {
        private readonly AiDecisionResult _result;

        public FixedDecisionBrain(AiDecisionResult result)
        {
            _result = result;
        }

        public AiDecisionResult Think(in NpcAiContext context) => _result;
    }
}
