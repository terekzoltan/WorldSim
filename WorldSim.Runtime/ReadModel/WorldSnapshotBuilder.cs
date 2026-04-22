using System.Linq;
using WorldSim.Simulation;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;

namespace WorldSim.Runtime.ReadModel;

public static class WorldSnapshotBuilder
{
    public static WorldRenderSnapshot Build(World world)
    {
        var colonyFactionById = world._colonies.ToDictionary(colony => colony.Id, colony => colony.Faction);
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
                var ownerColonyId = world.GetTileOwnerColonyId(x, y);
                var ownerFactionId = ownerColonyId < 0
                    ? -1
                    : colonyFactionById.TryGetValue(ownerColonyId, out var ownerFaction)
                        ? (int)ownerFaction
                        : -1;
                if (World.IsActiveFoodNode(tile.Node))
                    activeFoodNodes++;
                else if (World.IsDepletedFoodNode(tile.Node))
                    depletedFoodNodes++;

                tiles.Add(new TileRenderData(
                    x,
                    y,
                    MapGround(tile.Ground),
                    MapResource(nodeType),
                    nodeAmount,
                    ownerFactionId,
                    world.IsTileContested(x, y),
                    world.GetTileOwnershipStrength(x, y),
                    world.GetFoodRegrowthProgress(x, y)));
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

        var defensiveStructures = world.DefensiveStructures
            .Where(structure => !structure.IsDestroyed)
            .Select(structure => new DefensiveStructureRenderData(
                structure.Pos.x,
                structure.Pos.y,
                structure.Owner.Id,
                MapDefensiveStructureKind(structure.Kind),
                structure.Hp,
                structure.MaxHp,
                structure.IsActive))
            .ToList();

        var people = world._people
            .Select(p => new PersonRenderData(
                p.Pos.x,
                p.Pos.y,
                p.Id,
                p.Home.Id,
                p.Health,
                p.IsInCombat,
                p.LastCombatTick,
                p.NoProgressStreak,
                p.BackoffTicksRemaining,
                p.DebugDecisionCause,
                p.DebugTargetKey,
                p.CombatMorale,
                p.IsRouting,
                p.RoutingTicksRemaining,
                p.ActiveCombatGroupId,
                p.ActiveBattleId,
                p.AssignedFormation.ToString(),
                p.IsCombatCommander,
                p.CommanderIntelligence,
                p.CommanderMoraleStabilityBonus))
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
                var averageCombatMorale = colonyPeople.Count == 0
                    ? 100f
                    : (float)colonyPeople.Average(person => person.CombatMorale);
                var profSummary = string.Join(",", colonyPeople
                    .Where(p => p.Age >= 16f)
                    .GroupBy(p => p.Profession)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => $"{g.Key}:{g.Count()}"));

                return new ColonyHudData(
                    colony.Id,
                    (int)colony.Faction,
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
                    profSummary,
                    world.GetColonyWarState(colony.Id).ToString(),
                    world.GetColonyWarriorCount(colony.Id),
                    colony.WeaponLevel,
                    colony.ArmorLevel,
                    averageCombatMorale
                );
            })
            .ToList();

        var combatGroups = world.GetActiveCombatGroups()
            .Select(group => new CombatGroupRenderData(
                group.GroupId,
                group.ColonyId,
                group.FactionId,
                group.Formation.ToString(),
                group.MemberCount,
                group.RoutingMemberCount,
                group.IsRouting,
                group.AverageMorale,
                group.CommanderActorId,
                group.CommanderIntelligence,
                group.CommanderMoraleStabilityBonus,
                group.AnchorX,
                group.AnchorY,
                group.StrengthScore,
                group.DefenseScore,
                group.BattleId))
            .ToList();

        var battles = world.GetActiveBattles()
            .Select(battle => new BattleRenderData(
                battle.BattleId,
                battle.LeftGroupId,
                battle.RightGroupId,
                battle.LeftAverageMorale,
                battle.RightAverageMorale,
                battle.LeftIsRouting,
                battle.RightIsRouting,
                battle.LeftCommanderActorId,
                battle.RightCommanderActorId,
                battle.CenterX,
                battle.CenterY,
                battle.Radius,
                battle.Intensity,
                battle.ElapsedTicks))
            .ToList();

        var sieges = world.GetActiveSieges()
            .Select(siege => new SiegeRenderData(
                siege.SiegeId,
                siege.AttackerColonyId,
                siege.DefenderColonyId,
                siege.TargetStructureId,
                MapDefensiveStructureKind(siege.TargetKind),
                siege.CenterX,
                siege.CenterY,
                siege.ActiveAttackerCount,
                siege.StartedTick,
                siege.LastActiveTick,
                siege.BreachCount,
                siege.Status))
            .ToList();

        var breaches = world.GetRecentBreaches()
            .Select(breach => new BreachRenderData(
                breach.StructureId,
                breach.DefenderColonyId,
                breach.AttackerColonyId,
                breach.X,
                breach.Y,
                breach.CreatedTick,
                MapDefensiveStructureKind(breach.StructureKind)))
            .ToList();

        var factionStances = world.GetFactionStanceMatrix()
            .Select(entry => new FactionStanceRenderData(
                (int)entry.Left,
                (int)entry.Right,
                entry.Stance.ToString()))
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
            ComputeFoodPerPersonSpread(world),
            world.ActiveSoftReservationCount,
            world.TotalOverlapResolveMoves,
            world.TotalCrowdDissipationMoves,
            world.TotalBirthFallbackToOccupiedCount,
            world.TotalBirthFallbackToParentCount,
            world.TotalBuildSiteResetCount,
            world.TotalNoProgressBackoffResource,
            world.TotalNoProgressBackoffBuild,
            world.TotalNoProgressBackoffFlee,
            world.TotalNoProgressBackoffCombat,
            world.DenseNeighborhoodTicks,
            world.LastTickDenseActors
        );

        return new WorldRenderSnapshot(
            world.Width,
            world.Height,
            tiles,
            houses,
            specializedBuildings,
            defensiveStructures,
            people,
            animals,
            colonies,
            combatGroups,
            battles,
            sieges,
            breaches,
            factionStances,
            ecology,
            MapSeason(world.CurrentSeason),
            world.IsDroughtActive,
            world.RecentEvents.ToList(),
            DirectorRenderState.Empty
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

    private static DefensiveStructureKindView MapDefensiveStructureKind(DefensiveStructureKind kind) => kind switch
    {
        DefensiveStructureKind.StoneWall => DefensiveStructureKindView.StoneWall,
        DefensiveStructureKind.ReinforcedWall => DefensiveStructureKindView.ReinforcedWall,
        DefensiveStructureKind.Gate => DefensiveStructureKindView.Gate,
        DefensiveStructureKind.Watchtower => DefensiveStructureKindView.Watchtower,
        DefensiveStructureKind.ArrowTower => DefensiveStructureKindView.ArrowTower,
        DefensiveStructureKind.CatapultTower => DefensiveStructureKindView.CatapultTower,
        _ => DefensiveStructureKindView.WoodWall
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
