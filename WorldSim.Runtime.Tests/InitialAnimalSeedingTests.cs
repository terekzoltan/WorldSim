using System.Text.Json;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using Xunit;

namespace WorldSim.Runtime.Tests;

public sealed class InitialAnimalSeedingTests
{
    [Fact]
    public void InitialAnimalSeedingOptions_InvalidEnumValueFailsDeterministically()
    {
        var options = InitialAnimalSeedingOptions.HabitatAwareDefault with
        {
            Policy = (InitialAnimalSeedingPolicy)999
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => InitialAnimalSeeder.Plan(BuildInput(options)));
    }

    [Fact]
    public void InitialAnimalSeedingOptions_NonPositiveDensityFailsDeterministically()
    {
        var options = InitialAnimalSeedingOptions.HabitatAwareDefault with { AreaTilesPerAnimal = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => InitialAnimalSeeder.Plan(BuildInput(options)));
    }

    [Theory]
    [InlineData(-1, 5, 6)]
    [InlineData(7, -1, 6)]
    [InlineData(7, 5, -1)]
    public void InitialAnimalSeedingOptions_NegativeDistancesOrRadiiFailDeterministically(
        int preferredDistance,
        int foodRadius,
        int preyRadius)
    {
        var options = InitialAnimalSeedingOptions.HabitatAwareDefault with
        {
            PreferredPersonOrColonyDistance = preferredDistance,
            PreferredHerbivoreFoodRadius = foodRadius,
            PreferredPredatorPreyRadius = preyRadius
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => InitialAnimalSeeder.Plan(BuildInput(options)));
    }

    [Fact]
    public void HabitatAware_BackgroundBiomassWithoutActiveFoodDoesNotClaimFoodAccess()
    {
        var tiles = BuildLandTiles(width: 4, height: 4, activeFood: Array.Empty<(int x, int y)>());
        var result = InitialAnimalSeeder.Plan(BuildInput(tiles: tiles));

        Assert.Equal(0, result.HerbivoreBudget);
        Assert.Equal(0, result.PredatorBudget);
        Assert.Empty(result.Placements);
        Assert.Equal(result.AnimalCeiling, GetFallback(result, InitialAnimalSeedingFallbackReason.AnimalCeilingUnallocated));
    }

    [Fact]
    public void HabitatAware_NeverUsesLivingPersonOrColonyOriginTile()
    {
        var food = new[] { (x: 0, y: 0) };
        var tiles = BuildLandTiles(width: 4, height: 4, activeFood: food);
        var input = BuildInput(
            tiles: tiles,
            livingPeople: new[] { (x: 1, y: 0) },
            colonyOrigins: new[] { (x: 2, y: 0) });

        var result = InitialAnimalSeeder.Plan(input);

        Assert.NotEmpty(result.Placements);
        Assert.DoesNotContain(result.Placements, placement => placement.Pos == (1, 0));
        Assert.DoesNotContain(result.Placements, placement => placement.Pos == (2, 0));
        Assert.Equal(result.Placements.Count, result.Placements.Select(placement => placement.Pos).Distinct().Count());
    }

    [Fact]
    public void HabitatAware_FallbackReasonsMatchExactBudgetArithmetic()
    {
        var food = new[] { (x: 0, y: 0) };
        var options = InitialAnimalSeedingOptions.HabitatAwareDefault with
        {
            PreferredPersonOrColonyDistance = 7,
            PreferredHerbivoreFoodRadius = 1
        };
        var input = BuildInput(
            options,
            BuildLandTiles(width: 4, height: 4, activeFood: food),
            livingPeople: new[] { (x: 3, y: 3) });

        var result = InitialAnimalSeeder.Plan(input);

        Assert.Equal(
            result.AnimalCeiling - result.HerbivoreBudget - result.PredatorBudget,
            result.AnimalCeilingUnallocated);
        Assert.Equal(result.HerbivoreBudget - result.InitialHerbivoresSpawned, result.HerbivoreBudgetUnfilled);
        Assert.Equal(result.PredatorBudget - result.InitialPredatorsSpawned, result.PredatorBudgetUnfilled);
        Assert.Equal(result.AnimalCeilingUnallocated, GetFallback(result, InitialAnimalSeedingFallbackReason.AnimalCeilingUnallocated));
        Assert.True(GetFallback(result, InitialAnimalSeedingFallbackReason.HerbivorePersonOrColonyDistanceRelaxed) > 0);
        Assert.True(GetFallback(result, InitialAnimalSeedingFallbackReason.HerbivoreFoodRadiusRelaxed) > 0);
        Assert.Equal(result.Fallbacks.Sum(fallback => fallback.Count), result.InitialSeedingFallbackCount);
        Assert.Equal(
            result.Fallbacks.Select(fallback => fallback.Reason.ToWireValue()).OrderBy(value => value, StringComparer.Ordinal),
            result.Fallbacks.Select(fallback => fallback.Reason.ToWireValue()));
    }

