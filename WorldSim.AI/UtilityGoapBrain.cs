using System.Collections.Generic;

namespace WorldSim.AI;

public sealed class UtilityGoapBrain : INpcDecisionBrain
{
    private readonly GoalSelector _goalSelector = new();
    private readonly IPlanner _planner;
    private readonly IReadOnlyList<Goal> _goals;

    public UtilityGoapBrain(IPlanner planner, IReadOnlyList<Goal> goals)
    {
        _planner = planner;
        _goals = goals;
    }

    public NpcCommand Think(in NpcAiContext context)
    {
        _goalSelector.SelectGoal(_goals, _planner, context);
        return _planner.GetNextCommand(context);
    }
}
