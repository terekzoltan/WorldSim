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
        public List<House> Houses = new();
        public List<Animal> _animals = new();

        // Technology-affected properties
        public int WoodYield { get; set; } = 1; // Fa kitermelés hozama (mennyi fát kapnak egy gyűjtéskor)
        public int StoneYield { get; set; } = 1; // Kő kitermelés hozama
        public int FoodYield { get; set; } = 1; // Élelmiszer kitermelés hozama
        public float HealthBonus { get; set; } = 0; // Egészség bónusz (plusz életpont vagy egészség)
        public float MaxAge { get; set; } = 80; // Maximális életkor (meddig élhetnek az emberek)
        public float WorkEfficiencyMultiplier { get; set; } = 1.0f; // Munka hatékonyság szorzó (gyorsabban dolgoznak)
        public int HouseCapacity { get; set; } = 5; // Egy házban lakók maximális száma
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

            // 1) Biomes (Grass/Dirt/Water) via cheap seeded region growing
            Ground[,] grounds = GenerateBiomes();

            // 2) Resource nodes by biome (no resources on water)
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    ResourceNode? node = null;
                    if (grounds[x, y] != Ground.Water)
                    {
                        double r = _rng.NextDouble();
                        if (grounds[x, y] == Ground.Grass)
                        {
                            // Grass: a bit more wood
                            if (r < 0.08) node = new ResourceNode(Resource.Wood, _rng.Next(1, 10));
                            else if (r < 0.11) node = new ResourceNode(Resource.Stone, _rng.Next(1, 10));
                        }
                        else // Dirt
                        {
                            // Dirt: a bit more stone
                            if (r < 0.05) node = new ResourceNode(Resource.Wood, _rng.Next(1, 10));
                            else if (r < 0.11) node = new ResourceNode(Resource.Stone, _rng.Next(1, 10));
                        }
                    }
                    _map[x, y] = new Tile(grounds[x, y], node);
                }
            }

            // 2. Multiple colonies on the map (completely random positions)
            int colonyCount = 2; // 4 in future
            for (int ci = 0; ci < colonyCount; ci++)
            {
                (int, int) colPos = (_rng.Next(0, Width), _rng.Next(0, Height));

                Colony col = new Colony(ci, colPos);
                _colonies.Add(col);

                // Faction colors (jövőben 4 lesz):
                // 0: Sylvars (Cyan)
                // 1: Obsidari (Bronze)
                // 2: Aetheri (Violet)
                // 3: Chitáriak (Amber/Golden Yellow)
                col.Color = ci switch
                {
                    0 => Color.Cyan,                    // Sylvars
                    1 => new Color(205, 127, 50),       // Obsidari (Bronze)
                    2 => Color.Purple,                  // Aetheri
                    3 => new Color(255, 191, 0),        // Chitáriak (Amber)
                    _ => Color.White
                };

                // Residents near origin
                int pop = initialPop / colonyCount;
                for (int i = 0; i < pop; i++)
                {
                    int spawnRadius = 5;
                    int px = Math.Clamp(col.Origin.x + _rng.Next(-spawnRadius, spawnRadius + 1), 0, Width - 1);
                    int py = Math.Clamp(col.Origin.y + _rng.Next(-spawnRadius, spawnRadius + 1), 0, Height - 1);
                    _people.Add(Person.Spawn(col, (px, py)));
                }
            }

            // 3. Animals
            int animalCount = Math.Max(10, (Width * Height) / 256);
            for (int i = 0; i < animalCount; i++)
                _animals.Add(Animal.Spawn(RandomFreePos()));
        }

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
            foreach (var a in _animals) a.Update(this, dt);

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

        // Delegates to Tile.Harvest which uses the Node
        public bool TryHarvest((int x, int y) pos, Resource need, int qty)
        {
            ref Tile tile = ref _map[pos.x, pos.y];
            return tile.Harvest(need, qty);
        }

        (int, int) RandomFreePos() => (_rng.Next(Width), _rng.Next(Height));
        public Tile GetTile(int x, int y) => _map[x, y];
        public void AddHouse(Colony colony, (int x, int y) pos) => Houses.Add(new House(colony, pos));

        // --- Biome generation (cheap, blob-like) ---

        Ground[,] GenerateBiomes()
        {
            int[,] mask = new int[Width, Height]; // -1=unassigned, 0=Water, 1=Grass, 2=Dirt
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    mask[x, y] = -1;

            int total = Width * Height;
            int waterTarget = (int)(total * 0.10); // ~10% water
            int grassTarget = (int)(total * 0.60); // ~60% grass
            // rest becomes dirt

            // Grow a few water blobs
            GrowRegion(mask, label: 0, targetCount: waterTarget, spreadProb: 0.72);

            // Grow grass blobs (overwriting unassigned cells)
            GrowRegion(mask, label: 1, targetCount: grassTarget, spreadProb: 0.72);

            // Fill the rest as dirt
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (mask[x, y] == -1)
                        mask[x, y] = 2;

            // Convert to Ground[,]
            Ground[,] grounds = new Ground[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    grounds[x, y] = mask[x, y] switch
                    {
                        0 => Ground.Water,
                        1 => Ground.Grass,
                        _ => Ground.Dirt
                    };
                }
            }
            return grounds;
        }

        void GrowRegion(int[,] mask, int label, int targetCount, double spreadProb)
        {
            if (targetCount <= 0) return;

            int placed = 0;
            Queue<(int x, int y)> q = new();

            // Helper to enqueue neighbors with probability
            void EnqueueNeighbors(int x, int y)
            {
                // 4-neighborhood
                Span<(int dx, int dy)> neigh = stackalloc (int dx, int dy)[]
                {
                    (1,0), (-1,0), (0,1), (0,-1)
                };
                foreach (var (dx, dy) in neigh)
                {
                    if (_rng.NextDouble() < spreadProb)
                        q.Enqueue((x + dx, y + dy));
                }
            }

            // Keep seeding new blobs until target reached
            int safety = 0;
            while (placed < targetCount && safety++ < targetCount * 20)
            {
                // If queue empty, pick a random unassigned cell as new seed
                if (q.Count == 0)
                {
                    // find a few random candidates
                    for (int attempts = 0; attempts < 64 && q.Count == 0; attempts++)
                    {
                        int sx = _rng.Next(0, Width);
                        int sy = _rng.Next(0, Height);
                        if (mask[sx, sy] == -1)
                            q.Enqueue((sx, sy));
                    }
                    // If no unassigned left, we cannot place more
                    if (q.Count == 0) break;
                }

                var (x, y) = q.Dequeue();
                if (x < 0 || y < 0 || x >= Width || y >= 0 + Height) { if (y >= Height) { } continue; } // keep bounds clean
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                if (mask[x, y] != -1) continue;

                mask[x, y] = label;
                placed++;

                EnqueueNeighbors(x, y);
            }
        }
    }
}
