using System;

namespace WorldSim.AI;

public static class ThreatDecisionPolicy
{
    private const float BaseLowHealthThreshold = 45f;
    private const float LowEquipmentHighThreatThreshold = 0.55f;
    private const float LowEquipmentPowerPenaltyMultiplier = 0.7f;
    private const int CommanderGroupMinSize = 3;
    private const float CommanderRetreatMoraleThreshold = 32f;
    private const float CommanderCautionMoraleThreshold = 40f;
    private const float CommanderPressMoraleThreshold = 62f;
    private const int RoutingReengageThresholdTicks = 2;
    private const int BackoffReengageThresholdTicks = 1;
    private const float SiegeRetreatMoraleThreshold = 36f;
    private const float SiegePressureRetreatThreshold = 0.75f;

    public static bool IsCommanderCombatContext(in NpcAiContext context)
        => context.IsCommander && context.ActiveCombatGroupSize >= CommanderGroupMinSize;

    public static bool HasImmediateThreat(in NpcAiContext context)
        => context.HasImmediateThreat
            || Math.Max(0, context.NearbyPredators) > 0
            || Math.Max(0, context.NearbyHostilePeople) > 0
            || Math.Max(0, context.NearbyEnemyCount) > 0;

    public static bool HasImmediateFactionThreat(in NpcAiContext context)
        => context.HasImmediateFactionThreat
            || Math.Max(0, context.NearbyHostilePeople) > 0
            || Math.Max(0, context.NearbyEnemyCount) > 0;

    public static bool HasAmbientWarPressure(in NpcAiContext context)
        => context.AmbientThreatScore > 0f
            || context.IsWarStance
            || context.IsHostileStance
            || context.IsContestedTile
            || context.HasContestedTilesNearby;

    public static bool ShouldCommanderInitiateRetreat(in NpcAiContext context)
    {
        if (!IsCommanderCombatContext(context))
            return false;

        if (!HasImmediateThreat(context) && !context.IsNearActiveSiege)
            return false;

        if (context.ActiveGroupAverageMorale <= CommanderRetreatMoraleThreshold)
            return true;

        if (context.ActiveGroupAverageMorale <= CommanderCautionMoraleThreshold && context.LocalThreatScore >= 0.55f)
            return true;

        if (context.Health <= 55f && context.LocalThreatScore >= 0.5f)
            return true;

        return false;
    }

    public static bool ShouldCommanderPressAdvantage(in NpcAiContext context)
    {
        if (!IsCommanderCombatContext(context))
            return false;

        if (context.ActiveGroupAverageMorale >= CommanderPressMoraleThreshold
            && context.CommanderMoraleStabilityBonus >= 0.2f
            && context.LocalThreatScore <= 0.75f)
            return true;

        if (context.ActiveGroupAverageMorale >= 55f
            && context.ActiveCombatGroupSize >= 4
            && context.LocalThreatScore <= 0.6f)
            return true;

        return false;
    }

    public static bool ShouldSuppressReengage(in NpcAiContext context)
    {
        if (context.IsRouting)
            return true;
        if (context.RoutingTicksRemaining > RoutingReengageThresholdTicks)
            return true;
        if (context.BackoffTicksRemaining > BackoffReengageThresholdTicks
            && (HasImmediateThreat(context) || context.IsNearActiveSiege))
            return true;

        if (context.ActiveCombatGroupSize >= 3 && (HasImmediateThreat(context) || context.IsNearActiveSiege))
        {
            if (context.ActiveGroupAverageMorale < 35f)
                return true;
            if (context.ActiveGroupAverageMorale < 42f && context.LocalThreatScore >= 0.55f)
                return true;
        }

        return false;
    }

    public static bool ShouldPrioritizeSiegeTargeting(in NpcAiContext context)
    {
        if (!context.IsNearActiveSiege)
            return false;
        if (!context.IsSiegeAttackerRole)
            return false;
        if (context.NearbyEnemyDefensiveStructures <= 0)
            return false;

        if (ShouldAvoidTowerTunnel(context))
            return true;

        if (context.HasRecentBreachNearby && context.NearbyEnemyTowerCount > 0)
            return true;

        return context.NearbyEnemyTowerCount > context.NearbyEnemyWallCount;
    }

