using System;

namespace WorldSim.AI;

public static class GoalBiasCategories
{
    public const string Farming = "farming";
    public const string Gathering = "gathering";
    public const string Building = "building";
    public const string Crafting = "crafting";
    public const string Rest = "rest";
    public const string Social = "social";
    public const string Military = "military";

    public static float GetBiasForGoal(string goalName, in NpcAiContext context)
    {
        if (string.IsNullOrWhiteSpace(goalName))
            return 0f;

        return goalName switch
        {
            "DefendSelf" => context.BiasMilitary,
            "GatherWood" => context.BiasGathering,
            "GatherStone" => context.BiasGathering,
            "StabilizeResources" => Math.Max(context.BiasGathering, context.BiasCrafting),
            "SecureFood" => Math.Max(context.BiasFarming, context.BiasGathering),
            "BuildHouse" => context.BiasBuilding,
            "ExpandHousing" => context.BiasBuilding,
            "RecoverStamina" => context.BiasRest,
            _ => 0f
        };
    }
}
