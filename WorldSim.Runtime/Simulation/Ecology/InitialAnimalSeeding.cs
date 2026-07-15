using System.Collections.ObjectModel;

namespace WorldSim.Simulation.Ecology;

public enum InitialAnimalSeedingPolicy
{
    HabitatAware,
    LegacyCompare
}

public sealed record InitialAnimalSeedingOptions
{
    public static InitialAnimalSeedingOptions HabitatAwareDefault { get; } = new();

    public static InitialAnimalSeedingOptions LegacyCompare { get; } = new()
    {
        Policy = InitialAnimalSeedingPolicy.LegacyCompare
    };

    public InitialAnimalSeedingPolicy Policy { get; init; } = InitialAnimalSeedingPolicy.HabitatAware;
    public int AreaTilesPerAnimal { get; init; } = 256;
    public int PreferredPersonOrColonyDistance { get; init; } = 7;
    public int PreferredHerbivoreFoodRadius { get; init; } = 5;
    public int PreferredPredatorPreyRadius { get; init; } = 6;

    internal void Validate()
    {
        if (!Enum.IsDefined(Policy))
            throw new ArgumentOutOfRangeException(nameof(Policy), Policy, "Unknown initial animal seeding policy.");
        if (AreaTilesPerAnimal <= 0)
            throw new ArgumentOutOfRangeException(nameof(AreaTilesPerAnimal), AreaTilesPerAnimal, "Area tiles per animal must be positive.");
        if (PreferredPersonOrColonyDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(PreferredPersonOrColonyDistance), PreferredPersonOrColonyDistance, "Preferred distance cannot be negative.");
        if (PreferredHerbivoreFoodRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(PreferredHerbivoreFoodRadius), PreferredHerbivoreFoodRadius, "Preferred food radius cannot be negative.");
        if (PreferredPredatorPreyRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(PreferredPredatorPreyRadius), PreferredPredatorPreyRadius, "Preferred prey radius cannot be negative.");
    }
}

internal static class InitialAnimalSeedingWireValues
{
    internal const string HabitatAware = "habitat_aware";
    internal const string LegacyRandom = "legacy_random";
    internal const string RuntimeDefault = "runtime_default";
    internal const string RuntimeOptions = "runtime_options";
    internal const string CompareOverride = "compare_override";

    internal static string Policy(InitialAnimalSeedingPolicy policy)
        => policy switch
        {
            InitialAnimalSeedingPolicy.HabitatAware => HabitatAware,
            InitialAnimalSeedingPolicy.LegacyCompare => LegacyRandom,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown initial animal seeding policy.")
        };
}

internal enum InitialAnimalSeedingFallbackReason
{
    AnimalCeilingUnallocated,
    HerbivoreBudgetUnfilled,
    PredatorBudgetUnfilled,
    HerbivorePersonOrColonyDistanceRelaxed,
    HerbivoreFoodRadiusRelaxed,
    PredatorPersonOrColonyDistanceRelaxed,
    PredatorPreyRadiusRelaxed
}

internal static class InitialAnimalSeedingFallbackReasonFormatter
{
    internal static string ToWireValue(this InitialAnimalSeedingFallbackReason reason)
        => reason switch
        {
            InitialAnimalSeedingFallbackReason.AnimalCeilingUnallocated => "animal_ceiling_unallocated",
            InitialAnimalSeedingFallbackReason.HerbivoreBudgetUnfilled => "herbivore_budget_unfilled",
            InitialAnimalSeedingFallbackReason.PredatorBudgetUnfilled => "predator_budget_unfilled",
            InitialAnimalSeedingFallbackReason.HerbivorePersonOrColonyDistanceRelaxed => "herbivore_person_or_colony_distance_relaxed",
            InitialAnimalSeedingFallbackReason.HerbivoreFoodRadiusRelaxed => "herbivore_food_radius_relaxed",
            InitialAnimalSeedingFallbackReason.PredatorPersonOrColonyDistanceRelaxed => "predator_person_or_colony_distance_relaxed",
            InitialAnimalSeedingFallbackReason.PredatorPreyRadiusRelaxed => "predator_prey_radius_relaxed",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown initial animal seeding fallback reason.")
        };
}

