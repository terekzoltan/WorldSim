using System;

namespace WorldSim.Simulation.Ecology;

public readonly record struct HerbivoreLifecycleStep(
    float Energy,
    float Age,
    float StarvationPressure,
    float ReproductionCooldown,
    float MigrationPressure,
    bool Starved);

public readonly record struct PredatorLifecycleStep(
    float Energy,
    float Age,
    float StarvationPressure,
    float ReproductionCooldown,
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

    public const float PredatorInitialEnergy = 100f;
    public const float PredatorMaxEnergy = 120f;
    public const float PredatorEnergyDrainPerSecond = 1f;
    public const float PredatorCaptureEnergyGain = 18f;
    public const float PredatorHumanHarassEnergyGain = 4f;
    public const float PredatorStarvationEnergyThreshold = 1f;
    public const float PredatorStarvationWindowSeconds = 2f;
    public const float PredatorReproductionEnergyThreshold = 100f;
    public const float PredatorReproductionEnergyCost = 35f;
    public const float PredatorReproductionCooldownSeconds = 30f;
    public const float PredatorMaturitySeconds = 2f;
    public const float PredatorAgeGainPerSecond = 0.1f;

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

    public static PredatorLifecycleStep TickPredator(
        float energy,
        float age,
        float starvationPressure,
        float reproductionCooldown,
        float dt)
    {
        dt = Math.Max(0f, dt);
        var nextEnergy = Math.Clamp(energy - dt * PredatorEnergyDrainPerSecond, 0f, PredatorMaxEnergy);
        var nextStarvation = nextEnergy <= PredatorStarvationEnergyThreshold
            ? starvationPressure + dt
            : Math.Max(0f, starvationPressure - dt);
        var nextCooldown = Math.Max(0f, reproductionCooldown - dt);

        return new PredatorLifecycleStep(
            nextEnergy,
            age + dt * PredatorAgeGainPerSecond,
            nextStarvation,
            nextCooldown,
            nextStarvation >= PredatorStarvationWindowSeconds);
    }

    public static float ApplyPredatorCaptureGain(float energy)
        => Math.Clamp(energy + PredatorCaptureEnergyGain, 0f, PredatorMaxEnergy);

    public static float ApplyPredatorHumanHarassGain(float energy)
        => Math.Clamp(energy + PredatorHumanHarassEnergyGain, 0f, PredatorMaxEnergy);

    public static bool CanPredatorReproduce(float energy, float age, float reproductionCooldown)
        => energy >= PredatorReproductionEnergyThreshold
           && age >= PredatorMaturitySeconds
           && reproductionCooldown <= 0f;

    public static float ApplyPredatorReproductionCost(float energy)
        => Math.Clamp(energy - PredatorReproductionEnergyCost, 0f, PredatorMaxEnergy);
}
