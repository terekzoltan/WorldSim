using System.Reflection;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class Wave11AnimalLifecycleTests
{
    [Fact]
    public void HerbivoreGrazing_IncreasesEnergyAndUsesHarvestBiomassSeam()
    {
        var world = CreateControlledWorld(seed: 11201);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 3));
        var herbivore = new Herbivore(pos, world.CreateEntityRng(), energy: 40f);
        world._animals.Add(herbivore);
        var beforeEnergy = herbivore.Energy;
        var beforeBiomass = world.GetEcologyTileState(pos.x, pos.y).PlantBiomass;

        world.Update(1f);

        Assert.True(herbivore.Energy > beforeEnergy);
        Assert.Equal(2, world.GetTile(pos.x, pos.y).Node?.Amount);
        Assert.True(world.GetEcologyTileState(pos.x, pos.y).PlantBiomass < beforeBiomass);
    }

    [Fact]
    public void HerbivoreStarvation_KillsAndIncrementsLifecycleCounterWithoutPredators()
    {
        var world = CreateControlledWorld(seed: 11202);
        ClearResourceNodes(world);
        var pos = FindLandTile(world);
        var herbivore = new Herbivore(pos, world.CreateEntityRng(), energy: 0f);
        world._animals.Add(herbivore);

        world.Update(1f);
        world.Update(1f);

        Assert.False(herbivore.IsAlive);
        Assert.DoesNotContain(world._animals, animal => ReferenceEquals(animal, herbivore));
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().HerbivoreStarvations);
    }

    [Fact]
    public void HerbivoreReproduction_QueuesBirthWithoutNormalReplenishment()
    {
        var world = CreateControlledWorld(seed: 11203);
        var pos = FindLandTileWithNeighbor(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        var parent = new Herbivore(pos, world.CreateEntityRng(), energy: 110f);
        world._animals.Add(parent);

        world.Update(1f);

        var herbivores = world._animals.OfType<Herbivore>().ToList();
        Assert.Equal(2, herbivores.Count);
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.All(herbivores, herbivore => Assert.NotEqual(Ground.Water, world.GetTile(herbivore.Pos.x, herbivore.Pos.y).Ground));
        Assert.True(parent.ReproductionCooldown > 0f);
    }

    [Fact]
    public void HerbivoreReproduction_ReservesCapacityForSameTickQueuedBirths()
    {
        var (world, region, positions) = CreateCapacityWorld(seed: 11206);
        var capacityLimit = World.GetHerbivoreCapacityLimit(region);
        var fillerCount = capacityLimit - 3;
        for (var i = 0; i < fillerCount; i++)
            world._animals.Add(new Herbivore(positions[i % positions.Count], world.CreateEntityRng(), energy: 50f));

        var firstParentPos = positions[^1];
        var secondParentPos = positions[^2];
        world.GetTile(firstParentPos.x, firstParentPos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        world.GetTile(secondParentPos.x, secondParentPos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        world._animals.Add(new Herbivore(firstParentPos, world.CreateEntityRng(), energy: 110f));
        world._animals.Add(new Herbivore(secondParentPos, world.CreateEntityRng(), energy: 110f));

        Assert.Equal(capacityLimit - 1, CountHerbivoresInRegion(world, region.RegionId));

        world.Update(1f);

        Assert.Equal(capacityLimit, CountHerbivoresInRegion(world, region.RegionId));
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
    }

    [Fact]
    public void HerbivoreReproduction_FailedCapacityAttemptDoesNotQueueOrReportBirth()
    {
        var (world, region, positions) = CreateCapacityWorld(seed: 11207);
        var capacityLimit = World.GetHerbivoreCapacityLimit(region);
        for (var i = 0; i < capacityLimit - 1; i++)
            world._animals.Add(new Herbivore(positions[i % positions.Count], world.CreateEntityRng(), energy: 50f));

        var parentPos = positions[^1];
        world.GetTile(parentPos.x, parentPos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        var parent = new Herbivore(parentPos, world.CreateEntityRng(), energy: 110f);
        world._animals.Add(parent);

        Assert.Equal(capacityLimit, CountHerbivoresInRegion(world, region.RegionId));

        var queued = world.QueueHerbivoreBirth(parent);

        Assert.False(queued);
        Assert.Equal(capacityLimit, CountHerbivoresInRegion(world, region.RegionId));
        Assert.Equal(0, world.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.Equal(0, world.TotalHerbivoreReplenishmentSpawns);
    }

    [Fact]
    public void HerbivoreReproduction_FullParentRegionCanUseNeighboringTargetRegionCapacity()
    {
        var fixture = CreateTwoRegionBirthFixture(seed: 11212);
        var parent = new Herbivore(fixture.ParentPos, fixture.World.CreateEntityRng(), energy: 110f);
        fixture.World._animals.Add(parent);
        FillRegionToCapacity(fixture.World, fixture.ParentRegion.RegionId, fixture.ParentRegionCapacity - 1, exclude: fixture.ParentPos);
        MarkFood(fixture.World, fixture.ParentPos);

        Assert.Equal(fixture.ParentRegionCapacity, CountHerbivoresInRegion(fixture.World, fixture.ParentRegion.RegionId));
        Assert.True(CountHerbivoresInRegion(fixture.World, fixture.TargetRegion.RegionId) < fixture.TargetRegionCapacity);

        fixture.World.Update(0f);

        Assert.Equal(fixture.ParentRegionCapacity, CountHerbivoresInRegion(fixture.World, fixture.ParentRegion.RegionId));
        Assert.Equal(1, fixture.World.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.True(parent.ReproductionCooldown > 0f);
        Assert.True(parent.Energy < 110f);
        Assert.Contains(fixture.World._animals.OfType<Herbivore>(), herbivore =>
            !ReferenceEquals(herbivore, parent)
            && herbivore.ReproductionCooldown > 0f
            && fixture.World.GetEcologyTileState(herbivore.Pos.x, herbivore.Pos.y).RegionId == fixture.TargetRegion.RegionId);
    }

    [Fact]
    public void HerbivoreReproduction_AllTargetRegionsFullDoesNotMutateParentOrBirthCounter()
    {
        var (world, region, positions) = CreateCapacityWorld(seed: 11213);
        var capacityLimit = World.GetHerbivoreCapacityLimit(region);
        var parentPos = positions[^1];
        var parent = new Herbivore(parentPos, world.CreateEntityRng(), energy: AnimalLifecycleModel.HerbivoreMaxEnergy);
        world._animals.Add(parent);
        FillRegionToCapacity(world, region.RegionId, capacityLimit - 1, exclude: parentPos);
        MarkFood(world, parentPos);
        var beforeEnergy = parent.Energy;
        var beforeCooldown = parent.ReproductionCooldown;

        Assert.Equal(capacityLimit, CountHerbivoresInRegion(world, region.RegionId));

        world.Update(0f);

        Assert.Equal(beforeEnergy, parent.Energy);
        Assert.Equal(beforeCooldown, parent.ReproductionCooldown);
        Assert.Equal(0, world.BuildEcologyLifecycleCounters().HerbivoreBirths);
        Assert.Equal(capacityLimit, CountHerbivoresInRegion(world, region.RegionId));
    }

    [Fact]
    public void StarvingHerbivore_OnFoodGrazesBeforeDeath()
    {
        var world = CreateControlledWorld(seed: 11208);
        var pos = FindLandTile(world);
        world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 3));
        var herbivore = new Herbivore(pos, world.CreateEntityRng(), energy: 0f);
        world._animals.Add(herbivore);

        world.Update(2f);

        Assert.True(herbivore.IsAlive);
        Assert.Contains(herbivore, world._animals.OfType<Herbivore>());
        Assert.Equal(2, world.GetTile(pos.x, pos.y).Node?.Amount);
        Assert.Equal(0, world.BuildEcologyLifecycleCounters().HerbivoreStarvations);
    }

    [Fact]
    public void HerbivoreMigration_CountsOnlyRealMigrationMovement()
    {
        var world = CreateControlledWorld(seed: 11204);
        ClearResourceNodes(world);
        var (origin, _) = FindMigrationPair(world);
        var herbivore = new Herbivore(origin, world.CreateEntityRng(), energy: 30f);
        world._animals.Add(herbivore);

        world.Update(1f);

        Assert.NotEqual(origin, herbivore.Pos);
        Assert.Equal(1, world.BuildEcologyLifecycleCounters().HerbivoreMigrations);
    }

    [Fact]
    public void HerbivoreMigration_NoBetterTargetDoesNotIncrementCounter()
    {
        var world = CreateControlledWorld(seed: 11209);
        ClearResourceNodes(world);
        var origin = FindHighestScoreLandTile(world);
        world._animals.Add(new Herbivore(origin, world.CreateEntityRng(), energy: 30f));

        world.Update(1f);

        Assert.Equal(0, world.BuildEcologyLifecycleCounters().HerbivoreMigrations);
    }

    [Fact]
    public void SnapshotLifecycleCounters_MatchRuntimeCounters()
    {
        var world = CreateControlledWorld(seed: 11205);
        ClearResourceNodes(world);
        var pos = FindLandTile(world);
        world._animals.Add(new Herbivore(pos, world.CreateEntityRng(), energy: 0f));

        world.Update(1f);
        world.Update(1f);

        var runtimeCounters = world.BuildEcologyLifecycleCounters();
        var snapshot = WorldSnapshotBuilder.Build(world);

        Assert.Equal(runtimeCounters.HerbivoreStarvations, snapshot.EcologyDetails.LifecycleCounters.HerbivoreStarvations);
        Assert.Equal(runtimeCounters.HerbivoreBirths, snapshot.EcologyDetails.LifecycleCounters.HerbivoreBirths);
        Assert.Equal(runtimeCounters.HerbivoreMigrations, snapshot.EcologyDetails.LifecycleCounters.HerbivoreMigrations);
    }

    [Fact]
    public void SnapshotLifecycleCounters_IncludeNonZeroBirthsAndMigrations()
    {
        var birthWorld = CreateControlledWorld(seed: 11210);
        var birthPos = FindLandTileWithNeighbor(birthWorld);
        birthWorld.GetTile(birthPos.x, birthPos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));
        birthWorld._animals.Add(new Herbivore(birthPos, birthWorld.CreateEntityRng(), energy: 110f));

        birthWorld.Update(1f);

        var birthRuntimeCounters = birthWorld.BuildEcologyLifecycleCounters();
        var birthSnapshotCounters = WorldSnapshotBuilder.Build(birthWorld).EcologyDetails.LifecycleCounters;
        Assert.True(birthRuntimeCounters.HerbivoreBirths > 0);
        Assert.Equal(birthRuntimeCounters.HerbivoreBirths, birthSnapshotCounters.HerbivoreBirths);

        var migrationWorld = CreateControlledWorld(seed: 11211);
        ClearResourceNodes(migrationWorld);
        var (migrationOrigin, _) = FindMigrationPair(migrationWorld);
        migrationWorld._animals.Add(new Herbivore(migrationOrigin, migrationWorld.CreateEntityRng(), energy: 30f));

        migrationWorld.Update(1f);

        var migrationRuntimeCounters = migrationWorld.BuildEcologyLifecycleCounters();
        var migrationSnapshotCounters = WorldSnapshotBuilder.Build(migrationWorld).EcologyDetails.LifecycleCounters;
        Assert.True(migrationRuntimeCounters.HerbivoreMigrations > 0);
        Assert.Equal(migrationRuntimeCounters.HerbivoreMigrations, migrationSnapshotCounters.HerbivoreMigrations);
    }

    static World CreateControlledWorld(int seed, int width = 32, int height = 20)
    {
        var world = new World(width: width, height: height, initialPop: 0, randomSeed: seed)
        {
            AnimalReplenishmentChancePerSecond = 0f,
            PredatorReplenishmentChance = 0f
        };
        ResetGround(world, Ground.Dirt);
        world._animals.Clear();
        return world;
    }

    static void ResetGround(World world, Ground ground)
    {
        var map = new Tile[world.Width, world.Height];
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
                map[x, y] = new Tile(ground);
        }

        typeof(World).GetField("_map", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, map);
        typeof(World).GetField("_ecologyState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            world,
            EcologyState.Create(map, world.Width, world.Height));
    }

    static (World World, EcologyRegionSnapshot Region, List<(int x, int y)> Positions) CreateCapacityWorld(int seed)
    {
        for (var offset = 0; offset < 20; offset++)
        {
            var world = CreateControlledWorld(seed + offset, width: 8, height: 8);
            var region = world.BuildEcologyRegionSnapshots().Single();
            var positions = GetLandPositionsInRegion(world, region.RegionId);
            var capacityLimit = World.GetHerbivoreCapacityLimit(region);
            if (positions.Count >= capacityLimit && capacityLimit >= 4)
                return (world, region, positions);
        }

        throw new InvalidOperationException("Expected deterministic capacity test world.");
    }

    static TwoRegionBirthFixture CreateTwoRegionBirthFixture(int seed)
    {
        for (var offset = 0; offset < 40; offset++)
        {
            var world = CreateControlledWorld(seed + offset, width: 17, height: 16);
            var regions = world.BuildEcologyRegionSnapshots().OrderBy(region => region.RegionId).ToList();
            if (regions.Count < 2)
                continue;

            foreach (var parentRegion in regions)
            {
                foreach (var targetRegion in regions.Where(region => region.RegionId != parentRegion.RegionId))
                {
                    if (!TryFindAdjacentRegionPair(world, parentRegion.RegionId, targetRegion.RegionId, out var parentPos, out _))
                        continue;

                    var parentCapacity = World.GetHerbivoreCapacityLimit(parentRegion);
                    var targetCapacity = World.GetHerbivoreCapacityLimit(targetRegion);
                    var parentPositions = GetLandPositionsInRegion(world, parentRegion.RegionId).Where(pos => pos != parentPos).ToList();
                    var targetPositions = GetLandPositionsInRegion(world, targetRegion.RegionId);
                    if (parentCapacity < 2 || targetCapacity < 1)
                        continue;

                    if (parentPositions.Count < parentCapacity - 1)
                        continue;

                    if (targetPositions.Count < 1)
                        continue;

                    return new TwoRegionBirthFixture(world, parentRegion, targetRegion, parentPos, parentCapacity, targetCapacity);
                }
            }
        }

        throw new InvalidOperationException("Expected deterministic two-region birth fixture.");
    }

    static void ClearResourceNodes(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }
    }

    static void FillRegionToCapacity(World world, int regionId, int count, (int x, int y) exclude)
    {
        var added = 0;
        foreach (var pos in GetLandPositionsInRegion(world, regionId))
        {
            if (pos == exclude)
                continue;

            world._animals.Add(new Herbivore(pos, world.CreateEntityRng(), energy: 50f, reproductionCooldown: 30f));
            MarkFood(world, pos);
            added++;
            if (added == count)
                return;
        }

        throw new InvalidOperationException("Not enough region land positions to fill capacity.");
    }

    static void MarkFood(World world, (int x, int y) pos)
        => world.GetTile(pos.x, pos.y).ReplaceNode(new ResourceNode(Resource.Food, amount: 5));

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

        throw new InvalidOperationException("Expected land tile.");
    }

    static (int x, int y) FindLandTileWithNeighbor(World world)
    {
        for (var y = 1; y < world.Height - 1; y++)
        {
            for (var x = 1; x < world.Width - 1; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;

                if (world.GetTile(x + 1, y).Ground != Ground.Water ||
                    world.GetTile(x - 1, y).Ground != Ground.Water ||
                    world.GetTile(x, y + 1).Ground != Ground.Water ||
                    world.GetTile(x, y - 1).Ground != Ground.Water)
                    return (x, y);
            }
        }

        throw new InvalidOperationException("Expected land tile with land neighbor.");
    }

    static ((int x, int y) Origin, (int x, int y) Target) FindMigrationPair(World world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;

                var origin = (x, y);
                var originScore = Score(world, origin);
                for (var dy = -5; dy <= 5; dy++)
                {
                    for (var dx = -5; dx <= 5; dx++)
                    {
                        var tx = x + dx;
                        var ty = y + dy;
                        if (tx < 0 || ty < 0 || tx >= world.Width || ty >= world.Height)
                            continue;

                        var md = Math.Abs(dx) + Math.Abs(dy);
                        if (md == 0 || md > 5 || world.GetTile(tx, ty).Ground == Ground.Water)
                            continue;

                        var target = (tx, ty);
                        if (Score(world, target) <= originScore + 0.05f)
                            continue;

                        return (origin, target);
                    }
                }
            }
        }

        throw new InvalidOperationException("Expected migration pair with better plant score.");
    }

    static (int x, int y) FindHighestScoreLandTile(World world)
    {
        (int x, int y)? best = null;
        var bestScore = float.MinValue;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;

                var score = Score(world, (x, y));
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = (x, y);
            }
        }

        return best ?? throw new InvalidOperationException("Expected highest-score land tile.");
    }

    static List<(int x, int y)> GetLandPositionsInRegion(World world, int regionId)
    {
        var positions = new List<(int x, int y)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground != Ground.Water && world.GetEcologyTileState(x, y).RegionId == regionId)
                    positions.Add((x, y));
            }
        }

        return positions;
    }

    static bool TryFindAdjacentRegionPair(
        World world,
        int parentRegionId,
        int targetRegionId,
        out (int x, int y) parentPos,
        out (int x, int y) targetPos)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetTile(x, y).Ground == Ground.Water || world.GetEcologyTileState(x, y).RegionId != parentRegionId)
                    continue;

                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (next.Item1 < 0 || next.Item2 < 0 || next.Item1 >= world.Width || next.Item2 >= world.Height)
                        continue;

                    if (world.GetTile(next.Item1, next.Item2).Ground == Ground.Water)
                        continue;

                    if (world.GetEcologyTileState(next.Item1, next.Item2).RegionId != targetRegionId)
                        continue;

                    parentPos = (x, y);
                    targetPos = next;
                    return true;
                }
            }
        }

        parentPos = default;
        targetPos = default;
        return false;
    }

    static int CountHerbivoresInRegion(World world, int regionId)
        => world._animals
            .OfType<Herbivore>()
            .Count(herbivore => herbivore.IsAlive && world.GetEcologyTileState(herbivore.Pos.x, herbivore.Pos.y).RegionId == regionId);

    static float Score(World world, (int x, int y) pos)
    {
        var tile = world.GetEcologyTileState(pos.x, pos.y);
        return tile.PlantBiomass - tile.OvergrazingPressure;
    }

    sealed record TwoRegionBirthFixture(
        World World,
        EcologyRegionSnapshot ParentRegion,
        EcologyRegionSnapshot TargetRegion,
        (int x, int y) ParentPos,
        int ParentRegionCapacity,
        int TargetRegionCapacity);
}