    [Fact]
    public void HabitatAware_BudgetsUseRuntimeCapacityAuthority()
    {
        var input = BuildInput(tiles: BuildLandTiles(4, 4, new[] { (x: 0, y: 0) }));
        var constrainedRegion = input.Regions[0] with
        {
            PlantCapacityTotal = 0.1f,
            CarryingCapacity = 0.1f
        };
        input = input with { Regions = new[] { constrainedRegion } };

        var result = InitialAnimalSeeder.Plan(input);

        Assert.InRange(result.HerbivoreBudget, 0, World.GetHerbivoreCapacityLimit(constrainedRegion));
        var predatorRegion = constrainedRegion with { HerbivoreCount = result.HerbivoreBudget };
        Assert.InRange(result.PredatorBudget, 0, World.GetPredatorCapacityLimit(predatorRegion));
    }

    [Fact]
    public void HabitatAware_NoPreyReportsZeroPredatorBudgetAndSpawn()
    {
        var tiles = new[]
        {
            Tile(0, 0, activeFood: true),
            Tile(1, 0, activeFood: false, movementBlocked: true)
        };

        var result = InitialAnimalSeeder.Plan(BuildInput(width: 2, height: 1, tiles: tiles));

        Assert.Equal(1, result.HerbivoreBudget);
        Assert.Equal(0, result.PredatorBudget);
        Assert.Equal(0, result.InitialPredatorsSpawned);
        Assert.Equal(0, result.PredatorBudgetUnfilled);
        Assert.Equal(0, GetFallback(result, InitialAnimalSeedingFallbackReason.PredatorBudgetUnfilled));
    }

    [Fact]
    public void HabitatAware_SameInputProducesSamePlacementsBudgetsAndFallbacks()
    {
        var input = BuildInput(
            tiles: BuildLandTiles(width: 8, height: 4, activeFood: new[] { (x: 0, y: 0), (x: 7, y: 3) }),
            width: 8,
            height: 4);

        var first = InitialAnimalSeeder.Plan(input);
        var second = InitialAnimalSeeder.Plan(input);

        Assert.Equal(first.Policy, second.Policy);
        Assert.Equal(first.AnimalCeiling, second.AnimalCeiling);
        Assert.Equal(first.HerbivoreBudget, second.HerbivoreBudget);
        Assert.Equal(first.PredatorBudget, second.PredatorBudget);
        Assert.Equal(first.Placements, second.Placements);
        Assert.Equal(first.Fallbacks, second.Fallbacks);
        Assert.All(
            first.Placements.SkipWhile(placement => placement.Kind == AnimalKind.Herbivore),
            placement => Assert.Equal(AnimalKind.Predator, placement.Kind));
    }

