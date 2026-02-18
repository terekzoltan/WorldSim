using System;

namespace WorldSim.AI;

public sealed class UtilityEvaluator
{
    public float Evaluate(Goal goal, in NpcAiContext context)
    {
        var score = 1f;
        foreach (var consideration in goal.Considerations)
        {
            var value = Math.Clamp(consideration.Evaluate(context), 0f, 1f);
            score *= value;
            if (score <= 0f)
                break;
        }

        return score;
    }
}
