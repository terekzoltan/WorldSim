using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Ecology;

public sealed record EcologyRegionBaseState(
    int RegionId,
    int LandTileCount,
    int WaterTileCount,
    float PlantBiomassTotal,
    float PlantCapacityTotal,
    float CarryingCapacity,
    float OvergrazingPressure);

public sealed record EcologyRegionSnapshot(
    int RegionId,
    int LandTileCount,
    int WaterTileCount,
    float PlantBiomassTotal,
    float PlantCapacityTotal,
    int HerbivoreCount,
    int PredatorCount,
    float CarryingCapacity,
    float OvergrazingPressure,
    float SeasonModifier,
    float DroughtModifier);

public sealed class EcologyRegionCache
{
    readonly List<EcologyRegionBaseState> _regions;
    readonly Dictionary<int, int> _regionIndexById;

    public EcologyRegionCache(IReadOnlyList<EcologyRegionBaseState> regions)
    {
        _regions = regions.OrderBy(region => region.RegionId).ToList();
        _regionIndexById = _regions
            .Select((region, index) => new { region.RegionId, Index = index })
            .ToDictionary(entry => entry.RegionId, entry => entry.Index);
    }

    public int Count => _regions.Count;

    public void ApplyTileDelta(int regionId, float plantBiomassDelta, float overgrazingPressureDelta)
    {
        if (!_regionIndexById.TryGetValue(regionId, out var index))
            return;

        var region = _regions[index];
        var tileCount = Math.Max(1, region.LandTileCount + region.WaterTileCount);
        var biomassTotal = Math.Clamp(region.PlantBiomassTotal + plantBiomassDelta, 0f, region.PlantCapacityTotal);
        var pressure = Math.Clamp(region.OvergrazingPressure + overgrazingPressureDelta / tileCount, 0f, 1f);
        _regions[index] = region with
        {
            PlantBiomassTotal = biomassTotal,
            OvergrazingPressure = pressure
        };
    }

    public float GetOvergrazingPressure(int regionId)
        => _regionIndexById.TryGetValue(regionId, out var index) ? _regions[index].OvergrazingPressure : 0f;

    public IReadOnlyList<EcologyRegionSnapshot> BuildSnapshots(
        IEnumerable<Animal> animals,
        Season season,
        bool isDroughtActive,
        int regionSize,
        int regionColumns)
    {
        var herbivoresByRegion = new Dictionary<int, int>();
        var predatorsByRegion = new Dictionary<int, int>();

        foreach (var animal in animals)
        {
            if (!animal.IsAlive)
                continue;

            var regionId = EcologyState.GetRegionId(animal.Pos.x, animal.Pos.y, regionSize, regionColumns);
            if (animal is Herbivore)
                herbivoresByRegion[regionId] = herbivoresByRegion.GetValueOrDefault(regionId) + 1;
            else if (animal is Predator)
                predatorsByRegion[regionId] = predatorsByRegion.GetValueOrDefault(regionId) + 1;
        }

        var seasonModifier = GetSeasonModifier(season);
        var droughtModifier = isDroughtActive ? 0.75f : 1f;

        return _regions
            .Select(region => new EcologyRegionSnapshot(
                region.RegionId,
                region.LandTileCount,
                region.WaterTileCount,
                region.PlantBiomassTotal,
                region.PlantCapacityTotal,
                herbivoresByRegion.GetValueOrDefault(region.RegionId),
                predatorsByRegion.GetValueOrDefault(region.RegionId),
                region.CarryingCapacity,
                region.OvergrazingPressure,
                seasonModifier,
                droughtModifier))
            .ToList();
    }

    public static float GetSeasonModifier(Season season)
        => season switch
        {
            Season.Spring => 1.1f,
            Season.Summer => 1.0f,
            Season.Autumn => 0.9f,
            Season.Winter => 0.75f,
            _ => 1f
        };
}
