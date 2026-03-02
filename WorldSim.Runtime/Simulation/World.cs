using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation
{
    public sealed class ColonyDeathStats
    {
        public int OldAge;
        public int Starvation;
        public int Predator;
        public int Other;
    }

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public class World
    {
        readonly Func<Colony, RuntimeNpcBrain> _brainFactory;
        public readonly int Width, Height;
        Tile[,] _map;
        public List<Person> _people = new();
        public List<Colony> _colonies = new();
        public List<House> Houses = new();
        public List<SpecializedBuilding> SpecializedBuildings = new();
        public List<Animal> _animals = new();

        // Technology-affected properties
        public int WoodYield { get; set; } = 1; // Fa kitermelés hozama (mennyi fát kapnak egy gyűjtéskor)
        public int StoneYield { get; set; } = 1; // Kő kitermelés hozama
        public int IronYield { get; set; } = 1;
        public int GoldYield { get; set; } = 1;
        public int FoodYield { get; set; } = 2; // Élelmiszer kitermelés hozama
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
        public bool AllowFreeTechUnlocks { get; set; }
        // Disabled by default until bidirectional combat/retaliation exists.
        public bool EnablePredatorHumanAttacks { get; set; } = false;
        public float PredatorHumanDamage { get; set; } = 10f;

        public Season CurrentSeason { get; private set; } = Season.Spring;
        public bool IsDroughtActive { get; private set; }
        public IReadOnlyList<string> RecentEvents => _recentEvents;
        public int TotalAnimalStuckRecoveries { get; private set; }
        public int TotalPredatorDeaths { get; private set; }
        public int TotalPredatorHumanHits { get; private set; }
        public int TotalDeathsOldAge { get; private set; }
        public int TotalDeathsStarvation { get; private set; }
        public int TotalDeathsPredator { get; private set; }
        public int TotalDeathsOther { get; private set; }
        public int RecentDeathsStarvation60s => _recentStarvationDeaths.Count;
        public int TotalStarvationDeathsWithFood { get; private set; }

        readonly Random _rng = new();
        readonly List<(int x, int y, float timer, float target)> _foodRegrowth = new();
        readonly List<string> _recentEvents = new();
        readonly HashSet<int> _houseMilestones = new();
        readonly HashSet<int> _extinctionMilestones = new();
        readonly Dictionary<int, ColonyDeathStats> _colonyDeathStats = new();
        readonly Queue<float> _recentStarvationDeaths = new();

        float _simulationTimeSeconds;
        float _seasonTimer;
        float _droughtTimer;
        float _professionRebalanceTimer;
        float _specializedBuildTimer;
        float _foodParityTimer;

        const float SeasonDurationSeconds = 90f;
        const float DroughtDurationSeconds = 35f;
        const float ProfessionRebalancePeriod = 12f;
        const float SpecializedBuildPeriod = 14f;
        const float FoodParityPeriod = 6f;

        public World(int width, int height, int initialPop, Func<Colony, RuntimeNpcBrain>? brainFactory = null)
        {
            _brainFactory = brainFactory ?? (_ => new RuntimeNpcBrain());
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
                            if (r < 0.03) node = new ResourceNode(Resource.Wood, _rng.Next(1, 10));
                            else if (r < 0.04) node = new ResourceNode(Resource.Stone, _rng.Next(1, 10));
                            else if (r < 0.042) node = new ResourceNode(Resource.Iron, _rng.Next(1, 6));
                            else if (r < 0.043) node = new ResourceNode(Resource.Gold, _rng.Next(1, 4));
                            else if (r < 0.08) node = new ResourceNode(Resource.Food, _rng.Next(3, 8));
                        }
                        else // Dirt
                        {
                            if (r < 0.02) node = new ResourceNode(Resource.Wood, _rng.Next(1, 10));
                            else if (r < 0.028) node = new ResourceNode(Resource.Stone, _rng.Next(1, 10));
                            else if (r < 0.031) node = new ResourceNode(Resource.Iron, _rng.Next(1, 6));
                            else if (r < 0.032) node = new ResourceNode(Resource.Gold, _rng.Next(1, 4));
                            else if (r < 0.055) node = new ResourceNode(Resource.Food, _rng.Next(3, 7));
                        }
                    }
                    _map[x, y] = new Tile(grounds[x, y], node);
                }
            }

            // 2. Multiple colonies on the map (completely random positions)
            int colonyCount = 4;
            int basePopPerColony = Math.Max(1, initialPop / colonyCount);
            int extraPop = Math.Max(0, initialPop - (basePopPerColony * colonyCount));
            for (int ci = 0; ci < colonyCount; ci++)
            {
                // Ensure colony origin is not on water
                int ox, oy, guard = 0;
                do
                {
                    ox = _rng.Next(0, Width);
                    oy = _rng.Next(0, Height);
                } while (grounds[ox, oy] == Ground.Water && ++guard < 2048);
                (int, int) colPos = (ox, oy);

                Colony col = new Colony(ci, colPos);
                _colonies.Add(col);
                _colonyDeathStats[col.Id] = new ColonyDeathStats();

                // Faction setup kept for reference; explicit color assignment removed (using icons instead)
                // 0: Sylvars
                // 1: Obsidari
                // 2: Aetheri
                // 3: Chitáriak

                // Residents near origin (avoid water tiles)
                int pop = basePopPerColony + (ci < extraPop ? 1 : 0);
                for (int i = 0; i < pop; i++)
                {
                    int spawnRadius = 5;
                    int px, py, attempts = 0;

                    // Try random positions around origin until we hit non-water (same logic as origin)
                    do
                    {
                        px = Math.Clamp(col.Origin.x + _rng.Next(-spawnRadius, spawnRadius + 1), 0, Width - 1);
                        py = Math.Clamp(col.Origin.y + _rng.Next(-spawnRadius, spawnRadius + 1), 0, Height - 1);
                    } while (_map[px, py].Ground == Ground.Water && ++attempts < 64);

                    _people.Add(Person.Spawn(col, (px, py), CreateNpcBrain(col)));
                }
            }

            // 3. Animals
            int animalCount = Math.Max(10, (Width * Height) / 256);
            for (int i = 0; i < animalCount; i++)
                _animals.Add(Animal.Spawn(RandomFreePos()));
        }

        public void Update(float dt)
        {
            _simulationTimeSeconds += Math.Max(0f, dt);
            UpdateSeasonsAndEvents(dt);
            UpdateFoodRegrowth(dt);

            List<Person> births = new();
            for (int i = _people.Count - 1; i >= 0; i--)
            {
                if (!_people[i].Update(this, dt, births))
                {
                    ReportPersonDeath(_people[i]);
                    _people.RemoveAt(i);
                }
            }
            _people.AddRange(births);

            _professionRebalanceTimer += dt;
            if (_professionRebalanceTimer >= ProfessionRebalancePeriod)
            {
                _professionRebalanceTimer = 0f;
                RebalanceProfessions();
            }

            _specializedBuildTimer += dt;
            if (_specializedBuildTimer >= SpecializedBuildPeriod)
            {
                _specializedBuildTimer = 0f;
                TryAutoConstructSpecializedBuildings();
            }

            RecalculateInfrastructureEffects();

            foreach (Colony c in _colonies) c.Update(this, dt);

            _foodParityTimer += dt;
            if (_foodParityTimer >= FoodParityPeriod)
            {
                _foodParityTimer = 0f;
                ApplySoftFoodParityTransfer();
            }

            // Animals: update and remove the dead
            for (int i = _animals.Count - 1; i >= 0; i--)
            {
                _animals[i].Update(this, dt);
                if (!_animals[i].IsAlive)
                    _animals.RemoveAt(i);
            }

            UpdateAnimalPopulation(dt);
            UpdateMilestones();

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

            TrimRecentDeathWindows();
        }

        // Delegates to Tile.Harvest which uses the Node
        public bool TryHarvest((int x, int y) pos, Resource need, int qty)
        {
            ref Tile tile = ref _map[pos.x, pos.y];
            bool ok = tile.Harvest(need, qty);
            if (!ok) return false;

            if (need == Resource.Food && tile.Node != null && tile.Node.Type == Resource.Food && tile.Node.Amount == 0)
                RegisterFoodRegrowthSpot(pos.x, pos.y);

            return true;
        }

        (int, int) RandomFreePos() => (_rng.Next(Width), _rng.Next(Height));
        public Tile GetTile(int x, int y) => _map[x, y];
        public void AddHouse(Colony colony, (int x, int y) pos) => Houses.Add(new House(colony, pos, HouseCapacity));
        public void AddSpecializedBuilding(Colony colony, (int x, int y) pos, SpecializedBuildingKind kind)
            => SpecializedBuildings.Add(new SpecializedBuilding(colony, pos, kind));
        internal RuntimeNpcBrain CreateNpcBrain(Colony colony) => _brainFactory(colony);

        public void ReportAnimalStuckRecovery() => TotalAnimalStuckRecoveries++;
        public void ReportPredatorDeath() => TotalPredatorDeaths++;
        public void ReportPredatorHumanHit() => TotalPredatorHumanHits++;
        public ColonyDeathStats GetColonyDeathStats(int colonyId)
            => _colonyDeathStats.TryGetValue(colonyId, out var stats)
                ? stats
                : new ColonyDeathStats();

        private void ReportPersonDeath(Person person)
        {
            PersonDeathReason reason = person.LastDeathReason;
            switch (reason)
            {
                case PersonDeathReason.OldAge:
                    TotalDeathsOldAge++;
                    break;
                case PersonDeathReason.Starvation:
                    TotalDeathsStarvation++;
                    _recentStarvationDeaths.Enqueue(_simulationTimeSeconds);
                    if (person.Home.Stock.GetValueOrDefault(Resource.Food, 0) > 0)
                        TotalStarvationDeathsWithFood++;
                    break;
                case PersonDeathReason.Predator:
                    TotalDeathsPredator++;
                    break;
                default:
                    TotalDeathsOther++;
                    break;
            }

            if (!_colonyDeathStats.TryGetValue(person.Home.Id, out var colonyStats))
            {
                colonyStats = new ColonyDeathStats();
                _colonyDeathStats[person.Home.Id] = colonyStats;
            }

            switch (reason)
            {
                case PersonDeathReason.OldAge:
                    colonyStats.OldAge++;
                    break;
                case PersonDeathReason.Starvation:
                    colonyStats.Starvation++;
                    break;
                case PersonDeathReason.Predator:
                    colonyStats.Predator++;
                    break;
                default:
                    colonyStats.Other++;
                    break;
            }

            TrimRecentDeathWindows();
        }

        private void TrimRecentDeathWindows()
        {
            const float rollingWindowSeconds = 60f;
            while (_recentStarvationDeaths.Count > 0 && (_simulationTimeSeconds - _recentStarvationDeaths.Peek()) > rollingWindowSeconds)
                _recentStarvationDeaths.Dequeue();
        }

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

        void RegisterFoodRegrowthSpot(int x, int y)
        {
            if (_foodRegrowth.Any(s => s.x == x && s.y == y))
                return;

            _foodRegrowth.Add((x, y, 0f, 24f + (float)_rng.NextDouble() * 24f));
        }

        void UpdateFoodRegrowth(float dt)
        {
            float growSpeed = IsDroughtActive ? 0.95f : 1f;
            for (int i = _foodRegrowth.Count - 1; i >= 0; i--)
            {
                var spot = _foodRegrowth[i];
                spot.timer += dt * growSpeed;
                if (spot.timer >= spot.target)
                {
                    if (_map[spot.x, spot.y].Ground != Ground.Water)
                        _map[spot.x, spot.y].ReplaceNode(new ResourceNode(Resource.Food, _rng.Next(2, 7)));
                    _foodRegrowth.RemoveAt(i);
                    continue;
                }
                _foodRegrowth[i] = spot;
            }
        }

        void UpdateSeasonsAndEvents(float dt)
        {
            _seasonTimer += dt;
            if (_seasonTimer >= SeasonDurationSeconds)
            {
                _seasonTimer -= SeasonDurationSeconds;
                CurrentSeason = (Season)(((int)CurrentSeason + 1) % 4);
                AddEvent($"Season changed to {CurrentSeason}");

                if (!IsDroughtActive && _rng.NextDouble() < 0.25)
                {
                    IsDroughtActive = true;
                    _droughtTimer = 0f;
                    AddEvent("Drought started");
                }
            }

            if (IsDroughtActive)
            {
                _droughtTimer += dt;
                if (_droughtTimer >= DroughtDurationSeconds)
                {
                    IsDroughtActive = false;
                    _droughtTimer = 0f;
                    AddEvent("Drought ended");
                }
            }

            TrimRecentDeathWindows();
        }

        void AddEvent(string text)
        {
            _recentEvents.Add(text);
            if (_recentEvents.Count > 3)
                _recentEvents.RemoveAt(0);
        }

        void UpdateAnimalPopulation(float dt)
        {
            if (_rng.NextDouble() >= dt * 0.01)
                return;

            int herbivores = _animals.Count(a => a is Herbivore && a.IsAlive);
            int predators = _animals.Count(a => a is Predator && a.IsAlive);

            if (herbivores < Math.Max(8, (Width * Height) / 400))
            {
                _animals.Add(new Herbivore(RandomFreePos()));
                return;
            }

            if (predators < Math.Max(3, herbivores / 6) && _rng.NextDouble() < 0.5)
                _animals.Add(new Predator(RandomFreePos()));
        }

        void UpdateMilestones()
        {
            foreach (var colony in _colonies)
            {
                if (colony.HouseCount > 0 && !_houseMilestones.Contains(colony.Id))
                {
                    _houseMilestones.Add(colony.Id);
                    AddEvent($"{colony.Name} built first house");
                }

                int people = _people.Count(p => p.Home == colony && p.Health > 0f);
                if (people == 0 && !_extinctionMilestones.Contains(colony.Id))
                {
                    _extinctionMilestones.Add(colony.Id);
                    AddEvent($"{colony.Name} collapsed");
                }
            }
        }

        void RecalculateInfrastructureEffects()
        {
            foreach (var colony in _colonies)
            {
                int farms = SpecializedBuildings.Count(b => b.Owner == colony && b.Kind == SpecializedBuildingKind.FarmPlot);
                int workshops = SpecializedBuildings.Count(b => b.Owner == colony && b.Kind == SpecializedBuildingKind.Workshop);
                int storehouses = SpecializedBuildings.Count(b => b.Owner == colony && b.Kind == SpecializedBuildingKind.Storehouse);
                colony.UpdateInfrastructure(farms, workshops, storehouses);
            }
        }

        void ApplySoftFoodParityTransfer()
        {
            if (_colonies.Count < 2 || ResourceSharingEnabled)
                return;

            var living = _colonies
                .Select(colony => new
                {
                    Colony = colony,
                    Living = _people.Count(p => p.Home == colony && p.Health > 0f)
                })
                .Where(x => x.Living > 0)
                .ToList();

            if (living.Count < 2)
                return;

            var richest = living
                .Select(x => new { x.Colony, FoodPerPerson = x.Colony.Stock[Resource.Food] / (float)x.Living, x.Living })
                .OrderByDescending(x => x.FoodPerPerson)
                .First();

            var poorest = living
                .Select(x => new { x.Colony, FoodPerPerson = x.Colony.Stock[Resource.Food] / (float)x.Living, x.Living })
                .OrderBy(x => x.FoodPerPerson)
                .First();

            if (ReferenceEquals(richest.Colony, poorest.Colony))
                return;

            var spread = richest.FoodPerPerson - poorest.FoodPerPerson;
            if (spread < 7f)
                return;

            int donorFood = richest.Colony.Stock[Resource.Food];
            int transfer = Math.Clamp((int)MathF.Round(spread * 0.45f), 2, 14);
            transfer = Math.Min(transfer, Math.Max(0, donorFood - 20));
            if (transfer <= 0)
                return;

            richest.Colony.Stock[Resource.Food] -= transfer;
            poorest.Colony.Stock[Resource.Food] += transfer;
        }

        void TryAutoConstructSpecializedBuildings()
        {
            foreach (var colony in _colonies)
            {
                int people = _people.Count(p => p.Home == colony && p.Health > 0f);
                if (people == 0 || colony.HouseCount == 0)
                    continue;

                float foodPerCapita = colony.Stock[Resource.Food] / (float)Math.Max(1, people);
                int targetFarms = Math.Max(1, (int)Math.Ceiling(people / 10f));
                if (foodPerCapita < 1.4f)
                    targetFarms++;

                if (colony.FarmPlotCount < targetFarms && colony.Stock[Resource.Wood] >= 12 && colony.Stock[Resource.Stone] >= 4)
                {
                    if (TryPlaceSpecialized(colony, SpecializedBuildingKind.FarmPlot, woodCost: 12, stoneCost: 4, ironCost: 0, goldCost: 0))
                        AddEvent($"{colony.Name} built FarmPlot");
                    continue;
                }

                int targetWorkshops = people >= 18 ? 2 : 1;
                if (foodPerCapita >= 1.05f && colony.WorkshopCount < targetWorkshops && colony.Stock[Resource.Wood] >= 12 && colony.Stock[Resource.Stone] >= 6 && colony.Stock[Resource.Iron] >= 3)
                {
                    if (TryPlaceSpecialized(colony, SpecializedBuildingKind.Workshop, woodCost: 12, stoneCost: 6, ironCost: 3, goldCost: 0))
                        AddEvent($"{colony.Name} built Workshop");
                    continue;
                }

                if (foodPerCapita >= 1.1f && colony.StorehouseCount < 1 && colony.Stock[Resource.Wood] >= 14 && colony.Stock[Resource.Stone] >= 10 && colony.Stock[Resource.Gold] >= 1)
                {
                    if (TryPlaceSpecialized(colony, SpecializedBuildingKind.Storehouse, woodCost: 14, stoneCost: 10, ironCost: 0, goldCost: 1))
                        AddEvent($"{colony.Name} built Storehouse");
                }
            }
        }

        bool TryPlaceSpecialized(Colony colony, SpecializedBuildingKind kind, int woodCost, int stoneCost, int ironCost, int goldCost)
        {
            if (colony.Stock[Resource.Wood] < woodCost ||
                colony.Stock[Resource.Stone] < stoneCost ||
                colony.Stock[Resource.Iron] < ironCost ||
                colony.Stock[Resource.Gold] < goldCost)
                return false;

            var pos = FindBuildSpotNear(colony.Origin, radius: 6);
            if (pos == null)
                return false;

            colony.Stock[Resource.Wood] -= woodCost;
            colony.Stock[Resource.Stone] -= stoneCost;
            colony.Stock[Resource.Iron] -= ironCost;
            colony.Stock[Resource.Gold] -= goldCost;
            AddSpecializedBuilding(colony, pos.Value, kind);
            return true;
        }

        (int x, int y)? FindBuildSpotNear((int x, int y) origin, int radius)
        {
            for (int i = 0; i < 24; i++)
            {
                int x = Math.Clamp(origin.x + _rng.Next(-radius, radius + 1), 0, Width - 1);
                int y = Math.Clamp(origin.y + _rng.Next(-radius, radius + 1), 0, Height - 1);
                if (_map[x, y].Ground == Ground.Water)
                    continue;

                bool occupied = Houses.Any(h => h.Pos.x == x && h.Pos.y == y) ||
                                SpecializedBuildings.Any(b => b.Pos.x == x && b.Pos.y == y);
                if (!occupied)
                    return (x, y);
            }

            return null;
        }

        void RebalanceProfessions()
        {
            foreach (var colony in _colonies)
            {
                var adults = _people
                    .Where(p => p.Home == colony && p.Age >= 16f && p.Health > 0f)
                    .ToList();

                if (adults.Count == 0)
                    continue;

                bool emergencyFood = colony.Stock[Resource.Food] <= Math.Max(6, adults.Count + 2);

                var targets = CreateProfessionTargets(adults.Count, emergencyFood);
                var counts = adults
                    .GroupBy(p => p.Profession)
                    .ToDictionary(g => g.Key, g => g.Count());

                Profession? lacking = null;
                int gap = 0;
                foreach (var kv in targets)
                {
                    counts.TryGetValue(kv.Key, out int current);
                    int diff = kv.Value - current;
                    if (diff > gap)
                    {
                        gap = diff;
                        lacking = kv.Key;
                    }
                }

                if (lacking is null || gap <= 0)
                    continue;

                var candidate = adults
                    .Where(p => p.Profession != lacking.Value && p.Current == Job.Idle)
                    .OrderBy(p => p.Stamina)
                    .FirstOrDefault();

                if (candidate != null)
                    candidate.Profession = lacking.Value;
            }
        }

        Dictionary<Profession, int> CreateProfessionTargets(int adultCount, bool emergencyFood)
        {
            var ratios = emergencyFood
                ? new Dictionary<Profession, float>
                {
                    [Profession.Farmer] = 0.34f,
                    [Profession.Hunter] = 0.24f,
                    [Profession.Lumberjack] = 0.14f,
                    [Profession.Miner] = 0.14f,
                    [Profession.Builder] = 0.08f,
                    [Profession.Generalist] = 0.06f
                }
                : new Dictionary<Profession, float>
                {
                    [Profession.Farmer] = 0.24f,
                    [Profession.Hunter] = 0.12f,
                    [Profession.Lumberjack] = 0.2f,
                    [Profession.Miner] = 0.2f,
                    [Profession.Builder] = 0.14f,
                    [Profession.Generalist] = 0.1f
                };

            var targets = ratios.ToDictionary(
                kv => kv.Key,
                kv => Math.Max(0, (int)MathF.Round(adultCount * kv.Value))
            );

            int sum = targets.Values.Sum();
            if (sum < adultCount)
                targets[Profession.Generalist] += adultCount - sum;
            else if (sum > adultCount)
                targets[Profession.Generalist] = Math.Max(0, targets[Profession.Generalist] - (sum - adultCount));

            return targets;
        }
    }
}
