using System;
using System.Collections.Generic;

namespace WorldSim.Simulation
{
    public class Colony
    {
        public int Id { get; }
        public (int x, int y) Origin;

        public Dictionary<Resource, int> Stock = new()
        {
            [Resource.Wood] = 0,
            [Resource.Stone] = 0,
            [Resource.Iron] = 0,
            [Resource.Gold] = 0,
            [Resource.Food] = 0
        };
        public int HouseCount = 0;
        public int HouseWoodCost { get; set; } = 50;
        public int HouseStoneCost { get; set; } = 15;
        public bool CanBuildWithStone { get; set; } = false;
        public float MovementSpeedMultiplier { get; set; } = 1.0f;
        public HashSet<string> UnlockedTechs { get; } = new();

        float _age;

        public Colony(int id, (int, int) startPos)
        {
            Id = id;
            Origin = startPos;
        }

        public void Update(float dt)
        {
            _age += dt;
            // later tech tree, population cap, etc.
        }
    }
}
