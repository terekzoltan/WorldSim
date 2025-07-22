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
        public int WoodYield { get; set; } = 1; // Fa kitermelés hozama (mennyi fát kapnak egy gyűjtéskor)
        public int StoneYield { get; set; } = 1; // Kő kitermelés hozama
        public int FoodYield { get; set; } = 1; // Élelmiszer kitermelés hozama
        public float HealthBonus { get; set; } = 0; // Egészség bónusz (plusz életpont vagy egészség)
        public float MaxAge { get; set; } = 80; // Maximális életkor (meddig élhetnek az emberek)
        public float WorkEfficiencyMultiplier { get; set; } = 1.0f; // Munka hatékonyság szorzó (gyorsabban dolgoznak)
        public int HouseCapacity { get; set; } = 4; // Egy házban lakók maximális száma
        public bool ResourceSharingEnabled { get; set; } = false; // Erőforrás-megosztás engedélyezve (kolóniák között)
        public int IntelligenceBonus { get; set; } = 0; // Intelligencia bónusz (újszülöttek plusz intelligenciát kapnak)
        public int StrengthBonus { get; set; } = 0; // Erő bónusz (újszülöttek plusz erőt kapnak)
        public float MovementSpeedMultiplier { get; set; } = 1.0f; // Mozgási sebesség szorzó (gyorsabban mozognak)
        public float BirthRateMultiplier { get; set; } = 1.0f; // Születési arány szorzó (gyakoribb születések)
        public bool StoneBuildingsEnabled { get; set; } = false; // Kőből építkezés engedélyezve (lehet kőből építkezni)

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

            // 2. Multiple colonies on the map (completely random positions)
            int colonyCount = 4;
            for (int ci = 0; ci < colonyCount; ci++)
            {
                // Each colony gets a completely random position on the map
                (int, int) colPos = (
                    _rng.Next(0, Width),
                    _rng.Next(0, Height)
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

                // 3. Generate residents for this colony near their origin
                int pop = initialPop / colonyCount;
                for (int i = 0; i < pop; i++)
                {
                    // Spawn within a small radius (e.g. 5 tiles) of the colony origin
                    int spawnRadius = 5;
                    int px = Math.Clamp(
                        col.Origin.x + _rng.Next(-spawnRadius, spawnRadius + 1),
                        0, Width - 1
                    );
                    int py = Math.Clamp(
                        col.Origin.y + _rng.Next(-spawnRadius, spawnRadius + 1),
                        0, Height - 1
                    );
                    _people.Add(Person.Spawn(col, (px, py)));
                }
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

            if (ResourceSharingEnabled && _colonies.Count > 1)
            {
                foreach (var res in _colonies[0].Stock.Keys.ToList())
                {
                    int total = _colonies.Sum(c => c.Stock[res]);
                    int share = total / _colonies.Count;
                    foreach (var c in _colonies)
                        c.Stock[res] = share;
                }
            }
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
