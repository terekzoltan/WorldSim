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

    public bool Update(World w, float dt, List<Person> births)
    {
        Age += dt;
        if (Age > 80)
            return false;

        // simple reproduction chance if there is housing capacity
        int colonyPop = w._people.Count(p => p.Home == _home);
        int capacity = _home.HouseCount * 4;
        if (Age >= 18 && Age <= 40 && colonyPop < capacity && _rng.NextDouble() < 0.01)
        {
            births.Add(Person.Spawn(_home, Pos));
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
        int maxHouses = (int)Math.Ceiling(colonyPop / 4.0);
        if (_home.HouseCount < maxHouses && _home.Stock[Resource.Wood] >= _home.HouseWoodCost)
        {
            _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
            _home.HouseCount++;
        }
                {
                    _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
                    _home.HouseCount++;
                }
                Current = Job.Idle;
                break;
        }
        return true;
    }

    void Wander(World w)
    {
        Pos = (
          Math.Clamp(Pos.x + _rng.Next(-1, 2), 0, w.Width - 1),
          Math.Clamp(Pos.y + _rng.Next(-1, 2), 0, w.Height - 1)
        );
    }
}
