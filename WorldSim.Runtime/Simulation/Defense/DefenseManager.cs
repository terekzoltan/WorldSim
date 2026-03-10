using System;
using System.Linq;

namespace WorldSim.Simulation.Defense;

public sealed class DefenseManager
{
    public void Tick(World world, float dt)
    {
        foreach (var structure in world.DefensiveStructures)
        {
            if (structure.IsDestroyed)
                continue;

            structure.IsActive = TryPayUpkeep(structure);
            if (!structure.IsActive)
                continue;

            switch (structure)
            {
                case Watchtower watchtower:
                    TickWatchtower(world, watchtower, dt);
                    break;
                case ArrowTower arrowTower:
                    TickArrowTower(world, arrowTower, dt);
                    break;
                case CatapultTower catapultTower:
                    TickCatapultTower(world, catapultTower, dt);
                    break;
            }
        }

        world.RemoveDestroyedDefensiveStructures();
    }

    private static bool TryPayUpkeep(DefensiveStructure structure)
    {
        if (structure.UpkeepWoodPerTick <= 0 && structure.UpkeepStonePerTick <= 0 && structure.UpkeepGoldPerTick <= 0)
            return true;

        var stock = structure.Owner.Stock;
        if (stock.GetValueOrDefault(Resource.Wood) < structure.UpkeepWoodPerTick
            || stock.GetValueOrDefault(Resource.Stone) < structure.UpkeepStonePerTick
            || stock.GetValueOrDefault(Resource.Gold) < structure.UpkeepGoldPerTick)
        {
            return false;
        }

        if (structure.UpkeepWoodPerTick > 0)
            stock[Resource.Wood] -= structure.UpkeepWoodPerTick;
        if (structure.UpkeepStonePerTick > 0)
            stock[Resource.Stone] -= structure.UpkeepStonePerTick;
        if (structure.UpkeepGoldPerTick > 0)
            stock[Resource.Gold] -= structure.UpkeepGoldPerTick;

        return true;
    }

    private static void TickWatchtower(World world, Watchtower tower, float dt)
    {
        tower.CooldownRemainingSeconds = Math.Max(0f, tower.CooldownRemainingSeconds - dt);
        if (tower.CooldownRemainingSeconds > 0f)
            return;

        if (TryShootHostilePerson(world, tower.Pos, tower.Owner, tower.RangeTiles, tower.ShotDamage))
        {
            tower.CooldownRemainingSeconds = tower.CooldownSeconds;
            return;
        }

        if (TryShootPredator(world, tower.Pos, tower.Owner, tower.RangeTiles, tower.ShotDamage))
            tower.CooldownRemainingSeconds = tower.CooldownSeconds;
    }

    private static void TickArrowTower(World world, ArrowTower tower, float dt)
    {
        tower.CooldownRemainingSeconds = Math.Max(0f, tower.CooldownRemainingSeconds - dt);
        if (tower.CooldownRemainingSeconds > 0f)
            return;

        if (TryShootHostilePerson(world, tower.Pos, tower.Owner, tower.RangeTiles, tower.ShotDamage)
            || TryShootPredator(world, tower.Pos, tower.Owner, tower.RangeTiles, tower.ShotDamage))
        {
            tower.CooldownRemainingSeconds = tower.CooldownSeconds;
        }
    }

    private static void TickCatapultTower(World world, CatapultTower tower, float dt)
    {
        tower.CooldownRemainingSeconds = Math.Max(0f, tower.CooldownRemainingSeconds - dt);
        if (tower.CooldownRemainingSeconds > 0f)
            return;

        if (!TryCatapultShot(world, tower))
            return;

        tower.CooldownRemainingSeconds = tower.CooldownSeconds;
    }

    private static bool TryCatapultShot(World world, CatapultTower tower)
    {
        Person? target = null;
        var bestDistance = int.MaxValue;
        foreach (var person in world._people)
        {
            if (person.Health <= 0f || person.Home == tower.Owner)
                continue;

            var stance = world.GetFactionStance(tower.Owner.Faction, person.Home.Faction);
            if (stance < WorldSim.Simulation.Diplomacy.Stance.Hostile)
                continue;

            var distance = Math.Abs(person.Pos.x - tower.Pos.x) + Math.Abs(person.Pos.y - tower.Pos.y);
            if (distance > tower.RangeTiles || distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = person;
        }

        if (target == null)
            return false;

        foreach (var person in world._people)
        {
            if (person.Health <= 0f || person.Home == tower.Owner)
                continue;

            var dist = Math.Abs(person.Pos.x - target.Pos.x) + Math.Abs(person.Pos.y - target.Pos.y);
            if (dist <= tower.SplashRadius)
                person.ApplyCombatDamage(world, tower.ShotDamage * world.SiegeDamageMultiplier, "CatapultTower");
        }

        foreach (var predator in world._animals.OfType<Predator>().Where(candidate => candidate.IsAlive))
        {
            var dist = Math.Abs(predator.Pos.x - target.Pos.x) + Math.Abs(predator.Pos.y - target.Pos.y);
            if (dist <= tower.SplashRadius)
                predator.ApplyTowerDamage(world, tower.ShotDamage * world.SiegeDamageMultiplier);
        }

        world.AddExternalEvent($"{tower.Owner.Name} catapult tower fired");
        return true;
    }

    private static bool TryShootHostilePerson(World world, (int x, int y) origin, Colony owner, int rangeTiles, float shotDamage)
    {
        Person? target = null;
        var bestDistance = int.MaxValue;
        foreach (var person in world._people)
        {
            if (person.Health <= 0f || person.Home == owner)
                continue;

            var stance = world.GetFactionStance(owner.Faction, person.Home.Faction);
            if (stance < WorldSim.Simulation.Diplomacy.Stance.Hostile)
                continue;

            var distance = Math.Abs(person.Pos.x - origin.x) + Math.Abs(person.Pos.y - origin.y);
            if (distance > rangeTiles || distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = person;
        }

        if (target == null)
            return false;

        target.ApplyCombatDamage(world, shotDamage, "Watchtower");
        world.AddExternalEvent($"{owner.Name} tower fired");
        return true;
    }

    private static bool TryShootPredator(World world, (int x, int y) origin, Colony owner, int rangeTiles, float shotDamage)
    {
        var predator = world._animals
            .OfType<Predator>()
            .Where(candidate => candidate.IsAlive)
            .Select(candidate => new
            {
                Predator = candidate,
                Distance = Math.Abs(candidate.Pos.x - origin.x) + Math.Abs(candidate.Pos.y - origin.y)
            })
            .Where(entry => entry.Distance <= rangeTiles)
            .OrderBy(entry => entry.Distance)
            .FirstOrDefault();

        if (predator == null)
            return false;

        predator.Predator.ApplyTowerDamage(world, shotDamage);
        world.AddExternalEvent($"{owner.Name} tower hit predator");
        return true;
    }
}