internal sealed record InitialAnimalSeedingTileFact(
    int X,
    int Y,
    int RegionId,
    bool IsLand,
    bool IsMovementBlocked,
    bool IsLivingPersonOccupied,
    bool IsColonyOrigin,
    bool HasActiveFood,
    float Fertility,
    float PlantBiomass,
    float PlantCapacity)
{
    internal (int x, int y) Pos => (X, Y);
}

internal sealed record InitialAnimalSeedingInput(
    int Width,
    int Height,
    IReadOnlyList<InitialAnimalSeedingTileFact> Tiles,
    IReadOnlyList<EcologyRegionSnapshot> Regions,
    IReadOnlyList<(int x, int y)> LivingPeople,
    IReadOnlyList<(int x, int y)> ColonyOrigins,
    int HardPredatorPersonRadius,
    InitialAnimalSeedingOptions Options);

internal sealed record InitialAnimalPlacement(
    AnimalKind Kind,
    (int x, int y) Pos,
    int RegionId,
    bool PersonOrColonyDistanceRelaxed,
    bool FoodOrPreyRadiusRelaxed);

internal sealed record InitialAnimalSeedingFallback(
    InitialAnimalSeedingFallbackReason Reason,
    int Count);

internal sealed record InitialAnimalSeedingWorkMetrics(
    int TileFactCount,
    int HerbivorePoolCount,
    int PredatorCatalogCandidateCount,
    int PredatorCatalogTileVisits,
    int AllocationPrefixesEvaluated,
    int PredatorCandidateScoreEvaluations,
    int PredatorMaterializationPasses,
    int PeakRetainedAllocationChoices)
{
    internal static InitialAnimalSeedingWorkMetrics Empty { get; } = new(
        TileFactCount: 0,
        HerbivorePoolCount: 0,
        PredatorCatalogCandidateCount: 0,
        PredatorCatalogTileVisits: 0,
        AllocationPrefixesEvaluated: 0,
        PredatorCandidateScoreEvaluations: 0,
        PredatorMaterializationPasses: 0,
        PeakRetainedAllocationChoices: 0);
}

internal sealed record InitialAnimalSeedingResult(
    InitialAnimalSeedingPolicy Policy,
    int AnimalCeiling,
    int HerbivoreBudget,
    int PredatorBudget,
    int InitialHerbivoresSpawned,
    int InitialPredatorsSpawned,
    int AnimalCeilingUnallocated,
    int HerbivoreBudgetUnfilled,
    int PredatorBudgetUnfilled,
    IReadOnlyList<InitialAnimalPlacement> Placements,
    IReadOnlyList<InitialAnimalSeedingFallback> Fallbacks)
{
    internal int InitialSeedingFallbackCount => Fallbacks.Sum(fallback => fallback.Count);
    internal InitialAnimalSeedingWorkMetrics WorkMetrics { get; init; } = InitialAnimalSeedingWorkMetrics.Empty;

    internal static InitialAnimalSeedingResult Legacy(int animalCeiling, int herbivores, int predators)
        => new(
            InitialAnimalSeedingPolicy.LegacyCompare,
            animalCeiling,
            HerbivoreBudget: 0,
            PredatorBudget: 0,
            InitialHerbivoresSpawned: herbivores,
            InitialPredatorsSpawned: predators,
            AnimalCeilingUnallocated: 0,
            HerbivoreBudgetUnfilled: 0,
            PredatorBudgetUnfilled: 0,
            Placements: Array.Empty<InitialAnimalPlacement>(),
            Fallbacks: Array.Empty<InitialAnimalSeedingFallback>());
}

internal static class InitialAnimalSeeder
{
    internal static InitialAnimalSeedingResult Plan(InitialAnimalSeedingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Options);
        input.Options.Validate();
        if (input.Options.Policy != InitialAnimalSeedingPolicy.HabitatAware)
            throw new ArgumentException("The deterministic planner only accepts the habitat-aware policy.", nameof(input));

