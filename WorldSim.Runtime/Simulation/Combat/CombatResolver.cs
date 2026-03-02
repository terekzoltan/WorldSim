using System;

namespace WorldSim.Simulation.Combat;

public static class CombatResolver
{
    public static float RollDamage(Random rng, float baseDamage, int strength, int defense)
    {
        float factor = CombatConstants.RandomFactorMin + (float)rng.NextDouble() * (CombatConstants.RandomFactorMax - CombatConstants.RandomFactorMin);
        return CalculateDamage(baseDamage, strength, defense, factor);
    }

    public static float CalculateDamage(float baseDamage, int strength, int defense, float randomFactor)
    {
        float clampedFactor = Math.Clamp(randomFactor, CombatConstants.RandomFactorMin, CombatConstants.RandomFactorMax);
        float rawDamage = baseDamage * (1f + (Math.Max(0, strength) / 20f)) * clampedFactor;
        float defensePct = Math.Clamp(Math.Max(0, defense) / 100f, 0f, 0.75f);
        float effectiveDamage = rawDamage * (1f - defensePct);
        return Math.Max(1f, effectiveDamage);
    }
}
