using System;
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
    public void UtilityGoapBrain_CrowdPenalty_PrefersLessCrowdedEquivalentGoal()
    {
        var gatherWood = new Goal("GatherWood")
        {
            CooldownSeconds = 0f
        };
        gatherWood.Considerations.Add(new FixedConsideration(0.6f));

        var buildHouse = new Goal("BuildHouse")
        {
            CooldownSeconds = 0f
        };
        buildHouse.Considerations.Add(new FixedConsideration(0.6f));

        var goals = new List<Goal> { gatherWood, buildHouse };
        var brain = new UtilityGoapBrain(new SimplePlanner(), goals);

        var context = new NpcAiContext(
            SimulationTimeSeconds: 21f,
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
            ResourceCrowdPressure: 1f,
            BuildCrowdPressure: 0f);

        var result = brain.Think(context);

        Assert.Equal("BuildHouse", result.Trace.SelectedGoal);
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
            LocalThreatScore: 0.75f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

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
    public void ThreatNearbyConsideration_AmbientWarPressureWithoutImmediateThreat_ReturnsZero()
    {
        var consideration = new ThreatNearbyConsideration(threatCap: 3);
        var context = new NpcAiContext(
            SimulationTimeSeconds: 2f,
            Hunger: 24f,
            Stamina: 70f,
            HomeWood: 4,
            HomeStone: 3,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 6,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 88f,
            Strength: 12,
            Defense: 10,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            LocalThreatScore: 0.35f,
            AmbientThreatScore: 0.35f);

        Assert.Equal(0f, consideration.Evaluate(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_AmbientWarPressureWithoutImmediateThreat_DoesNotPrioritizeDefense()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 2f,
            Hunger: 24f,
            Stamina: 70f,
            HomeWood: 4,
            HomeStone: 3,
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
            Health: 90f,
            Strength: 14,
            Defense: 12,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            LocalThreatScore: 0.35f,
            AmbientThreatScore: 0.35f);

        Assert.False(ThreatDecisionPolicy.ShouldPrioritizeDefense(context));
        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void GoalSelector_AmbientWarPressureWithoutImmediateThreat_DoesNotSelectDefendSelf()
    {
        var goals = GoalLibrary.CreateDefaultGoals();
        var planner = new SimplePlanner();
        var selector = new GoalSelector();
        var context = new NpcAiContext(
            SimulationTimeSeconds: 3f,
            Hunger: 14f,
            Stamina: 84f,
            HomeWood: 6,
            HomeStone: 6,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 12,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            HomeMilitaryTechCount: 0,
            LocalThreatScore: 0.35f,
            AmbientThreatScore: 0.35f);

        var selection = selector.SelectGoal(goals, planner, context);

        Assert.NotNull(selection.SelectedGoal);
        Assert.NotEqual("DefendSelf", selection.SelectedGoal!.Name);
        var defendScore = selection.Scores.Single(entry => entry.GoalName == "DefendSelf");
        Assert.Equal(0f, defendScore.Score);
    }

    [Fact]
    public void GoalSelector_ZeroScoreGoalsRemainVisibleButAreNotSelected()
    {
        var goals = new List<Goal>
        {
            new("ForageArmySupply") { Considerations = { new FixedConsideration(0f) } },
            new("MaintainArmySupply") { Considerations = { new FixedConsideration(0f) } }
        };
        var selector = new GoalSelector();
        var planner = new SimplePlanner();

        var selection = selector.SelectGoal(goals, planner, CreateArmyForageContext(hasArmyForageDemand: false));

        Assert.Null(selection.SelectedGoal);
        var forageScore = selection.Scores.Single(entry => entry.GoalName == "ForageArmySupply");
        var supplyScore = selection.Scores.Single(entry => entry.GoalName == "MaintainArmySupply");
        Assert.Equal(0f, forageScore.Score);
        Assert.Equal(0f, supplyScore.Score);
    }

    [Fact]
    public void GoalSelector_PositiveScoreGoalStillSelected()
    {
        var goals = new List<Goal>
        {
            new("ForageArmySupply") { Considerations = { new FixedConsideration(0f) } },
            new("GatherStone") { Considerations = { new FixedConsideration(0.4f) } }
        };
        var selector = new GoalSelector();
        var planner = new SimplePlanner();

        var selection = selector.SelectGoal(goals, planner, CreateArmyForageContext(hasArmyForageDemand: false));

        Assert.NotNull(selection.SelectedGoal);
        Assert.Equal("GatherStone", selection.SelectedGoal!.Name);
        var forageScore = selection.Scores.Single(entry => entry.GoalName == "ForageArmySupply");
        Assert.Equal(0f, forageScore.Score);
    }

    [Fact]
    public void ThreatDecisionPolicy_LowEquipmentHighThreat_AvoidsFight()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 3f,
            Hunger: 15f,
            Stamina: 82f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 90f,
            Strength: 20,
            Defense: 16,
            NearbyHostilePeople: 2,
            IsWarStance: true,
            IsWarriorRole: true,
            NearbyEnemyCount: 2,
            LocalThreatScore: 0.7f,
            HomeWeaponLevel: 0,
            HomeArmorLevel: 0);

        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_CommanderLowGroupMorale_PrefersRetreat()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 3f,
            Hunger: 12f,
            Stamina: 78f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 82f,
            Strength: 18,
            Defense: 14,
            NearbyHostilePeople: 1,
            NearbyEnemyCount: 2,
            LocalThreatScore: 0.66f,
            IsWarStance: true,
            IsWarriorRole: true,
            IsCommander: true,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 28f,
            CommanderMoraleStabilityBonus: 0.35f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        Assert.True(ThreatDecisionPolicy.ShouldCommanderInitiateRetreat(context));
        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_CommanderHighMorale_CanPressAdvantage()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 4f,
            Hunger: 10f,
            Stamina: 86f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 92f,
            Strength: 20,
            Defense: 16,
            NearbyHostilePeople: 1,
            NearbyEnemyCount: 1,
            LocalThreatScore: 0.46f,
            IsWarStance: true,
            IsWarriorRole: true,
            IsCommander: true,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 70f,
            CommanderMoraleStabilityBonus: 0.32f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        Assert.True(ThreatDecisionPolicy.ShouldCommanderPressAdvantage(context));
        Assert.True(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_RoutingState_SuppressesReengage()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 4f,
            Hunger: 10f,
            Stamina: 86f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 90f,
            Strength: 20,
            Defense: 16,
            NearbyHostilePeople: 2,
            NearbyEnemyCount: 2,
            LocalThreatScore: 0.65f,
            IsWarStance: true,
            IsWarriorRole: true,
            IsRouting: true,
            RoutingTicksRemaining: 5,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        Assert.True(ThreatDecisionPolicy.ShouldSuppressReengage(context));
        Assert.False(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void ThreatDecisionPolicy_CommanderPressAdvantage_OverridesSuppressionGuard()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 5f,
            Hunger: 12f,
            Stamina: 84f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 1,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 92f,
            Strength: 20,
            Defense: 16,
            NearbyHostilePeople: 1,
            NearbyEnemyCount: 1,
            LocalThreatScore: 0.5f,
            IsWarStance: true,
            IsWarriorRole: true,
            IsCommander: true,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 70f,
            CommanderMoraleStabilityBonus: 0.3f,
            RoutingTicksRemaining: 3,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        Assert.True(ThreatDecisionPolicy.ShouldSuppressReengage(context));
        Assert.True(ThreatDecisionPolicy.ShouldCommanderPressAdvantage(context));
        Assert.True(ThreatDecisionPolicy.ShouldFight(context));
    }

    [Fact]
    public void SimplePlanner_RaidBorder_CommanderPressAdvantage_CanRaidDuringSuppressionWindow()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("RaidBorder"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 16f,
            Hunger: 15f,
            Stamina: 86f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 90f,
            Strength: 18,
            Defense: 14,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            IsCommander: true,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 72f,
            CommanderMoraleStabilityBonus: 0.35f,
            NearbyEnemyCount: 1,
            LocalThreatScore: 0.5f,
            RoutingTicksRemaining: 3,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.RaidBorder, decision.Command);
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
    public void SimplePlanner_RaidBorder_CommanderLowMorale_FallsBackToFlee()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("RaidBorder"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 15f,
            Hunger: 18f,
            Stamina: 82f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 84f,
            Strength: 16,
            Defense: 14,
            IsWarStance: true,
            IsContestedTile: true,
            IsWarriorRole: true,
            IsCommander: true,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 26f,
            CommanderMoraleStabilityBonus: 0.3f,
            NearbyEnemyCount: 2,
            LocalThreatScore: 0.68f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.Flee, decision.Command);
    }

    [Fact]
    public void SimplePlanner_UnlockMilitaryTech_FiresUnderWarPressure_WhenTechCountLow()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("UnlockMilitaryTech"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 9f,
            Hunger: 20f,
            Stamina: 75f,
            HomeWood: 12,
            HomeStone: 4,
            HomeIron: 2,
            HomeGold: 0,
            HomeFood: 8,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            IsWarStance: true,
            HomeMilitaryTechCount: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.ResearchTech, decision.Command);
    }

    [Fact]
    public void SimplePlanner_UnlockMilitaryTech_DoesNotFire_WhenThresholdReached()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("UnlockMilitaryTech"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 9f,
            Hunger: 20f,
            Stamina: 75f,
            HomeWood: 12,
            HomeStone: 4,
            HomeIron: 2,
            HomeGold: 0,
            HomeFood: 8,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            IsWarStance: true,
            HomeMilitaryTechCount: 3);

        var decision = planner.GetNextCommand(context);

        Assert.NotEqual(NpcCommand.ResearchTech, decision.Command);
    }

    [Fact]
    public void ThreatDecisionPolicy_SiegeAttackerWithTowerPressure_PrioritizesStructureTargeting()
    {
        var context = new NpcAiContext(
            SimulationTimeSeconds: 11f,
            Hunger: 18f,
            Stamina: 74f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 0,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 78f,
            Strength: 16,
            Defense: 14,
            IsNearActiveSiege: true,
            IsSiegeAttackerRole: true,
            NearbyEnemyDefensiveStructures: 3,
            NearbyEnemyTowerCount: 2,
            NearbyEnemyWallCount: 1,
            NearbySiegePressure: 0.68f,
            LocalThreatScore: 0.62f,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 54f);

        Assert.True(ThreatDecisionPolicy.ShouldPrioritizeSiegeTargeting(context));
        Assert.True(ThreatDecisionPolicy.ShouldAvoidTowerTunnel(context));
    }

    [Fact]
    public void SimplePlanner_RaidBorder_PrefersAttackStructure_WhenSiegeTargetingIsPreferred()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("RaidBorder"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 12f,
            Hunger: 12f,
            Stamina: 82f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 86f,
            Strength: 17,
            Defense: 15,
            IsWarStance: true,
            IsWarriorRole: true,
            NearbyEnemyCount: 0,
            IsNearActiveSiege: true,
            IsSiegeAttackerRole: true,
            NearbyEnemyDefensiveStructures: 2,
            NearbyEnemyTowerCount: 2,
            NearbyEnemyWallCount: 0,
            NearbySiegePressure: 0.6f,
            LocalThreatScore: 0.58f,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 56f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.AttackStructure, decision.Command);
    }

    [Fact]
    public void SimplePlanner_DefendSelf_SiegeDefenderSortie_PrefersFight()
    {
        var planner = new SimplePlanner();
        planner.SetGoal(new Goal("DefendSelf"));

        var context = new NpcAiContext(
            SimulationTimeSeconds: 13f,
            Hunger: 14f,
            Stamina: 88f,
            HomeWood: 0,
            HomeStone: 0,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 6,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: 90f,
            Strength: 18,
            Defense: 16,
            NearbyEnemyCount: 2,
            IsNearActiveSiege: true,
            IsSiegeDefenderRole: true,
            NearbyFriendlyTowerCount: 1,
            NearbyFriendlyWallCount: 2,
            NearbySiegePressure: 0.4f,
            IsRouting: false,
            ActiveCombatGroupSize: 4,
            ActiveGroupAverageMorale: 68f,
            CommanderMoraleStabilityBonus: 0.25f,
            LocalThreatScore: 0.42f,
            HomeWeaponLevel: 1,
            HomeArmorLevel: 1);

        var decision = planner.GetNextCommand(context);

        Assert.Equal(NpcCommand.Fight, decision.Command);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_AssignsCarrier_WhenNoCarrierExists(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var decision = planner.GetNextCommand(CreateSupplyCarrierContext(
            hasColonySupplyCarrier: false,
            canAssignSupplyCarrier: true,
            hasArmySupplyDemand: true));

        Assert.Equal(NpcCommand.AssignSupplyCarrier, decision.Command);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_RefillsCarrier_WhenRefillIsAvailable(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var decision = planner.GetNextCommand(CreateSupplyCarrierContext(
            isSupplyCarrier: true,
            hasColonySupplyCarrier: true,
            supplyCarrierNeedsRefill: true,
            supplyCarrierCanRefill: true,
            hasArmySupplyDemand: true));

        Assert.Equal(NpcCommand.RefillInventory, decision.Command);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_DeliversOnlyWhenDeliveryContextIsExplicit(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var withoutDelivery = planner.GetNextCommand(CreateSupplyCarrierContext(
            isSupplyCarrier: true,
            hasColonySupplyCarrier: true,
            supplyCarrierCanDeliver: false,
            hasArmySupplyDemand: true));

        planner.SetGoal(new Goal("MaintainArmySupply"));
        var withDelivery = planner.GetNextCommand(CreateSupplyCarrierContext(
            isSupplyCarrier: true,
            hasColonySupplyCarrier: true,
            supplyCarrierCanDeliver: true,
            armySupplyRatio: 0.2f,
            hasArmySupplyDemand: true));

        Assert.NotEqual(NpcCommand.DeliverSupply, withoutDelivery.Command);
        Assert.Equal(NpcCommand.DeliverSupply, withDelivery.Command);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_AbortsInvalidSupplySource(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var decision = planner.GetNextCommand(CreateSupplyCarrierContext(
            isSupplyCarrier: true,
            hasColonySupplyCarrier: true,
            supplyCarrierSourceValid: false,
            hasArmySupplyDemand: true));

        Assert.Equal(NpcCommand.AbortSupplyDelivery, decision.Command);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_NoDemandReturnsIdleEvenWhenCarrierWorkIsPossible(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var decision = planner.GetNextCommand(CreateSupplyCarrierContext(
            hasColonySupplyCarrier: false,
            canAssignSupplyCarrier: true,
            supplyCarrierSourceValid: false,
            supplyCarrierCanDeliver: true,
            hasArmySupplyDemand: false));

        Assert.Equal(NpcCommand.Idle, decision.Command);
        Assert.DoesNotContain(NpcCommand.AssignSupplyCarrier, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.RefillInventory, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.DeliverSupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.AbortSupplyDelivery, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_MaintainArmySupply_ThreatReturnsIdleEvenWhenDemandExists(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("MaintainArmySupply"));

        var decision = planner.GetNextCommand(CreateSupplyCarrierContext(
            hasColonySupplyCarrier: false,
            canAssignSupplyCarrier: true,
            directThreatScore: 0.7f,
            hasImmediateThreat: true,
            hasArmySupplyDemand: true));

        Assert.Equal(NpcCommand.Idle, decision.Command);
        Assert.DoesNotContain(NpcCommand.AssignSupplyCarrier, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.DeliverSupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.AbortSupplyDelivery, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_DefendSelfDominatesSupplyCarrierSupport_WhenImmediateThreatExists(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var result = brain.Think(CreateSupplyCarrierContext(
            hasColonySupplyCarrier: false,
            canAssignSupplyCarrier: true,
            health: 90f,
            strength: 18,
            defense: 18,
            nearbyPredators: 1,
            directThreatScore: 0.8f,
            hasImmediateThreat: true,
            hasArmySupplyDemand: true));

        Assert.Equal("DefendSelf", result.Trace.SelectedGoal);
        Assert.False(IsCarrierCommand(result.Command));
        Assert.NotEqual(NpcCommand.GatherFood, result.Command);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_ForageArmySupply_Forages_WhenDemandAndCapabilitiesArePresent(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("ForageArmySupply"));

        var decision = planner.GetNextCommand(CreateArmyForageContext());

        Assert.Equal(NpcCommand.ForageArmySupply, decision.Command);
        Assert.Contains(NpcCommand.ForageArmySupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void Planner_ForageArmySupply_NoDemandReturnsIdleEvenWhenCapabilitiesArePresent(string plannerMode)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("ForageArmySupply"));

        var decision = planner.GetNextCommand(CreateArmyForageContext(hasArmyForageDemand: false));

        Assert.Equal(NpcCommand.Idle, decision.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple", true, 0f, false)]
    [InlineData("goap", true, 0f, false)]
    [InlineData("htn", true, 0f, false)]
    [InlineData("simple", false, 0.7f, false)]
    [InlineData("goap", false, 0.7f, false)]
    [InlineData("htn", false, 0.7f, false)]
    [InlineData("simple", false, 0f, true)]
    [InlineData("goap", false, 0f, true)]
    [InlineData("htn", false, 0f, true)]
    public void Planner_ForageArmySupply_ThreatOrRoutingReturnsIdle(string plannerMode, bool hasImmediateThreat, float directThreatScore, bool isRouting)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("ForageArmySupply"));

        var decision = planner.GetNextCommand(CreateArmyForageContext(
            hasImmediateThreat: hasImmediateThreat,
            directThreatScore: directThreatScore,
            isRouting: isRouting));

        Assert.Equal(NpcCommand.Idle, decision.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple", "can_forage")]
    [InlineData("goap", "can_forage")]
    [InlineData("htn", "can_forage")]
    [InlineData("simple", "source_available")]
    [InlineData("goap", "source_available")]
    [InlineData("htn", "source_available")]
    [InlineData("simple", "source_in_range")]
    [InlineData("goap", "source_in_range")]
    [InlineData("htn", "source_in_range")]
    [InlineData("simple", "consumer_cap")]
    [InlineData("goap", "consumer_cap")]
    [InlineData("htn", "consumer_cap")]
    [InlineData("simple", "pool_capacity")]
    [InlineData("goap", "pool_capacity")]
    [InlineData("htn", "pool_capacity")]
    public void Planner_ForageArmySupply_MissingCapabilityReturnsIdle(string plannerMode, string missingCapability)
    {
        var planner = CreatePlanner(plannerMode);
        planner.SetGoal(new Goal("ForageArmySupply"));

        var decision = planner.GetNextCommand(CreateArmyForageContext(
            canForageArmySupply: missingCapability != "can_forage",
            armyForageSourceAvailable: missingCapability != "source_available",
            armyForageSourceInRange: missingCapability != "source_in_range",
            armyForageConsumerCapRemaining: missingCapability != "consumer_cap",
            armyForageRationPoolHasCapacity: missingCapability != "pool_capacity"));

        Assert.Equal(NpcCommand.Idle, decision.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, decision.PlanPreview);
        Assert.DoesNotContain(NpcCommand.GatherFood, decision.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_SelectsForageArmySupply_WhenDemandAndLowSupplyExist(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var result = brain.Think(CreateArmyForageContext());

        Assert.Equal("ForageArmySupply", result.Trace.SelectedGoal);
        Assert.Equal(NpcCommand.ForageArmySupply, result.Command);
        Assert.Contains(NpcCommand.ForageArmySupply, result.Trace.PlanPreview);
        Assert.DoesNotContain(NpcCommand.GatherFood, result.Trace.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_ForageArmySupplyBiasDoesNotCreateDemand(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var result = brain.Think(CreateArmyForageContext(
            hasArmyForageDemand: false,
            biasFarming: 1f,
            biasGathering: 1f));

        Assert.NotEqual("ForageArmySupply", result.Trace.SelectedGoal);
        Assert.NotEqual(NpcCommand.ForageArmySupply, result.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, result.Trace.PlanPreview);
        var forageScore = result.Trace.GoalScores.Single(entry => entry.GoalName == "ForageArmySupply");
        Assert.Equal(0f, forageScore.Score);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_DemandRemovedAfterForage_DoesNotKeepStaleForageTrace(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var first = brain.Think(CreateArmyForageContext(
            simulationTimeSeconds: 30f,
            hunger: 0f,
            stamina: 100f));
        var second = brain.Think(CreateArmyForageContext(
            simulationTimeSeconds: 31f,
            hunger: 0f,
            stamina: 100f,
            hasArmyForageDemand: false));

        Assert.Equal("ForageArmySupply", first.Trace.SelectedGoal);
        Assert.Equal(NpcCommand.ForageArmySupply, first.Command);
        Assert.NotEqual("ForageArmySupply", second.Trace.SelectedGoal);
        Assert.NotEqual(NpcCommand.ForageArmySupply, second.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, second.Trace.PlanPreview);
        var forageScore = second.Trace.GoalScores.Single(entry => entry.GoalName == "ForageArmySupply");
        Assert.Equal(0f, forageScore.Score);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_NoDemandDoesNotSelectTraceOnlySupportGoals(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var result = brain.Think(CreateArmyForageContext(
            hasArmyForageDemand: false,
            hasArmySupplyDemand: false,
            canAssignSupplyCarrier: true,
            simulationTimeSeconds: 32f,
            hunger: 0f,
            stamina: 100f));

        Assert.NotEqual("ForageArmySupply", result.Trace.SelectedGoal);
        Assert.NotEqual("MaintainArmySupply", result.Trace.SelectedGoal);
        Assert.NotEqual(NpcCommand.ForageArmySupply, result.Command);
        Assert.DoesNotContain(NpcCommand.ForageArmySupply, result.Trace.PlanPreview);
        Assert.DoesNotContain(NpcCommand.AssignSupplyCarrier, result.Trace.PlanPreview);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void UtilityBrain_DefendSelfDominatesForageArmySupply_WhenImmediateThreatExists(string plannerMode)
    {
        var brain = new UtilityGoapBrain(CreatePlanner(plannerMode), GoalLibrary.CreateDefaultGoals(), "Test");

        var result = brain.Think(CreateArmyForageContext(
            health: 90f,
            strength: 18,
            defense: 18,
            nearbyPredators: 1,
            directThreatScore: 0.8f,
            hasImmediateThreat: true));

        Assert.Equal("DefendSelf", result.Trace.SelectedGoal);
        Assert.NotEqual(NpcCommand.ForageArmySupply, result.Command);
        Assert.NotEqual(NpcCommand.GatherFood, result.Command);
    }

    private static IPlanner CreatePlanner(string plannerMode)
        => plannerMode switch
        {
            "simple" => new SimplePlanner(),
            "goap" => new GoapPlanner(),
            "htn" => new HtnPlanner(),
            _ => throw new ArgumentOutOfRangeException(nameof(plannerMode), plannerMode, "Unknown planner mode")
        };

    private static bool IsCarrierCommand(NpcCommand command)
        => command is NpcCommand.AssignSupplyCarrier
            or NpcCommand.DeliverSupply
            or NpcCommand.AbortSupplyDelivery;

    private static NpcAiContext CreateSupplyCarrierContext(
        bool isSupplyCarrier = false,
        bool hasColonySupplyCarrier = false,
        bool canAssignSupplyCarrier = false,
        bool supplyCarrierNeedsRefill = false,
        bool supplyCarrierCanRefill = false,
        bool supplyCarrierCanDeliver = false,
        bool supplyCarrierSourceValid = true,
        float armySupplyRatio = 1f,
        float health = 100f,
        int strength = 10,
        int defense = 10,
        int nearbyPredators = 0,
        float directThreatScore = 0f,
        bool hasImmediateThreat = false,
        bool hasArmySupplyDemand = false)
        => new(
            SimulationTimeSeconds: 20f,
            Hunger: 10f,
            Stamina: 80f,
            HomeWood: 20,
            HomeStone: 10,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 12,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: health,
            Strength: strength,
            Defense: defense,
            NearbyPredators: nearbyPredators,
            DirectThreatScore: directThreatScore,
            HasImmediateThreat: hasImmediateThreat,
            IsSupplyCarrier: isSupplyCarrier,
            HasColonySupplyCarrier: hasColonySupplyCarrier,
            CanAssignSupplyCarrier: canAssignSupplyCarrier,
            SupplyCarrierNeedsRefill: supplyCarrierNeedsRefill,
            SupplyCarrierCanRefill: supplyCarrierCanRefill,
            SupplyCarrierCanDeliver: supplyCarrierCanDeliver,
            SupplyCarrierSourceValid: supplyCarrierSourceValid,
            ArmySupplyRatio: armySupplyRatio,
            HasArmySupplyDemand: hasArmySupplyDemand);

    private static NpcAiContext CreateArmyForageContext(
        bool hasArmyForageDemand = true,
        bool hasArmySupplyDemand = false,
        bool canAssignSupplyCarrier = false,
        bool canForageArmySupply = true,
        bool armyForageSourceAvailable = true,
        bool armyForageSourceInRange = true,
        bool armyForageConsumerCapRemaining = true,
        bool armyForageRationPoolHasCapacity = true,
        float armySupplyRatio = 0.25f,
        float simulationTimeSeconds = 21f,
        float hunger = 10f,
        float stamina = 80f,
        float health = 100f,
        int strength = 10,
        int defense = 10,
        int nearbyPredators = 0,
        float directThreatScore = 0f,
        bool hasImmediateThreat = false,
        bool isRouting = false,
        float biasFarming = 0f,
        float biasGathering = 0f)
        => new(
            SimulationTimeSeconds: simulationTimeSeconds,
            Hunger: hunger,
            Stamina: stamina,
            HomeWood: 20,
            HomeStone: 10,
            HomeIron: 0,
            HomeGold: 0,
            HomeFood: 12,
            HomeHouseCount: 2,
            HouseWoodCost: 50,
            ColonyPopulation: 8,
            HouseCapacity: 5,
            StoneBuildingsEnabled: false,
            CanBuildWithStone: false,
            HouseStoneCost: 100,
            Health: health,
            Strength: strength,
            Defense: defense,
            NearbyPredators: nearbyPredators,
            BiasFarming: biasFarming,
            BiasGathering: biasGathering,
            DirectThreatScore: directThreatScore,
            HasImmediateThreat: hasImmediateThreat,
            IsRouting: isRouting,
            CanAssignSupplyCarrier: canAssignSupplyCarrier,
            ArmySupplyRatio: armySupplyRatio,
            HasArmySupplyDemand: hasArmySupplyDemand,
            HasArmyForageDemand: hasArmyForageDemand,
            CanForageArmySupply: canForageArmySupply,
            ArmyForageSourceAvailable: armyForageSourceAvailable,
            ArmyForageSourceInRange: armyForageSourceInRange,
            ArmyForageConsumerCapRemaining: armyForageConsumerCapRemaining,
            ArmyForageRationPoolHasCapacity: armyForageRationPoolHasCapacity);

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
