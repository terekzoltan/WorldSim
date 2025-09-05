using System;

namespace WorldSim.Simulation;

public class UtilityEvaluator
{
    public float Evaluate(Goal goal, Person p, World w)
    {
        float score = 1f;
        foreach (var c in goal.Considerations)
        {
            float v = Math.Clamp(c.Evaluate(p, w), 0f, 1f);
            score *= v; // multiplicative utility
            if (score <= 0f) break;
        }
        return score;
    }
}