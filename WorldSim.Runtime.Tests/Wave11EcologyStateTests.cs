using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11EcologyStateTests
{
    [Fact]
    public void FixedGridRegions_CoverMapAndUseStableIds()
    {
        var state = EcologyState.Create(CreateMap(33, 17, Ground.Grass), width: 33, height: 17);

        var regions = state.BuildRegionSnapshots(Array.Empty<Animal>(), Season.Spring, isDroughtActive: false);

        Assert.Equal(6, regions.Count);
        Assert.Equal(33 * 17, regions.Sum(region => region.LandTileCount + region.WaterTileCount));
        Assert.Equal(0, state.GetTile(0, 0).RegionId);
        Assert.Equal(0, state.GetTile(15, 15).RegionId);
        Assert.Equal(1, state.GetTile(16, 0).RegionId);
        Assert.Equal(2, state.GetTile(32, 0).RegionId);
        Assert.Equal(3, state.GetTile(0, 16).RegionId);
        Assert.Equal(5, state.GetTile(32, 16).RegionId);

        var small = EcologyState.Create(CreateMap(8, 8, Ground.Grass), width: 8, height: 8);
        Assert.Single(small.BuildRegionSnapshots(Array.Empty<Animal>(), Season.Spring, isDroughtActive: false));
    }

    [Fact]
    public void TileEcologyDefaults_AreBoundedAndWaterSafe()
    {
        var map = new Tile[3, 1];
        map[0, 0] = new Tile(Ground.Water);
        map[1, 0] = new Tile(Ground.Grass, new ResourceNode(Resource.Food, 4));
        map[2, 0] = new Tile(Ground.Dirt, new ResourceNode(Resource.Food, 0));

        var state = EcologyState.Create(map, width: 3, height: 1);

        var water = state.GetTile(0, 0);
        Assert.Equal(0f, water.Fertility);
        Assert.Equal(0f, water.PlantCapacity);
        Assert.Equal(0f, water.PlantBiomass);
        Assert.Equal(0f, water.OvergrazingPressure);

        var activeFood = state.GetTile(1, 0);
        Assert.InRange(activeFood.Fertility, 0f, 1f);
        Assert.InRange(activeFood.PlantCapacity, 0f, 1f);
        Assert.Equal(activeFood.PlantCapacity, activeFood.PlantBiomass);
        Assert.Equal(0f, activeFood.OvergrazingPressure);

        var depletedFood = state.GetTile(2, 0);
        Assert.InRange(depletedFood.Fertility, 0f, 1f);
        Assert.InRange(depletedFood.PlantCapacity, 0f, 1f);
        Assert.Equal(0f, depletedFood.PlantBiomass);
        Assert.Equal(0f, depletedFood.OvergrazingPressure);
    }

    [Fact]
    public void EcologyState_IsDeterministicForSameMap()
    {
        var first = EcologyState.Create(CreateMixedMap(), width: 5, height: 4);
        var second = EcologyState.Create(CreateMixedMap(), width: 5, height: 4);

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                Assert.Equal(first.GetTile(x, y), second.GetTile(x, y));
            }
        }

        Assert.Equal(
            first.BuildRegionSnapshots(Array.Empty<Animal>(), Season.Summer, isDroughtActive: true),
            second.BuildRegionSnapshots(Array.Empty<Animal>(), Season.Summer, isDroughtActive: true));
    }

    [Fact]
    public void WorldSnapshotBuilder_ExportsBoundedEcologyContract()
    {
        var world = new World(width: 33, height: 17, initialPop: 0, randomSeed: 1101);

        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Equal(world.Width * world.Height, snapshot.Tiles.Count);
        Assert.Equal(world.Width * world.Height, snapshot.EcologyDetails.Regions.Sum(region => region.LandTileCount + region.WaterTileCount));
        Assert.Equal(6, snapshot.EcologyDetails.Regions.Count);
        Assert.All(snapshot.Tiles, tile =>
        {
            Assert.InRange(tile.EcologyRegionId, 0, snapshot.EcologyDetails.Regions.Count - 1);
            Assert.InRange(tile.Fertility, 0f, 1f);
            Assert.InRange(tile.PlantCapacity, 0f, 1f);
            Assert.InRange(tile.PlantBiomass, 0f, tile.PlantCapacity);
            Assert.InRange(tile.OvergrazingPressure, 0f, 1f);
            if (tile.Ground == TileGroundView.Water)
            {
                Assert.Equal(0f, tile.Fertility);
                Assert.Equal(0f, tile.PlantCapacity);
                Assert.Equal(0f, tile.PlantBiomass);
            }
        });
        Assert.All(snapshot.EcologyDetails.Regions, region =>
        {
            Assert.InRange(region.PlantBiomassTotal, 0f, region.PlantCapacityTotal);
            Assert.InRange(region.OvergrazingPressure, 0f, 1f);
            Assert.True(region.LandTileCount + region.WaterTileCount > 0);
        });
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.HerbivoreBirths);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.PredatorBirths);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.HerbivoreStarvations);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.PredatorStarvations);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.HerbivoreMigrations);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.PredatorMigrations);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.LandSafeSpawnFallbacks);
        Assert.Equal(0, snapshot.EcologyDetails.LifecycleCounters.EmergencyRescues);
    }

    static Tile[,] CreateMap(int width, int height, Ground ground)
    {
        var map = new Tile[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                map[x, y] = new Tile(ground);
        }

        return map;
    }

    static Tile[,] CreateMixedMap()
    {
        var map = CreateMap(5, 4, Ground.Dirt);
        map[0, 0] = new Tile(Ground.Water);
        map[1, 0] = new Tile(Ground.Grass, new ResourceNode(Resource.Food, 5));
        map[2, 0] = new Tile(Ground.Grass, new ResourceNode(Resource.Food, 0));
        map[4, 3] = new Tile(Ground.Water);
        return map;
    }
}
