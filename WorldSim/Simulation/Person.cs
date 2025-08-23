using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation;

public enum Job { Idle, GatherWood, BuildHouse }

public class Person
{
    public (int x, int y) Pos;
    public Job Current = Job.Idle;
    public float Health = 100;
    public float Age = 0;
    public int Strength, Intelligence;
    public Colony Home => _home;
    public Color Color => _home.Color;

    Colony _home;
    Random _rng = new();

    private Person(Colony home, (int, int) pos)
    {
        _home = home;
        Pos = pos;
        Strength = _rng.Next(3, 11);
        Intelligence = _rng.Next(3, 11);
    }

    public static Person Spawn(Colony home, (int, int) pos)
        => new Person(home, pos);

    public static Person SpawnWithBonus(Colony home, (int, int) pos, World world)
    {
        var person = new Person(home, pos);
        person.Strength = Math.Min(20, person.Strength + world.StrengthBonus);
        person.Intelligence = Math.Min(20, person.Intelligence + world.IntelligenceBonus);
        person.Health += world.HealthBonus;
        return person;
    }

    public bool Update(World w, float dt, List<Person> births)
    {
        Age += dt/10;
        if (Age > w.MaxAge)
            return false;

        // simple reproduction chance if there is housing capacity
        int colonyPop = w._people.Count(p => p.Home == _home);
        int capacity = _home.HouseCount * w.HouseCapacity;
        if (Age >= 18 && Age <= 60 && colonyPop < capacity && _rng.NextDouble() < (0.001 * w.BirthRateMultiplier))
        {
            births.Add(Person.SpawnWithBonus(_home, Pos, w));
        }

        switch (Current)
        {
            case Job.Idle:
                Current = _rng.NextDouble() < 0.5
                            ? Job.GatherWood
                            : Job.BuildHouse;
                break;

            case Job.GatherWood:
                // attempts to harvest wood
                if (w.TryHarvest(Pos, Resource.Wood, 1))
                    _home.Stock[Resource.Wood] += w.WoodYield;
                else
                    Wander(w);
                Current = Job.Idle;
                break;

            case Job.BuildHouse:
                // Szükséges házak száma, de legalább annyi, mint a már meglévő házak (nem csökkenhet a házak száma).
                int maxHouses = Math.Max(_home.HouseCount, (int)Math.Ceiling((colonyPop + 3) / (double)w.HouseCapacity)); 
                if (_home.HouseCount < maxHouses)
                {
                    if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] >= _home.HouseStoneCost)
                    {
                        _home.Stock[Resource.Stone] -= _home.HouseStoneCost;
                        _home.HouseCount++;
                        w.AddHouse(_home, Pos);
                    }
                    else if (_home.Stock[Resource.Wood] >= _home.HouseWoodCost)
                    {
                        _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
                        _home.HouseCount++;
                        w.AddHouse(_home, Pos);
                    }
                }
                Current = Job.Idle;
                break;
        }
        return true;
    }

    void Wander(World w)
    {
        int moveDistance = (int)_home.MovementSpeedMultiplier;
        Pos = (
          Math.Clamp(Pos.x + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Width - 1),
          Math.Clamp(Pos.y + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Height - 1)
        );
    }
}
