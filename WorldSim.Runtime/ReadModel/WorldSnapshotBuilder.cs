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

        var people = world._people
            .Select(p => new PersonRenderData(p.Pos.x, p.Pos.y, p.Home.Id))
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
                    colonyPeople.Count,
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
            world.TotalPredatorHumanHits
        );

        return new WorldRenderSnapshot(
            world.Width,
            world.Height,
            tiles,
            houses,
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
}
