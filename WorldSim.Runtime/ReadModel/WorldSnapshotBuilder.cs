using System.Linq;
using WorldSim.Simulation;

namespace WorldSim.Runtime.ReadModel;

public static class WorldSnapshotBuilder
{
    public static WorldRenderSnapshot Build(World world)
    {
        var tiles = new List<TileRenderData>(world.Width * world.Height);
        var activeFoodNodes = 0;
        var depletedFoodNodes = 0;

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var tile = world.GetTile(x, y);
                var nodeType = tile.Node?.Type ?? Resource.None;
                var nodeAmount = tile.Node?.Amount ?? 0;
                if (nodeType == Resource.Food)
                {
                    if (nodeAmount > 0)
                        activeFoodNodes++;
                    else
                        depletedFoodNodes++;
                }

                tiles.Add(new TileRenderData(
                    x,
                    y,
                    MapGround(tile.Ground),
                    MapResource(nodeType),
                    nodeAmount));
            }
        }

        var houses = world.Houses
            .Select(h => new HouseRenderData(h.Pos.x, h.Pos.y, h.Owner.Id))
            .ToList();

        var specializedBuildings = world.SpecializedBuildings
            .Select(b => new SpecializedBuildingRenderData(
                b.Pos.x,
                b.Pos.y,
                b.Owner.Id,
                MapSpecializedBuildingKind(b.Kind)))
            .ToList();

        var people = world._people
            .Select(p => new PersonRenderData(
                p.Pos.x,
                p.Pos.y,
                p.Home.Id,
                Health: MathF.Round(Math.Clamp(p.Health, 0f, 150f), 2),
                IsInCombat: p.IsInCombat,
                LastCombatTick: p.LastCombatTick,
                IsWarrior: p.Roles.HasFlag(PersonRole.Warrior) || p.Profession == Profession.Hunter,
                Defense: MathF.Round(Math.Clamp(p.Defense, 0f, 100f), 2)))
            .ToList();

        var animals = world._animals
            .Select(a => new AnimalRenderData(a.Pos.x, a.Pos.y, MapAnimalKind(a.Kind)))
            .ToList();

        var colonies = world._colonies
            .Select(colony =>
            {
                var colonyPeople = world._people.Where(p => p.Home == colony).ToList();
                var avgHunger = colonyPeople.Count == 0
                    ? 0f
                    : colonyPeople.Average(p => p.Needs.GetValueOrDefault("Hunger", 0f));
                var avgStamina = colonyPeople.Count == 0
                    ? 0f
                    : colonyPeople.Average(p => p.Stamina);
                var foodPerPerson = colony.Stock[Resource.Food] / (float)Math.Max(1, colonyPeople.Count);
                var deathStats = world.GetColonyDeathStats(colony.Id);
                var profSummary = string.Join(",", colonyPeople
                    .Where(p => p.Age >= 16f)
                    .GroupBy(p => p.Profession)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => $"{g.Key}:{g.Count()}"));

                return new ColonyHudData(
                    colony.Id,
                    colony.Name,
                    colony.Morale,
                    colony.Stock[Resource.Food],
                    colony.Stock[Resource.Wood],
                    colony.Stock[Resource.Stone],
                    colony.Stock[Resource.Iron],
                    colony.Stock[Resource.Gold],
                    colony.HouseCount,
                    colony.FarmPlotCount,
                    colony.WorkshopCount,
                    colony.StorehouseCount,
                    colony.ToolCharges,
                    colonyPeople.Count,
                    foodPerPerson,
                    deathStats.OldAge,
                    deathStats.Starvation,
                    deathStats.Predator,
                    deathStats.Other,
                    avgHunger,
                    avgStamina,
                    profSummary
                );
            })
            .ToList();

        var ecology = new EcoHudData(
            world._animals.Count(a => a is Herbivore && a.IsAlive),
            world._animals.Count(a => a is Predator && a.IsAlive),
            activeFoodNodes,
            depletedFoodNodes,
            world._people.Count(p => p.Needs.GetValueOrDefault("Hunger", 0f) >= 85f),
            world.TotalAnimalStuckRecoveries,
            world.TotalPredatorDeaths,
            world.TotalPredatorHumanHits,
            world.TotalDeathsOldAge,
            world.TotalDeathsStarvation,
            world.TotalDeathsPredator,
            world.TotalDeathsOther,
            world.RecentDeathsStarvation60s,
            world.TotalStarvationDeathsWithFood,
            world.EnablePredatorHumanAttacks,
            ComputeAverageFoodPerPerson(world),
            world._colonies.Count(c => c.Stock[Resource.Food] <= Math.Max(3, world._people.Count(p => p.Home == c && p.Health > 0f) / 2)),
            ComputeFoodPerPersonSpread(world)
        );

        return new WorldRenderSnapshot(
            world.Width,
            world.Height,
            tiles,
            houses,
            specializedBuildings,
            people,
            animals,
            colonies,
            ecology,
            MapSeason(world.CurrentSeason),
            world.IsDroughtActive,
            world.RecentEvents.ToList()
        );
    }

    private static TileGroundView MapGround(Ground ground) => ground switch
    {
        Ground.Water => TileGroundView.Water,
        Ground.Grass => TileGroundView.Grass,
        _ => TileGroundView.Dirt
    };

    private static ResourceView MapResource(Resource resource) => resource switch
    {
        Resource.Wood => ResourceView.Wood,
        Resource.Stone => ResourceView.Stone,
        Resource.Iron => ResourceView.Iron,
        Resource.Gold => ResourceView.Gold,
        Resource.Food => ResourceView.Food,
        Resource.Water => ResourceView.Water,
        _ => ResourceView.None
    };

    private static AnimalKindView MapAnimalKind(AnimalKind kind) => kind switch
    {
        AnimalKind.Predator => AnimalKindView.Predator,
        _ => AnimalKindView.Herbivore
    };

    private static SeasonView MapSeason(Season season) => season switch
    {
        Season.Summer => SeasonView.Summer,
        Season.Autumn => SeasonView.Autumn,
        Season.Winter => SeasonView.Winter,
        _ => SeasonView.Spring
    };

    private static SpecializedBuildingKindView MapSpecializedBuildingKind(SpecializedBuildingKind kind) => kind switch
    {
        SpecializedBuildingKind.Workshop => SpecializedBuildingKindView.Workshop,
        SpecializedBuildingKind.Storehouse => SpecializedBuildingKindView.Storehouse,
        _ => SpecializedBuildingKindView.FarmPlot
    };

    private static float ComputeAverageFoodPerPerson(World world)
    {
        int livingPeople = world._people.Count(p => p.Health > 0f);
        if (livingPeople == 0)
            return 0f;

        int totalFood = world._colonies.Sum(c => c.Stock[Resource.Food]);
        return totalFood / (float)livingPeople;
    }

    private static float ComputeFoodPerPersonSpread(World world)
    {
        var values = world._colonies
            .Select(colony =>
            {
                int living = world._people.Count(p => p.Home == colony && p.Health > 0f);
                return colony.Stock[Resource.Food] / (float)Math.Max(1, living);
            })
            .ToList();

        if (values.Count <= 1)
            return 0f;

        return values.Max() - values.Min();
    }
}
