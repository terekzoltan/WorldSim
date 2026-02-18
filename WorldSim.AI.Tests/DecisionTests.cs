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
            HomeWood: 0,
            HomeStone: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 5,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var command = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.GatherWood, command);
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
            HomeWood: 80,
            HomeStone: 0,
            HomeFood: 3,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100);

        var command = brain.Think(context);

        Assert.Equal(NpcCommand.BuildHouse, command);
    }
}
