using System;

namespace WorldSim.AI;

public sealed class SimplePlanner : IPlanner
{
    private Goal? _goal;
    private bool _goalChanged;
    public string Name => "Simple";

    public void SetGoal(Goal goal)
    {
        _goalChanged = _goal?.Name != goal.Name;
        _goal = goal;
    }

    public PlannerDecision GetNextCommand(in NpcAiContext context)
    {
        if (_goal == null)
            return new PlannerDecision(NpcCommand.Idle, 0, Array.Empty<NpcCommand>(), 0, "NoGoal", "SimpleRule");

        var command = _goal.Name switch
        {
            "DefendSelf" => SelectDefendSelfCommand(context),
            "BuildDefenses" => context.HomeWood >= 16 && context.HomeStone >= 6
                ? NpcCommand.BuildWatchtower
                : (context.HomeWood >= 8 ? NpcCommand.BuildWall : NpcCommand.GatherWood),
            "RaidBorder" => SelectRaidBorderCommand(context),
            "UnlockMilitaryTech" => ShouldUnlockMilitaryTech(context) ? NpcCommand.ResearchTech : NpcCommand.CraftTools,
            "GatherWood" => NpcCommand.GatherWood,
            "GatherStone" => NpcCommand.GatherStone,
            "SecureFood" => context.Hunger >= 68f && context.HomeFood > 0 ? NpcCommand.EatFood : NpcCommand.GatherFood,
            "RecoverStamina" => NpcCommand.Rest,
            "StabilizeResources" => NpcCommand.GatherStone,
            "ExpandHousing" => context.HomeWood >= context.HouseWoodCost ? NpcCommand.BuildHouse : NpcCommand.GatherWood,
            "BuildHouse" => context.HomeWood >= context.HouseWoodCost
                ? NpcCommand.BuildHouse
                : NpcCommand.GatherWood,
            _ => NpcCommand.Idle
        };

        if (command == NpcCommand.Idle)
            return new PlannerDecision(command, 0, Array.Empty<NpcCommand>(), 0, _goalChanged ? "GoalChanged" : "NoRule", "SimpleRule");

        var reason = _goalChanged ? "GoalChanged" : "RuleMatch";
        _goalChanged = false;
        return new PlannerDecision(command, 1, new[] { command }, 1, reason, "SimpleRule");
    }

    private static NpcCommand SelectDefendSelfCommand(in NpcAiContext context)
    {
        if (ThreatDecisionPolicy.ShouldSuppressReengage(context)
            && !ThreatDecisionPolicy.ShouldCommanderPressAdvantage(context))
            return NpcCommand.Flee;

        if (ThreatDecisionPolicy.ShouldCommanderInitiateRetreat(context))
            return NpcCommand.Flee;

        return ThreatDecisionPolicy.ShouldFight(context) ? NpcCommand.Fight : NpcCommand.Flee;
    }

    private static NpcCommand SelectRaidBorderCommand(in NpcAiContext context)
    {
        if (ThreatDecisionPolicy.ShouldSuppressReengage(context)
            && !ThreatDecisionPolicy.ShouldCommanderPressAdvantage(context))
            return NpcCommand.Flee;

        if (!context.IsWarriorRole)
            return NpcCommand.Flee;

        if (ThreatDecisionPolicy.ShouldCommanderInitiateRetreat(context))
            return NpcCommand.Flee;

        if (ThreatDecisionPolicy.ShouldCommanderPressAdvantage(context)
            && context.IsWarStance
            && (context.IsContestedTile || context.HasContestedTilesNearby))
            return NpcCommand.RaidBorder;

        if (context.NearbyEnemyCount > 0 && ThreatDecisionPolicy.ShouldFight(context))
            return NpcCommand.Fight;

        return context.IsWarStance || context.IsHostileStance
            ? NpcCommand.RaidBorder
            : NpcCommand.Flee;
    }

    private static bool ShouldUnlockMilitaryTech(in NpcAiContext context)
    {
        if (context.HomeMilitaryTechCount >= 3)
            return false;

        var minimumFoodReserve = Math.Max(4, context.ColonyPopulation / 2);
        if (context.HomeFood < minimumFoodReserve)
            return false;

        return context.IsWarStance || (context.IsHostileStance && context.LocalThreatScore >= 0.4f);
    }

}