    [Fact]
    public void HabitatAware_IncrementalAllocationPreservesCurrentSelectionSemantics()
    {
        var predatorCapablePreference = InitialAnimalSeeder.Plan(BuildInput(
            options: InitialAnimalSeedingOptions.HabitatAwareDefault with
            {
                PreferredPersonOrColonyDistance = 0
            },
            tiles: BuildLandTiles(width: 11, height: 1, activeFood: new[] { (x: 9, y: 0) }),
            livingPeople: new[] { (x: 0, y: 0) },
            width: 11,
            height: 1,
            hardPredatorPersonRadius: 9));
        AssertAllocation(
            predatorCapablePreference,
            expectedHerbivoreBudget: 2,
            expectedPredatorBudget: 1,
            expectedPlacements: new[]
            {
                Placement(AnimalKind.Herbivore, 9, 0),
                Placement(AnimalKind.Herbivore, 8, 0),
                Placement(AnimalKind.Predator, 10, 0)
            },
            expectedFallbacks: new[]
            {
                Fallback(InitialAnimalSeedingFallbackReason.AnimalCeilingUnallocated, 7)
            });

        var maximumTotal = InitialAnimalSeeder.Plan(BuildInput(
            tiles: BuildLandTiles(width: 12, height: 1, activeFood: new[] { (x: 0, y: 0) }),
            width: 12,
            height: 1));
        AssertAllocation(
            maximumTotal,
            expectedHerbivoreBudget: 8,
            expectedPredatorBudget: 2,
            expectedPlacements: Enumerable.Range(0, 8)
                .Select(x => Placement(
                    AnimalKind.Herbivore,
                    x,
                    0,
                    foodOrPreyRadiusRelaxed: x > 5))
                .Concat(new[]
                {
                    Placement(AnimalKind.Predator, 8, 0),
                    Placement(AnimalKind.Predator, 9, 0)
                })
                .ToArray(),
            expectedFallbacks: new[]
            {
                Fallback(InitialAnimalSeedingFallbackReason.HerbivoreFoodRadiusRelaxed, 2)
            });

        var equalTotalHerbivoreTieBreak = InitialAnimalSeeder.Plan(BuildInput(
            tiles: BuildLandTiles(width: 10, height: 1, activeFood: new[] { (x: 0, y: 0) }),
            width: 10,
            height: 1));
        AssertAllocation(
            equalTotalHerbivoreTieBreak,
            expectedHerbivoreBudget: 9,
            expectedPredatorBudget: 1,
            expectedPlacements: Enumerable.Range(0, 9)
                .Select(x => Placement(
                    AnimalKind.Herbivore,
                    x,
                    0,
                    foodOrPreyRadiusRelaxed: x > 5))
                .Append(Placement(AnimalKind.Predator, 9, 0))
                .ToArray(),
            expectedFallbacks: new[]
            {
                Fallback(InitialAnimalSeedingFallbackReason.HerbivoreFoodRadiusRelaxed, 3)
            });

        var regionalReservationEffect = InitialAnimalSeeder.Plan(BuildInput(
            tiles: Enumerable.Range(0, 10)
                .Select(x => Tile(
                    x,
                    0,
                    activeFood: x is 0 or 5,
                    regionId: x < 5 ? 0 : 1))
                .ToArray(),
            width: 10,
            height: 1));
        AssertAllocation(
            regionalReservationEffect,
            expectedHerbivoreBudget: 9,
            expectedPredatorBudget: 1,
            expectedPlacements: new[]
            {
                Placement(AnimalKind.Herbivore, 0, 0, regionId: 0),
                Placement(AnimalKind.Herbivore, 5, 0, regionId: 1),
                Placement(AnimalKind.Herbivore, 1, 0, regionId: 0),
                Placement(AnimalKind.Herbivore, 6, 0, regionId: 1),
                Placement(AnimalKind.Herbivore, 2, 0, regionId: 0),
                Placement(AnimalKind.Herbivore, 7, 0, regionId: 1),
                Placement(AnimalKind.Herbivore, 3, 0, regionId: 0),
                Placement(AnimalKind.Herbivore, 8, 0, regionId: 1),
                Placement(AnimalKind.Herbivore, 4, 0, regionId: 0),
                Placement(AnimalKind.Predator, 9, 0, regionId: 1)
            },
            expectedFallbacks: Array.Empty<InitialAnimalSeedingFallback>());
    }