    public static bool ShouldAvoidTowerTunnel(in NpcAiContext context)
    {
        if (!context.IsNearActiveSiege || !context.IsSiegeAttackerRole)
            return false;
        if (context.NearbyEnemyTowerCount <= 0)
            return false;

        var towerPressure = context.NearbyEnemyTowerCount >= Math.Max(1, context.NearbyEnemyWallCount);
        var moraleRisk = context.ActiveCombatGroupSize >= 3 && context.ActiveGroupAverageMorale < 58f;
        var pressureRisk = context.NearbySiegePressure >= 0.55f || context.LocalThreatScore >= 0.6f;

        return towerPressure && (moraleRisk || pressureRisk);
    }

    public static bool ShouldRetreatFromSiege(in NpcAiContext context)
    {
        if (!context.IsNearActiveSiege)
            return false;

        if (context.IsSiegeAttackerRole)
        {
            if (context.ActiveCombatGroupSize >= 3 && context.ActiveGroupAverageMorale <= SiegeRetreatMoraleThreshold)
                return true;
            if (context.NearbySiegePressure >= SiegePressureRetreatThreshold && context.ActiveGroupAverageMorale < 52f)
                return true;
            if (context.BackoffTicksRemaining > 0 && context.NearbySiegePressure >= 0.6f)
                return true;
            return false;
        }

        if (context.IsSiegeDefenderRole)
        {
            if (context.ActiveCombatGroupSize >= 3 && context.ActiveGroupAverageMorale < 30f)
                return true;
            if (context.Health < 42f && context.LocalThreatScore >= 0.55f)
                return true;
        }

        return false;
    }

    public static bool ShouldSortie(in NpcAiContext context)
    {
        if (!context.IsSiegeDefenderRole)
            return false;
        if (!context.IsNearActiveSiege)
            return false;
        if (context.IsRouting || context.RoutingTicksRemaining > 0)
            return false;
        if (context.ActiveCombatGroupSize < 3)
            return false;
        if (context.ActiveGroupAverageMorale < 58f)
            return false;
        if (context.CommanderMoraleStabilityBonus < 0.15f)
            return false;

        var attackerWeak = context.NearbyEnemyCount <= 2 && context.NearbySiegePressure <= 0.5f;
        var defenderHasCover = context.NearbyFriendlyTowerCount >= 1 || context.NearbyFriendlyWallCount >= 2;
        return attackerWeak && defenderHasCover;
    }

    public static bool IsPeacefulZeroSignal(in NpcAiContext context)
    {
        if (HasImmediateThreat(context))
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

        if (ShouldRetreatFromSiege(context) || ShouldSortie(context))
            return true;

        if (HasImmediateThreat(context))
            return true;

        return false;
    }

    public static bool ShouldFight(in NpcAiContext context)
    {
        if (!ShouldPrioritizeDefense(context))
            return false;

        if (ShouldRetreatFromSiege(context))
            return false;

        if (ShouldSuppressReengage(context) && !ShouldCommanderPressAdvantage(context))
            return false;

        if (ShouldCommanderInitiateRetreat(context))
            return false;

        if (HasImmediateFactionThreat(context) && !context.IsWarriorRole)
            return false;

        if (context.Health < BaseLowHealthThreshold)
            return false;

        bool lowEquipment = context.HomeWeaponLevel == 0 && context.HomeArmorLevel == 0;

        var power = context.Strength + (context.Defense / 2f);
        if (ShouldCommanderPressAdvantage(context))
            power *= 1.1f;
        if (lowEquipment && context.LocalThreatScore >= LowEquipmentHighThreatThreshold)
            power *= LowEquipmentPowerPenaltyMultiplier;

        if (IsCommanderCombatContext(context) && context.ActiveGroupAverageMorale < 38f)
            power *= 0.78f;

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
