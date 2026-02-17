using System.Collections.Generic;

namespace WorldSim.Simulation;

public class GoalSelector
{
    readonly UtilityEvaluator _evaluator = new();

    public Goal? SelectGoal(IEnumerable<Goal> goals, IPlanner planner, Person p, World w)
    {
        Goal? best = null;
        float bestScore = float.MinValue;

        foreach (var goal in goals)
        {
            if (goal.IsOnCooldown) continue;

            float score = _evaluator.Evaluate(goal, p, w);
            if (score > bestScore)
            {
                bestScore = score;
                best = goal;
            }
        }

        if (best != null)
        {
            best.MarkSelected();
            planner.SetGoal(best);
        }

        return best;
    }
}