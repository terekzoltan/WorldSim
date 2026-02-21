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

        var secureFood = new Goal("SecureFood")
        {
            CooldownSeconds = 1.5f
        };
        secureFood.Considerations.Add(new LowFoodStockConsideration());
        secureFood.Considerations.Add(new HungerConsideration());
        goals.Add(secureFood);

        var recoverStamina = new Goal("RecoverStamina")
        {
            CooldownSeconds = 1f
        };
        recoverStamina.Considerations.Add(new StaminaDeficitConsideration());
        recoverStamina.Considerations.Add(new InvertedConsideration(new HungerConsideration()));
        goals.Add(recoverStamina);

        var buildHouse = new Goal("BuildHouse")
        {
            CooldownSeconds = 5f
        };
        buildHouse.Considerations.Add(new BuildHouseFeasibleConsideration());
        buildHouse.Considerations.Add(new InvertedConsideration(new HungerConsideration()));
        goals.Add(buildHouse);

        var expandHousing = new Goal("ExpandHousing")
        {
            CooldownSeconds = 4f
        };
        expandHousing.Considerations.Add(new HousingPressureConsideration());
        expandHousing.Considerations.Add(new BuildHouseFeasibleConsideration());
        goals.Add(expandHousing);

        var stabilizeResources = new Goal("StabilizeResources")
        {
            CooldownSeconds = 2.5f
        };
        stabilizeResources.Considerations.Add(new LowStoneStockConsideration(threshold: 8));
        stabilizeResources.Considerations.Add(new InvertedConsideration(new LowFoodStockConsideration()));
        goals.Add(stabilizeResources);

        return goals;
    }
}
