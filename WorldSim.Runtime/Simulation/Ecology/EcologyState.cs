using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Ecology;

public sealed record EcologyTileState(
    int X,
    int Y,
    int RegionId,
    float Fertility,
    float PlantCapacity,
    float PlantBiomass,
    float OvergrazingPressure);

public sealed record EcologyLifecycleCounters(
    int HerbivoreBirths,
    int PredatorBirths,
    int HerbivoreStarvations,
    int PredatorStarvations,
    int HerbivoreMigrations,
    int PredatorMigrations,
    int LandSafeSpawnFallbacks,
    int EmergencyRescues)
{
    public static EcologyLifecycleCounters Empty { get; } = new(
        HerbivoreBirths: 0,
        PredatorBirths: 0,
        HerbivoreStarvations: 0,
        PredatorStarvations: 0,
        HerbivoreMigrations: 0,
        PredatorMigrations: 0,
        LandSafeSpawnFallbacks: 0,
        EmergencyRescues: 0);
}

public sealed class EcologyState
{
    public const int DefaultRegionSize = 16;

    readonly EcologyTileState[,] _tiles;
    readonly EcologyRegionCache _regionCache;

    EcologyState(int width, int height, int regionSize, EcologyTileState[,] tiles, EcologyRegionCache regionCache)
    {
        Width = width;
        Height = height;
        RegionSize = regionSize;
        RegionColumns = Math.Max(1, (int)Math.Ceiling(width / (double)regionSize));
        _tiles = tiles;
        _regionCache = regionCache;
    }

    public int Width { get; }
    public int Height { get; }
    public int RegionSize { get; }
    public int RegionColumns { get; }
    public int RegionCount => _regionCache.Count;
    public EcologyLifecycleCounters LifecycleCounters { get; } = EcologyLifecycleCounters.Empty;

    public static EcologyState Create(Tile[,] map, int width, int height, int regionSize = DefaultRegionSize)
    {
        regionSize = Math.Max(1, regionSize);
        var tiles = new EcologyTileState[width, height];
        var regionColumns = Math.Max(1, (int)Math.Ceiling(width / (double)regionSize));
        var regionBuilders = new Dictionary<int, EcologyRegionCacheBuilder>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var tile = map[x, y];
                var regionId = GetRegionId(x, y, regionSize, regionColumns);
                var fertility = ComputeFertility(x, y, tile.Ground);
                var plantCapacity = ComputePlantCapacity(tile.Ground, fertility);
                var plantBiomass = ComputeInitialPlantBiomass(tile, plantCapacity);
                var state = new EcologyTileState(
                    X: x,
                    Y: y,
                    RegionId: regionId,
                    Fertility: fertility,
                    PlantCapacity: plantCapacity,
                    PlantBiomass: plantBiomass,
                    OvergrazingPressure: 0f);

                tiles[x, y] = state;
                if (!regionBuilders.TryGetValue(regionId, out var builder))
                {
                    builder = new EcologyRegionCacheBuilder(regionId);
                    regionBuilders[regionId] = builder;
                }

                builder.AddTile(tile.Ground, state);
            }
        }

        var cache = new EcologyRegionCache(regionBuilders.Values.Select(builder => builder.Build()).ToList());
        return new EcologyState(width, height, regionSize, tiles, cache);
    }

    public EcologyTileState GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return new EcologyTileState(x, y, RegionId: -1, Fertility: 0f, PlantCapacity: 0f, PlantBiomass: 0f, OvergrazingPressure: 0f);

        return _tiles[x, y];
    }

    public IReadOnlyList<EcologyRegionSnapshot> BuildRegionSnapshots(IEnumerable<Animal> animals, Season season, bool isDroughtActive)
        => _regionCache.BuildSnapshots(animals, season, isDroughtActive, RegionSize, RegionColumns);

    internal static int GetRegionId(int x, int y, int regionSize, int regionColumns)
    {
        var chunkX = Math.Max(0, x) / Math.Max(1, regionSize);
        var chunkY = Math.Max(0, y) / Math.Max(1, regionSize);
        return chunkY * Math.Max(1, regionColumns) + chunkX;
    }

    static float ComputeFertility(int x, int y, Ground ground)
    {
        if (ground == Ground.Water)
            return 0f;

        var baseline = ground == Ground.Grass ? 0.65f : 0.35f;
        var variation = (((x * 73856093) ^ (y * 19349663)) & 0xff) / 255f * 0.2f;
        return Math.Clamp(baseline + variation, 0f, 1f);
    }

    static float ComputePlantCapacity(Ground ground, float fertility)
        => ground == Ground.Water ? 0f : Math.Clamp(fertility, 0f, 1f);

    static float ComputeInitialPlantBiomass(Tile tile, float plantCapacity)
    {
        if (tile.Ground == Ground.Water || plantCapacity <= 0f)
            return 0f;

        return tile.Node switch
        {
            { Type: Resource.Food, Amount: > 0 } => plantCapacity,
            { Type: Resource.Food, Amount: <= 0 } => 0f,
            _ => Math.Clamp(plantCapacity * 0.35f, 0f, plantCapacity)
        };
    }

    sealed class EcologyRegionCacheBuilder
    {
        int _landTileCount;
        int _waterTileCount;
        float _plantBiomassTotal;
        float _plantCapacityTotal;
        float _overgrazingPressureTotal;

        public EcologyRegionCacheBuilder(int regionId)
        {
            RegionId = regionId;
        }

        public int RegionId { get; }

        public void AddTile(Ground ground, EcologyTileState tile)
        {
            if (ground == Ground.Water)
                _waterTileCount++;
            else
                _landTileCount++;

            _plantBiomassTotal += tile.PlantBiomass;
            _plantCapacityTotal += tile.PlantCapacity;
            _overgrazingPressureTotal += tile.OvergrazingPressure;
        }

        public EcologyRegionBaseState Build()
        {
            var tileCount = Math.Max(1, _landTileCount + _waterTileCount);
            return new EcologyRegionBaseState(
                RegionId,
                _landTileCount,
                _waterTileCount,
                _plantBiomassTotal,
                _plantCapacityTotal,
                CarryingCapacity: _plantCapacityTotal,
                OvergrazingPressure: Math.Clamp(_overgrazingPressureTotal / tileCount, 0f, 1f));
        }
    }
}
