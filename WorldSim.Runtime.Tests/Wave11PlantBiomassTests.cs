using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11PlantBiomassTests
{
    [Fact]
    public void FoodHarvest_UpdatesTileAndRegionBiomassInSnapshots()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 11101);
        var pos = FindFoodTile(world);
        var before = WorldSnapshotBuilder.Build(world);
        var beforeTile = FindSnapshotTile(before, pos);
        var beforeRegion = FindRegion(before, beforeTile.EcologyRegionId);

        Assert.True(world.TryHarvest(pos, Resource.Food, 1));

        var after = WorldSnapshotBuilder.Build(world);
        var afterTile = FindSnapshotTile(after, pos);
        var afterRegion = FindRegion(after, afterTile.EcologyRegionId);
        var afterTileSum = SumTileBiomassForRegion(after, afterTile.EcologyRegionId);

        Assert.True(afterTile.PlantBiomass < beforeTile.PlantBiomass);
        Assert.True(afterRegion.PlantBiomassTotal < beforeRegion.PlantBiomassTotal);
        AssertApprox(afterTileSum, afterRegion.PlantBiomassTotal);
    }

    [Fact]
    public void FoodRegrowthSpot_GrowsBiomassWithoutStaleSnapshotRegionTotal()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 11102);
        var pos = FindFoodTile(world);
        DepleteFoodNode(world, pos);
        var depleted = WorldSnapshotBuilder.Build(world);
        var depletedTile = FindSnapshotTile(depleted, pos);

        world.Update(1f);

        var afterGrowth = WorldSnapshotBuilder.Build(world);
        var afterTile = FindSnapshotTile(afterGrowth, pos);
        var afterRegion = FindRegion(afterGrowth, afterTile.EcologyRegionId);
        var afterTileSum = SumTileBiomassForRegion(afterGrowth, afterTile.EcologyRegionId);

        Assert.True(afterTile.PlantBiomass > depletedTile.PlantBiomass);
        AssertApprox(afterTileSum, afterRegion.PlantBiomassTotal);
        Assert.True(world.BuildEcologyPlantCounters().PlantGrowthTicks > 0);
        Assert.Equal(0, world.BuildEcologyPlantCounters().DroughtPlantPenaltyTicks);
    }

    [Fact]
    public void PartialFoodHarvest_RecoversBiomassWithoutNodeDepletion()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 11105);
        var pos = FindFoodTileWithAtLeast(world, minAmount: 2);
        var beforeAmount = world.GetTile(pos.x, pos.y).Node!.Amount;

        Assert.True(world.TryHarvest(pos, Resource.Food, 1));
        Assert.True(world.GetTile(pos.x, pos.y).Node!.Amount > 0);

        var harvested = WorldSnapshotBuilder.Build(world);
        var harvestedTile = FindSnapshotTile(harvested, pos);
        var harvestedBiomass = harvestedTile.PlantBiomass;

        world.Update(1f);

        var recovered = WorldSnapshotBuilder.Build(world);
        var recoveredTile = FindSnapshotTile(recovered, pos);
        var recoveredRegion = FindRegion(recovered, recoveredTile.EcologyRegionId);
        var recoveredTileSum = SumTileBiomassForRegion(recovered, recoveredTile.EcologyRegionId);

        Assert.Equal(beforeAmount - 1, world.GetTile(pos.x, pos.y).Node!.Amount);
        Assert.True(recoveredTile.PlantBiomass > harvestedBiomass);
        AssertApprox(recoveredTileSum, recoveredRegion.PlantBiomassTotal);
    }

    [Fact]
    public void FoodRegrowthCompletion_RestoresNodeAndBiomassConsistency()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 11106)
        {
            FoodRegrowthMinSeconds = 0.1f,
            FoodRegrowthJitterSeconds = 0f
        };
        var pos = FindFoodTile(world);
        DepleteFoodNode(world, pos);

        world.Update(0.2f);

        var snapshot = WorldSnapshotBuilder.Build(world);
        var tile = FindSnapshotTile(snapshot, pos);
        var region = FindRegion(snapshot, tile.EcologyRegionId);
        var tileSum = SumTileBiomassForRegion(snapshot, tile.EcologyRegionId);

        Assert.True(world.GetTile(pos.x, pos.y).Node?.Amount > 0);
        Assert.Equal(tile.PlantCapacity, tile.PlantBiomass);
        Assert.Equal(0f, tile.FoodRegrowthProgress);
        AssertApprox(tileSum, region.PlantBiomassTotal);
    }

    [Fact]
    public void RepeatedHarvestPressure_CannotMakeBiomassNegative()
    {
        var state = EcologyState.Create(CreateSingleTileMap(Resource.Food, amount: 5), width: 1, height: 1);

        state.ReportPlantHarvested(0, 0, harvestedAmount: 1000, depletedFoodNode: true);

        var tile = state.GetTile(0, 0);
        var region = state.BuildRegionSnapshots(Array.Empty<Animal>(), Season.Spring, isDroughtActive: false).Single();
        Assert.Equal(0f, tile.PlantBiomass);
        Assert.InRange(tile.OvergrazingPressure, 0f, 1f);
        Assert.Equal(0f, region.PlantBiomassTotal);
        Assert.InRange(region.OvergrazingPressure, 0f, 1f);
    }

    [Fact]
    public void NonFoodHarvest_DoesNotAlterPlantBiomass()
    {
        var world = new World(width: 16, height: 16, initialPop: 0, randomSeed: 11103);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Wood, amount: 3));
        var before = world.GetEcologyTileState(pos.x, pos.y);

        Assert.True(world.TryHarvest(pos, Resource.Wood, 1));

        var after = world.GetEcologyTileState(pos.x, pos.y);
        Assert.Equal(before.PlantBiomass, after.PlantBiomass);
        Assert.Equal(before.OvergrazingPressure, after.OvergrazingPressure);
    }

    [Fact]
    public void DroughtAndSeason_ModulateBiomassGrowthDeltaOnly()
    {
        var spring = CreateHarvestedFoodState();
        var drought = CreateHarvestedFoodState();
        var winter = CreateHarvestedFoodState();

        var springBefore = spring.GetTile(0, 0).PlantBiomass;
        var droughtBefore = drought.GetTile(0, 0).PlantBiomass;
        var winterBefore = winter.GetTile(0, 0).PlantBiomass;

        spring.TickPlantBiomassForRegrowthSpot(0, 0, dt: 1f, Season.Spring, isDroughtActive: false);
        drought.TickPlantBiomassForRegrowthSpot(0, 0, dt: 1f, Season.Spring, isDroughtActive: true);
        winter.TickPlantBiomassForRegrowthSpot(0, 0, dt: 1f, Season.Winter, isDroughtActive: false);

        var springDelta = spring.GetTile(0, 0).PlantBiomass - springBefore;
        var droughtDelta = drought.GetTile(0, 0).PlantBiomass - droughtBefore;
        var winterDelta = winter.GetTile(0, 0).PlantBiomass - winterBefore;

        Assert.True(springDelta > droughtDelta);
        Assert.True(springDelta > winterDelta);
        Assert.True(drought.PlantCounters.DroughtPlantPenaltyTicks > 0);
    }

    [Fact]
    public void OvergrazingPressure_ReducesGrowthDelta()
    {
        var lowPressure = CreateHarvestedFoodState(harvestedAmount: 1, depletedFoodNode: false);
        var highPressure = CreateHarvestedFoodState(harvestedAmount: 4, depletedFoodNode: true);

        var lowBefore = lowPressure.GetTile(0, 0).PlantBiomass;
        var highBefore = highPressure.GetTile(0, 0).PlantBiomass;

        lowPressure.TickPlantBiomassForRegrowthSpot(0, 0, dt: 1f, Season.Summer, isDroughtActive: false);
        highPressure.TickPlantBiomassForRegrowthSpot(0, 0, dt: 1f, Season.Summer, isDroughtActive: false);

        var lowDelta = lowPressure.GetTile(0, 0).PlantBiomass - lowBefore;
        var highDelta = highPressure.GetTile(0, 0).PlantBiomass - highBefore;

        Assert.True(highPressure.GetTile(0, 0).OvergrazingPressure > lowPressure.GetTile(0, 0).OvergrazingPressure);
        Assert.True(lowDelta > highDelta);
    }

    [Fact]
    public void EcologyRegionCacheRebuildCounter_IsConstructionOnlyForDeltaUpdates()
    {
        var world = new World(width: 32, height: 20, initialPop: 0, randomSeed: 11104);
        var pos = FindFoodTile(world);

        Assert.Equal(1, world.BuildEcologyPlantCounters().EcologyRegionCacheRebuilds);
        Assert.True(world.TryHarvest(pos, Resource.Food, 1));

        Assert.Equal(1, world.BuildEcologyPlantCounters().EcologyRegionCacheRebuilds);
    }

    static EcologyState CreateHarvestedFoodState(int harvestedAmount = 1, bool depletedFoodNode = false)
    {
        var state = EcologyState.Create(CreateSingleTileMap(Resource.Food, amount: 5), width: 1, height: 1);
        state.ReportPlantHarvested(0, 0, harvestedAmount, depletedFoodNode);
        return state;
    }

    static Tile[,] CreateSingleTileMap(Resource resource, int amount)
    {
        var map = new Tile[1, 1];
        map[0, 0] = new Tile(Ground.Grass, new ResourceNode(resource, amount));
        return map;
    }

    static (int x, int y) FindFoodTile(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.Ground != Ground.Water && tile.Node?.Type == Resource.Food && tile.Node.Amount > 0)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected at least one active food tile.");
    }

    static (int x, int y) FindFoodTileWithAtLeast(World world, int minAmount)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                if (tile.Ground != Ground.Water && tile.Node?.Type == Resource.Food && tile.Node.Amount >= minAmount)
                    return (x, y);
            }
        }

        throw new InvalidOperationException($"Expected at least one active food tile with amount >= {minAmount}.");
    }

    static (int x, int y) FindLandTile(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected at least one land tile.");
    }

    static void DepleteFoodNode(World world, (int x, int y) pos)
    {
        var guard = 0;
        while (world.GetTile(pos.x, pos.y).Node?.Amount > 0)
        {
            Assert.True(world.TryHarvest(pos, Resource.Food, 1));
            guard++;
            if (guard > 256)
                throw new InvalidOperationException("Guard tripped while depleting food node.");
        }
    }

    static TileRenderData FindSnapshotTile(WorldRenderSnapshot snapshot, (int x, int y) pos)
        => snapshot.Tiles.Single(tile => tile.X == pos.x && tile.Y == pos.y);

    static EcologyRegionRenderData FindRegion(WorldRenderSnapshot snapshot, int regionId)
        => snapshot.EcologyDetails.Regions.Single(region => region.RegionId == regionId);

    static float SumTileBiomassForRegion(WorldRenderSnapshot snapshot, int regionId)
        => snapshot.Tiles
            .Where(tile => tile.EcologyRegionId == regionId)
            .Sum(tile => tile.PlantBiomass);

    static void AssertApprox(float expected, float actual)
        => Assert.True(Math.Abs(expected - actual) < 0.0001f, $"Expected {expected} but got {actual}.");
}
