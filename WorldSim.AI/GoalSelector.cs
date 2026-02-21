using System.Collections.Generic;

namespace WorldSim.AI;

public sealed class GoalSelector
{
    private readonly UtilityEvaluator _evaluator = new();

    public GoalSelectionResult SelectGoal(IEnumerable<Goal> goals, IPlanner planner, in NpcAiContext context)
    {
        Goal? best = null;
        var bestScore = float.MinValue;
        var scores = new List<GoalScoreEntry>();

        foreach (var goal in goals)
        {
            var onCooldown = goal.IsOnCooldown(context.SimulationTimeSeconds);
            var score = onCooldown ? 0f : _evaluator.Evaluate(goal, context);
            scores.Add(new GoalScoreEntry(goal.Name, score, onCooldown));

            if (onCooldown)
                continue;

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

        return new GoalSelectionResult(best, scores);
    }
}

public sealed record GoalSelectionResult(Goal? SelectedGoal, IReadOnlyList<GoalScoreEntry> Scores);
