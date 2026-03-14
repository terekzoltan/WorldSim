using System;

namespace WorldSim.Simulation.Combat;

public static class GroupCombatResolver
{
    public static (float Attack, float Defense) GetFormationModifiers(Formation formation)
        => formation switch
        {
            Formation.Wedge => (1.20f, 0.90f),
            Formation.DefensiveCircle => (0.88f, 1.22f),
            Formation.Skirmish => (1.05f, 1.00f),
            _ => (1.00f, 1.00f)
        };

    public static float ComputeGroupAttackScore(float basePower, Formation formation, int weaponLevel)
    {
        var (attack, _) = GetFormationModifiers(formation);
        var weaponMultiplier = 1f + (Math.Max(0, weaponLevel) * 0.10f);
        return Math.Max(1f, basePower * attack * weaponMultiplier);
    }

    public static float ComputeGroupDefenseScore(float basePower, Formation formation, int armorLevel)
    {
        var (_, defense) = GetFormationModifiers(formation);
        var armorMultiplier = 1f + (Math.Max(0, armorLevel) * 0.08f);
        return Math.Max(1f, basePower * defense * armorMultiplier);
    }

    public static float ComputePerHitDamage(Random rng, float attackScore, float defenseScore, int attackerCount, int defenderCount)
    {
        var attackerScale = Math.Clamp(attackerCount / (float)Math.Max(1, defenderCount), 0.5f, 2.2f);
        var baseDamage = Math.Max(2f, (attackScore / Math.Max(1, attackerCount)) * 0.40f * attackerScale);
        var strength = Math.Max(1, (int)MathF.Round(attackScore / Math.Max(1, attackerCount)));
        var defense = Math.Max(0, (int)MathF.Round(defenseScore / Math.Max(1, defenderCount)));
        return CombatResolver.RollDamage(rng, baseDamage, strength, defense);
    }
}
