using System;

namespace WorldSim.AI;

public static class ThreatDecisionPolicy
{
    private const float BaseLowHealthThreshold = 45f;
    private const float LowEquipmentHighThreatThreshold = 0.55f;
    private const float LowEquipmentPowerPenaltyMultiplier = 0.7f;

    public static bool IsPeacefulZeroSignal(in NpcAiContext context)
    {
        if (Math.Max(0, context.NearbyPredators) > 0)
            return false;
        if (Math.Max(0, context.NearbyHostilePeople) > 0)
            return false;
        if (Math.Max(0, context.NearbyEnemyCount) > 0)
            return false;
        if (context.IsWarStance || context.IsHostileStance)
            return false;
        if (context.IsContestedTile || context.HasContestedTilesNearby)
            return false;
        if (context.HostileProximityScore > 0.08f)
            return false;

        return context.LocalThreatScore <= 0.1f;
    }

    public static bool ShouldPrioritizeDefense(in NpcAiContext context)
    {
        if (IsPeacefulZeroSignal(context))
            return false;

        if (Math.Max(0, context.NearbyPredators) > 0)
            return true;

        if (context.LocalThreatScore >= 0.25f)
            return true;

        if ((context.IsWarStance || context.IsHostileStance) && (context.IsContestedTile || context.HasContestedTilesNearby))
            return true;

        return false;
    }

    public static bool ShouldFight(in NpcAiContext context)
    {
        if (!ShouldPrioritizeDefense(context))
            return false;

        var hasFactionThreat =
            context.NearbyEnemyCount > 0 ||
            context.NearbyHostilePeople > 0 ||
            context.IsWarStance ||
            context.IsHostileStance ||
            context.IsContestedTile ||
            context.HasContestedTilesNearby;
        if (hasFactionThreat && !context.IsWarriorRole)
            return false;

        if (context.Health < BaseLowHealthThreshold)
            return false;

        bool lowEquipment = context.HomeWeaponLevel == 0 && context.HomeArmorLevel == 0;

        var power = context.Strength + (context.Defense / 2f);
        if (lowEquipment && context.LocalThreatScore >= LowEquipmentHighThreatThreshold)
            power *= LowEquipmentPowerPenaltyMultiplier;
        var threatLoad =
            (6f * Math.Max(0, context.NearbyPredators)) +
            (8f * Math.Max(0, context.NearbyHostilePeople)) +
            (10f * Math.Max(0, context.NearbyEnemyCount));

        threatLoad *= 1f + Math.Clamp(context.HostileProximityScore * 0.35f, 0f, 0.35f);
        if (context.IsContestedTile)
            threatLoad *= 0.92f;
        else if (context.HasContestedTilesNearby)
            threatLoad *= 0.97f;

        return power >= threatLoad;
    }
}
