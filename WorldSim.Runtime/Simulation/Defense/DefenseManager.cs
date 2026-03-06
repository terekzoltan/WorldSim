using System;
using System.Linq;

namespace WorldSim.Simulation.Defense;

public sealed class DefenseManager
{
    public void Tick(World world, float dt)
    {
        foreach (var structure in world.DefensiveStructures)
        {
            if (structure is not Watchtower tower || tower.IsDestroyed)
                continue;

            tower.CooldownRemainingSeconds = Math.Max(0f, tower.CooldownRemainingSeconds - dt);
            if (tower.CooldownRemainingSeconds > 0f)
                continue;

            if (TryShootHostilePerson(world, tower))
            {
                tower.CooldownRemainingSeconds = tower.CooldownSeconds;
                continue;
            }

            if (TryShootPredator(world, tower))
                tower.CooldownRemainingSeconds = tower.CooldownSeconds;
        }

        world.RemoveDestroyedDefensiveStructures();
    }

    private static bool TryShootHostilePerson(World world, Watchtower tower)
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

        target.ApplyCombatDamage(world, tower.ShotDamage, "Watchtower");
        world.AddExternalEvent($"{tower.Owner.Name} watchtower fired");
        return true;
    }

    private static bool TryShootPredator(World world, Watchtower tower)
    {
        var predator = world._animals
            .OfType<Predator>()
            .Where(candidate => candidate.IsAlive)
            .Select(candidate => new
            {
                Predator = candidate,
                Distance = Math.Abs(candidate.Pos.x - tower.Pos.x) + Math.Abs(candidate.Pos.y - tower.Pos.y)
            })
            .Where(entry => entry.Distance <= tower.RangeTiles)
            .OrderBy(entry => entry.Distance)
            .FirstOrDefault();

        if (predator == null)
            return false;

        predator.Predator.ApplyTowerDamage(world, tower.ShotDamage);
        world.AddExternalEvent($"{tower.Owner.Name} tower hit predator");
        return true;
    }
}