        var animalCeiling = CalculateAnimalCeiling(input.Width, input.Height, input.Options.AreaTilesPerAnimal);
        var regionById = input.Regions
            .OrderBy(region => region.RegionId)
            .ToDictionary(region => region.RegionId);
        var livingPeople = input.LivingPeople.OrderBy(pos => pos.y).ThenBy(pos => pos.x).ToArray();
        var colonyOrigins = input.ColonyOrigins.OrderBy(pos => pos.y).ThenBy(pos => pos.x).ToArray();
        var personOrColonyTargets = livingPeople
            .Concat(colonyOrigins)
            .Distinct()
            .OrderBy(pos => pos.y)
            .ThenBy(pos => pos.x)
            .ToArray();
        var hardOccupied = personOrColonyTargets.ToHashSet();
        var tilesByRegion = input.Tiles
            .GroupBy(tile => tile.RegionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<InitialAnimalSeedingTileFact>)group.ToArray());
        var activeFoodByRegion = tilesByRegion
            .Select(entry => new
            {
                entry.Key,
                Positions = entry.Value
                    .Where(tile => tile.HasActiveFood && tile.IsLand && !tile.IsMovementBlocked)
                    .Select(tile => tile.Pos)
                    .OrderBy(pos => pos.y)
                    .ThenBy(pos => pos.x)
                    .ToArray()
            })
            .Where(entry => entry.Positions.Length > 0)
            .ToDictionary(entry => entry.Key, entry => entry.Positions);

        var herbivoreCandidates = new Dictionary<int, IReadOnlyList<PlacementCandidate>>();
        var herbivoreLimits = new Dictionary<int, int>();
        foreach (var region in input.Regions.OrderBy(region => region.RegionId))
        {
            if (!activeFoodByRegion.TryGetValue(region.RegionId, out var foodPositions) || foodPositions.Length == 0)
                continue;
            if (!tilesByRegion.TryGetValue(region.RegionId, out var regionTiles))
                continue;

            var candidates = regionTiles
                .Where(tile => IsHardSafe(tile)
                    && !hardOccupied.Contains(tile.Pos))
                .Select(tile => BuildCandidate(
                    tile,
                    nearestResourceDistance: NearestDistance(tile.Pos, foodPositions),
                    nearestPersonOrColonyDistance: NearestDistance(tile.Pos, personOrColonyTargets),
                    preferredResourceRadius: input.Options.PreferredHerbivoreFoodRadius,
                    preferredPersonOrColonyDistance: input.Options.PreferredPersonOrColonyDistance))
                .OrderBy(candidate => candidate.FallbackRank)
                .ThenBy(candidate => candidate.NearestResourceDistance)
                .ThenByDescending(candidate => candidate.Tile.PlantBiomass)
                .ThenByDescending(candidate => candidate.Tile.PlantCapacity)
                .ThenByDescending(candidate => candidate.Tile.Fertility)
                .ThenBy(candidate => candidate.Tile.Y)
                .ThenBy(candidate => candidate.Tile.X)
                .ToArray();
            var capacity = Math.Min(World.GetHerbivoreCapacityLimit(region), candidates.Length);
            if (capacity <= 0)
                continue;

            herbivoreCandidates[region.RegionId] = candidates;
            herbivoreLimits[region.RegionId] = capacity;
        }

        var workMetrics = new WorkMetricsCounter(input.Tiles.Count);
        var predatorCatalog = BuildPredatorCandidateCatalog(
            input.Tiles,
            regionById,
            livingPeople,
            hardOccupied,
            input.HardPredatorPersonRadius,
            workMetrics);
        var herbivorePool = BuildRoundRobinPlacements(
            herbivoreCandidates,
            herbivoreLimits,
            AnimalKind.Herbivore,
            animalCeiling);
        var selected = SelectAllocation(
            herbivorePool,
            animalCeiling,
            predatorCatalog,
            regionById,
            workMetrics);
        var herbivores = herbivorePool.Take(selected.HerbivoreCount).ToArray();
        var predators = BuildPredatorPlacements(
            predatorCatalog,
            livingPeople,
            personOrColonyTargets,
            input.Options,
            herbivores,
            workMetrics);
        if (predators.Count != selected.PredatorCount)
            throw new InvalidOperationException("Initial animal seeding scalar predator count did not match final materialization.");

        var placements = herbivores.Concat(predators).ToArray();
        var herbivoreBudget = selected.HerbivoreCount;
        var predatorBudget = selected.PredatorCount;
        var initialHerbivoresSpawned = herbivores.Length;
        var initialPredatorsSpawned = predators.Count;
        var animalCeilingUnallocated = Math.Max(0, animalCeiling - herbivoreBudget - predatorBudget);
        var herbivoreBudgetUnfilled = Math.Max(0, herbivoreBudget - initialHerbivoresSpawned);
        var predatorBudgetUnfilled = Math.Max(0, predatorBudget - initialPredatorsSpawned);
        var fallbacks = BuildFallbacks(
            herbivores,
            predators,
            animalCeilingUnallocated,
            herbivoreBudgetUnfilled,
            predatorBudgetUnfilled);

        return new InitialAnimalSeedingResult(
            InitialAnimalSeedingPolicy.HabitatAware,
            animalCeiling,
            herbivoreBudget,
            predatorBudget,
            initialHerbivoresSpawned,
            initialPredatorsSpawned,
            animalCeilingUnallocated,
            herbivoreBudgetUnfilled,
            predatorBudgetUnfilled,
            Array.AsReadOnly(placements),
            Array.AsReadOnly(fallbacks))
        {
            WorkMetrics = workMetrics.ToSnapshot(herbivorePool.Count)
        };
    }

    internal static int CalculateAnimalCeiling(int width, int height, int areaTilesPerAnimal)
    {
        if (areaTilesPerAnimal <= 0)
            throw new ArgumentOutOfRangeException(nameof(areaTilesPerAnimal));

        var area = (long)Math.Max(0, width) * Math.Max(0, height);
        return (int)Math.Min(int.MaxValue, Math.Max(10L, area / areaTilesPerAnimal));
    }

    private static IReadOnlyDictionary<int, PredatorRegionCatalog> BuildPredatorCandidateCatalog(
        IReadOnlyList<InitialAnimalSeedingTileFact> tiles,
        IReadOnlyDictionary<int, EcologyRegionSnapshot> regionById,
        IReadOnlyList<(int x, int y)> livingPeople,
        IReadOnlySet<(int x, int y)> hardOccupied,
        int hardPredatorPersonRadius,
        WorkMetricsCounter workMetrics)
    {
        var candidatesByRegion = new Dictionary<int, List<InitialAnimalSeedingTileFact>>();
        foreach (var tile in tiles)
        {
            workMetrics.RecordPredatorCatalogTileVisit();
            if (!regionById.ContainsKey(tile.RegionId)
                || !IsHardSafe(tile)
                || hardOccupied.Contains(tile.Pos)
                || NearestDistance(tile.Pos, livingPeople) <= hardPredatorPersonRadius)
                continue;

            if (!candidatesByRegion.TryGetValue(tile.RegionId, out var candidates))
            {
                candidates = new List<InitialAnimalSeedingTileFact>();
                candidatesByRegion[tile.RegionId] = candidates;
            }

            candidates.Add(tile);
            workMetrics.RecordPredatorCatalogCandidate();
        }

        return candidatesByRegion.ToDictionary(
            entry => entry.Key,
            entry => new PredatorRegionCatalog(
                regionById[entry.Key],
                Array.AsReadOnly(entry.Value.ToArray()),
                entry.Value.Select(tile => tile.Pos).ToHashSet()));
    }

    private static AllocationChoice SelectAllocation(
        IReadOnlyList<InitialAnimalPlacement> herbivorePool,
        int animalCeiling,
        IReadOnlyDictionary<int, PredatorRegionCatalog> predatorCatalog,
        IReadOnlyDictionary<int, EcologyRegionSnapshot> regionById,
        WorkMetricsCounter workMetrics)
    {
        var herbivoreCountByRegion = new Dictionary<int, int>();
        var availablePredatorCandidateCountByRegion = predatorCatalog.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Tiles.Count);
        var predatorCountByRegion = new Dictionary<int, int>();
        var totalPredatorCount = 0;
        AllocationChoice? bestAnyAllocation = null;
        AllocationChoice? bestPredatorCapableAllocation = null;
        var maxHerbivores = Math.Min(animalCeiling, herbivorePool.Count);

        for (var herbivorePrefixCount = 0; herbivorePrefixCount <= maxHerbivores; herbivorePrefixCount++)
        {
            if (herbivorePrefixCount > 0)
            {
                var addedHerbivore = herbivorePool[herbivorePrefixCount - 1];
                var regionId = addedHerbivore.RegionId;
                var oldPredatorCount = predatorCountByRegion.GetValueOrDefault(regionId);
                var selectedHerbivoreCount = herbivoreCountByRegion.GetValueOrDefault(regionId) + 1;
                herbivoreCountByRegion[regionId] = selectedHerbivoreCount;

                if (predatorCatalog.TryGetValue(regionId, out var catalog)
                    && catalog.Positions.Contains(addedHerbivore.Pos))
                {
                    availablePredatorCandidateCountByRegion[regionId]--;
                }

                var newPredatorCount = 0;
                if (regionById.TryGetValue(regionId, out var baseRegion))
                {
                    var region = baseRegion with
                    {
                        HerbivoreCount = selectedHerbivoreCount,
                        PredatorCount = 0
                    };
                    newPredatorCount = Math.Min(
                        World.GetPredatorCapacityLimit(region),
                        availablePredatorCandidateCountByRegion.GetValueOrDefault(regionId));
                }

                predatorCountByRegion[regionId] = newPredatorCount;
                totalPredatorCount += newPredatorCount - oldPredatorCount;
            }

            workMetrics.RecordAllocationPrefixEvaluation();
            if ((long)herbivorePrefixCount + totalPredatorCount <= animalCeiling)
            {
                var candidate = new AllocationChoice(herbivorePrefixCount, totalPredatorCount);
                if (IsBetterAllocation(candidate, bestAnyAllocation))
                    bestAnyAllocation = candidate;
                if (candidate.PredatorCount > 0
                    && IsBetterAllocation(candidate, bestPredatorCapableAllocation))
                {
                    bestPredatorCapableAllocation = candidate;
                }
            }

            workMetrics.ObserveRetainedAllocationChoices(
                (bestAnyAllocation.HasValue ? 1 : 0)
                + (bestPredatorCapableAllocation.HasValue ? 1 : 0));
        }

        return bestPredatorCapableAllocation ?? bestAnyAllocation ?? AllocationChoice.Empty;
    }

    private static bool IsBetterAllocation(AllocationChoice candidate, AllocationChoice? current)
    {
        if (!current.HasValue)
            return true;

        var candidateTotal = (long)candidate.HerbivoreCount + candidate.PredatorCount;
        var currentTotal = (long)current.Value.HerbivoreCount + current.Value.PredatorCount;
        return candidateTotal > currentTotal
            || (candidateTotal == currentTotal && candidate.HerbivoreCount > current.Value.HerbivoreCount);
    }

    private static IReadOnlyList<InitialAnimalPlacement> BuildPredatorPlacements(
        IReadOnlyDictionary<int, PredatorRegionCatalog> predatorCatalog,
        IReadOnlyList<(int x, int y)> livingPeople,
        IReadOnlyList<(int x, int y)> personOrColonyTargets,
        InitialAnimalSeedingOptions options,
        IReadOnlyList<InitialAnimalPlacement> herbivores,
        WorkMetricsCounter workMetrics)
    {
        if (herbivores.Count == 0)
            return Array.Empty<InitialAnimalPlacement>();

        workMetrics.RecordPredatorMaterializationPass();

        var reserved = herbivores.Select(placement => placement.Pos).ToHashSet();
        var preyByRegion = herbivores
            .GroupBy(placement => placement.RegionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(placement => placement.Pos).OrderBy(pos => pos.y).ThenBy(pos => pos.x).ToArray());
        var candidatesByRegion = new Dictionary<int, IReadOnlyList<PlacementCandidate>>();
        var limitsByRegion = new Dictionary<int, int>();
        foreach (var entry in preyByRegion.OrderBy(entry => entry.Key))
        {
            if (!predatorCatalog.TryGetValue(entry.Key, out var catalog))
                continue;

            var herbivoreCount = entry.Value.Length;
            var region = catalog.Region with { HerbivoreCount = herbivoreCount, PredatorCount = 0 };
            var capacity = World.GetPredatorCapacityLimit(region);
            if (capacity <= 0)
                continue;

            var candidates = catalog.Tiles
                .Where(tile => !reserved.Contains(tile.Pos))
                .Select(tile =>
                {
                    workMetrics.RecordPredatorCandidateScoreEvaluation();
                    return BuildCandidate(
                        tile,
                        nearestResourceDistance: NearestDistance(tile.Pos, entry.Value),
                        nearestPersonOrColonyDistance: NearestDistance(tile.Pos, personOrColonyTargets),
                        preferredResourceRadius: options.PreferredPredatorPreyRadius,
                        preferredPersonOrColonyDistance: options.PreferredPersonOrColonyDistance);
                })
                .OrderBy(candidate => candidate.FallbackRank)
                .ThenBy(candidate => candidate.NearestResourceDistance)
                .ThenByDescending(candidate => candidate.NearestPersonOrColonyDistance)
                .ThenBy(candidate => candidate.Tile.Y)
                .ThenBy(candidate => candidate.Tile.X)
                .ToArray();
            capacity = Math.Min(capacity, candidates.Length);
            if (capacity <= 0)
                continue;

            candidatesByRegion[entry.Key] = candidates;
            limitsByRegion[entry.Key] = capacity;
        }

        return BuildRoundRobinPlacements(
            candidatesByRegion,
            limitsByRegion,
            AnimalKind.Predator,
            int.MaxValue);
    }

    private static List<InitialAnimalPlacement> BuildRoundRobinPlacements(
        IReadOnlyDictionary<int, IReadOnlyList<PlacementCandidate>> candidatesByRegion,
        IReadOnlyDictionary<int, int> limitsByRegion,
        AnimalKind kind,
        int maximum)
    {
        var placements = new List<InitialAnimalPlacement>();
        var indexes = candidatesByRegion.Keys.ToDictionary(regionId => regionId, _ => 0);
        var regionCounts = candidatesByRegion.Keys.ToDictionary(regionId => regionId, _ => 0);
        var regionIds = candidatesByRegion.Keys.OrderBy(regionId => regionId).ToArray();
        while (placements.Count < maximum)
        {
            var added = false;
            foreach (var regionId in regionIds)
            {
                if (placements.Count >= maximum)
                    break;
                if (regionCounts[regionId] >= limitsByRegion[regionId])
                    continue;

                var candidates = candidatesByRegion[regionId];
                if (indexes[regionId] >= candidates.Count)
                    continue;

                var candidate = candidates[indexes[regionId]++];
                regionCounts[regionId]++;
                placements.Add(new InitialAnimalPlacement(
                    kind,
                    candidate.Tile.Pos,
                    regionId,
                    candidate.PersonOrColonyDistanceRelaxed,
                    candidate.ResourceRadiusRelaxed));
                added = true;
            }

            if (!added)
                break;
        }

        return placements;
    }

    private static InitialAnimalSeedingFallback[] BuildFallbacks(
        IReadOnlyList<InitialAnimalPlacement> herbivores,
        IReadOnlyList<InitialAnimalPlacement> predators,
        int animalCeilingUnallocated,
        int herbivoreBudgetUnfilled,
        int predatorBudgetUnfilled)
    {
        var counts = new Dictionary<InitialAnimalSeedingFallbackReason, int>();
        Add(counts, InitialAnimalSeedingFallbackReason.AnimalCeilingUnallocated, animalCeilingUnallocated);
        Add(counts, InitialAnimalSeedingFallbackReason.HerbivoreBudgetUnfilled, herbivoreBudgetUnfilled);
        Add(counts, InitialAnimalSeedingFallbackReason.PredatorBudgetUnfilled, predatorBudgetUnfilled);
        Add(
            counts,
            InitialAnimalSeedingFallbackReason.HerbivorePersonOrColonyDistanceRelaxed,
            herbivores.Count(placement => placement.PersonOrColonyDistanceRelaxed));
        Add(
            counts,
            InitialAnimalSeedingFallbackReason.HerbivoreFoodRadiusRelaxed,
            herbivores.Count(placement => placement.FoodOrPreyRadiusRelaxed));
        Add(
            counts,
            InitialAnimalSeedingFallbackReason.PredatorPersonOrColonyDistanceRelaxed,
            predators.Count(placement => placement.PersonOrColonyDistanceRelaxed));
        Add(
            counts,
            InitialAnimalSeedingFallbackReason.PredatorPreyRadiusRelaxed,
            predators.Count(placement => placement.FoodOrPreyRadiusRelaxed));

        return counts
            .Where(entry => entry.Value > 0)
            .OrderBy(entry => entry.Key.ToWireValue(), StringComparer.Ordinal)
            .Select(entry => new InitialAnimalSeedingFallback(entry.Key, entry.Value))
            .ToArray();
    }

    private static void Add(
        IDictionary<InitialAnimalSeedingFallbackReason, int> counts,
        InitialAnimalSeedingFallbackReason reason,
        int count)
    {
        if (count > 0)
            counts[reason] = count;
    }

    private static bool IsHardSafe(InitialAnimalSeedingTileFact tile)
        => tile.IsLand
            && !tile.IsMovementBlocked
            && !tile.IsLivingPersonOccupied
            && !tile.IsColonyOrigin;

    private static PlacementCandidate BuildCandidate(
        InitialAnimalSeedingTileFact tile,
        int nearestResourceDistance,
        int nearestPersonOrColonyDistance,
        int preferredResourceRadius,
        int preferredPersonOrColonyDistance)
    {
        var personRelaxed = nearestPersonOrColonyDistance < preferredPersonOrColonyDistance;
        var resourceRelaxed = nearestResourceDistance > preferredResourceRadius;
        var fallbackRank = (personRelaxed ? 1 : 0) + (resourceRelaxed ? 2 : 0);
        return new PlacementCandidate(
            tile,
            nearestResourceDistance,
            nearestPersonOrColonyDistance,
            personRelaxed,
            resourceRelaxed,
            fallbackRank);
    }

    private static int NearestDistance(
        (int x, int y) source,
        IReadOnlyList<(int x, int y)> targets)
    {
        if (targets.Count == 0)
            return int.MaxValue;

        var nearest = int.MaxValue;
        foreach (var target in targets)
            nearest = Math.Min(nearest, Math.Abs(source.x - target.x) + Math.Abs(source.y - target.y));
        return nearest;
    }

    private sealed record PlacementCandidate(
        InitialAnimalSeedingTileFact Tile,
        int NearestResourceDistance,
        int NearestPersonOrColonyDistance,
        bool PersonOrColonyDistanceRelaxed,
        bool ResourceRadiusRelaxed,
        int FallbackRank);

    private sealed record PredatorRegionCatalog(
        EcologyRegionSnapshot Region,
        IReadOnlyList<InitialAnimalSeedingTileFact> Tiles,
        IReadOnlySet<(int x, int y)> Positions);

    private readonly record struct AllocationChoice(int HerbivoreCount, int PredatorCount)
    {
        internal static AllocationChoice Empty => new(0, 0);
    }

    private sealed class WorkMetricsCounter
    {
        private readonly int _tileFactCount;
        private int _predatorCatalogCandidateCount;
        private int _predatorCatalogTileVisits;
        private int _allocationPrefixesEvaluated;
        private int _predatorCandidateScoreEvaluations;
        private int _predatorMaterializationPasses;
        private int _peakRetainedAllocationChoices;

        internal WorkMetricsCounter(int tileFactCount)
        {
            _tileFactCount = tileFactCount;
        }

        internal void RecordPredatorCatalogCandidate() => _predatorCatalogCandidateCount++;
        internal void RecordPredatorCatalogTileVisit() => _predatorCatalogTileVisits++;
        internal void RecordAllocationPrefixEvaluation() => _allocationPrefixesEvaluated++;
        internal void RecordPredatorCandidateScoreEvaluation() => _predatorCandidateScoreEvaluations++;
        internal void RecordPredatorMaterializationPass() => _predatorMaterializationPasses++;

        internal void ObserveRetainedAllocationChoices(int count)
            => _peakRetainedAllocationChoices = Math.Max(_peakRetainedAllocationChoices, count);

        internal InitialAnimalSeedingWorkMetrics ToSnapshot(int herbivorePoolCount)
            => new(
                _tileFactCount,
                herbivorePoolCount,
                _predatorCatalogCandidateCount,
                _predatorCatalogTileVisits,
                _allocationPrefixesEvaluated,
                _predatorCandidateScoreEvaluations,
                _predatorMaterializationPasses,
                _peakRetainedAllocationChoices);
    }
}
