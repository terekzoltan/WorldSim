using System.Collections.Generic;

namespace WorldSim.AI;

public static class GoalLibrary
{
    public static List<Goal> CreateDefaultGoals()
    {
        var goals = new List<Goal>();

        var gatherWood = new Goal("GatherWood")
        {
            CooldownSeconds = 2f
        };
        gatherWood.Considerations.Add(new LowWoodStockConsideration(threshold: 6));
        gatherWood.Considerations.Add(new HungerConsideration());
        goals.Add(gatherWood);

        var gatherStone = new Goal("GatherStone")
        {
            CooldownSeconds = 3f
        };
        gatherStone.Considerations.Add(new HungerConsideration());
        goals.Add(gatherStone);

        var buildHouse = new Goal("BuildHouse")
        {
            CooldownSeconds = 5f
        };
        buildHouse.Considerations.Add(new BuildHouseFeasibleConsideration());
        buildHouse.Considerations.Add(new InvertedConsideration(new HungerConsideration()));
        goals.Add(buildHouse);

        return goals;
    }
}
