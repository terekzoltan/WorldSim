using System.Collections.Generic;
using WorldSim.AI;
using Xunit;

namespace WorldSim.AI.Tests;

public class DecisionTests
{
    [Fact]
    public void GoalCooldown_UsesSimulationTime()
    {
        var goal = new Goal("BuildHouse") { CooldownSeconds = 5f };

        goal.MarkSelected(10f);

        Assert.True(goal.IsOnCooldown(12f));
        Assert.False(goal.IsOnCooldown(16f));
    }

    [Fact]
    public void GoapPlanner_BuildHouseGoalFallsBackToGatherWood_WhenStockIsLow()
    {
        var planner = new GoapPlanner();
        planner.SetGoal(new Goal("BuildHouse"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 1f,
            Hunger: 20f,
            Stamina: 70f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 5,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.GatherWood, decision.Command);
    }

    [Fact]
    public void UtilityGoapBrain_SelectsBuildHouse_WhenAffordableAndCapacityIsLow()
    {
        var goals = new List<Goal>
        {
            new Goal("GatherWood")
            {
                CooldownSeconds = 0f,
                Considerations = { new LowWoodStockConsideration(threshold: 6), new HungerConsideration() }
            },
            new Goal("GatherStone")
            {
                CooldownSeconds = 0f,
                Considerations = { new HungerConsideration() }
            },
            new Goal("BuildHouse")
            {
                CooldownSeconds = 0f,
                Considerations = { new BuildHouseFeasibleConsideration(), new InvertedConsideration(new HungerConsideration()) }
            }
        };

        var brain = new UtilityGoapBrain(new GoapPlanner(), goals);
        var context = new NpcAiContext(
            SimulationTimeSeconds: 3f,
            Hunger: 10f,
            Stamina: 90f,
            HomeWood: 80,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 3,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var result = brain.Think(context);

        Assert.Equal(NpcCommand.BuildHouse, result.Command);
        Assert.Equal("Goap", result.Trace.PlannerName);
        Assert.NotEmpty(result.Trace.GoalScores);
    }

    [Fact]
    public void GoapPlanner_RebuildsPlan_WhenCurrentStepBecomesInvalid()
    {
        var planner = new GoapPlanner();
        planner.SetGoal(new Goal("BuildHouse"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 2f,
            Hunger: 20f,
            Stamina: 80f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 4,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var first = planner.GetNextCommand(context);
        var second = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.GatherWood, first.Command);
        Assert.Equal(NpcCommand.GatherWood, second.Command);
        Assert.Equal("PlanRebuiltAfterInvalidation", second.ReplanReason);
    }

    [Fact]
    public void GoapPlanner_AppliesReplanBackoff_WhenNoPlanExists()
    {
        var planner = new GoapPlanner();
        planner.SetGoal(new Goal("UnknownGoal"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 7f,
            Hunger: 10f,
            Stamina: 70f,
            HomeWood: 5,
            HomeStone: 2,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 1,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 5,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var first = planner.GetNextCommand(context);
        var second = planner.GetNextCommand(context);

        Assert.Equal("NoPlan", first.ReplanReason);
        Assert.Equal("ReplanBackoff", second.ReplanReason);
    }

    [Fact]
    public void HtnPlanner_SelectsHighestScoreMethod_ForSecureFood()
    {
        var planner = new HtnPlanner();
        planner.SetGoal(new Goal("SecureFood"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 4f,
            Hunger: 82f,
            Stamina: 65f,
            HomeWood: 12,
            HomeStone: 4,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 3,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.EatFood, decision.Command);
        Assert.Equal("EmergencyEat", decision.MethodName);
    }
}
