using System;
using System.Collections.Generic;

namespace WorldSim.Simulation;

public static class GoalLibrary
{
    public static List<Goal> CreateDefaultGoals()
    {
        var list = new List<Goal>();

        var gWood = new Goal("GatherWood")
        {
            Cooldown = TimeSpan.FromSeconds(2)
        };
        gWood.Considerations.Add(new LowWoodStockConsideration(threshold: 6));
        gWood.Considerations.Add(new HungerConsideration()); // hunger pushes toward gathering
        list.Add(gWood);

        var gStone = new Goal("GatherStone")
        {
            Cooldown = TimeSpan.FromSeconds(3)
        };
        gStone.Considerations.Add(new HungerConsideration());
        list.Add(gStone);

        var gBuild = new Goal("BuildHouse")
        {
            Cooldown = TimeSpan.FromSeconds(5)
        };
        gBuild.Considerations.Add(new BuildHouseFeasibleConsideration());
        gBuild.Considerations.Add(new Inverted(new HungerConsideration())); // less likely if hungry
        list.Add(gBuild);

        return list;
    }
}

// Utility: invert any consideration 1-v ? v
public sealed class Inverted : Consideration
{
    private readonly Consideration _inner;
    public Inverted(Consideration inner) => _inner = inner;

    public override float Evaluate(Person p, World w)
    {
        float v = _inner.Evaluate(p, w);
        return 1f - v;
    }
}