using System;

namespace WorldSim.Simulation.Ecology;

public sealed record EcologyPlantCounters(
    int EcologyRegionCacheRebuilds,
    int PlantGrowthTicks,
    int OvergrazedRegionTicks,
    int DroughtPlantPenaltyTicks)
{
    public static EcologyPlantCounters Empty { get; } = new(0, 0, 0, 0);
}

public sealed record EcologySupplyCounters(
    int PlantFoodProduced,
    int MeatFoodProduced,
    int PlantFoodConsumedByAnimals,
    int MeatFromHunt,
    int SupplyBridgeSkippedByNoBiomass)
{
    public static EcologySupplyCounters Empty { get; } = new(0, 0, 0, 0, 0);
}

public readonly record struct PlantBiomassUpdate(
    EcologyTileState Tile,
    float BiomassDelta,
    float OvergrazingPressureDelta,
    bool GrowthApplied,
    bool DroughtReducedGrowth);

public static class PlantBiomassModel
{
    const float HarvestBiomassCostPerUnit = 0.2f;
    const float HarvestPressurePerUnit = 0.04f;
    const float DepletionPressure = 0.18f;
    const float BaseGrowthPerSecond = 0.08f;
    const float PressureRecoveryPerSecond = 0.015f;

    public static PlantBiomassUpdate ApplyHarvest(EcologyTileState tile, int harvestedAmount, bool depletedFoodNode)
    {
        if (harvestedAmount <= 0 || tile.PlantCapacity <= 0f)
            return NoChange(tile);

        var biomassBefore = tile.PlantBiomass;
        var pressureBefore = tile.OvergrazingPressure;
        var biomassCost = harvestedAmount * HarvestBiomassCostPerUnit;
        var biomassAfter = Math.Clamp(biomassBefore - biomassCost, 0f, tile.PlantCapacity);
        var pressureGain = harvestedAmount * HarvestPressurePerUnit + (depletedFoodNode ? DepletionPressure : 0f);
        var pressureAfter = Math.Clamp(pressureBefore + pressureGain, 0f, 1f);
        var updated = tile with
        {
            PlantBiomass = biomassAfter,
            OvergrazingPressure = pressureAfter
        };

        return new PlantBiomassUpdate(
            updated,
            biomassAfter - biomassBefore,
            pressureAfter - pressureBefore,
            GrowthApplied: false,
            DroughtReducedGrowth: false);
    }

    public static PlantBiomassUpdate ApplyGrowth(
        EcologyTileState tile,
        float dt,
        float seasonModifier,
        float droughtModifier)
    {
        if (dt <= 0f || tile.PlantCapacity <= 0f)
            return NoChange(tile);

        var biomassBefore = tile.PlantBiomass;
        var pressureBefore = tile.OvergrazingPressure;
        var pressureModifier = Math.Clamp(1f - pressureBefore * 0.5f, 0.1f, 1f);
        var unclampedDroughtModifier = Math.Clamp(droughtModifier, 0f, 1f);
        var growth = dt * BaseGrowthPerSecond * Math.Max(0f, seasonModifier) * unclampedDroughtModifier * pressureModifier;
        var biomassAfter = Math.Clamp(biomassBefore + growth, 0f, tile.PlantCapacity);
        var pressureAfter = Math.Clamp(pressureBefore - dt * PressureRecoveryPerSecond, 0f, 1f);
        var updated = tile with
        {
            PlantBiomass = biomassAfter,
            OvergrazingPressure = pressureAfter
        };

        return new PlantBiomassUpdate(
            updated,
            biomassAfter - biomassBefore,
            pressureAfter - pressureBefore,
            GrowthApplied: biomassAfter > biomassBefore || pressureAfter < pressureBefore,
            DroughtReducedGrowth: droughtModifier < 1f && biomassAfter > biomassBefore);
    }

    static PlantBiomassUpdate NoChange(EcologyTileState tile)
        => new(tile, 0f, 0f, GrowthApplied: false, DroughtReducedGrowth: false);
}
