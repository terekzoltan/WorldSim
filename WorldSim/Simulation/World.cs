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

                // Faction setup kept for reference; explicit color assignment removed (using icons instead)
                // 0: Sylvars
                // 1: Obsidari
                // 2: Aetheri
                // 3: Chitáriak

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

            // Animals: update and remove the dead
            for (int i = _animals.Count - 1; i >= 0; i--)
            {
                _animals[i].Update(this, dt);
                if (!_animals[i].IsAlive)
                    _animals.RemoveAt(i);
            }

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
            int waterTarget = (int)(total * 0.15); // ~10% water
            int grassTarget = (int)(total * 0.65); // ~60% grass

            // 1) Tömör víz-blokkok (nincs lyuk blobon belül)
            GrowRegionSolid(mask, label: 0, targetCount: waterTarget, minBlob: Math.Max(64, total/500), maxBlob: Math.Max(256, total/160));

            // 2) Tömör fű-blokkok a maradékba
            GrowRegionSolid(mask, label: 1, targetCount: grassTarget, minBlob: Math.Max(64, total/500), maxBlob: Math.Max(256, total/160));

            // 3) Ami kimaradt: dirt
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (mask[x, y] == -1) mask[x, y] = 2;

            // 4) Víz „lyukbetömés”: ami nem víz és nem ér szélére → legyen víz
            FillEnclosed(mask, labelToKeep: 0);

            // 5) Gyors simítás (opcionális, 0–2 iteráció elég)
            SmoothMajority(mask, targetLabel: 0, iterations: 1); // víz simítás
            SmoothMajority(mask, targetLabel: 1, iterations: 1); // fű simítás

            // Konverzió Ground[,] -ra
            Ground[,] grounds = new Ground[Width, Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    grounds[x, y] = mask[x, y] == 0 ? Ground.Water :
                                    (mask[x, y] == 1 ? Ground.Grass : Ground.Dirt);

            return grounds;
        }

        // --- Tömör blob-növesztés (BFS), olcsó és lyukmentes ---
        void GrowRegionSolid(int[,] mask, int label, int targetCount, int minBlob, int maxBlob)
        {
            if (targetCount <= 0) return;
            int placed = 0;

            while (placed < targetCount)
            {
                // új mag pont: még fel nem osztott cella
                int sx, sy, guard = 0;
                do
                {
                    sx = _rng.Next(Width);
                    sy = _rng.Next(Height);
                } while (mask[sx, sy] != -1 && ++guard < 2048);
                if (guard >= 2048) break;

                int blobTarget = Math.Min(targetCount - placed, _rng.Next(minBlob, maxBlob + 1));

                Queue<(int x, int y)> q = new();
                q.Enqueue((sx, sy));

                while (q.Count > 0 && blobTarget > 0 && placed < targetCount)
                {
                    var (x, y) = q.Dequeue();
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (mask[x, y] != -1) continue;

                    // kissé „fodros” part: néha kihagyunk
                    if (_rng.NextDouble() < 0.06) { // 6%: part-véletlen
                        // de szomszédokat ettől még sorba tesszük -> tovább nő a blob
                    }
                    else
                    {
                        mask[x, y] = label;
                        placed++;
                        blobTarget--;
                    }

                    // 4-szomszéd MINDIG sorba (ettől lesz tömör a blob)
                    q.Enqueue((x + 1, y));
                    q.Enqueue((x - 1, y));
                    q.Enqueue((x, y + 1));
                    q.Enqueue((x, y - 1));
                }
            }
        }

        // --- Perem-flood-fill: a zárt „száraz” üregeket vízzé alakítja ---
        void FillEnclosed(int[,] mask, int labelToKeep)
        {
            bool[,] seen = new bool[Width, Height];
            Queue<(int x, int y)> q = new();

            // indulás: minden szegély, ami NEM labelToKeep
            for (int x = 0; x < Width; x++)
            {
                if (mask[x, 0] != labelToKeep) { q.Enqueue((x, 0)); seen[x, 0] = true; }
                if (mask[x, Height - 1] != labelToKeep) { q.Enqueue((x, Height - 1)); seen[x, Height - 1] = true; }
            }
            for (int y = 0; y < Height; y++)
            {
                if (mask[0, y] != labelToKeep) { q.Enqueue((0, y)); seen[0, y] = true; }
                if (mask[Width - 1, y] != labelToKeep) { q.Enqueue((Width - 1, y)); seen[Width - 1, y] = true; }
            }

            // bejárjuk, mi éri el a szegélyt (nem-víz komponensek)
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    if (seen[nx, ny]) continue;
                    if (mask[nx, ny] == labelToKeep) continue; // vízbe nem lépünk
                    seen[nx, ny] = true;
                    q.Enqueue((nx, ny));
                }
            }

            // ami nem látható a szegélyről és nem víz -> zárt üreg -> legyen víz
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (mask[x, y] != labelToKeep && !seen[x, y])
                        mask[x, y] = labelToKeep;
        }

        // --- 8-szomszédos többségi simítás (nagyon olcsó) ---
        void SmoothMajority(int[,] mask, int targetLabel, int iterations)
        {
            for (int it = 0; it < iterations; it++)
            {
                int[,] copy = (int[,])mask.Clone();
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    int cnt = 0, tot = 0;
                    for (int yy = y - 1; yy <= y + 1; yy++)
                    for (int xx = x - 1; xx <= x + 1; xx++)
                    {
                        if (xx == x && yy == y) continue;
                        if (xx < 0 || yy < 0 || xx >= Width || yy >= Height) continue;
                        tot++;
                        if (copy[xx, yy] == targetLabel) cnt++;
                    }
                    // ha sok a cél-szomszéd → odasimítjuk
                    if (cnt >= 5) mask[x, y] = targetLabel;
                    // ha alig van → elvékonyítjuk
                    else if (cnt <= 1 && mask[x, y] == targetLabel) mask[x, y] = (targetLabel == 0 ? 2 : 2);
                }
            }
        }
    }
}
