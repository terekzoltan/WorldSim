using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public enum Job { Idle, GatherWood, BuildHouse }

    public class Person
    {
        public (int x, int y) Pos;
        public Job Current = Job.Idle;
        public float Health = 100;
        public int Strength, Intelligence;

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

        public void Update(World w, float dt)
        {
            switch (Current)
            {
                case Job.Idle:
                    Current = _rng.NextDouble() < 0.5
                                ? Job.GatherWood
                                : Job.BuildHouse;
                    break;

                case Job.GatherWood:
                    // próbál fát kitermelni
                    if (w.TryHarvest(Pos, Resource.Wood, 1))
                        _home.Stock[Resource.Wood]++;
                    else
                        Wander(w);
                    Current = Job.Idle;
                    break;

                case Job.BuildHouse:
                    if (_home.Stock[Resource.Wood] >= 10)
                    {
                        _home.Stock[Resource.Wood] -= 10;
                        _home.HouseCount++;
                    }
                    Current = Job.Idle;
                    break;
            }
        }

        void Wander(World w)
        {
            Pos = (
              Math.Clamp(Pos.x + _rng.Next(-1, 2), 0, w.Width - 1),
              Math.Clamp(Pos.y + _rng.Next(-1, 2), 0, w.Height - 1)
            );
        }
    }
}
