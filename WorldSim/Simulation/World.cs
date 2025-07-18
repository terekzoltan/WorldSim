using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public class World
    {
        public readonly int Width, Height;
        Tile[,] _map;
        public List<Person> _people = new();
        public List<Colony> _colonies = new();

        // Technology-affected properties
        public int WoodYield { get; set; } = 1;
        public int StoneYield { get; set; } = 1;
        public int FoodYield { get; set; } = 1;
        public float HealthBonus { get; set; } = 0;
        public float MaxAge { get; set; } = 80;
        public float WorkEfficiencyMultiplier { get; set; } = 1.0f;
        public int HouseCapacity { get; set; } = 4;
        public bool ResourceSharingEnabled { get; set; } = false;
        public int IntelligenceBonus { get; set; } = 0;
        public int StrengthBonus { get; set; } = 0;
        public float MovementSpeedMultiplier { get; set; } = 1.0f;
        public float FoodSpoilageRate { get; set; } = 1.0f;
        public float JobSwitchDelay { get; set; } = 1.0f;
        public float BirthRateMultiplier { get; set; } = 1.0f;
        public bool StoneBuildingsEnabled { get; set; } = false;

        readonly Random _rng = new();

        public World(int width, int height, int initialPop)
        {
            Width = width;
            Height = height;
            _map = new Tile[width, height];

            // 1. Randomly scatter resources
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    _map[x, y] = new Tile(
                        _rng.NextDouble() < 0.05 ? Resource.Wood :
                        _rng.NextDouble() < 0.02 ? Resource.Stone :
                                                   Resource.None,
                        _rng.Next(20, 100));

            // 2. Multiple colonies on the map (3 as example)
            int colonyCount = 4;
            for (int ci = 0; ci < colonyCount; ci++)
            {
                
                (int, int) colPos = (
                    _rng.Next(Width / 4, Width * 3 / 4) , // spawn near the center of the map
                    _rng.Next(Height / 4, Height * 3 / 4)
                );
                

                Colony col = new Colony(ci, colPos);
                _colonies.Add(col);

                col.Color = ci switch 
                { 
                    0 => Color.Red,    
                    1 => Color.Blue,   
                    2 => Color.Yellow,
                    3 => Color.Purple,
                    _ => Color.White   
                };

                // 3. Generate residents for this colony
                int pop = initialPop / colonyCount;
                for (int i = 0; i < pop; i++)
                    _people.Add(Person.Spawn(col, RandomFreePos()));
            }
        }

        // Tick-based update
        public void Update(float dt)
        {
            List<Person> births = new();
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                if (!_people[i].Update(this, dt, births))
                    _people.RemoveAt(i);
            }
            _people.AddRange(births);
            foreach (Colony c in _colonies) c.Update(dt);
        }

        // Can be tested like this:
        public bool TryHarvest((int x, int y) pos, Resource need, int qty)
        {
            ref Tile tile = ref _map[pos.x, pos.y];
            return tile.Harvest(need, qty);
        }

        (int, int) RandomFreePos()
            => (_rng.Next(Width), _rng.Next(Height));

        /// <summary>Returns the tile at (x,y) from _map.</summary>
        public Tile GetTile(int x, int y)
            => _map[x, y];
    }
}
