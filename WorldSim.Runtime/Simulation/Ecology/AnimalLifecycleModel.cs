using System;

namespace WorldSim.Simulation.Ecology;

public readonly record struct HerbivoreLifecycleStep(
    float Energy,
    float Age,
    float StarvationPressure,
    float ReproductionCooldown,
    float MigrationPressure,
    bool Starved);

public static class AnimalLifecycleModel
{
    public const float HerbivoreInitialEnergy = 80f;
    public const float HerbivoreMaxEnergy = 120f;
    public const float HerbivoreEnergyDrainPerSecond = 1.5f;
    public const float HerbivoreGrazingEnergyGain = 16f;
    public const float HerbivoreStarvationEnergyThreshold = 1f;
    public const float HerbivoreStarvationWindowSeconds = 2f;
    public const float HerbivoreReproductionEnergyThreshold = 95f;
    public const float HerbivoreReproductionEnergyCost = 28f;
    public const float HerbivoreReproductionCooldownSeconds = 18f;
    public const float HerbivoreMaturitySeconds = 12f;
    public const float HerbivoreMigrationEnergyThreshold = 35f;
    public const float HerbivoreMigrationPressureThreshold = 1f;

    public static HerbivoreLifecycleStep TickHerbivore(
        float energy,
        float age,
        float starvationPressure,
        float reproductionCooldown,
        float migrationPressure,
        float dt)
    {
        dt = Math.Max(0f, dt);
        var nextEnergy = Math.Clamp(energy - dt * HerbivoreEnergyDrainPerSecond, 0f, HerbivoreMaxEnergy);
        var nextStarvation = nextEnergy <= HerbivoreStarvationEnergyThreshold
            ? starvationPressure + dt
            : Math.Max(0f, starvationPressure - dt);
        var nextCooldown = Math.Max(0f, reproductionCooldown - dt);
        var nextMigration = nextEnergy <= HerbivoreMigrationEnergyThreshold
            ? migrationPressure + dt
            : Math.Max(0f, migrationPressure - dt);

        return new HerbivoreLifecycleStep(
            nextEnergy,
            age + dt,
            nextStarvation,
            nextCooldown,
            nextMigration,
            nextStarvation >= HerbivoreStarvationWindowSeconds);
    }

    public static float ApplyHerbivoreGrazing(float energy)
        => Math.Clamp(energy + HerbivoreGrazingEnergyGain, 0f, HerbivoreMaxEnergy);

    public static bool CanHerbivoreReproduce(float energy, float age, float reproductionCooldown)
        => energy >= HerbivoreReproductionEnergyThreshold
           && age >= HerbivoreMaturitySeconds
           && reproductionCooldown <= 0f;

    public static float ApplyHerbivoreReproductionCost(float energy)
        => Math.Clamp(energy - HerbivoreReproductionEnergyCost, 0f, HerbivoreMaxEnergy);
}
