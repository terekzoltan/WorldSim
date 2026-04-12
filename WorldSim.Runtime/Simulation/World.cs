using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorldSim.AI;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Defense;
using WorldSim.Simulation.Diplomacy;
using WorldSim.Simulation.Effects;

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

    public sealed record CombatGroupState(
        int GroupId,
        int ColonyId,
        int FactionId,
        Formation Formation,
        int MemberCount,
        int RoutingMemberCount,
        bool IsRouting,
        float AverageMorale,
        int CommanderActorId,
        int CommanderIntelligence,
        float CommanderMoraleStabilityBonus,
        int AnchorX,
        int AnchorY,
        float StrengthScore,
        float DefenseScore,
        int BattleId);

    public sealed record BattleState(
        int BattleId,
        int LeftGroupId,
        int RightGroupId,
        float LeftAverageMorale,
        float RightAverageMorale,
        bool LeftIsRouting,
        bool RightIsRouting,
        int LeftCommanderActorId,
        int RightCommanderActorId,
        int CenterX,
        int CenterY,
        int Radius,
        int Intensity,
        int ElapsedTicks);

    public sealed record SiegeState(
        int SiegeId,
        int AttackerColonyId,
        int DefenderColonyId,
        int TargetStructureId,
        DefensiveStructureKind TargetKind,
        int CenterX,
        int CenterY,
        int ActiveAttackerCount,
        int StartedTick,
        int LastActiveTick,
        int BreachCount,
        string Status);

    public sealed record BreachState(
        int StructureId,
        int DefenderColonyId,
        int AttackerColonyId,
        int X,
        int Y,
        int CreatedTick,
        DefensiveStructureKind StructureKind);

    public class World
    {
        readonly Func<Colony, RuntimeNpcBrain> _brainFactory;
        public readonly int Width, Height;
        Tile[,] _map;
        public List<Person> _people = new();
        public List<Colony> _colonies = new();
        public List<House> Houses = new();
        public List<SpecializedBuilding> SpecializedBuildings = new();
        public List<DefensiveStructure> DefensiveStructures = new();
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
        float _movementSpeedMultiplier = 1.0f;
        public float MovementSpeedMultiplier
        {
            get => _movementSpeedMultiplier;
            set
            {
                _movementSpeedMultiplier = Math.Max(0f, value);
                foreach (var colony in _colonies)
                    colony.MovementSpeedMultiplier = _movementSpeedMultiplier;
            }
        } // Mozgási sebesség szorzó (gyorsabban mozognak)
        public float BirthRateMultiplier { get; set; } = 1.0f; // Születési arány szorzó (gyakoribb születések)
        public bool StoneBuildingsEnabled { get; set; } = false; // Kőből építkezés engedélyezve (lehet kőből építkezni)
        public bool AllowFreeTechUnlocks { get; set; }
        // Disabled by default until bidirectional combat/retaliation exists.
        public bool EnablePredatorHumanAttacks { get; set; } = false;
        public bool EnableCombatPrimitives { get; set; }
        public bool EnableDiplomacy { get; set; }
        public bool EnableSiege { get; set; } = true;
        public float PredatorHumanDamage { get; set; } = 10f;
        public float CombatDamageBonusMultiplier { get; set; } = 1f;
        public float CombatDefenseBonusMultiplier { get; set; } = 1f;
        public float SiegeDamageMultiplier { get; set; } = 1f;
        public bool RequireFortificationTechUnlock { get; set; }
        public int NavigationTopologyVersion => _navigationTopologyVersion;

        public Season CurrentSeason { get; private set; } = Season.Spring;
        public bool IsDroughtActive { get; private set; }
        public IReadOnlyList<string> RecentEvents => _recentEvents;

        public void AddExternalEvent(string text) => AddEvent(text);
        public int TotalAnimalStuckRecoveries { get; private set; }
        public int TotalPredatorDeaths { get; private set; }
        public int TotalPredatorKillsByHumans { get; private set; }
        public int TotalPredatorHumanHits { get; private set; }
        public int TotalCombatEngagements { get; private set; }
        public int TotalCombatDeaths { get; private set; }
        public int TotalBattleTicks { get; private set; }
        public int TotalSiegesStarted { get; private set; }
        public int TotalSiegesRepelled { get; private set; }
        public int TotalBreaches { get; private set; }
        public int TotalStructuresDestroyed { get; private set; }
        public int TotalWallsDestroyed { get; private set; }
        public int TotalGatesDestroyed { get; private set; }
        public int TotalTowersDestroyed { get; private set; }
        public int TotalDeathsOldAge { get; private set; }
        public int TotalDeathsStarvation { get; private set; }
        public int TotalDeathsPredator { get; private set; }
        public int TotalDeathsOther { get; private set; }
        public int RecentDeathsStarvation60s => _recentStarvationDeaths.Count;
        public int TotalStarvationDeathsWithFood { get; private set; }
        public int TotalOverlapResolveMoves { get; private set; }
        public int TotalCrowdDissipationMoves { get; private set; }
        public int TotalBirthFallbackToOccupiedCount { get; private set; }
        public int TotalBirthFallbackToParentCount { get; private set; }
        public int TotalBuildSiteResetCount { get; private set; }
        public int TotalNoProgressBackoffResource { get; private set; }
        public int TotalNoProgressBackoffBuild { get; private set; }
        public int TotalNoProgressBackoffFlee { get; private set; }
        public int TotalNoProgressBackoffCombat { get; private set; }
        public int TotalAiNoPlanDecisions { get; private set; }
        public int TotalAiReplanBackoffDecisions { get; private set; }
        public int TotalAiResearchTechDecisions { get; private set; }
        public int DenseNeighborhoodTicks { get; private set; }
        public int LastTickDenseActors { get; private set; }
        public int ActiveCombatGroupCount => _activeCombatGroups.Count;
        public int ActiveBattleCount => _activeBattles.Count;
        public int ActiveSiegeCount => _activeSieges.Count;

        public ScenarioAiTelemetrySnapshot BuildScenarioAiTelemetrySnapshot()
        {
            var trackedDecisions = _people
                .Select(person => new
                {
                    Decision = person.LastAiDecision,
                    DebugDecisionCause = NormalizeAiValue(person.DebugDecisionCause, "none"),
                    DebugTargetKey = NormalizeAiValue(person.DebugTargetKey, "none")
                })
                .Where(entry => entry.Decision != null)
                .Select(entry => new
                {
                    Decision = entry.Decision!,
                    entry.DebugDecisionCause,
                    entry.DebugTargetKey,
                    TargetKind = ScenarioAiTargetKindClassifier.Normalize(entry.DebugTargetKey)
                })
                .ToList();

            if (trackedDecisions.Count == 0)
                return ScenarioAiTelemetrySnapshot.Empty;

            var goalCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => NormalizeAiValue(entry.Decision.Trace.SelectedGoal, "None")));
            var commandCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => entry.Decision.Job.ToString()));
            var replanReasonCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => NormalizeAiValue(entry.Decision.Trace.ReplanReason, "None")));
            var methodCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => NormalizeAiValue(entry.Decision.Trace.MethodName, "None")));
            var debugCauseCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => entry.DebugDecisionCause));
            var targetKindCounts = BuildScenarioAiCounts(trackedDecisions.Select(entry => entry.TargetKind));

            var latest = trackedDecisions
                .OrderByDescending(entry => entry.Decision.WorldTick)
                .ThenByDescending(entry => entry.Decision.Sequence)
                .ThenBy(entry => entry.Decision.ActorId)
                .First();

            return new ScenarioAiTelemetrySnapshot(
                DecisionCount: trackedDecisions.Count,
                GoalCounts: goalCounts,
                CommandCounts: commandCounts,
                ReplanReasonCounts: replanReasonCounts,
                MethodCounts: methodCounts,
                DebugCauseCounts: debugCauseCounts,
                TargetKindCounts: targetKindCounts,
                TopGoals: goalCounts.Take(3).ToList(),
                TopDebugCauses: debugCauseCounts.Take(3).ToList(),
                LatestDecision: new ScenarioAiLatestDecisionSample(
                    ActorId: latest.Decision.ActorId,
                    ColonyId: latest.Decision.ColonyId,
                    X: latest.Decision.X,
                    Y: latest.Decision.Y,
                    SelectedGoal: NormalizeAiValue(latest.Decision.Trace.SelectedGoal, "None"),
                    NextCommand: latest.Decision.Job.ToString(),
                    PlanLength: latest.Decision.Trace.PlanLength,
                    PlanCost: latest.Decision.Trace.PlanCost,
                    ReplanReason: NormalizeAiValue(latest.Decision.Trace.ReplanReason, "None"),
                    MethodName: NormalizeAiValue(latest.Decision.Trace.MethodName, "None"),
                    DebugDecisionCause: latest.DebugDecisionCause,
                    DebugTargetKey: latest.DebugTargetKey,
                    TargetKind: latest.TargetKind));
        }

        public bool CanBuildFortifications(Colony colony)
        {
            if (!RequireFortificationTechUnlock)
                return true;

            return colony.FortificationsUnlocked || colony.UnlockedTechs.Contains("fortification");
        }

        private static List<ScenarioAiCountEntry> BuildScenarioAiCounts(IEnumerable<string> values)
        {
            return values
                .GroupBy(value => value, StringComparer.Ordinal)
                .Select(group => new ScenarioAiCountEntry(group.Key, group.Count()))
                .OrderByDescending(entry => entry.Count)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static string NormalizeAiValue(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        readonly Random _rng;
        readonly List<(int x, int y, float timer, float target)> _foodRegrowth = new();
        readonly List<string> _recentEvents = new();
        readonly HashSet<int> _houseMilestones = new();
        readonly HashSet<int> _extinctionMilestones = new();
        readonly Dictionary<int, ColonyDeathStats> _colonyDeathStats = new();
        readonly Queue<float> _recentStarvationDeaths = new();
        readonly int[,] _tileOwnerColonyIds;
        readonly bool[,] _tileContested;
        readonly Dictionary<int, ColonyWarState> _colonyWarStates = new();
        readonly Dictionary<int, int> _colonyWarriorCounts = new();
        readonly DomainModifierEngine _domainModifierEngine = new();
        readonly GoalBiasEngine _goalBiasEngine = new();
        readonly Dictionary<(Faction left, Faction right), Stance> _factionStances = new();
        readonly Dictionary<(Faction left, Faction right), int> _contestedTilesByFactionPair = new();
        readonly RelationManager _relationManager = new();
        readonly DefenseManager _defenseManager = new();
        readonly Dictionary<string, int> _softReservations = new(StringComparer.Ordinal);
        readonly List<RuntimeCombatGroup> _activeCombatGroups = new();
        readonly List<RuntimeBattleState> _activeBattles = new();
        readonly List<RuntimeSiegeState> _activeSieges = new();
        readonly List<RuntimeBreachState> _recentBreaches = new();
        readonly List<RuntimeSiegePressure> _siegePressureThisTick = new();
        readonly Dictionary<(int attackerColonyId, int defenderColonyId), RuntimeSiegeSession> _siegeSessions = new();
        int _navigationTopologyVersion;
        int _nextDefenseStructureId = 1;
        int _nextPersonId = 1;
        int _nextCombatGroupId = 1;
        int _nextBattleId = 1;
        int _nextSiegeId = 1;

        float _simulationTimeSeconds;
        int _tickCounter;
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
        const int TerritoryRecomputeIntervalTicks = 5;
        const int CrowdDissipationNeighborRadius = 2;
        const int CrowdDissipationSearchRadius = 4;
        const int CrowdDissipationThreshold = 4;

        int _lastTerritoryRecomputeTick;
        bool _territoryDirty = true;
        int _territoryRecomputeCount;

        public World(int width, int height, int initialPop, Func<Colony, RuntimeNpcBrain>? brainFactory = null, int? randomSeed = null)
        {
            _brainFactory = brainFactory ?? (_ => new RuntimeNpcBrain());
            _rng = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            Width = width;
            Height = height;
            _map = new Tile[width, height];
            _tileOwnerColonyIds = new int[width, height];
            _tileContested = new bool[width, height];

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

                Colony col = new Colony(ci, colPos)
                {
                    MovementSpeedMultiplier = _movementSpeedMultiplier
                };
                _colonies.Add(col);
                _colonyDeathStats[col.Id] = new ColonyDeathStats();
                _colonyWarStates[col.Id] = ColonyWarState.Peace;
                _colonyWarriorCounts[col.Id] = 0;

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

                    _people.Add(Person.Spawn(col, (px, py), CreateNpcBrain(col), CreateEntityRng(), AllocatePersonId()));
                }
            }

            // 3. Animals
            int animalCount = Math.Max(10, (Width * Height) / 256);
            for (int i = 0; i < animalCount; i++)
                _animals.Add(Animal.Spawn(RandomFreePos(), CreateEntityRng()));

            InitializeFactionStances();

            RefreshTerritoryStateIfNeeded(force: true);
        }

        public void Update(float dt)
        {
            _tickCounter++;
            _softReservations.Clear();
            _siegePressureThisTick.Clear();
            _simulationTimeSeconds += Math.Max(0f, dt);
            _domainModifierEngine.Tick();
            _goalBiasEngine.Tick();
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
            if (births.Count > 0)
                MarkTerritoryDirty();

            DeconflictPeopleEndPositions();

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

            _defenseManager.Tick(this, dt);

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

            RefreshTerritoryStateIfNeeded();
            if (EnableDiplomacy)
                _relationManager.Tick(this);
            RecomputeMobilizationState();

            _activeCombatGroups.Clear();
            _activeBattles.Clear();
            _activeSieges.Clear();
            foreach (var person in _people)
                person.SetCombatAssignment(null, null, Formation.Line, isCommander: false);
            ResolveGroupCombatPhase();
            ResolveSiegeStatePhase();
            TrimRecentBreachWindow();

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
        internal Random CreateEntityRng() => new Random(_rng.Next());
        public int AllocatePersonId() => _nextPersonId++;
        public Tile GetTile(int x, int y) => _map[x, y];
        public bool CanPlaceStructureAt(int x, int y)
            => InBounds(x, y)
               && GetTile(x, y).Ground != Ground.Water
               && !IsOccupiedByStructure(x, y);

        public bool IsActorOccupied(int x, int y, Person? exclude = null)
            => _people.Any(person => person != exclude && person.Health > 0f && person.Pos == (x, y));

        public bool CanPlaceStructureAtActorFree(int x, int y, Person? exclude = null)
            => CanPlaceStructureAt(x, y) && !IsActorOccupied(x, y, exclude);

        public (int x, int y) GetBirthSpawnPosition(Colony colony, (int x, int y) parentPos)
        {
            const int MaxRadius = 4;
            (int x, int y)? best = null;
            float bestScore = float.MaxValue;

            for (int radius = 1; radius <= MaxRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int md = Math.Abs(dx) + Math.Abs(dy);
                        if (md == 0 || md > radius)
                            continue;

                        int x = parentPos.x + dx;
                        int y = parentPos.y + dy;
                        if (!CanPlaceStructureAtActorFree(x, y))
                            continue;

                        float colonyDist = Math.Abs(x - colony.Origin.x) + Math.Abs(y - colony.Origin.y);
                        float score = md + (colonyDist * 0.2f);
                        if (score < bestScore)
                        {
                            best = (x, y);
                            bestScore = score;
                        }
                    }
                }

                if (best != null)
                    return best.Value;
            }

            best = null;
            bestScore = float.MaxValue;
            for (int radius = 1; radius <= MaxRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int md = Math.Abs(dx) + Math.Abs(dy);
                        if (md == 0 || md > radius)
                            continue;

                        int x = parentPos.x + dx;
                        int y = parentPos.y + dy;
                        if (!CanPlaceStructureAt(x, y))
                            continue;

                        int occupancy = _people.Count(person => person.Health > 0f && person.Pos == (x, y));
                        float colonyDist = Math.Abs(x - colony.Origin.x) + Math.Abs(y - colony.Origin.y);
                        float score = occupancy * 6f + md + (colonyDist * 0.2f);
                        if (score < bestScore)
                        {
                            best = (x, y);
                            bestScore = score;
                        }
                    }
                }

                if (best != null)
                {
                    TotalBirthFallbackToOccupiedCount++;
                    return best.Value;
                }
            }

            if (CanPlaceStructureAt(parentPos.x, parentPos.y))
            {
                TotalBirthFallbackToParentCount++;
                return parentPos;
            }

            var fallback = FindAnyFreeLandNear(colony.Origin);
            if (fallback == null)
                TotalBirthFallbackToParentCount++;
            return fallback ?? parentPos;
        }

        public void AddHouse(Colony colony, (int x, int y) pos)
        {
            Houses.Add(new House(colony, pos, HouseCapacity));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
        }

        public void AddSpecializedBuilding(Colony colony, (int x, int y) pos, SpecializedBuildingKind kind)
        {
            SpecializedBuildings.Add(new SpecializedBuilding(colony, pos, kind));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
        }

        public bool TryAddWoodWall(Colony colony, (int x, int y) pos)
        {
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new WoodWallSegment(_nextDefenseStructureId++, colony, pos));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddWatchtower(Colony colony, (int x, int y) pos)
        {
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new Watchtower(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, Watchtower.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddStoneWall(Colony colony, (int x, int y) pos)
        {
            if (!colony.UnlockedTechs.Contains("fortification"))
                return false;
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new StoneWallSegment(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, StoneWallSegment.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddReinforcedWall(Colony colony, (int x, int y) pos)
        {
            if (!colony.UnlockedTechs.Contains("advanced_fortification"))
                return false;
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new ReinforcedWallSegment(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, ReinforcedWallSegment.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddGate(Colony colony, (int x, int y) pos)
        {
            if (!colony.UnlockedTechs.Contains("fortification"))
                return false;
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new GateStructure(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, GateStructure.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddArrowTower(Colony colony, (int x, int y) pos)
        {
            if (!colony.UnlockedTechs.Contains("fortification"))
                return false;
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new ArrowTower(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, ArrowTower.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        public bool TryAddCatapultTower(Colony colony, (int x, int y) pos)
        {
            if (!colony.UnlockedTechs.Contains("siege_craft"))
                return false;
            if (!InBounds(pos.x, pos.y) || IsOccupiedByStructure(pos.x, pos.y) || GetTile(pos.x, pos.y).Ground == Ground.Water)
                return false;

            DefensiveStructures.Add(new CatapultTower(
                _nextDefenseStructureId++,
                colony,
                pos,
                maxHp: ScaleFortificationHp(colony, CatapultTower.DefaultHp)));
            _navigationTopologyVersion++;
            MarkTerritoryDirty();
            return true;
        }

        private static float ScaleFortificationHp(Colony colony, float baseHp)
        {
            var multiplier = Math.Max(1f, colony.FortificationHpMultiplier);
            return baseHp * multiplier;
        }

        public bool TryDamageDefensiveStructure((int x, int y) pos, float damage)
            => TryDamageDefensiveStructure(pos, damage, null);

        public bool TryDamageDefensiveStructure((int x, int y) pos, float damage, Colony? attacker)
        {
            var structure = DefensiveStructures.FirstOrDefault(s => s.Pos == pos && !s.IsDestroyed);
            if (structure == null)
                return false;

            var wasDestroyed = structure.IsDestroyed;
            structure.ApplyDamage(damage);
            if (!wasDestroyed && structure.IsDestroyed)
            {
                _navigationTopologyVersion++;
                MarkTerritoryDirty();

                TotalStructuresDestroyed++;
                switch (structure.Kind)
                {
                    case DefensiveStructureKind.WoodWall:
                    case DefensiveStructureKind.StoneWall:
                    case DefensiveStructureKind.ReinforcedWall:
                        TotalWallsDestroyed++;
                        break;
                    case DefensiveStructureKind.Gate:
                        TotalGatesDestroyed++;
                        break;
                    case DefensiveStructureKind.Watchtower:
                    case DefensiveStructureKind.ArrowTower:
                    case DefensiveStructureKind.CatapultTower:
                        TotalTowersDestroyed++;
                        break;
                }

                if (structure.Kind is DefensiveStructureKind.WoodWall
                    or DefensiveStructureKind.StoneWall
                    or DefensiveStructureKind.ReinforcedWall
                    or DefensiveStructureKind.Gate)
                {
                    TotalBreaches++;
                    var attackerColonyId = attacker?.Id ?? -1;
                    _recentBreaches.Add(new RuntimeBreachState(
                        structure.Id,
                        structure.Owner.Id,
                        attackerColonyId,
                        structure.Pos,
                        _tickCounter,
                        structure.Kind));
                    AddEvent($"Wall breached near {structure.Owner.Name}!");

                    if (attacker != null)
                    {
                        var key = (attackerColonyId: attacker.Id, defenderColonyId: structure.Owner.Id);
                        if (_siegeSessions.TryGetValue(key, out var session))
                            session.BreachCount++;
                    }
                }

                AddEvent($"{structure.Owner.Name} lost {structure.Kind}");
            }
            return true;
        }

        internal void RemoveDestroyedDefensiveStructures()
        {
            int before = DefensiveStructures.Count;
            DefensiveStructures.RemoveAll(s => s.IsDestroyed);
            if (DefensiveStructures.Count != before)
            {
                _navigationTopologyVersion++;
                MarkTerritoryDirty();
            }
        }

        public bool IsMovementBlocked(int x, int y, int moverColonyId)
        {
            if (!InBounds(x, y))
                return true;

            if (GetTile(x, y).Ground == Ground.Water)
                return true;

            if (Houses.Any(h => h.Pos.x == x && h.Pos.y == y))
                return true;

            if (SpecializedBuildings.Any(b => b.Pos.x == x && b.Pos.y == y))
                return true;

            var defense = DefensiveStructures.FirstOrDefault(s => s.Pos.x == x && s.Pos.y == y && !s.IsDestroyed);
            if (defense == null)
                return false;

            if (moverColonyId < 0)
                return true;

            if (defense.Owner.Id == moverColonyId)
                return false;

            var moverColony = _colonies.FirstOrDefault(c => c.Id == moverColonyId);
            if (moverColony == null)
                return true;

            if (defense is GateStructure gate)
            {
                if (gate.IsOpen)
                    return false;
                return moverColony.Faction != defense.Owner.Faction;
            }

            return moverColony.Faction != defense.Owner.Faction;
        }

        private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        private (int x, int y)? FindAnyFreeLandNear((int x, int y) origin)
        {
            int maxRadius = Math.Max(Width, Height);
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int md = Math.Abs(dx) + Math.Abs(dy);
                        if (md > radius)
                            continue;

                        int x = origin.x + dx;
                        int y = origin.y + dy;
                        if (CanPlaceStructureAt(x, y))
                            return (x, y);
                    }
                }
            }

            return null;
        }

        private bool IsOccupiedByStructure(int x, int y)
            => Houses.Any(h => h.Pos.x == x && h.Pos.y == y)
               || SpecializedBuildings.Any(b => b.Pos.x == x && b.Pos.y == y)
               || DefensiveStructures.Any(s => s.Pos.x == x && s.Pos.y == y && !s.IsDestroyed);

        internal RuntimeNpcBrain CreateNpcBrain(Colony colony) => _brainFactory(colony);

        public void ReportAnimalStuckRecovery() => TotalAnimalStuckRecoveries++;
        public void ReportPredatorDeath() => TotalPredatorDeaths++;
        public void ReportPredatorKilledByHumans() => TotalPredatorKillsByHumans++;
        public void ReportPredatorHumanHit() => TotalPredatorHumanHits++;
        public void ReportCombatEngagement() => TotalCombatEngagements++;
        public void ReportCombatDeath() => TotalCombatDeaths++;
        public void ReportSiegePressure(Colony attacker, DefensiveStructure target)
        {
            if (attacker == null || target == null)
                return;

            _siegePressureThisTick.Add(new RuntimeSiegePressure(
                attackerColonyId: attacker.Id,
                defenderColonyId: target.Owner.Id,
                targetStructureId: target.Id,
                targetKind: target.Kind,
                targetPos: target.Pos));
        }
        public void ReportBuildSiteReset() => TotalBuildSiteResetCount++;
        public void ReportNoProgressBackoff(string context)
        {
            switch (context)
            {
                case "resource":
                    TotalNoProgressBackoffResource++;
                    break;
                case "build":
                    TotalNoProgressBackoffBuild++;
                    break;
                case "flee":
                    TotalNoProgressBackoffFlee++;
                    break;
                case "combat":
                    TotalNoProgressBackoffCombat++;
                    break;
            }
        }

        public void ReportAiDecisionSignal(string? replanReason, NpcCommand command)
        {
            if (string.Equals(replanReason, "NoPlan", StringComparison.Ordinal))
                TotalAiNoPlanDecisions++;
            else if (string.Equals(replanReason, "ReplanBackoff", StringComparison.Ordinal))
                TotalAiReplanBackoffDecisions++;

            if (command == NpcCommand.ResearchTech)
                TotalAiResearchTechDecisions++;
        }
        public ColonyDeathStats GetColonyDeathStats(int colonyId)
            => _colonyDeathStats.TryGetValue(colonyId, out var stats)
                ? stats
                : new ColonyDeathStats();

        public int GetTileOwnerColonyId(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return -1;
            return _tileOwnerColonyIds[x, y];
        }

        public bool IsTileContested(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return false;
            return _tileContested[x, y];
        }

        public int GetContestedTilesForFactionPair(Faction left, Faction right)
        {
            var pair = NormalizeFactionPair(left, right);
            return _contestedTilesByFactionPair.GetValueOrDefault(pair, 0);
        }

        public ColonyWarState GetColonyWarState(int colonyId)
            => _colonyWarStates.TryGetValue(colonyId, out var state) ? state : ColonyWarState.Peace;

        public int GetColonyWarriorCount(int colonyId)
            => _colonyWarriorCounts.TryGetValue(colonyId, out var count) ? count : 0;

        public int CurrentTick => _tickCounter;
        public int TerritoryRecomputeCount => _territoryRecomputeCount;
        public int ActiveSoftReservationCount => _softReservations.Count;

        public int GetSoftReservationCount(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return 0;
            return _softReservations.GetValueOrDefault(key, 0);
        }

        public void ReserveSoftTarget(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
            _softReservations[key] = _softReservations.GetValueOrDefault(key, 0) + 1;
        }

        public void RegisterDomainModifier(string sourceId, RuntimeDomain domain, double modifier, int durationTicks, double dampeningFactor)
            => _domainModifierEngine.RegisterModifier(sourceId, domain, modifier, durationTicks, dampeningFactor);

        public double GetDomainModifier(RuntimeDomain domain)
            => _domainModifierEngine.GetEffectiveModifier(domain);

        public double GetDomainMultiplier(RuntimeDomain domain)
            => 1d + GetDomainModifier(domain);

        public IReadOnlyList<ActiveDomainModifierInfo> GetActiveDomainModifiers()
            => _domainModifierEngine.GetActiveModifiers();

        public void RegisterGoalBiases(string sourceId, int colonyId, IReadOnlyList<GoalBiasSpec> biases, int durationTicks, double dampeningFactor)
            => _goalBiasEngine.RegisterBiases(sourceId, colonyId, biases, durationTicks, dampeningFactor);

        public void ReplaceGoalBiases(string sourceId, int colonyId, IReadOnlyList<GoalBiasSpec> biases, int durationTicks, double dampeningFactor)
            => _goalBiasEngine.ReplaceDirective(sourceId, colonyId, biases, durationTicks, dampeningFactor);

        public double GetEffectiveGoalBias(int colonyId, string goalCategory)
            => _goalBiasEngine.GetEffectiveBias(colonyId, goalCategory);

        public bool IsGoalPriorityActive(int colonyId, string goalCategory)
            => _goalBiasEngine.IsJobPriorityActive(colonyId, goalCategory);

        public IReadOnlyList<ActiveGoalBiasInfo> GetActiveGoalBiases(int colonyId)
            => _goalBiasEngine.GetActiveBiases(colonyId);

        public Stance GetFactionStance(Faction left, Faction right)
        {
            if (_factionStances.TryGetValue((left, right), out var stance))
                return stance;
            return Stance.Neutral;
        }

        public void SetFactionStance(Faction left, Faction right, Stance stance)
        {
            _factionStances[(left, right)] = stance;
            _factionStances[(right, left)] = stance;
        }

        public void RegisterRaidImpact(Faction attacker, Faction defender, double pressureBoost = 60d)
        {
            _relationManager.RegisterRaidImpact(attacker, defender, pressureBoost);
        }

        public bool DeclareWar(Faction attacker, Faction defender, out Stance previous, out Stance current)
        {
            var changed = _relationManager.DeclareWar(this, attacker, defender, out previous, out current);
            RecomputeMobilizationState();
            return changed;
        }

        public bool ProposeTreaty(Faction proposer, Faction receiver, string treatyKind, out Stance previous, out Stance current)
        {
            var changed = _relationManager.ProposeTreaty(this, proposer, receiver, treatyKind, out previous, out current);
            RecomputeMobilizationState();
            return changed;
        }

        public IReadOnlyList<FactionStanceState> GetFactionStanceMatrix()
            => _factionStances
                .Where(entry => entry.Key.left <= entry.Key.right)
                .Select(entry => new FactionStanceState(entry.Key.left, entry.Key.right, entry.Value))
                .OrderBy(entry => entry.Left)
                .ThenBy(entry => entry.Right)
                .ToList();

        public IReadOnlyList<CombatGroupState> GetActiveCombatGroups()
            => _activeCombatGroups
                .Select(group => new CombatGroupState(
                    GroupId: group.GroupId,
                    ColonyId: group.Colony.Id,
                    FactionId: (int)group.Colony.Faction,
                    Formation: group.Formation,
                    MemberCount: group.Members.Count,
                    RoutingMemberCount: group.RoutingMemberCount,
                    IsRouting: group.IsRouting,
                    AverageMorale: group.AverageMorale,
                    CommanderActorId: group.Commander?.Id ?? -1,
                    CommanderIntelligence: group.CommanderIntelligence,
                    CommanderMoraleStabilityBonus: group.CommanderMoraleStabilityBonus,
                    AnchorX: group.Anchor.x,
                    AnchorY: group.Anchor.y,
                    StrengthScore: group.StrengthScore,
                    DefenseScore: group.DefenseScore,
                    BattleId: group.BattleId))
                .ToList();

        public IReadOnlyList<BattleState> GetActiveBattles()
            => _activeBattles
                .Select(battle => new BattleState(
                    BattleId: battle.BattleId,
                    LeftGroupId: battle.Left.GroupId,
                    RightGroupId: battle.Right.GroupId,
                    LeftAverageMorale: battle.Left.AverageMorale,
                    RightAverageMorale: battle.Right.AverageMorale,
                    LeftIsRouting: battle.Left.IsRouting,
                    RightIsRouting: battle.Right.IsRouting,
                    LeftCommanderActorId: battle.Left.Commander?.Id ?? -1,
                    RightCommanderActorId: battle.Right.Commander?.Id ?? -1,
                    CenterX: battle.Center.x,
                    CenterY: battle.Center.y,
                    Radius: battle.Radius,
                    Intensity: battle.Intensity,
                    ElapsedTicks: battle.ElapsedTicks))
                .ToList();

        public IReadOnlyList<SiegeState> GetActiveSieges()
            => _activeSieges
                .Select(siege => new SiegeState(
                    siege.SiegeId,
                    siege.AttackerColonyId,
                    siege.DefenderColonyId,
                    siege.TargetStructureId,
                    siege.TargetKind,
                    siege.Center.x,
                    siege.Center.y,
                    siege.ActiveAttackerCount,
                    siege.StartedTick,
                    siege.LastActiveTick,
                    siege.BreachCount,
                    siege.Status))
                .ToList();

        public IReadOnlyList<BreachState> GetRecentBreaches()
            => _recentBreaches
                .Select(breach => new BreachState(
                    breach.StructureId,
                    breach.DefenderColonyId,
                    breach.AttackerColonyId,
                    breach.Pos.x,
                    breach.Pos.y,
                    breach.CreatedTick,
                    breach.StructureKind))
                .ToList();

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
                case PersonDeathReason.Combat:
                    TotalCombatDeaths++;
                    TotalDeathsOther++;
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
                case PersonDeathReason.Combat:
                    colonyStats.Other++;
                    break;
                default:
                    colonyStats.Other++;
                    break;
            }

            MarkTerritoryDirty();
            TrimRecentDeathWindows();
        }

        private void TrimRecentDeathWindows()
        {
            const float rollingWindowSeconds = 60f;
            while (_recentStarvationDeaths.Count > 0 && (_simulationTimeSeconds - _recentStarvationDeaths.Peek()) > rollingWindowSeconds)
                _recentStarvationDeaths.Dequeue();
        }

        private void DeconflictPeopleEndPositions()
        {
            var occupied = new HashSet<(int x, int y)>();
            var keeperByTile = new Dictionary<(int x, int y), Person>();
            foreach (var person in _people)
            {
                if (person.Health <= 0f)
                    continue;

                var tile = person.Pos;
                if (!keeperByTile.TryGetValue(tile, out var keeper))
                {
                    occupied.Add(tile);
                    keeperByTile[tile] = person;
                    continue;
                }

                var mover = person;
                if (person.IsActivePeacefulIntentProtected && !keeper.IsActivePeacefulIntentProtected)
                {
                    mover = keeper;
                    keeperByTile[tile] = person;
                }

                if (TryFindNearbyFreePersonTile(mover, mover.Pos, occupied, out var fallback))
                {
                    mover.Pos = fallback;
                    occupied.Add(fallback);
                    keeperByTile[fallback] = mover;
                    TotalOverlapResolveMoves++;
                }
            }

            DissipateLocalCrowds(occupied);
            UpdateDenseNeighborhoodTelemetry(occupied);
        }

        private void ResolveGroupCombatPhase()
        {
            if (!EnableCombatPrimitives)
                return;

            var groups = BuildCombatGroups();
            if (groups.Count == 0)
                return;

            _activeCombatGroups.AddRange(groups);
            var paired = new HashSet<int>();

            while (true)
            {
                RuntimeCombatGroup? left = null;
                RuntimeCombatGroup? right = null;
                var bestDistance = int.MaxValue;

                for (var i = 0; i < groups.Count; i++)
                {
                    var a = groups[i];
                    if (paired.Contains(a.GroupId) || a.IsRouting || a.Members.Count == 0)
                        continue;

                    for (var j = i + 1; j < groups.Count; j++)
                    {
                        var b = groups[j];
                        if (paired.Contains(b.GroupId) || b.IsRouting || b.Members.Count == 0)
                            continue;
                        if (a.Colony.Id == b.Colony.Id)
                            continue;

                        var stance = GetFactionStance(a.Colony.Faction, b.Colony.Faction);
                        if (stance < Stance.Hostile)
                            continue;

                        var distance = Math.Abs(a.Anchor.x - b.Anchor.x) + Math.Abs(a.Anchor.y - b.Anchor.y);
                        if (distance > 6 || distance >= bestDistance)
                            continue;

                        bestDistance = distance;
                        left = a;
                        right = b;
                    }
                }

                if (left == null || right == null)
                    break;

                paired.Add(left.GroupId);
                paired.Add(right.GroupId);

                var battle = new RuntimeBattleState(_nextBattleId++, left, right);
                left.BattleId = battle.BattleId;
                right.BattleId = battle.BattleId;
                _activeBattles.Add(battle);

                foreach (var member in left.Members)
                    member.SetCombatAssignment(left.GroupId, battle.BattleId, left.Formation, isCommander: ReferenceEquals(member, left.Commander));
                foreach (var member in right.Members)
                    member.SetCombatAssignment(right.GroupId, battle.BattleId, right.Formation, isCommander: ReferenceEquals(member, right.Commander));

                ResolveBattleTick(battle);
                TotalBattleTicks++;
            }

            ResolveBattleLocalSpacing();
        }

        private List<RuntimeCombatGroup> BuildCombatGroups()
        {
            var groups = new List<RuntimeCombatGroup>();
            var eligibleByColony = _people
                .Where(person => person.Health > 0f
                                 && !person.IsRouting
                                 && (person.Current is Job.Fight or Job.RaidBorder or Job.AttackStructure
                                     || HasNearbyHostile(person, radius: 3)))
                .GroupBy(person => person.Home)
                .ToList();

            foreach (var colonyGroup in eligibleByColony)
            {
                foreach (var members in ClusterByProximity(colonyGroup.ToList(), radius: 3))
                {
                    if (members.Count == 0)
                        continue;

                    var formation = PickFormation(members, colonyGroup.Key);
                    var runtimeGroup = new RuntimeCombatGroup(
                        groupId: _nextCombatGroupId++,
                        colony: colonyGroup.Key,
                        formation: formation,
                        members: members);

                    foreach (var member in members)
                    {
                        member.SetCombatAssignment(runtimeGroup.GroupId, null, formation, isCommander: ReferenceEquals(member, runtimeGroup.Commander));
                        member.ApplyMoraleDelta(0.20f);
                    }

                    groups.Add(runtimeGroup);
                }
            }

            return groups;
        }

        private static Formation PickFormation(IReadOnlyList<Person> members, Colony colony)
        {
            if (members.Count <= 2)
                return Formation.Skirmish;

            var commander = members
                .OrderByDescending(person => person.Intelligence)
                .ThenByDescending(person => person.Strength + person.Defense)
                .FirstOrDefault();
            var commanderIq = commander?.Intelligence ?? 0;
            var avgStrength = members.Average(person => person.Strength);
            var avgDefense = members.Average(person => person.Defense);

            if (commanderIq >= 14)
            {
                if (colony.WeaponLevel > colony.ArmorLevel + 1)
                    return Formation.Wedge;
                if (avgDefense > avgStrength * 1.10f)
                    return Formation.DefensiveCircle;
            }

            if (avgDefense > avgStrength * 1.20f)
                return Formation.DefensiveCircle;
            if (colony.WeaponLevel > colony.ArmorLevel && members.Count >= 3)
                return Formation.Wedge;

            return Formation.Line;
        }

        private bool HasNearbyHostile(Person actor, int radius)
        {
            for (var i = 0; i < _people.Count; i++)
            {
                var other = _people[i];
                if (ReferenceEquals(actor, other) || other.Health <= 0f || other.Home == actor.Home)
                    continue;

                var stance = GetFactionStance(actor.Home.Faction, other.Home.Faction);
                if (stance < Stance.Hostile)
                    continue;

                var distance = Math.Abs(actor.Pos.x - other.Pos.x) + Math.Abs(actor.Pos.y - other.Pos.y);
                if (distance <= radius)
                    return true;
            }

            return false;
        }

        private static List<List<Person>> ClusterByProximity(List<Person> people, int radius)
        {
            var clusters = new List<List<Person>>();
            var visited = new HashSet<int>();

            for (var i = 0; i < people.Count; i++)
            {
                var seed = people[i];
                if (!visited.Add(seed.Id))
                    continue;

                var queue = new Queue<Person>();
                var cluster = new List<Person>();
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    cluster.Add(current);

                    for (var j = 0; j < people.Count; j++)
                    {
                        var candidate = people[j];
                        if (visited.Contains(candidate.Id))
                            continue;

                        var distance = Math.Abs(current.Pos.x - candidate.Pos.x) + Math.Abs(current.Pos.y - candidate.Pos.y);
                        if (distance > radius)
                            continue;

                        visited.Add(candidate.Id);
                        queue.Enqueue(candidate);
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        private void ResolveBattleTick(RuntimeBattleState battle)
        {
            var leftMembers = battle.Left.Members.Where(member => member.Health > 0f && !member.IsRouting).ToList();
            var rightMembers = battle.Right.Members.Where(member => member.Health > 0f && !member.IsRouting).ToList();
            if (leftMembers.Count == 0 || rightMembers.Count == 0)
                return;

            battle.ElapsedTicks++;

            var leftAttack = GroupCombatResolver.ComputeGroupAttackScore(
                battle.Left.StrengthScore,
                battle.Left.Formation,
                battle.Left.Colony.WeaponLevel);
            var leftDefense = GroupCombatResolver.ComputeGroupDefenseScore(
                battle.Left.DefenseScore,
                battle.Left.Formation,
                battle.Left.Colony.ArmorLevel);
            var rightAttack = GroupCombatResolver.ComputeGroupAttackScore(
                battle.Right.StrengthScore,
                battle.Right.Formation,
                battle.Right.Colony.WeaponLevel);
            var rightDefense = GroupCombatResolver.ComputeGroupDefenseScore(
                battle.Right.DefenseScore,
                battle.Right.Formation,
                battle.Right.Colony.ArmorLevel);

            var exchanges = Math.Max(1, Math.Min(6, (leftMembers.Count + rightMembers.Count) / 2));
            battle.Intensity = exchanges;

            var leftBefore = leftMembers.Count;
            var rightBefore = rightMembers.Count;

            for (var i = 0; i < exchanges; i++)
            {
                if (leftMembers.Count == 0 || rightMembers.Count == 0)
                    break;

                var left = leftMembers[_rng.Next(leftMembers.Count)];
                var right = rightMembers[_rng.Next(rightMembers.Count)];

                left.MarkCombatPresence(this);
                right.MarkCombatPresence(this);

                var leftDamage = GroupCombatResolver.ComputePerHitDamage(_rng, leftAttack, rightDefense, leftMembers.Count, rightMembers.Count);
                right.ApplyCombatDamage(this, left.ScaleOutgoingCombatDamage(this, leftDamage), "GroupCombat");
                ReportCombatEngagement();

                if (right.Health > 0f && _rng.NextDouble() < 0.65)
                {
                    var rightDamage = GroupCombatResolver.ComputePerHitDamage(_rng, rightAttack, leftDefense, rightMembers.Count, leftMembers.Count);
                    left.ApplyCombatDamage(this, right.ScaleOutgoingCombatDamage(this, rightDamage), "GroupCombat");
                    ReportCombatEngagement();
                }

                leftMembers = leftMembers.Where(member => member.Health > 0f && !member.IsRouting).ToList();
                rightMembers = rightMembers.Where(member => member.Health > 0f && !member.IsRouting).ToList();
            }

            var leftLosses = Math.Max(0, leftBefore - leftMembers.Count);
            var rightLosses = Math.Max(0, rightBefore - rightMembers.Count);

            ApplyBattleMoraleShift(battle.Left, ownLosses: leftLosses, enemyLosses: rightLosses);
            ApplyBattleMoraleShift(battle.Right, ownLosses: rightLosses, enemyLosses: leftLosses);

            if (battle.Left.StrengthScore > battle.Right.StrengthScore * 1.8f)
                ApplyBattleMoraleShift(battle.Right, ownLosses: 1, enemyLosses: 0);
            else if (battle.Right.StrengthScore > battle.Left.StrengthScore * 1.8f)
                ApplyBattleMoraleShift(battle.Left, ownLosses: 1, enemyLosses: 0);

            TryStartRouting(battle.Left);
            TryStartRouting(battle.Right);
        }

        private static void ApplyBattleMoraleShift(RuntimeCombatGroup group, int ownLosses, int enemyLosses)
        {
            var delta = (enemyLosses * 2.4f) - (ownLosses * 6.0f) - 1.2f;
            if (group.Members.Count > 0)
            {
                var ownLossRatio = ownLosses / (float)Math.Max(1, group.Members.Count);
                delta -= ownLossRatio * 6f;
            }

            foreach (var member in group.Members)
            {
                if (member.Health <= 0f)
                    continue;
                member.ApplyMoraleDelta(delta);
            }
        }

        private void TryStartRouting(RuntimeCombatGroup group)
        {
            if (group.Members.Count == 0)
                return;

            var shouldRoute = group.AverageMorale <= 24f || group.RoutingMemberCount >= Math.Max(1, group.Members.Count / 2);
            if (!shouldRoute)
                return;

            foreach (var member in group.Members)
            {
                if (member.Health <= 0f)
                    continue;
                member.BeginRouting(6 + _rng.Next(0, 3), origin: group.Anchor);
            }
        }

        private void ResolveBattleLocalSpacing()
        {
            if (_activeBattles.Count == 0)
                return;

            var occupied = _people
                .Where(person => person.Health > 0f)
                .Select(person => person.Pos)
                .ToHashSet();

            var battleCenters = _activeBattles
                .ToDictionary(battle => battle.BattleId, battle => battle.Center);

            int maxMoves = Math.Max(1, _activeBattles.Count * 3);
            int moves = 0;

            var candidates = _people
                .Where(person => person.Health > 0f && person.ActiveBattleId >= 0)
                .OrderByDescending(person => person.IsRouting)
                .ThenByDescending(person => CountNeighbors(occupied, person.Pos, 1))
                .ToList();

            foreach (var person in candidates)
            {
                if (moves >= maxMoves)
                    break;
                if (!battleCenters.TryGetValue(person.ActiveBattleId, out var center))
                    continue;

                int crowd = CountNeighbors(occupied, person.Pos, 1);
                if (!person.IsRouting && crowd <= 3)
                    continue;

                if (!TryFindBattleSpacingTile(person, center, occupied, out var target))
                    continue;

                occupied.Remove(person.Pos);
                person.Pos = target;
                occupied.Add(target);
                moves++;
                TotalOverlapResolveMoves++;
            }
        }

        private bool TryFindBattleSpacingTile(Person person, (int x, int y) center, HashSet<(int x, int y)> occupied, out (int x, int y) tile)
        {
            float bestScore = float.MinValue;
            (int x, int y)? best = null;
            int currentCrowd = CountNeighbors(occupied, person.Pos, 1);
            int currentDist = Math.Abs(person.Pos.x - center.x) + Math.Abs(person.Pos.y - center.y);

            for (int radius = 1; radius <= 3; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = person.Pos.x + dx;
                        int ny = person.Pos.y + dy;
                        if (!InBounds(nx, ny))
                            continue;
                        if (_map[nx, ny].Ground == Ground.Water)
                            continue;
                        if (IsMovementBlocked(nx, ny, person.Home.Id))
                            continue;
                        if (occupied.Contains((nx, ny)))
                            continue;

                        int dist = Math.Abs(nx - center.x) + Math.Abs(ny - center.y);
                        int crowd = CountNeighbors(occupied, (nx, ny), 1);
                        if (!person.IsRouting)
                        {
                            if (dist > 4)
                                continue;
                            if (crowd >= currentCrowd)
                                continue;
                        }

                        float score = person.IsRouting
                            ? (dist * 10f) - crowd - radius
                            : ((4 - dist) * 4f) - (crowd * 6f) - radius;

                        if (!person.IsRouting && dist > currentDist + 1)
                            score -= 10f;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = (nx, ny);
                        }
                    }
                }
            }

            tile = best ?? person.Pos;
            return best != null;
        }

        private void ResolveSiegeStatePhase()
        {
            if (!EnableSiege || !EnableCombatPrimitives)
            {
                _activeSieges.Clear();
                return;
            }

            foreach (var session in _siegeSessions.Values)
                session.ActiveAttackerCount = 0;

            var activeKeys = new HashSet<(int attackerColonyId, int defenderColonyId)>();
            foreach (var pressure in _siegePressureThisTick)
            {
                if (pressure.attackerColonyId == pressure.defenderColonyId)
                    continue;

                var key = (attackerColonyId: pressure.attackerColonyId, defenderColonyId: pressure.defenderColonyId);
                activeKeys.Add(key);

                if (!_siegeSessions.TryGetValue(key, out var session))
                {
                    session = new RuntimeSiegeSession(_nextSiegeId++, pressure.attackerColonyId, pressure.defenderColonyId, _tickCounter);
                    _siegeSessions[key] = session;
                    TotalSiegesStarted++;
                    var defender = _colonies.FirstOrDefault(colony => colony.Id == pressure.defenderColonyId);
                    if (defender != null)
                        AddEvent($"Siege began near {defender.Name}");
                }

                session.LastActiveTick = _tickCounter;
                session.ActiveAttackerCount++;

                var status = session.BreachCount > 0 ? "breached" : "active";
                _activeSieges.Add(new RuntimeSiegeState(
                    session.SiegeId,
                    session.AttackerColonyId,
                    session.DefenderColonyId,
                    pressure.targetStructureId,
                    pressure.targetKind,
                    pressure.targetPos,
                    session.ActiveAttackerCount,
                    session.StartedTick,
                    session.LastActiveTick,
                    session.BreachCount,
                    status));
            }

            var stale = _siegeSessions
                .Where(entry => !activeKeys.Contains(entry.Key) && (_tickCounter - entry.Value.LastActiveTick) > 6)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in stale)
            {
                var session = _siegeSessions[key];
                if (session.BreachCount == 0)
                {
                    TotalSiegesRepelled++;
                    var defender = _colonies.FirstOrDefault(colony => colony.Id == session.DefenderColonyId);
                    if (defender != null)
                        AddEvent($"Siege repelled by {defender.Name}");
                }

                _siegeSessions.Remove(key);
            }
        }

        private void TrimRecentBreachWindow()
        {
            const int breachVisibleTicks = 120;
            _recentBreaches.RemoveAll(breach => (_tickCounter - breach.CreatedTick) > breachVisibleTicks);
        }

        private bool TryFindNearbyFreePersonTile(Person person, (int x, int y) center, HashSet<(int x, int y)> occupied, out (int x, int y) tile)
        {
            for (int radius = 1; radius <= 12; radius++)
            {
                int minX = Math.Max(0, center.x - radius);
                int maxX = Math.Min(Width - 1, center.x + radius);
                int minY = Math.Max(0, center.y - radius);
                int maxY = Math.Min(Height - 1, center.y + radius);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (_map[x, y].Ground == Ground.Water)
                            continue;
                        if (IsMovementBlocked(x, y, person.Home.Id))
                            continue;
                        if (occupied.Contains((x, y)))
                            continue;

                        tile = (x, y);
                        return true;
                    }
                }
            }

            tile = center;
            return false;
        }

        private void DissipateLocalCrowds(HashSet<(int x, int y)> occupied)
        {
            var alive = _people
                .Where(person => person.Health > 0f)
                .OrderByDescending(person => CountNeighbors(occupied, person.Pos, CrowdDissipationNeighborRadius))
                .ThenBy(person => person.Home.Id)
                .ThenBy(person => person.Pos.x)
                .ThenBy(person => person.Pos.y)
                .ToList();

            int maxMoves = Math.Max(1, alive.Count / 3);
            int moves = 0;
            foreach (var person in alive)
            {
                if (moves >= maxMoves)
                    break;
                if (!ShouldDissipate(person))
                    continue;

                int currentCrowd = CountNeighbors(occupied, person.Pos, CrowdDissipationNeighborRadius);
                if (currentCrowd < CrowdDissipationThreshold)
                    continue;

                if (!TryFindCrowdDissipationTile(person, occupied, currentCrowd, out var target))
                    continue;

                occupied.Remove(person.Pos);
                person.Pos = target;
                occupied.Add(target);
                moves++;
                TotalCrowdDissipationMoves++;
            }
        }

        private void UpdateDenseNeighborhoodTelemetry(HashSet<(int x, int y)> occupied)
        {
            int denseActors = 0;
            foreach (var pos in occupied)
            {
                if (CountNeighbors(occupied, pos, CrowdDissipationNeighborRadius) >= CrowdDissipationThreshold)
                    denseActors++;
            }

            LastTickDenseActors = denseActors;
            if (denseActors > 0)
                DenseNeighborhoodTicks++;
        }

        private bool ShouldDissipate(Person person)
        {
            if (person.IsInCombat)
                return false;
            if (person.BackoffTicksRemaining > 0)
                return false;
            if (person.IsActivePeacefulIntentProtected)
                return false;

            return person.Current is not (Job.Fight or Job.AttackStructure or Job.RaidBorder);
        }

        private bool TryFindCrowdDissipationTile(
            Person person,
            HashSet<(int x, int y)> occupied,
            int currentCrowd,
            out (int x, int y) tile)
        {
            float bestScore = float.MaxValue;
            (int x, int y)? best = null;

            for (int radius = 1; radius <= CrowdDissipationSearchRadius; radius++)
            {
                int minX = Math.Max(0, person.Pos.x - radius);
                int maxX = Math.Min(Width - 1, person.Pos.x + radius);
                int minY = Math.Max(0, person.Pos.y - radius);
                int maxY = Math.Min(Height - 1, person.Pos.y + radius);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int dist = Math.Abs(x - person.Pos.x) + Math.Abs(y - person.Pos.y);
                        if (dist == 0 || dist > radius)
                            continue;
                        if (_map[x, y].Ground == Ground.Water)
                            continue;
                        if (IsMovementBlocked(x, y, person.Home.Id))
                            continue;
                        if (occupied.Contains((x, y)))
                            continue;

                        int neighborCrowd = CountNeighbors(occupied, (x, y), CrowdDissipationNeighborRadius);
                        if (neighborCrowd + 1 >= currentCrowd)
                            continue;

                        float score = neighborCrowd * 5f + dist;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = (x, y);
                        }
                    }
                }
            }

            tile = best ?? person.Pos;
            return best != null;
        }

        private static int CountNeighbors(HashSet<(int x, int y)> occupied, (int x, int y) center, int radius)
        {
            int count = 0;
            foreach (var pos in occupied)
            {
                int dist = Math.Abs(pos.x - center.x) + Math.Abs(pos.y - center.y);
                if (dist <= radius)
                    count++;
            }

            return count;
        }

        private void MarkTerritoryDirty()
        {
            _territoryDirty = true;
        }

        private void RefreshTerritoryStateIfNeeded(bool force = false)
        {
            if (!force)
            {
                bool intervalElapsed = (_tickCounter - _lastTerritoryRecomputeTick) >= TerritoryRecomputeIntervalTicks;
                if (!_territoryDirty && !intervalElapsed)
                    return;
            }

            RecomputeTerritoryOwnership();
            _lastTerritoryRecomputeTick = _tickCounter;
            _territoryDirty = false;
            _territoryRecomputeCount++;
        }

        private void InitializeFactionStances()
        {
            var factions = (Faction[])Enum.GetValues(typeof(Faction));
            foreach (var left in factions)
            {
                foreach (var right in factions)
                    _factionStances[(left, right)] = Stance.Neutral;
            }
        }

        private void RecomputeTerritoryOwnership()
        {
            _contestedTilesByFactionPair.Clear();
            var livingByColony = _colonies.ToDictionary(
                colony => colony.Id,
                colony => _people.Count(person => person.Home == colony && person.Health > 0f));
            var housesByColony = Houses
                .GroupBy(house => house.Owner.Id)
                .ToDictionary(group => group.Key, group => group.Count());
            var specializedByColony = SpecializedBuildings
                .GroupBy(building => building.Owner.Id)
                .ToDictionary(group => group.Key, group => group.Count());

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (_map[x, y].Ground == Ground.Water || _colonies.Count == 0)
                    {
                        _tileOwnerColonyIds[x, y] = -1;
                        _tileContested[x, y] = false;
                        continue;
                    }

                    int bestId = -1;
                    int secondId = -1;
                    double bestScore = double.MinValue;
                    double secondScore = double.MinValue;

                    foreach (var colony in _colonies)
                    {
                        int distance = Math.Abs(x - colony.Origin.x) + Math.Abs(y - colony.Origin.y);
                        int living = livingByColony.GetValueOrDefault(colony.Id, 0);
                        int houseCount = housesByColony.GetValueOrDefault(colony.Id, 0);
                        int specializedCount = specializedByColony.GetValueOrDefault(colony.Id, 0);

                        double score = (24d / (1d + distance))
                                     + (living * 0.12d)
                                     + (houseCount * 0.35d)
                                     + (specializedCount * 0.45d);

                        if (score > bestScore || (Math.Abs(score - bestScore) < 0.0001d && colony.Id < bestId))
                        {
                            secondScore = bestScore;
                            secondId = bestId;
                            bestScore = score;
                            bestId = colony.Id;
                        }
                        else if (score > secondScore || (Math.Abs(score - secondScore) < 0.0001d && colony.Id < secondId))
                        {
                            secondScore = score;
                            secondId = colony.Id;
                        }
                    }

                    _tileOwnerColonyIds[x, y] = bestId;

                    bool contested = secondId >= 0 && (bestScore - secondScore) <= 2.2d;
                    _tileContested[x, y] = contested;

                    if (!contested)
                        continue;

                    var bestColony = _colonies.FirstOrDefault(colony => colony.Id == bestId);
                    var secondColony = _colonies.FirstOrDefault(colony => colony.Id == secondId);
                    if (bestColony == null || secondColony == null || bestColony.Faction == secondColony.Faction)
                        continue;

                    var pair = NormalizeFactionPair(bestColony.Faction, secondColony.Faction);
                    _contestedTilesByFactionPair[pair] = _contestedTilesByFactionPair.GetValueOrDefault(pair, 0) + 1;
                }
            }
        }

        private static (Faction left, Faction right) NormalizeFactionPair(Faction left, Faction right)
            => left <= right ? (left, right) : (right, left);

        private void RecomputeMobilizationState()
        {
            foreach (var colony in _colonies)
            {
                _colonyWarStates[colony.Id] = ColonyWarState.Peace;
                _colonyWarriorCounts[colony.Id] = 0;
            }

            if (!EnableDiplomacy || !EnableCombatPrimitives)
                return;

            var hostileColonyIds = new HashSet<int>();
            for (int i = 0; i < _people.Count; i++)
            {
                var a = _people[i];
                if (a.Health <= 0f)
                    continue;

                for (int j = i + 1; j < _people.Count; j++)
                {
                    var b = _people[j];
                    if (b.Health <= 0f || a.Home == b.Home)
                        continue;

                    int d = Math.Abs(a.Pos.x - b.Pos.x) + Math.Abs(a.Pos.y - b.Pos.y);
                    if (d <= 2)
                    {
                        hostileColonyIds.Add(a.Home.Id);
                        hostileColonyIds.Add(b.Home.Id);
                    }
                }
            }

            foreach (var colony in _colonies)
            {
                int living = _people.Count(p => p.Home == colony && p.Health > 0f);
                if (living <= 0)
                    continue;

                var maxStance = Stance.Neutral;
                foreach (var other in _colonies)
                {
                    if (other == colony)
                        continue;

                    var stance = GetFactionStance(colony.Faction, other.Faction);
                    if (stance > maxStance)
                        maxStance = stance;
                }

                bool hasHostileContact = hostileColonyIds.Contains(colony.Id);
                if (maxStance == Stance.War)
                {
                    _colonyWarStates[colony.Id] = ColonyWarState.War;
                    _colonyWarriorCounts[colony.Id] = Math.Max(1, living / 4);
                }
                else if (maxStance == Stance.Hostile || hasHostileContact)
                {
                    _colonyWarStates[colony.Id] = ColonyWarState.Tense;
                    _colonyWarriorCounts[colony.Id] = Math.Max(1, living / 8);
                }
                else
                {
                    _colonyWarStates[colony.Id] = ColonyWarState.Peace;
                    _colonyWarriorCounts[colony.Id] = 0;
                }
            }
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
                _animals.Add(new Herbivore(RandomFreePos(), CreateEntityRng()));
                return;
            }

            if (predators < Math.Max(3, herbivores / 6) && _rng.NextDouble() < 0.5)
                _animals.Add(new Predator(RandomFreePos(), CreateEntityRng()));
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
                                SpecializedBuildings.Any(b => b.Pos.x == x && b.Pos.y == y) ||
                                DefensiveStructures.Any(s => s.Pos.x == x && s.Pos.y == y && !s.IsDestroyed);
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
                bool prioritizeFarming = IsGoalPriorityActive(colony.Id, WorldSim.AI.GoalBiasCategories.Farming);
                bool prioritizeGathering = IsGoalPriorityActive(colony.Id, WorldSim.AI.GoalBiasCategories.Gathering);
                bool prioritizeBuilding = IsGoalPriorityActive(colony.Id, WorldSim.AI.GoalBiasCategories.Building);
                bool prioritizeCrafting = IsGoalPriorityActive(colony.Id, WorldSim.AI.GoalBiasCategories.Crafting);

                var targets = CreateProfessionTargets(
                    adults.Count,
                    emergencyFood,
                    prioritizeFarming,
                    prioritizeGathering,
                    prioritizeBuilding,
                    prioritizeCrafting);
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

        Dictionary<Profession, int> CreateProfessionTargets(
            int adultCount,
            bool emergencyFood,
            bool prioritizeFarming,
            bool prioritizeGathering,
            bool prioritizeBuilding,
            bool prioritizeCrafting)
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

            if (prioritizeFarming)
            {
                ratios[Profession.Farmer] += 0.22f;
                ratios[Profession.Hunter] += 0.08f;
                ratios[Profession.Generalist] = Math.Max(0.02f, ratios[Profession.Generalist] - 0.08f);
            }

            if (prioritizeGathering)
            {
                ratios[Profession.Lumberjack] += 0.12f;
                ratios[Profession.Miner] += 0.12f;
                ratios[Profession.Generalist] = Math.Max(0.02f, ratios[Profession.Generalist] - 0.06f);
            }

            if (prioritizeBuilding)
            {
                ratios[Profession.Builder] += 0.5f;
                ratios[Profession.Generalist] = Math.Max(0.02f, ratios[Profession.Generalist] - 0.14f);
            }

            if (prioritizeCrafting)
            {
                ratios[Profession.Builder] += 0.08f;
                ratios[Profession.Miner] += 0.14f;
                ratios[Profession.Generalist] = Math.Max(0.02f, ratios[Profession.Generalist] - 0.06f);
            }

            var ratioTotal = ratios.Values.Sum();
            if (ratioTotal > 0f)
            {
                var keys = ratios.Keys.ToList();
                foreach (var key in keys)
                    ratios[key] /= ratioTotal;
            }

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