    [Fact]
    public void HabitatAware_LargeMapUsesSinglePredatorMaterializationAndBoundedPrefixScan()
    {
        var firstInput = BuildLargeMapInput();
        var secondInput = BuildLargeMapInput();

        var first = InitialAnimalSeeder.Plan(firstInput);
        var second = InitialAnimalSeeder.Plan(secondInput);

        Assert.Equal(first.Policy, second.Policy);
        Assert.Equal(first.AnimalCeiling, second.AnimalCeiling);
        Assert.Equal(first.HerbivoreBudget, second.HerbivoreBudget);
        Assert.Equal(first.PredatorBudget, second.PredatorBudget);
        Assert.Equal(first.Placements, second.Placements);
        Assert.Equal(first.Fallbacks, second.Fallbacks);
        Assert.Equal(first.WorkMetrics, second.WorkMetrics);
        Assert.True(first.HerbivoreBudget > 0);
        Assert.True(first.PredatorBudget > 0);
        Assert.All(
            first.Placements.SkipWhile(placement => placement.Kind == AnimalKind.Herbivore),
            placement => Assert.Equal(AnimalKind.Predator, placement.Kind));

        var metrics = first.WorkMetrics;
        Assert.Equal(firstInput.Tiles.Count, metrics.TileFactCount);
        Assert.Equal(firstInput.Tiles.Count, metrics.PredatorCatalogTileVisits);
        Assert.Equal(
            Math.Min(first.AnimalCeiling, metrics.HerbivorePoolCount) + 1,
            metrics.AllocationPrefixesEvaluated);
        Assert.True(metrics.PredatorCatalogCandidateCount > 0);
        Assert.InRange(
            metrics.PredatorCandidateScoreEvaluations,
            1,
            metrics.PredatorCatalogCandidateCount);
        Assert.Equal(1, metrics.PredatorMaterializationPasses);
        Assert.InRange(metrics.PeakRetainedAllocationChoices, 1, 2);
    }

    [Fact]
    public void HabitatAware_DefaultReportsRuntimeDefaultSourceAndSafePlacement()
    {
        var first = new World(32, 32, 8, randomSeed: 90210);
        var second = new World(32, 32, 8, randomSeed: 90210);
        var initial = first.BuildScenarioInitialEcologyTelemetrySnapshot();

        Assert.Equal("habitat_aware", initial.InitialAnimalPolicy);
        Assert.Equal("runtime_default", initial.InitialAnimalPolicySource);
        Assert.Equal(0, initial.AnimalsOnWater);
        Assert.Equal(0, initial.AnimalsOnMovementBlockedTiles);
        Assert.Equal(
            first._animals.Select(animal => (animal.Kind, animal.Pos)).ToArray(),
            second._animals.Select(animal => (animal.Kind, animal.Pos)).ToArray());
        Assert.Equal(first.CreateEntityRng().Next(), second.CreateEntityRng().Next());
        Assert.DoesNotContain(first._animals, animal => first._people.Any(person => person.Health > 0f && person.Pos == animal.Pos));
        Assert.DoesNotContain(first._animals, animal => first._colonies.Any(colony => colony.Origin == animal.Pos));
        Assert.Equal(0, initial.PredatorsInPreyEmptyRegions);
        Assert.Equal(0, initial.PredatorsWithinHumanHarassRadius);
        Assert.Equal(0, first.TotalHerbivoreReplenishmentSpawns);
        Assert.Equal(0, first.TotalPredatorReplenishmentSpawns);
        Assert.Equal(0, first.BuildEcologyLifecycleCounters().EmergencyRescues);
        var fallbackCollection = Assert.IsAssignableFrom<ICollection<ScenarioInitialEcologyFallbackSnapshot>>(
            initial.InitialSeedingFallbacks);
        Assert.True(fallbackCollection.IsReadOnly);
    }

    [Fact]
    public void HabitatAware_ExplicitOptionsReportRuntimeOptionsSource()
    {
        var world = new World(
            32,
            32,
            8,
            brainFactory: null,
            randomSeed: 90211,
            InitialAnimalSeedingOptions.HabitatAwareDefault);

        var initial = world.BuildScenarioInitialEcologyTelemetrySnapshot();

        Assert.Equal("habitat_aware", initial.InitialAnimalPolicy);
        Assert.Equal("runtime_options", initial.InitialAnimalPolicySource);
    }

    [Fact]
    public void LegacyRandom_ExplicitComparePreservesRosterRngAndSource()
    {
        var world = new World(
            16,
            16,
            8,
            brainFactory: null,
            randomSeed: 42,
            InitialAnimalSeedingOptions.LegacyCompare);
        var expectedRoster = new[]
        {
            (AnimalKind.Herbivore, 11, 9),
            (AnimalKind.Herbivore, 14, 6),
            (AnimalKind.Predator, 1, 7),
            (AnimalKind.Predator, 10, 15),
            (AnimalKind.Predator, 4, 11),
            (AnimalKind.Herbivore, 8, 7),
            (AnimalKind.Herbivore, 2, 10),
            (AnimalKind.Herbivore, 7, 8),
            (AnimalKind.Herbivore, 10, 4),
            (AnimalKind.Herbivore, 7, 0)
        };

        Assert.Equal(expectedRoster, world._animals.Select(animal => (animal.Kind, animal.Pos.x, animal.Pos.y)).ToArray());
        Assert.Equal(313862347, world.CreateEntityRng().Next());
        Assert.Equal("legacy_random", world.BuildScenarioInitialEcologyTelemetrySnapshot().InitialAnimalPolicy);
        Assert.Equal("compare_override", world.BuildScenarioInitialEcologyTelemetrySnapshot().InitialAnimalPolicySource);
        Assert.Null(world.BuildScenarioInitialEcologyTelemetrySnapshot().InitialHerbivoreBudget);
        Assert.Null(world.BuildScenarioInitialEcologyTelemetrySnapshot().PreferredPersonOrColonyDistance);
    }

