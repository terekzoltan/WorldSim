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

    [Fact]
    public void SimplePlanner_DefendSelf_ChoosesFight_WhenStrongAndHealthy()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("DefendSelf"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 5f,
            Hunger: 35f,
            Stamina: 70f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 88f,
            Strength: 16,
            Defense: 20,
            NearbyPredators: 1,
            NearbyHostilePeople: 0,
            IsHostileStance: true,
            IsWarriorRole: true,
            LocalThreatScore: 0.45f);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.Fight, decision.Command);
    }

    [Fact]
    public void GoapPlanner_DefendSelf_ChoosesFlee_WhenLowHealth()
    {
        var planner = new GoapPlanner();
        planner.SetGoal(new Goal("DefendSelf"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 6f,
            Hunger: 25f,
            Stamina: 60f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 24f,
            Strength: 18,
            Defense: 30,
            NearbyPredators: 1,
            NearbyHostilePeople: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.Flee, decision.Command);
    }

    [Fact]
    public void UtilityGoapBrain_AppliesGoalBias_WhenSelectingGoal()
    {
        var gatherWood = new Goal("GatherWood")
        {
            CooldownSeconds = 0f
        };
        gatherWood.Considerations.Add(new FixedConsideration(0.3f));

        var buildHouse = new Goal("BuildHouse")
        {
            CooldownSeconds = 0f
        };
        buildHouse.Considerations.Add(new FixedConsideration(0.3f));

        var goals = new List<Goal> { gatherWood, buildHouse };
        var brain = new UtilityGoapBrain(new SimplePlanner(), goals);

        var context = new NpcAiContext(
            SimulationTimeSeconds: 7f,
            Hunger: 20f,
            Stamina: 80f,
            HomeWood: 80,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 8,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            BiasGathering: 0.0f,
            BiasBuilding: 0.25f);

        var result = brain.Think(context);

        Assert.Equal("BuildHouse", result.Trace.SelectedGoal);
        Assert.Equal(NpcCommand.BuildHouse, result.Command);
    }

    [Fact]
    public void ThreatDecisionPolicy_WarriorInContestedWar_PrefersFight()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 1f,
            Hunger: 15f,
            Stamina: 80f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 88f,
            Strength: 18,
            Defense: 14,
            NearbyHostilePeople: 1,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            NearbyEnemyCount: 1,
            HostileProximityScore: 0.6f,
            LocalThreatScore: 0.75f);

        Assert.True(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_CivilianUnderThreat_PrefersFlee()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 1f,
            Hunger: 15f,
            Stamina: 80f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 92f,
            Strength: 17,
            Defense: 16,
            NearbyHostilePeople: 2,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: false,
            NearbyEnemyCount: 2,
            HostileProximityScore: 0.9f,
            LocalThreatScore: 0.85f);

        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
        Assert.True(ThreatDecisionPolicy.ShouldPrioritizeDefense(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_PeacefulZeroSignal_DoesNotPrioritizeDefense()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 2f,
            Hunger: 24f,
            Stamina: 70f,
            HomeWood: 4,
            HomeStone: 3,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 4,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 80f,
            Strength: 10,
            Defense: 8,
            LocalThreatScore: 0.05f);

        Assert.True(ThreatDecisionPolicy.IsPeacefulZeroSignal(context));
        Assert.False(ThreatDecisionPolicy.ShouldPrioritizeDefense(context));
        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void SimplePlanner_BuildDefenses_UsesFortificationCommands_WhenHostile()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("BuildDefenses"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 12f,
            Hunger: 22f,
            Stamina: 75f,
            HomeWood: 20,
            HomeStone: 8,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 12,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            IsHostileStance: true);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.BuildWatchtower, decision.Command);
    }

    [Fact]
    public void GoapPlanner_RaidBorder_PrefersRaid_ForWarriorInWar()
    {
        var planner = new GoapPlanner();
        planner.SetGoal(new Goal("RaidBorder"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 14f,
            Hunger: 18f,
            Stamina: 84f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 88f,
            Strength: 16,
            Defense: 14,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            NearbyEnemyCount: 0,
            LocalThreatScore: 0.5f);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.RaidBorder, decision.Command);
    }

    [Fact]
    public void UtilityGoapBrain_CrowdPenalty_PrefersLessCrowdedEquivalentGoal()
    {
        var gatherWood = new Goal("GatherWood") { CooldownSeconds = 0f };
        gatherWood.Considerations.Add(new FixedConsideration(0.6f));

        var buildHouse = new Goal("BuildHouse") { CooldownSeconds = 0f };
        buildHouse.Considerations.Add(new FixedConsideration(0.6f));

        var goals = new List<Goal> { gatherWood, buildHouse };
        var brain = new UtilityGoapBrain(new SimplePlanner(), goals);

        var context = new NpcAiContext(
            SimulationTimeSeconds: 22f,
            Hunger: 18f,
            Stamina: 80f,
            HomeWood: 80,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 8,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            ResourceCrowdPressure: 1f,
            BuildCrowdPressure: 0f);

        var result = brain.Think(context);

        Assert.Equal("BuildHouse", result.Trace.SelectedGoal);
    }

    private sealed class FixedConsideration : Consideration
    {
        private readonly float _value;

        public FixedConsideration(float value)
        {
            _value = value;
        }

        public override float Evaluate(in NpcAiContext context)
        {
            return _value;
        }
    }
}
