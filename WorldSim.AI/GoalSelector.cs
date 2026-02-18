using System.Collections.Generic;

namespace WorldSim.AI;

public sealed class GoalSelector
{
    private readonly UtilityEvaluator _evaluator = new();

    public Goal? SelectGoal(IEnumerable<Goal> goals, IPlanner planner, in NpcAiContext context)
    {
        Goal? best = null;
        var bestScore = float.MinValue;

        foreach (var goal in goals)
        {
            if (goal.IsOnCooldown(context.SimulationTimeSeconds))
                continue;

            var score = _evaluator.Evaluate(goal, context);
            if (score > bestScore)
            {
                bestScore = score;
                best = goal;
            }
        }

        if (best != null)
        {
            best.MarkSelected(context.SimulationTimeSeconds);
            planner.SetGoal(best);
        }

        return best;
    }
}