    [Fact]
    public void InitialEcologyTelemetry_PreStep5c2ObjectWithoutNewFieldsDeserializesSafely()
    {
        const string json = """
        {
          "initialAnimalPolicy": "legacy_random",
          "initialAnimalPolicySource": "runtime_default",
          "totalAnimals": 0,
          "herbivores": 0,
          "predators": 0,
          "predatorHerbivoreRatio": null,
          "animalsOnWater": 0,
          "animalsOnMovementBlockedTiles": 0,
          "viableRegions": 0,
          "viableRegionsWithoutHerbivores": 0,
          "predatorsInPreyEmptyRegions": 0,
          "herbivoresWithFoodInVision": 0,
          "predatorsWithPreyInVision": 0,
          "predatorsWithinHumanHarassRadius": 0,
          "predatorsWithinEarlyHumanContactRadius": 0,
          "foodVisionRadius": 5,
          "preyVisionRadius": 6,
          "humanHarassRadius": 2,
          "earlyHumanContactRadius": 6,
          "herbivoreToNearestFoodDistance": { "sampleCount": 0, "minimum": null, "maximum": null, "average": null },
          "predatorToNearestPreyDistance": { "sampleCount": 0, "minimum": null, "maximum": null, "average": null },
          "predatorToNearestPersonDistance": { "sampleCount": 0, "minimum": null, "maximum": null, "average": null },
          "regions": []
        }
        """;

        var snapshot = JsonSerializer.Deserialize<ScenarioInitialEcologyTelemetrySnapshot>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.InitialAnimalBudgetCeiling);
        Assert.Null(snapshot.InitialHerbivoreBudget);
        Assert.Null(snapshot.PreferredPersonOrColonyDistance);
        Assert.NotNull(snapshot.InitialSeedingFallbacks);
        Assert.Empty(snapshot.InitialSeedingFallbacks);
    }

    [Fact]
    public void InitialEcologyTelemetry_ExportsPersonOrColonyDistanceOutcome()
    {
        var world = new World(48, 48, 8, randomSeed: 90212);
        var initial = world.BuildScenarioInitialEcologyTelemetrySnapshot();

        Assert.Equal(7, initial.PreferredPersonOrColonyDistance);
        Assert.NotNull(initial.PredatorsWithinPreferredPersonOrColonyDistance);
        Assert.InRange(initial.PredatorsWithinPreferredPersonOrColonyDistance!.Value, 0, initial.Predators);
    }

    [Fact]
    public void InitialEcologyTelemetry_EmptyUsesHabitatDefaultAndSafeEmptyValues()
    {
        var empty = ScenarioInitialEcologyTelemetrySnapshot.Empty;

        Assert.Equal("habitat_aware", empty.InitialAnimalPolicy);
        Assert.Equal("runtime_default", empty.InitialAnimalPolicySource);
        Assert.Equal(0, empty.InitialAnimalBudgetCeiling);
        Assert.Null(empty.InitialHerbivoreBudget);
        Assert.Null(empty.InitialPredatorBudget);
        Assert.Null(empty.PreferredPersonOrColonyDistance);
        Assert.NotNull(empty.InitialSeedingFallbacks);
        Assert.Empty(empty.InitialSeedingFallbacks);
    }

    private static InitialAnimalSeedingInput BuildInput(
        InitialAnimalSeedingOptions? options = null,
        IReadOnlyList<InitialAnimalSeedingTileFact>? tiles = null,
        IReadOnlyList<(int x, int y)>? livingPeople = null,
        IReadOnlyList<(int x, int y)>? colonyOrigins = null,
        int width = 4,
        int height = 4,
        int hardPredatorPersonRadius = 2)
    {
        tiles ??= BuildLandTiles(width, height, new[] { (x: 0, y: 0) });
        var regions = tiles
            .GroupBy(tile => tile.RegionId)
            .OrderBy(group => group.Key)
            .Select(group => new EcologyRegionSnapshot(
                RegionId: group.Key,
                LandTileCount: group.Count(tile => tile.IsLand),
                WaterTileCount: group.Count(tile => !tile.IsLand),
                PlantBiomassTotal: group.Sum(tile => tile.PlantBiomass),
                PlantCapacityTotal: group.Sum(tile => tile.PlantCapacity),
                HerbivoreCount: 0,
                PredatorCount: 0,
                CarryingCapacity: group.Sum(tile => tile.PlantCapacity),
                OvergrazingPressure: 0f,
                SeasonModifier: 1f,
                DroughtModifier: 1f))
            .ToArray();

        return new InitialAnimalSeedingInput(
            Width: width,
            Height: height,
            Tiles: tiles,
            Regions: regions,
            LivingPeople: livingPeople ?? Array.Empty<(int x, int y)>(),
            ColonyOrigins: colonyOrigins ?? Array.Empty<(int x, int y)>(),
            HardPredatorPersonRadius: hardPredatorPersonRadius,
            Options: options ?? InitialAnimalSeedingOptions.HabitatAwareDefault);
    }

    private static IReadOnlyList<InitialAnimalSeedingTileFact> BuildLandTiles(
        int width,
        int height,
        IReadOnlyList<(int x, int y)> activeFood)
    {
        var food = activeFood.ToHashSet();
        var tiles = new List<InitialAnimalSeedingTileFact>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                tiles.Add(Tile(x, y, activeFood: food.Contains((x, y))));
        }

        return tiles;
    }

    private static InitialAnimalSeedingInput BuildLargeMapInput()
    {
        const int width = 256;
        const int height = 256;
        var tiles = BuildLandTiles(
            width,
            height,
            new[]
            {
                (x: 0, y: 0),
                (x: width - 1, y: 0),
                (x: 0, y: height - 1),
                (x: width - 1, y: height - 1)
            });
        return BuildInput(
            tiles: tiles,
            livingPeople: new[] { (x: 64, y: 64) },
            colonyOrigins: new[] { (x: 192, y: 192) },
            width: width,
            height: height);
    }

    private static InitialAnimalSeedingTileFact Tile(
        int x,
        int y,
        bool activeFood,
        bool movementBlocked = false,
        int regionId = 0)
        => new(
            X: x,
            Y: y,
            RegionId: regionId,
            IsLand: true,
            IsMovementBlocked: movementBlocked,
            IsLivingPersonOccupied: false,
            IsColonyOrigin: false,
            HasActiveFood: activeFood,
            Fertility: 0.75f,
            PlantBiomass: 0.75f,
            PlantCapacity: 0.75f);

    private static int GetFallback(InitialAnimalSeedingResult result, InitialAnimalSeedingFallbackReason reason)
        => result.Fallbacks.SingleOrDefault(fallback => fallback.Reason == reason)?.Count ?? 0;

    private static InitialAnimalPlacement Placement(
        AnimalKind kind,
        int x,
        int y,
        int regionId = 0,
        bool personOrColonyDistanceRelaxed = false,
        bool foodOrPreyRadiusRelaxed = false)
        => new(
            kind,
            (x, y),
            regionId,
            personOrColonyDistanceRelaxed,
            foodOrPreyRadiusRelaxed);

    private static InitialAnimalSeedingFallback Fallback(InitialAnimalSeedingFallbackReason reason, int count)
        => new(reason, count);

    private static void AssertAllocation(
        InitialAnimalSeedingResult actual,
        int expectedHerbivoreBudget,
        int expectedPredatorBudget,
        IReadOnlyList<InitialAnimalPlacement> expectedPlacements,
        IReadOnlyList<InitialAnimalSeedingFallback> expectedFallbacks)
    {
        Assert.Equal(expectedHerbivoreBudget, actual.HerbivoreBudget);
        Assert.Equal(expectedPredatorBudget, actual.PredatorBudget);
        Assert.Equal(expectedPlacements, actual.Placements);
        Assert.Equal(expectedFallbacks, actual.Fallbacks);
    }
}
