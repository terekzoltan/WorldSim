using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WorldSim.AI;
using WorldSim.Simulation.Combat;
using WorldSim.Simulation.Effects;
using WorldSim.Simulation.Navigation;

namespace WorldSim.Simulation;

public enum Job
{
    Idle,
    GatherWood,
    GatherStone,
    GatherIron,
    GatherGold,
    GatherFood,
    EatFood,
    Rest,
    BuildHouse,
    CraftTools,
    BuildWall,
    BuildWatchtower,
    RaidBorder,
    AttackStructure,
    RefillInventory,
    Fight,
    Flee
}
public enum Profession { Generalist, Lumberjack, Miner, Farmer, Hunter, Builder }
public enum PersonDeathReason { None, OldAge, Starvation, Predator, Combat, Other }

public class Person
{
    private static int _nextFallbackPersonId = 1;

    public int Id { get; }
    public (int x, int y) Pos;
    public Job Current = Job.Idle;
    public float Health = 100;  
    public float Age = 0;
    public float Stamina { get; private set; } = 100f;
    public Profession Profession { get; internal set; }
    public PersonDeathReason LastDeathReason { get; private set; } = PersonDeathReason.None;
    public int Strength, Intelligence;
    public int Defense { get; set; }
    public bool IsInCombat { get; private set; }
    public int LastCombatTick { get; private set; } = -1;
    public float CombatMorale { get; private set; } = 100f;
    public bool IsRouting { get; private set; }
    public int RoutingTicksRemaining { get; private set; }
    public int ActiveCombatGroupId { get; private set; } = -1;
    public int ActiveBattleId { get; private set; } = -1;
    public Formation AssignedFormation { get; private set; } = Formation.Line;
    public bool IsCombatCommander { get; private set; }
    public int CommanderIntelligence { get; private set; }
    public float CommanderMoraleStabilityBonus { get; private set; }
    public Colony Home => _home;
    public PersonInventory Inventory { get; } = new();

    Colony _home;
    readonly Random _rng;

    // Perception and internal state
    public Blackboard Blackboard { get; } = new();
    public Memory Memory { get; } = new();
    public List<Sensor> Sensors { get; } = new() { new EnvironmentSensor() };

    public Dictionary<string, float> Needs { get; } = new();
    public Dictionary<string, float> Emotions { get; } = new();
    public HashSet<string> Traits { get; } = new();

    // Utility/GOAP AI via runtime adapter boundary
    private readonly RuntimeNpcBrain _brain;
    internal RuntimeAiDecision? LastAiDecision => _brain.LastDecision;

    const int WoodWorkTime = 5;
    const int StoneWorkTime = 8;
    const int IronWorkTime = 10;
    const int GoldWorkTime = 12;
    const int FoodWorkTime = 4;
    const int EatWorkTime = 2;
    const int RestWorkTime = 4;
    const int BuildHouseTime = 20;
    const int CraftToolsTime = 14;
    const int BuildWallTime = 8;
    const int BuildWatchtowerTime = 16;
    const int WallWoodCost = 8;
    const int StoneWallStoneCost = 10;
    const int ReinforcedWallStoneCost = 14;
    const int ReinforcedWallIronCost = 4;
    const int GateWoodCost = 8;
    const int GateStoneCost = 8;
    const int WatchtowerWoodCost = 16;
    const int WatchtowerStoneCost = 6;
    const int ArrowTowerWoodCost = 18;
    const int ArrowTowerStoneCost = 10;
    const int CatapultTowerWoodCost = 20;
    const int CatapultTowerStoneCost = 12;
    const int CatapultTowerIronCost = 8;
    const float StructureRaidDamage = 26f;
    const float AgingTickDivisor = 90f;

    int _doingJob = 0; // csinalni hogy ideig dolgozzon ne instant

    // Idle loitering → wander only after some time doing nothing
    float _idleTimeSeconds = 0f;
    float _loiterThresholdSeconds; // randomized per person
    (int x, int y) _lastPos;
    float _combatMarkerSeconds;
    readonly NavigationPathCache _pathCache = new();
    int _unstickStepsRemaining;
    int _noProgressStreak;
    int _backoffTicksRemaining;
    bool _trackNoProgressForCurrentMove;
    string _noProgressTrackContext = "move";
    bool _suppressPeacefulActionsDuringBackoff;
    bool _protectFromDissipationThisTick;
    (int x, int y)? _routingOrigin;
    int _lastAiThinkTick = -1;
    float _lastAiThinkDt = -1f;
    Job _lastAiThinkResult = Job.Idle;
    bool _hasCachedAiThink;
    float _movementStepCarry;
    int _recentHostileContactTick = int.MinValue;
    int _recentHostileActorId = -1;
    (int x, int y) _recentHostilePos;

    const int PathCacheHorizon = 12;
    const int PathMaxExpansions = 4096;
    const int UnstickSteps = 3;
    const int NoProgressThreshold = 4;
    const int NoProgressBackoffTicks = 4;
    const int RecentHostileMemoryTicks = 10;
    const int RecentHostilePursuitRadius = 10;
    const int EngageChaseRadius = 6;
    const int RaidContactRadius = 6;

    public int NoProgressStreak => _noProgressStreak;
    public int BackoffTicksRemaining => _backoffTicksRemaining;
    public string DebugDecisionCause { get; private set; } = "none";
    public string DebugTargetKey { get; private set; } = "none";
    public bool IsActivePeacefulIntentProtected => _protectFromDissipationThisTick;

    private Job _activeBuildSiteJob = Job.Idle;
    private (int x, int y)? _activeBuildSite;

    private Person(int id, Colony home, (int, int) pos, bool newborn, RuntimeNpcBrain brain, Random rng)
    {
        Id = id;
        _home = home;
        _brain = brain;
        _rng = rng;
        Pos = pos;
        _lastPos = pos;
        Strength = _rng.Next(3, 11);
        Intelligence = _rng.Next(3, 11);
        Age = newborn ? 0f : 18f + (float)_rng.NextDouble() * 22f;
        Profession = PickInitialProfession(home, _rng);

        // Needs/Emotions baseline-ok
        Needs["Hunger"] = 20f; // 0..100, kisebb = jobb (jóllakott)
        Emotions["Happy"] = 0f;
        Emotions["Hope"] = 0f;

        _loiterThresholdSeconds = 2.5f + (float)_rng.NextDouble() * 2.5f; // 2.5..5.0s
    }

    public static Person Spawn(Colony home, (int, int) pos, RuntimeNpcBrain brain, Random rng)
        => Spawn(home, pos, brain, rng, Interlocked.Increment(ref _nextFallbackPersonId));

    public static Person Spawn(Colony home, (int, int) pos, RuntimeNpcBrain brain, Random rng, int actorId)
        => new Person(actorId, home, pos, newborn: false, brain, rng);

    public static Person SpawnWithBonus(Colony home, (int, int) pos, World world, RuntimeNpcBrain brain, Random rng)
    {
        var person = new Person(world.AllocatePersonId(), home, pos, newborn: true, brain, rng);
        person.Strength = Math.Min(20, person.Strength + world.StrengthBonus);
        person.Intelligence = Math.Min(20, person.Intelligence + world.IntelligenceBonus);
        person.Health += world.HealthBonus;
        return person;
    }

    private static Profession PickInitialProfession(Colony colony, Random rng)
    {
        var roll = rng.NextDouble();
        return colony.Faction switch
        {
            Faction.Sylvars => roll switch
            {
                < 0.30 => Profession.Farmer,
                < 0.48 => Profession.Lumberjack,
                < 0.60 => Profession.Hunter,
                < 0.80 => Profession.Builder,
                < 0.92 => Profession.Miner,
                _ => Profession.Generalist
            },
            Faction.Obsidari => roll switch
            {
                < 0.32 => Profession.Miner,
                < 0.52 => Profession.Builder,
                < 0.66 => Profession.Lumberjack,
                < 0.78 => Profession.Hunter,
                < 0.92 => Profession.Farmer,
                _ => Profession.Generalist
            },
            _ => roll switch
            {
                < 0.22 => Profession.Farmer,
                < 0.40 => Profession.Lumberjack,
                < 0.58 => Profession.Miner,
                < 0.73 => Profession.Builder,
                < 0.86 => Profession.Hunter,
                _ => Profession.Generalist
            }
        };
    }

    // Run sensors and store perceived facts to memory
    void Perceive(World w)
    {
        Blackboard.Clear();
        foreach (var sensor in Sensors)
            sensor.Sense(w, this, Blackboard);

        foreach (var factual in Blackboard.FactualEvents)
            ProcessEvent(factual);

        if (Blackboard.FactualEvents.Count > 0)
            Memory.Remember(Blackboard.FactualEvents);
    }

    void ProcessEvent(FactualEvent factual)
    {
        if (factual.Type == EventTypes.ResourceHere && factual.Data is Resource res)
        {
            // Wood/Stone → kis remény növekedés
            if (res == Resource.Wood || res == Resource.Stone)
            {
                Emotions["Hope"] = Math.Clamp(Emotions.GetValueOrDefault("Hope", 0f) + 0.5f, -100f, 100f);
            }

            // Food látványa kis pozitív hatás
            if (res == Resource.Food)
            {
                Emotions["Happy"] = Math.Clamp(Emotions.GetValueOrDefault("Happy", 0f) + 1f, -100f, 100f);
            }
        }
    }

    public bool Update(World w, float dt, List<Person> births)
    {
        _protectFromDissipationThisTick = false;
        if (_backoffTicksRemaining > 0)
            _backoffTicksRemaining--;
        if (_backoffTicksRemaining <= 0)
            _suppressPeacefulActionsDuringBackoff = false;

        if (RoutingTicksRemaining > 0)
        {
            RoutingTicksRemaining--;
            IsRouting = true;
            Current = Job.Flee;
        }
        else if (IsRouting)
        {
            IsRouting = false;
            _routingOrigin = null;
        }

        // perception step
        Perceive(w);

        Age += dt / AgingTickDivisor;
        if (Age > w.MaxAge)
        {
            LastDeathReason = PersonDeathReason.OldAge;
            return false;
        }

        // Needs időbeli változása
        if (Needs.TryGetValue("Hunger", out var h))
            Needs["Hunger"] = Math.Clamp(h + dt * 1.65f, 0f, 100f);

        if (Current == Job.Idle)
            Stamina = Math.Clamp(Stamina + dt * 2.2f, 0f, 100f);
        else
            Stamina = Math.Clamp(Stamina - dt * 1.6f, 0f, 100f);

        _combatMarkerSeconds = Math.Max(0f, _combatMarkerSeconds - dt);
        if (_combatMarkerSeconds <= 0f)
            IsInCombat = false;

        float hunger = Needs.GetValueOrDefault("Hunger", 0f);

        if (LastAiDecision == null)
            _ = ThinkAiOncePerTick(w, dt);

        // Critical hunger preemption happens before starvation damage to avoid dying with available food.
        if (hunger >= 78f && HasConsumableFood())
        {
            if (hunger >= 96f)
            {
                if (TryConsumeFoodForHunger(w, hungerReduction: 28f, staminaGain: 8f, healthGain: 0.8f))
                {
                    Current = Job.Idle;
                    _doingJob = 0;
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
            }

            Current = Job.EatFood;
            _doingJob = 1;
            _idleTimeSeconds = 0f;
            _lastPos = Pos;
            return true;
        }

        if (hunger >= 95f)
            Health -= dt * 2.6f;
        else if (hunger >= 85f)
            Health -= dt * 1.2f;

        if (Health <= 0f)
        {
            if (LastDeathReason != PersonDeathReason.None)
                return false;

            // Last-chance starvation rescue: consume food if available before declaring death.
            if (hunger >= 85f && HasConsumableFood())
            {
                if (TryConsumeFoodForHunger(w, hungerReduction: 30f, staminaGain: 0f, healthGain: 1.2f, minimumHealth: 1f))
                {
                    Current = Job.Idle;
                    _doingJob = 0;
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
            }

            LastDeathReason = hunger >= 85f ? PersonDeathReason.Starvation : PersonDeathReason.Other;
            return false;
        }

        if (Age < 16f)
        {
            if (hunger >= 68f && HasConsumableFood())
            {
                Current = Job.EatFood;
                _doingJob = ComputeTicks(EatWorkTime, w, isHeavyWork: false);
                return true;
            }

            if (Stamina < 40f)
            {
                Current = Job.Rest;
                _doingJob = ComputeTicks(RestWorkTime, w, isHeavyWork: false);
                return true;
            }

            if (_rng.NextDouble() < 0.1)
                Wander(w);

            return true;
        }

        // simple reproduction chance if there is housing capacity
        int colonyPop = w._people.Count(p => p.Home == _home);
        int colonyFoodStock = _home.Stock[Resource.Food];
        int capacity = _home.HouseCount * w.HouseCapacity;
        int adultsInColony = w._people.Count(p => p.Home == _home && p.Age >= 18f && p.Health > 0f);
        bool hasHousingRoom = colonyPop < capacity;
        bool recoveryMode = colonyPop < 8 && hasHousingRoom && colonyFoodStock >= Math.Max(4, colonyPop / 2 + 2);
        bool socialGate = adultsInColony >= 2 && (_home.Morale >= 28f || (recoveryMode && _home.Morale >= 20f));
        double birthChance = 0.001 * w.BirthRateMultiplier;
        if (recoveryMode)
            birthChance *= colonyPop <= 4 ? 3.2 : 2.1;

        if (Age >= 18 && Age <= 60 && hasHousingRoom && socialGate && _rng.NextDouble() < birthChance)
        {
            var birthPos = w.GetBirthSpawnPosition(_home, Pos);
            births.Add(Person.SpawnWithBonus(_home, birthPos, w, w.CreateNpcBrain(_home), w.CreateEntityRng()));
        }

        int reserveBonus = _home.FoodReserveBonus;
        float foodPerPerson = colonyFoodStock / (float)Math.Max(1, colonyPop);
        bool emergencyFood = colonyFoodStock <= Math.Max(5 + reserveBonus, colonyPop + 1 + reserveBonus);
        bool veryLowFood = colonyFoodStock <= Math.Max(2 + reserveBonus / 2, colonyPop / 2 + reserveBonus / 2);
        bool abundantFood = foodPerPerson >= 20f;

        if (_doingJob > 0 && Current != Job.Idle)
        {
            if (Current is Job.GatherWood or Job.GatherStone or Job.GatherIron or Job.GatherGold or Job.GatherFood
                or Job.BuildHouse or Job.BuildWall or Job.BuildWatchtower)
            {
                _protectFromDissipationThisTick = true;
            }

            // working → not idle
            _idleTimeSeconds = 0f;

            _doingJob--;
            if (_doingJob <= 0)
            {
                switch (Current)
                {
                    case Job.GatherWood:
                        if (w.TryHarvest(Pos, Resource.Wood, 1))
                            _home.Stock[Resource.Wood] += GetGatherAmount(w, Resource.Wood, w.WoodYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherStone:
                        if (w.TryHarvest(Pos, Resource.Stone, 1))
                            _home.Stock[Resource.Stone] += GetGatherAmount(w, Resource.Stone, w.StoneYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherIron:
                        if (w.TryHarvest(Pos, Resource.Iron, 1))
                            _home.Stock[Resource.Iron] += GetGatherAmount(w, Resource.Iron, w.IronYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherGold:
                        if (w.TryHarvest(Pos, Resource.Gold, 1))
                            _home.Stock[Resource.Gold] += GetGatherAmount(w, Resource.Gold, w.GoldYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.BuildHouse:
                        TryCompleteHouseBuild(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.CraftTools:
                        if (_home.Stock[Resource.Wood] >= 2 && _home.Stock[Resource.Iron] >= 2)
                        {
                            _home.Stock[Resource.Wood] -= 2;
                            _home.Stock[Resource.Iron] -= 2;
                            _home.ToolCharges += 8 + _home.WorkshopCount * 2;
                        }
                        break;

                    case Job.BuildWall:
                        ExecuteBuildWallAction(w);
                        break;

                    case Job.BuildWatchtower:
                        ExecuteBuildWatchtowerAction(w);
                        break;

                    case Job.RaidBorder:
                        ExecuteRaidBorderAction(w);
                        break;

                    case Job.AttackStructure:
                        ExecuteAttackStructureAction(w);
                        break;

                    case Job.GatherFood:
                        if (w.TryHarvest(Pos, Resource.Food, 1))
                            _home.Stock[Resource.Food] += GetGatherAmount(w, Resource.Food, w.FoodYield);
                        else if (veryLowFood && TryHuntNearbyHerbivore(w, 1))
                            _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(w, Resource.Food, w.FoodYield));
                        else
                            Wander(w);
                        break;

                    case Job.EatFood:
                        TryConsumeFoodForHunger(w, hungerReduction: 35f, staminaGain: 12f, healthGain: 1.5f);
                        break;

                    case Job.Rest:
                        float restGain = HasNearbyOwnHouse(w, radius: 2) ? 30f : 22f;
                        Stamina = Math.Clamp(Stamina + restGain, 0f, 100f);
                        break;

                    case Job.Fight:
                        ExecuteFightAction(w);
                        break;

                    case Job.Flee:
                        ExecuteFleeAction(w);
                        break;
                }

                if (_doingJob <= 0)
                    Current = Job.Idle;
            }
        }
        else if (Current == Job.Idle)
        {
            float localHunger = Needs.GetValueOrDefault("Hunger", 0f);

            if (Stamina <= 20f)
            {
                Current = Job.Rest;
                _doingJob = ComputeTicks(RestWorkTime, w, isHeavyWork: false);
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            float eatNowThreshold = emergencyFood ? 54f : 62f;
            if (abundantFood)
                eatNowThreshold = Math.Min(eatNowThreshold, 58f);

            float seekFoodThreshold = emergencyFood ? 42f : 50f;
            if (abundantFood)
                seekFoodThreshold += 8f;

            if (HasConsumableFood() && localHunger >= 86f)
            {
                Current = Job.EatFood;
                _doingJob = 1;
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            if (abundantFood && localHunger < 58f)
                veryLowFood = false;

            if (localHunger >= eatNowThreshold && HasConsumableFood())
            {
                Current = Job.EatFood;
                _doingJob = ComputeTicks(EatWorkTime, w, isHeavyWork: false);
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            if (localHunger >= seekFoodThreshold)
            {
                var hereFood = w.GetTile(Pos.x, Pos.y).Node;
                if (hereFood != null && hereFood.Type == Resource.Food && hereFood.Amount > 0)
                {
                    Current = Job.GatherFood;
                    _doingJob = ComputeTicks(FoodWorkTime, w, isHeavyWork: true);
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }

                if (veryLowFood && TryHuntNearbyHerbivore(w, range: 1))
                {
                    _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(w, Resource.Food, w.FoodYield + 1));
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }

                if (TryMoveTowardsNearestResource(w, searchRadius: 4, Resource.Food))
                {
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
            }

            if (w.EnableCombatPrimitives && TryHandleThreatResponse(w, dt))
            {
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            if (_suppressPeacefulActionsDuringBackoff && _backoffTicksRemaining > 0)
            {
                DebugDecisionCause = "peaceful_backoff_wait";
                DebugTargetKey = "none";
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            if (TryProfessionDirectedAction(w, veryLowFood))
            {
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            // 1) Immediate resource on current tile → start job
            var hereNode = w.GetTile(Pos.x, Pos.y).Node;
            if (hereNode != null && hereNode.Amount > 0)
            {
                if (hereNode.Type == Resource.Wood)
                {
                    Current = Job.GatherWood;
                    _doingJob = ComputeTicks(WoodWorkTime, w, isHeavyWork: true);
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
                if (hereNode.Type == Resource.Stone)
                {
                    Current = Job.GatherStone;
                    _doingJob = ComputeTicks(StoneWorkTime, w, isHeavyWork: true);
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
                if (hereNode.Type == Resource.Iron)
                {
                    Current = Job.GatherIron;
                    _doingJob = ComputeTicks(IronWorkTime, w, isHeavyWork: true);
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
                if (hereNode.Type == Resource.Gold)
                {
                    Current = Job.GatherGold;
                    _doingJob = ComputeTicks(GoldWorkTime, w, isHeavyWork: true);
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }
            }

            // 2) Move one step toward nearest resource in small radius
            if (TryMoveTowardsNearestResource(w, searchRadius: 6, Resource.Wood, Resource.Stone, Resource.Iron, Resource.Gold))
            {
                _idleTimeSeconds = 0f; // movement → not idle
                _lastPos = Pos;
                return true;
            }

            // 3) Utility-goal fallback → pick a job (e.g., BuildHouse if feasible)
            var next = ThinkAiOncePerTick(w, dt);
            if (next == Job.RefillInventory)
            {
                if (TryExecuteRefillInventoryIntent(w))
                {
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }

                next = Job.Idle;
            }

            if (next == Job.BuildHouse && !CanStartHouseBuild(w))
                next = ResolveHouseBuildFallback(w);

            if (IsBuildJob(next))
            {
                if (TryExecuteBuildIntent(w, next))
                {
                    _idleTimeSeconds = 0f;
                    _lastPos = Pos;
                    return true;
                }

                next = ResolveBuildFallback(w, next);
            }

            if (next != Job.Idle)
            {
                if (!IsBuildJob(next))
                    ResetBuildSiteState(w);

                Current = next;
                _doingJob = next switch
                {
                    Job.GatherWood  => Math.Max(1, (int)MathF.Ceiling(WoodWorkTime  / w.WorkEfficiencyMultiplier)),
                    Job.GatherStone => Math.Max(1, (int)MathF.Ceiling(StoneWorkTime / w.WorkEfficiencyMultiplier)),
                    Job.GatherIron  => Math.Max(1, (int)MathF.Ceiling(IronWorkTime / w.WorkEfficiencyMultiplier)),
                    Job.GatherGold  => Math.Max(1, (int)MathF.Ceiling(GoldWorkTime / w.WorkEfficiencyMultiplier)),
                    Job.GatherFood  => ComputeTicks(FoodWorkTime, w, isHeavyWork: true),
                    Job.EatFood     => ComputeTicks(EatWorkTime, w, isHeavyWork: false),
                    Job.Rest        => ComputeTicks(RestWorkTime, w, isHeavyWork: false),
                    Job.BuildHouse  => Math.Max(1, (int)MathF.Ceiling(BuildHouseTime / w.WorkEfficiencyMultiplier)),
                    Job.CraftTools  => ComputeTicks(CraftToolsTime, w, isHeavyWork: true),
                    Job.BuildWall => ComputeTicks(BuildWallTime, w, isHeavyWork: true),
                    Job.BuildWatchtower => ComputeTicks(BuildWatchtowerTime, w, isHeavyWork: true),
                    Job.RaidBorder => 1,
                    Job.AttackStructure => 1,
                    Job.Fight       => 1,
                    Job.Flee        => 1,
                    _ => 0
                };
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            // 4) Loiter for a while, then wander
            _idleTimeSeconds += dt;
            if (_idleTimeSeconds >= _loiterThresholdSeconds)
            {
                Wander(w);
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }
        }

        _lastPos = Pos;
        return true;
    }

    int ComputeTicks(int baseTicks, World w, bool isHeavyWork)
    {
        float staminaFactor = isHeavyWork
            ? Math.Clamp(0.45f + (Stamina / 100f) * 0.55f, 0.45f, 1f)
            : Math.Clamp(0.7f + (Stamina / 100f) * 0.3f, 0.7f, 1f);

        float professionSpeed = Profession switch
        {
            Profession.Builder when baseTicks == BuildHouseTime => 1.18f,
            Profession.Farmer when baseTicks == FoodWorkTime => 1.16f,
            Profession.Lumberjack when baseTicks == WoodWorkTime => 1.15f,
            Profession.Miner when baseTicks == StoneWorkTime || baseTicks == IronWorkTime || baseTicks == GoldWorkTime => 1.15f,
            Profession.Hunter when baseTicks == FoodWorkTime => 1.08f,
            _ => 1f
        };

        float toolSpeed = _home.ToolCharges > 0 ? 1.08f : 1f;
        float economyMultiplier = (float)w.GetDomainMultiplier(RuntimeDomain.Economy);

        float effectiveSpeed = Math.Max(0.2f, w.WorkEfficiencyMultiplier * _home.ColonyWorkMultiplier * staminaFactor * professionSpeed * toolSpeed * economyMultiplier);
        return Math.Max(1, (int)MathF.Ceiling(baseTicks / effectiveSpeed));
    }

    int GetGatherAmount(World w, Resource res, int baseYield)
    {
        float colonyMultiplier = res switch
        {
            Resource.Wood => _home.WoodGatherMultiplier,
            Resource.Stone => _home.StoneGatherMultiplier,
            Resource.Iron => _home.IronGatherMultiplier,
            Resource.Gold => _home.GoldGatherMultiplier,
            Resource.Food => _home.FoodGatherMultiplier,
            _ => 1f
        };

        float professionMultiplier = (res, Profession) switch
        {
            (Resource.Wood, Profession.Lumberjack) => 1.22f,
            (Resource.Stone, Profession.Miner) => 1.22f,
            (Resource.Iron, Profession.Miner) => 1.2f,
            (Resource.Gold, Profession.Miner) => 1.14f,
            (Resource.Food, Profession.Farmer) => 1.2f,
            (Resource.Food, Profession.Hunter) => 1.12f,
            _ => 1f
        };

        float economyMultiplier = (float)w.GetDomainMultiplier(RuntimeDomain.Economy);
        float foodDomainMultiplier = res == Resource.Food
            ? (float)w.GetDomainMultiplier(RuntimeDomain.Food)
            : 1f;

        return Math.Max(1, (int)MathF.Round(baseYield * colonyMultiplier * professionMultiplier * economyMultiplier * foodDomainMultiplier));
    }

    bool HasConsumableFood()
        => Inventory.GetCount(ItemType.Food) > 0 || _home.Stock[Resource.Food] > 0;

    bool TryConsumeFoodForHunger(
        World w,
        float hungerReduction,
        float staminaGain,
        float healthGain,
        float? minimumHealth = null)
    {
        if (Inventory.TryRemove(ItemType.Food))
        {
            w.ReportInventoryFoodConsumed();
            ApplyFoodConsumptionEffects(w, hungerReduction, staminaGain, healthGain, minimumHealth);
            return true;
        }

        if (_home.Stock[Resource.Food] <= 0)
            return false;

        _home.Stock[Resource.Food] -= 1;
        ApplyFoodConsumptionEffects(w, hungerReduction, staminaGain, healthGain, minimumHealth);
        return true;
    }

    void ApplyFoodConsumptionEffects(
        World w,
        float hungerReduction,
        float staminaGain,
        float healthGain,
        float? minimumHealth)
    {
        Needs["Hunger"] = Math.Max(0f, Needs.GetValueOrDefault("Hunger", 30f) - hungerReduction);
        Stamina = Math.Clamp(Stamina + staminaGain, 0f, 100f);
        Health = minimumHealth.HasValue
            ? Math.Max(minimumHealth.Value, Health + healthGain)
            : Math.Min(100f + w.HealthBonus, Health + healthGain);
    }

    internal bool TryRefillInventoryFromStorehouse(World w)
    {
        if (!w.IsOwnedStorehouseAccessTile(_home, Pos))
            return false;

        var freeSlots = Inventory.FreeSlots;
        var availableFood = _home.Stock[Resource.Food];
        var amount = Math.Min(freeSlots, availableFood);
        if (amount <= 0)
            return false;

        if (!Inventory.TryAdd(ItemType.Food, amount))
            return false;

        _home.Stock[Resource.Food] -= amount;
        return true;
    }

    internal bool TryDepositInventoryToStorehouse(World w)
    {
        if (!w.IsOwnedStorehouseAccessTile(_home, Pos))
            return false;

        var amount = Inventory.GetCount(ItemType.Food);
        if (amount <= 0)
            return false;

        if (!Inventory.TryRemove(ItemType.Food, amount))
            return false;

        _home.Stock[Resource.Food] += amount;
        return true;
    }

    bool TryExecuteRefillInventoryIntent(World w)
    {
        if (Inventory.FreeSlots <= 0 || _home.Stock[Resource.Food] <= 0)
            return false;

        if (!w.TryFindNearestOwnedStorehouseAccessTile(_home, Pos, out var accessTile))
            return false;

        if (!w.IsOwnedStorehouseAccessTile(_home, Pos))
        {
            DebugDecisionCause = "move_to_storehouse";
            DebugTargetKey = $"storehouse:{accessTile.x}:{accessTile.y}";
            _trackNoProgressForCurrentMove = true;
            _noProgressTrackContext = "resource";
            MoveTowards(w, accessTile, 1);
            return true;
        }

        DebugDecisionCause = "refill_inventory";
        DebugTargetKey = $"storehouse:{Pos.x}:{Pos.y}";
        return TryRefillInventoryFromStorehouse(w);
    }

    private Job ThinkAiOncePerTick(World w, float dt)
    {
        if (_hasCachedAiThink && _lastAiThinkTick == w.CurrentTick && Math.Abs(_lastAiThinkDt - dt) < 0.0001f)
            return _lastAiThinkResult;

        _lastAiThinkResult = _brain.Think(this, w, dt);
        _lastAiThinkTick = w.CurrentTick;
        _lastAiThinkDt = dt;
        _hasCachedAiThink = true;
        return _lastAiThinkResult;
    }

    bool TryProfessionDirectedAction(World w, bool veryLowFood)
    {
        switch (Profession)
        {
            case Profession.Builder:
                if (TryStartDefenseConstruction(w))
                    return true;

                if (_home.ToolCharges <= 2 && _home.Stock[Resource.Iron] >= 2 && _home.Stock[Resource.Wood] >= 2)
                {
                    Current = Job.CraftTools;
                    _doingJob = ComputeTicks(CraftToolsTime, w, isHeavyWork: true);
                    return true;
                }

                if (CanStartHouseBuild(w))
                {
                    return TryExecuteBuildIntent(w, Job.BuildHouse);
                }
                break;
            case Profession.Lumberjack:
                if (TryMoveTowardsNearestResource(w, searchRadius: 4, Resource.Wood))
                    return true;
                break;
            case Profession.Miner:
                if (_home.ToolCharges <= 1 && _home.Stock[Resource.Iron] >= 2 && _home.Stock[Resource.Wood] >= 2)
                {
                    Current = Job.CraftTools;
                    _doingJob = ComputeTicks(CraftToolsTime, w, isHeavyWork: true);
                    return true;
                }

                if (TryMoveTowardsNearestResource(w, searchRadius: 5, Resource.Iron, Resource.Gold, Resource.Stone))
                    return true;
                break;
            case Profession.Farmer:
                if (TryMoveTowardsNearestResource(w, searchRadius: 5, Resource.Food))
                    return true;
                break;
            case Profession.Hunter:
                if (veryLowFood && TryHuntNearbyHerbivore(w, range: 2))
                {
                    _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(w, Resource.Food, w.FoodYield + 1));
                    return true;
                }
                break;
        }

        return false;
    }

    bool TryStartDefenseConstruction(World w)
    {
        if (!w.CanBuildFortifications(_home))
            return false;

        var warState = w.GetColonyWarState(_home.Id);
        bool hasHostileStance = w._colonies.Any(colony =>
            colony != _home
            && w.GetFactionStance(_home.Faction, colony.Faction) >= WorldSim.Simulation.Diplomacy.Stance.Hostile);
        if (warState == ColonyWarState.Peace && !hasHostileStance)
            return false;

        if (CanAffordTowerBuild(w)
            && CountOwnWatchtowers(w) < Math.Max(1, _home.HouseCount / 2))
        {
            return TryExecuteBuildIntent(w, Job.BuildWatchtower);
        }

        if (CanAffordWallBuild(w))
        {
            return TryExecuteBuildIntent(w, Job.BuildWall);
        }

        return false;
    }

    private bool NeedsHousing(World w)
    {
        int colonyPop = w._people.Count(person => person.Home == _home && person.Health > 0f);
        int targetHouseCount = Math.Max(_home.HouseCount, (int)Math.Ceiling((colonyPop + 3) / (double)w.HouseCapacity));
        return _home.HouseCount < targetHouseCount;
    }

    private bool CanAffordHouseBuild(World w)
    {
        if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] >= _home.HouseStoneCost)
            return true;

        return _home.Stock[Resource.Wood] >= _home.HouseWoodCost;
    }

    private bool CanStartHouseBuild(World w)
        => NeedsHousing(w) && CanAffordHouseBuild(w);

    private Job ResolveHouseBuildFallback(World w)
    {
        if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] < _home.HouseStoneCost)
            return Job.GatherStone;

        return Job.GatherWood;
    }

    private static bool IsBuildJob(Job job)
        => job is Job.BuildHouse or Job.BuildWall or Job.BuildWatchtower;

    private Job ResolveBuildFallback(World w, Job buildJob)
    {
        return buildJob switch
        {
            Job.BuildHouse => ResolveHouseBuildFallback(w),
            Job.BuildWatchtower when NeedsStoneForTowerBuild(w) => Job.GatherStone,
            _ => Job.GatherWood
        };
    }

    private bool CanAffordWallBuild(World w)
    {
        if (HasTech("advanced_fortification")
            && _home.Stock[Resource.Stone] >= ReinforcedWallStoneCost
            && _home.Stock[Resource.Iron] >= ReinforcedWallIronCost)
            return true;

        if (HasTech("fortification")
            && _home.Stock[Resource.Stone] >= StoneWallStoneCost)
            return true;

        return _home.Stock[Resource.Wood] >= WallWoodCost;
    }

    private bool CanAffordTowerBuild(World w)
    {
        if (HasTech("siege_craft")
            && _home.Stock[Resource.Wood] >= CatapultTowerWoodCost
            && _home.Stock[Resource.Stone] >= CatapultTowerStoneCost
            && _home.Stock[Resource.Iron] >= CatapultTowerIronCost)
            return true;

        if (HasTech("fortification")
            && _home.Stock[Resource.Wood] >= ArrowTowerWoodCost
            && _home.Stock[Resource.Stone] >= ArrowTowerStoneCost)
            return true;

        return _home.Stock[Resource.Wood] >= WatchtowerWoodCost && _home.Stock[Resource.Stone] >= WatchtowerStoneCost;
    }

    private bool NeedsStoneForTowerBuild(World w)
    {
        if (HasTech("siege_craft") && _home.Stock[Resource.Stone] < CatapultTowerStoneCost)
            return true;
        if (HasTech("fortification") && _home.Stock[Resource.Stone] < ArrowTowerStoneCost)
            return true;
        return _home.Stock[Resource.Stone] < WatchtowerStoneCost;
    }

    private bool HasTech(string techId)
        => _home.UnlockedTechs.Contains(techId);

    private void ClearBuildSiteState()
    {
        _activeBuildSite = null;
        _activeBuildSiteJob = Job.Idle;
    }

    private void ResetBuildSiteState(World w)
    {
        if (_activeBuildSite != null && _activeBuildSiteJob != Job.Idle)
            w.ReportBuildSiteReset();

        ClearBuildSiteState();
    }

    private bool HasValidActiveBuildSite(World w, Job buildJob)
    {
        if (_activeBuildSiteJob != buildJob || _activeBuildSite == null)
            return false;

        return IsBuildSiteValid(w, _activeBuildSite.Value, buildJob);
    }

    private bool IsBuildSiteValid(World w, (int x, int y) site, Job buildJob)
    {
        if (!w.CanPlaceStructureAt(site.x, site.y))
            return false;

        if (buildJob is Job.BuildWall or Job.BuildWatchtower)
        {
            int distFromOrigin = Math.Abs(site.x - _home.Origin.x) + Math.Abs(site.y - _home.Origin.y);
            if (distFromOrigin is < 2 or > 6)
                return false;
        }

        return true;
    }

    private bool TryAcquireBuildSite(World w, Job buildJob)
    {
        if (HasValidActiveBuildSite(w, buildJob))
            return true;

        (int x, int y)? site = buildJob switch
        {
            Job.BuildHouse => FindHouseBuildPlacement(w),
            Job.BuildWall => FindDefensePlacement(w, preferContested: true),
            Job.BuildWatchtower => FindDefensePlacement(w, preferContested: true),
            _ => null
        };

        if (site == null)
        {
            ResetBuildSiteState(w);
            return false;
        }

        _activeBuildSite = site;
        _activeBuildSiteJob = buildJob;
        return true;
    }

    private bool TryExecuteBuildIntent(World w, Job buildJob)
    {
        if (buildJob == Job.BuildHouse && !CanStartHouseBuild(w))
        {
            ResetBuildSiteState(w);
            return false;
        }

        if (buildJob == Job.BuildWall && !CanAffordWallBuild(w))
        {
            ResetBuildSiteState(w);
            return false;
        }

        if (buildJob == Job.BuildWatchtower
            && !CanAffordTowerBuild(w))
        {
            ResetBuildSiteState(w);
            return false;
        }

        if (!TryAcquireBuildSite(w, buildJob))
            return false;

        var site = _activeBuildSite!.Value;
        var reservationKey = BuildBuildReservationKey(site.x, site.y);
        w.ReserveSoftTarget(reservationKey);

        if (Pos != site)
        {
            DebugDecisionCause = "build_site_move";
            DebugTargetKey = reservationKey;
            _protectFromDissipationThisTick = true;
            _trackNoProgressForCurrentMove = true;
            _noProgressTrackContext = "build";
            MoveTowards(w, site, 1);
            return true;
        }

        DebugDecisionCause = buildJob switch
        {
            Job.BuildHouse => "build_house_work",
            Job.BuildWall => "build_wall_work",
            Job.BuildWatchtower => "build_watchtower_work",
            _ => "build_work"
        };
        DebugTargetKey = reservationKey;
        _protectFromDissipationThisTick = true;

        Current = buildJob;
        _doingJob = buildJob switch
        {
            Job.BuildHouse => ComputeTicks(BuildHouseTime, w, isHeavyWork: true),
            Job.BuildWall => ComputeTicks(BuildWallTime, w, isHeavyWork: true),
            Job.BuildWatchtower => ComputeTicks(BuildWatchtowerTime, w, isHeavyWork: true),
            _ => 0
        };
        return _doingJob > 0;
    }

    private void TryCompleteHouseBuild(World w)
    {
        if (!HasValidActiveBuildSite(w, Job.BuildHouse) || _activeBuildSite == null)
        {
            ResetBuildSiteState(w);
            return;
        }

        if (Pos != _activeBuildSite.Value)
            return;

        if (!NeedsHousing(w))
        {
            ResetBuildSiteState(w);
            return;
        }

        var site = _activeBuildSite.Value;

        if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] >= _home.HouseStoneCost)
        {
            _home.Stock[Resource.Stone] -= _home.HouseStoneCost;
            _home.HouseCount++;
            w.AddHouse(_home, site);
            ClearBuildSiteState();
            return;
        }

        if (_home.Stock[Resource.Wood] >= _home.HouseWoodCost)
        {
            _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
            _home.HouseCount++;
            w.AddHouse(_home, site);
            ClearBuildSiteState();
        }
    }

    bool TryHandleThreatResponse(World w, float dt)
    {
        if (_backoffTicksRemaining > 0)
            return false;

        var context = BuildThreatContext(w);

        if (ThreatDecisionPolicy.IsPeacefulZeroSignal(context))
        {
            DebugDecisionCause = "peaceful_skip";
            DebugTargetKey = "none";
            return false;
        }

        bool hasImmediateThreats = ThreatDecisionPolicy.HasImmediateThreat(context);
        bool shouldDefend = ThreatDecisionPolicy.ShouldPrioritizeDefense(context);
        if (!hasImmediateThreats && !shouldDefend)
            return false;

        var next = ThinkAiOncePerTick(w, dt);
        if (next != Job.Fight && next != Job.Flee && next != Job.RaidBorder && next != Job.AttackStructure)
        {
            if (context.IsWarriorRole && context.IsWarStance && (context.IsContestedTile || context.HasContestedTilesNearby))
                next = Job.RaidBorder;
            else
                next = ThreatDecisionPolicy.ShouldFight(context) ? Job.Fight : Job.Flee;
        }

        bool hasImmediateFactionThreat = ThreatDecisionPolicy.HasImmediateFactionThreat(context);
        if (hasImmediateFactionThreat && !context.IsWarriorRole)
            next = Job.Flee;

        Current = next;
        _doingJob = 1;
        return true;
    }

    int CountNearbyPredators(World w, int radius)
    {
        int count = 0;
        foreach (var animal in w._animals)
        {
            if (animal is not Predator predator || !predator.IsAlive)
                continue;

            int dist = Math.Abs(predator.Pos.x - Pos.x) + Math.Abs(predator.Pos.y - Pos.y);
            if (dist <= radius)
                count++;
        }

        return count;
    }

    int CountNearbyHostilePeople(World w, int radius)
    {
        int count = 0;
        foreach (var person in w._people)
        {
            if (person == this || person.Health <= 0f || person.Home == _home)
                continue;

            var stance = w.GetFactionStance(_home.Faction, person.Home.Faction);
            if (stance < WorldSim.Simulation.Diplomacy.Stance.Hostile)
                continue;

            int dist = Math.Abs(person.Pos.x - Pos.x) + Math.Abs(person.Pos.y - Pos.y);
            if (dist <= radius)
                count++;
        }

        return count;
    }

    void ExecuteFightAction(World w)
    {
        if (!w.EnableCombatPrimitives)
            return;

        if (IsRouting)
        {
            ExecuteFleeAction(w);
            return;
        }

        var threatContext = BuildThreatContext(w);
        bool hasImmediateFactionThreat = ThreatDecisionPolicy.HasImmediateFactionThreat(threatContext);
        if (hasImmediateFactionThreat && !threatContext.IsWarriorRole)
        {
            ExecuteFleeAction(w);
            return;
        }

        if (TryAttackOrPursueHostilePerson(w, radius: GetCombatChaseRadius(w, EngageChaseRadius), switchToFight: false, pursuitCause: "fight_chase"))
            return;

        if (TryPursueRecentHostile(w))
            return;

        Predator? nearest = null;
        int best = int.MaxValue;
        if (w.EnablePredatorHumanAttacks)
        {
            foreach (var animal in w._animals)
            {
                if (animal is not Predator predator || !predator.IsAlive)
                    continue;

                int dist = Math.Abs(predator.Pos.x - Pos.x) + Math.Abs(predator.Pos.y - Pos.y);
                if (dist < best)
                {
                    best = dist;
                    nearest = predator;
                }
            }
        }

        if (nearest == null)
        {
            if (threatContext.IsContestedTile || threatContext.HasContestedTilesNearby)
            {
                var patrolTarget = FindNearestContestedTile(w, radius: 6);
                if (patrolTarget != null)
                    MoveTowards(w, patrolTarget.Value, 1);
                else
                    MoveTowards(w, _home.Origin, 1);
            }

            return;
        }

        if (best > 1)
        {
            MoveTowards(w, nearest.Pos, 1);
            return;
        }

        w.ReportCombatEngagement();
        float fightAdvantage = Strength + (Defense / 2f);
        float winChance = Math.Clamp(0.35f + (fightAdvantage / 55f), 0.2f, 0.95f);
        if (_rng.NextDouble() < winChance)
        {
            nearest.IsAlive = false;
            w.ReportPredatorKilledByHumans();
            return;
        }

        ApplyCombatDamage(w, Math.Max(1f, w.PredatorHumanDamage * 0.65f), "Predator");
    }

    void ExecuteFleeAction(World w)
    {
        if (IsRouting && _routingOrigin.HasValue)
        {
            var origin = _routingOrigin.Value;
            var best = Pos;
            var bestDist = Math.Abs(Pos.x - origin.x) + Math.Abs(Pos.y - origin.y);

            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                        continue;

                    int nx = Math.Clamp(Pos.x + ox, 0, w.Width - 1);
                    int ny = Math.Clamp(Pos.y + oy, 0, w.Height - 1);
                    if (w.IsMovementBlocked(nx, ny, _home.Id))
                        continue;

                    var dist = Math.Abs(nx - origin.x) + Math.Abs(ny - origin.y);
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        best = (nx, ny);
                    }
                }
            }

            if (best != Pos)
            {
                _trackNoProgressForCurrentMove = true;
                _noProgressTrackContext = "flee";
                MoveTowards(w, best, 1);
                return;
            }
        }

        var threatContext = BuildThreatContext(w);
        bool hasFactionThreat = ThreatDecisionPolicy.HasAmbientWarPressure(threatContext)
            || ThreatDecisionPolicy.HasImmediateFactionThreat(threatContext);

        bool useRefugeRing =
            hasFactionThreat &&
            (threatContext.NearbyEnemyCount > 0 || threatContext.IsContestedTile || threatContext.HasContestedTilesNearby);

        var retreatTarget = useRefugeRing
            ? FindRetreatTarget(w)
            : _home.Origin;

        if (!threatContext.IsWarriorRole && hasFactionThreat)
        {
            _trackNoProgressForCurrentMove = useRefugeRing;
            _noProgressTrackContext = useRefugeRing ? "flee" : "move";
            MoveTowards(w, retreatTarget, 1);
            return;
        }

        var nearestThreat = FindNearestThreat(w, radius: 6);
        if (nearestThreat == null)
        {
            _trackNoProgressForCurrentMove = false;
            _noProgressTrackContext = "move";
            MoveTowards(w, _home.Origin, 1);
            return;
        }

        int dx = Pos.x - nearestThreat.Value.x;
        int dy = Pos.y - nearestThreat.Value.y;
        int stepX = Pos.x + Math.Sign(dx == 0 ? (_rng.NextDouble() < 0.5 ? -1 : 1) : dx);
        int stepY = Pos.y + Math.Sign(dy == 0 ? (_rng.NextDouble() < 0.5 ? -1 : 1) : dy);

        stepX = Math.Clamp(stepX, 0, w.Width - 1);
        stepY = Math.Clamp(stepY, 0, w.Height - 1);
        if (!w.IsMovementBlocked(stepX, stepY, _home.Id))
        {
            Pos = (stepX, stepY);
            _noProgressStreak = 0;
            return;
        }

        _trackNoProgressForCurrentMove = hasFactionThreat && useRefugeRing;
        _noProgressTrackContext = hasFactionThreat && useRefugeRing ? "flee" : "move";
        MoveTowards(w, hasFactionThreat ? retreatTarget : _home.Origin, 1);
    }

    (int x, int y) FindRetreatTarget(World w)
    {
        var origin = _home.Origin;
        var nearestThreat = FindNearestThreat(w, radius: 12);

        (int x, int y)? best = null;
        float bestScore = float.MaxValue;

        for (int radius = 2; radius <= 10; radius++)
        {
            int minX = Math.Max(0, origin.x - radius);
            int maxX = Math.Min(w.Width - 1, origin.x + radius);
            int minY = Math.Max(0, origin.y - radius);
            int maxY = Math.Min(w.Height - 1, origin.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int ring = Math.Abs(x - origin.x) + Math.Abs(y - origin.y);
                    if (ring != radius)
                        continue;
                    if (w.IsMovementBlocked(x, y, _home.Id))
                        continue;

                    int occupancy = w._people.Count(person => person != this && person.Health > 0f && person.Pos == (x, y));
                    int localThreatCount = CountThreatsNearPosition(w, (x, y), radius: 2);
                    float contestedPenalty = w.IsTileContested(x, y) ? 4f : 0f;
                    float threatPenalty = localThreatCount * 2.5f;
                    float occupancyPenalty = occupancy * 3f;
                    float reservationPenalty = w.GetSoftReservationCount(BuildRetreatReservationKey(x, y)) * 2f;
                    float distancePenalty = Math.Abs(x - Pos.x) + Math.Abs(y - Pos.y);

                    float score = contestedPenalty + threatPenalty + occupancyPenalty + reservationPenalty + distancePenalty;
                    if (nearestThreat != null)
                        score -= Math.Abs(x - nearestThreat.Value.x) + Math.Abs(y - nearestThreat.Value.y);

                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = (x, y);
                    }
                }
            }
        }

        if (best != null)
        {
            var selected = best.Value;
            var key = BuildRetreatReservationKey(selected.x, selected.y);
            w.ReserveSoftTarget(key);
            DebugDecisionCause = "retreat_refuge";
            DebugTargetKey = key;
            return selected;
        }

        for (int radius = 1; radius <= 12; radius++)
        {
            int minX = Math.Max(0, origin.x - radius);
            int maxX = Math.Min(w.Width - 1, origin.x + radius);
            int minY = Math.Max(0, origin.y - radius);
            int maxY = Math.Min(w.Height - 1, origin.y + radius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (w.IsMovementBlocked(x, y, _home.Id))
                        continue;
                    var key = BuildRetreatReservationKey(x, y);
                    w.ReserveSoftTarget(key);
                    DebugDecisionCause = "retreat_fallback";
                    DebugTargetKey = key;
                    return (x, y);
                }
            }
        }

        DebugDecisionCause = "retreat_origin";
        DebugTargetKey = BuildRetreatReservationKey(origin.x, origin.y);
        return origin;
    }

    private static string BuildRetreatReservationKey(int x, int y)
        => $"retreat:{x}:{y}";

    private static string BuildBuildReservationKey(int x, int y)
        => $"build:{x}:{y}";

    int CountThreatsNearPosition(World w, (int x, int y) pos, int radius)
    {
        int count = 0;
        foreach (var person in w._people)
        {
            if (person == this || person.Health <= 0f || person.Home == _home)
                continue;
            if (w.GetFactionStance(_home.Faction, person.Home.Faction) < WorldSim.Simulation.Diplomacy.Stance.Hostile)
                continue;

            int dist = Math.Abs(person.Pos.x - pos.x) + Math.Abs(person.Pos.y - pos.y);
            if (dist <= radius)
                count++;
        }

        foreach (var animal in w._animals)
        {
            if (animal is not Predator predator || !predator.IsAlive)
                continue;
            int dist = Math.Abs(predator.Pos.x - pos.x) + Math.Abs(predator.Pos.y - pos.y);
            if (dist <= radius)
                count++;
        }

        return count;
    }

    void ExecuteBuildWallAction(World w)
    {
        if (!CanAffordWallBuild(w))
            return;

        if (!HasValidActiveBuildSite(w, Job.BuildWall) || _activeBuildSite == null)
        {
            ResetBuildSiteState(w);
            return;
        }

        var spot = _activeBuildSite.Value;
        if (Pos != spot)
            return;

        var reservationKey = BuildBuildReservationKey(spot.x, spot.y);
        w.ReserveSoftTarget(reservationKey);
        DebugDecisionCause = "build_wall";
        DebugTargetKey = reservationKey;

        var built = false;
        if (HasTech("advanced_fortification")
            && _home.Stock[Resource.Stone] >= ReinforcedWallStoneCost
            && _home.Stock[Resource.Iron] >= ReinforcedWallIronCost)
        {
            built = w.TryAddReinforcedWall(_home, spot);
            if (built)
            {
                _home.Stock[Resource.Stone] -= ReinforcedWallStoneCost;
                _home.Stock[Resource.Iron] -= ReinforcedWallIronCost;
            }
        }
        else if (HasTech("fortification") && _home.Stock[Resource.Stone] >= StoneWallStoneCost)
        {
            built = w.TryAddStoneWall(_home, spot);
            if (built)
                _home.Stock[Resource.Stone] -= StoneWallStoneCost;
        }
        else
        {
            built = w.TryAddWoodWall(_home, spot);
            if (built)
                _home.Stock[Resource.Wood] -= WallWoodCost;
        }

        if (!built)
        {
            ResetBuildSiteState(w);
            return;
        }

        TryBuildGateNearby(w, spot);
        ClearBuildSiteState();
        w.AddExternalEvent($"{_home.Name} raised a border wall");
    }

    void ExecuteBuildWatchtowerAction(World w)
    {
        if (!CanAffordTowerBuild(w))
            return;

        if (!HasValidActiveBuildSite(w, Job.BuildWatchtower) || _activeBuildSite == null)
        {
            ResetBuildSiteState(w);
            return;
        }

        var spot = _activeBuildSite.Value;
        if (Pos != spot)
            return;

        var reservationKey = BuildBuildReservationKey(spot.x, spot.y);
        w.ReserveSoftTarget(reservationKey);
        DebugDecisionCause = "build_watchtower";
        DebugTargetKey = reservationKey;

        var built = false;
        if (HasTech("siege_craft")
            && _home.Stock[Resource.Wood] >= CatapultTowerWoodCost
            && _home.Stock[Resource.Stone] >= CatapultTowerStoneCost
            && _home.Stock[Resource.Iron] >= CatapultTowerIronCost)
        {
            built = w.TryAddCatapultTower(_home, spot);
            if (built)
            {
                _home.Stock[Resource.Wood] -= CatapultTowerWoodCost;
                _home.Stock[Resource.Stone] -= CatapultTowerStoneCost;
                _home.Stock[Resource.Iron] -= CatapultTowerIronCost;
            }
        }
        else if (HasTech("fortification")
                 && _home.Stock[Resource.Wood] >= ArrowTowerWoodCost
                 && _home.Stock[Resource.Stone] >= ArrowTowerStoneCost)
        {
            built = w.TryAddArrowTower(_home, spot);
            if (built)
            {
                _home.Stock[Resource.Wood] -= ArrowTowerWoodCost;
                _home.Stock[Resource.Stone] -= ArrowTowerStoneCost;
            }
        }
        else
        {
            built = w.TryAddWatchtower(_home, spot);
            if (built)
            {
                _home.Stock[Resource.Wood] -= WatchtowerWoodCost;
                _home.Stock[Resource.Stone] -= WatchtowerStoneCost;
            }
        }

        if (!built)
        {
            ResetBuildSiteState(w);
            return;
        }

        ClearBuildSiteState();
        w.AddExternalEvent($"{_home.Name} completed a watchtower");
    }

    private void TryBuildGateNearby(World w, (int x, int y) wallSpot)
    {
        if (!HasTech("fortification"))
            return;
        if (_home.Stock[Resource.Wood] < GateWoodCost || _home.Stock[Resource.Stone] < GateStoneCost)
            return;
        if (w.DefensiveStructures.Count(structure => structure.Owner == _home && structure.Kind == WorldSim.Simulation.Defense.DefensiveStructureKind.Gate) >= 1)
            return;

        var origin = _home.Origin;
        var candidates = new[]
        {
            (origin.x + 2, origin.y),
            (origin.x - 2, origin.y),
            (origin.x, origin.y + 2),
            (origin.x, origin.y - 2),
            (wallSpot.x + 1, wallSpot.y),
            (wallSpot.x - 1, wallSpot.y),
            (wallSpot.x, wallSpot.y + 1),
            (wallSpot.x, wallSpot.y - 1)
        };

        foreach (var candidate in candidates)
        {
            if (!w.TryAddGate(_home, candidate))
                continue;

            _home.Stock[Resource.Wood] -= GateWoodCost;
            _home.Stock[Resource.Stone] -= GateStoneCost;
            break;
        }
    }

    void ExecuteRaidBorderAction(World w)
    {
        var threatContext = BuildThreatContext(w);
        if (ThreatDecisionPolicy.ShouldRetreatFromSiege(threatContext))
        {
            ExecuteFleeAction(w);
            return;
        }

        if (TryAttackOrPursueHostilePerson(w, radius: GetCombatChaseRadius(w, RaidContactRadius), switchToFight: true, pursuitCause: "raid_engage_enemy"))
            return;

        if (TryPursueRecentHostile(w, debugCause: "raid_pursue_recent_hostile"))
        {
            Current = Job.Fight;
            _doingJob = Math.Max(_doingJob, 1);
            return;
        }

        var preferTowerTargets = ThreatDecisionPolicy.ShouldPrioritizeSiegeTargeting(threatContext);

        var adjacentEnemyStructure = FindNearestEnemyStructure(w, radius: 1, prioritizeSiege: true, preferTowerTargets: preferTowerTargets);
        if (adjacentEnemyStructure != null)
        {
            Current = Job.AttackStructure;
            _doingJob = 1;
            return;
        }

        var target = FindRaidTarget(w, preferTowerTargets);
        if (target == null)
            return;

        var previous = Pos;
        MoveTowards(w, target.Value, 1);
        if (Pos != previous)
            return;

        var nearbyEnemyStructure = FindNearestEnemyStructure(w, radius: 2, prioritizeSiege: true, preferTowerTargets: preferTowerTargets);
        if (nearbyEnemyStructure != null)
        {
            Current = Job.AttackStructure;
            _doingJob = 1;
        }
    }

    void ExecuteAttackStructureAction(World w)
    {
        if (!w.EnableSiege)
            return;

        var threatContext = BuildThreatContext(w);
        if (ThreatDecisionPolicy.ShouldRetreatFromSiege(threatContext))
        {
            ExecuteFleeAction(w);
            return;
        }

        var preferTowerTargets = ThreatDecisionPolicy.ShouldPrioritizeSiegeTargeting(threatContext);
        var target = FindNearestEnemyStructure(w, radius: 1, prioritizeSiege: true, preferTowerTargets: preferTowerTargets);
        if (target == null)
            return;

        w.ReportCombatEngagement();
        w.ReportSiegePressure(_home, target.Value.structure);
        var beforeDestroyed = target.Value.structure.IsDestroyed;
        var damage = ComputeStructureRaidDamage(w, target.Value.structure);
        var damaged = w.TryDamageDefensiveStructure(target.Value.structure.Pos, damage, _home);
        if (!damaged)
            return;

        if (!beforeDestroyed && target.Value.structure.IsDestroyed)
        {
            ApplyRaidSuccess(w, target.Value.structure.Owner);
            w.AddExternalEvent($"{_home.Name} raiders destroyed {target.Value.structure.Owner.Name} defense");
        }
    }

    private NpcAiContext BuildThreatContext(World w)
    {
        return RuntimeNpcBrain.CreateContext(this, w, simulationTimeSeconds: 0f);
    }

    private void CountNearbySiegeStructures(
        World w,
        int radius,
        out int enemyDefensiveStructures,
        out int enemyTowerCount,
        out int enemyWallCount,
        out int friendlyTowerCount,
        out int friendlyWallCount)
    {
        enemyDefensiveStructures = 0;
        enemyTowerCount = 0;
        enemyWallCount = 0;
        friendlyTowerCount = 0;
        friendlyWallCount = 0;

        foreach (var structure in w.DefensiveStructures)
        {
            if (structure.IsDestroyed)
                continue;

            int dist = Math.Abs(structure.Pos.x - Pos.x) + Math.Abs(structure.Pos.y - Pos.y);
            if (dist > radius)
                continue;

            bool isEnemy = structure.Owner.Faction != _home.Faction;
            bool isTower = IsTowerKind(structure.Kind);
            bool isWall = IsWallKind(structure.Kind);

            if (isEnemy)
            {
                enemyDefensiveStructures++;
                if (isTower)
                    enemyTowerCount++;
                else if (isWall)
                    enemyWallCount++;
            }
            else
            {
                if (isTower)
                    friendlyTowerCount++;
                else if (isWall)
                    friendlyWallCount++;
            }
        }
    }

    private static bool IsTowerKind(WorldSim.Simulation.Defense.DefensiveStructureKind kind)
        => kind is WorldSim.Simulation.Defense.DefensiveStructureKind.Watchtower
            or WorldSim.Simulation.Defense.DefensiveStructureKind.ArrowTower
            or WorldSim.Simulation.Defense.DefensiveStructureKind.CatapultTower;

    private static bool IsWallKind(WorldSim.Simulation.Defense.DefensiveStructureKind kind)
        => kind is WorldSim.Simulation.Defense.DefensiveStructureKind.WoodWall
            or WorldSim.Simulation.Defense.DefensiveStructureKind.StoneWall
            or WorldSim.Simulation.Defense.DefensiveStructureKind.ReinforcedWall
            or WorldSim.Simulation.Defense.DefensiveStructureKind.Gate;

    private bool IsWarriorRole(World w)
    {
        if (Profession == Profession.Hunter)
            return true;

        var warState = w.GetColonyWarState(_home.Id);
        if (warState == ColonyWarState.Peace)
            return false;

        var warriorCount = w.GetColonyWarriorCount(_home.Id);
        if (warriorCount <= 0)
            return false;

        var myPower = Strength + Defense;
        var stronger = w._people.Count(person =>
            person != this
            && person.Home == _home
            && person.Health > 0f
            && (person.Strength + person.Defense) > myPower);
        return stronger < warriorCount;
    }

    public float ScaleOutgoingCombatDamage(World world, float baseDamage)
    {
        if (baseDamage <= 0f)
            return 0f;

        if (!IsWarriorRole(world))
            return baseDamage;

        float weaponMultiplier = 1f + (_home.WeaponLevel * 0.12f);
        return baseDamage * weaponMultiplier * world.CombatDamageBonusMultiplier;
    }

    private float GetIncomingCombatDamageMultiplier(World world)
    {
        if (!IsWarriorRole(world))
            return 1f;

        float armorMultiplier = Math.Clamp(1f - (_home.ArmorLevel * 0.10f), 0.55f, 1f);
        float globalDefense = Math.Clamp(world.CombatDefenseBonusMultiplier, 0.5f, 2f);
        float globalMultiplier = 1f / globalDefense;
        return Math.Clamp(armorMultiplier * globalMultiplier, 0.45f, 1f);
    }

    private bool HasContestedTileNearby(World w, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                if (w.IsTileContested(Pos.x + dx, Pos.y + dy))
                    return true;
            }
        }

        return false;
    }

    private int CountOwnWatchtowers(World w)
        => w.DefensiveStructures.Count(structure =>
            structure.Owner == _home
            && structure is WorldSim.Simulation.Defense.Watchtower
            && !structure.IsDestroyed);

    private (int x, int y)? FindHouseBuildPlacement(World w)
    {
        var origin = _home.Origin;
        var actorFreeCandidates = new List<(int x, int y)>();
        var fallbackCandidates = new List<(int x, int y)>();
        for (int dy = -6; dy <= 6; dy++)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                int dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist < 2 || dist > 6)
                    continue;

                int x = origin.x + dx;
                int y = origin.y + dy;
                if (x < 0 || y < 0 || x >= w.Width || y >= w.Height)
                    continue;
                if (!w.CanPlaceStructureAt(x, y))
                    continue;

                var candidate = (x, y);
                fallbackCandidates.Add(candidate);
                if (!w.IsActorOccupied(x, y, exclude: this))
                    actorFreeCandidates.Add(candidate);
            }
        }

        var candidates = actorFreeCandidates.Count > 0 ? actorFreeCandidates : fallbackCandidates;
        if (candidates.Count == 0)
            return null;

        (int x, int y)? best = null;
        float bestScore = float.MaxValue;
        foreach (var candidate in candidates)
        {
            float score = Math.Abs(candidate.x - Pos.x) + Math.Abs(candidate.y - Pos.y);
            var reservationKey = BuildBuildReservationKey(candidate.x, candidate.y);
            score += w.GetSoftReservationCount(reservationKey) * 1.5f;

            int localOccupancy = w._people.Count(person => person != this && person.Health > 0f && person.Pos == candidate);
            score += localOccupancy * 1.2f;

            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best ?? candidates[_rng.Next(candidates.Count)];
    }

    private (int x, int y)? FindDefensePlacement(World w, bool preferContested)
    {
        var origin = _home.Origin;
        var actorFreeCandidates = new List<(int x, int y)>();
        var fallbackCandidates = new List<(int x, int y)>();
        for (int dy = -6; dy <= 6; dy++)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                int dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist < 2 || dist > 6)
                    continue;

                int x = origin.x + dx;
                int y = origin.y + dy;
                if (x < 0 || y < 0 || x >= w.Width || y >= w.Height)
                    continue;
                if (!w.CanPlaceStructureAt(x, y))
                    continue;

                var candidate = (x, y);
                fallbackCandidates.Add(candidate);
                if (!w.IsActorOccupied(x, y, exclude: this))
                    actorFreeCandidates.Add(candidate);
            }
        }

        var candidates = actorFreeCandidates.Count > 0 ? actorFreeCandidates : fallbackCandidates;
        if (candidates.Count == 0)
            return null;

        (int x, int y)? best = null;
        float bestScore = float.MaxValue;
        foreach (var candidate in candidates)
        {
            float score = Math.Abs(candidate.x - Pos.x) + Math.Abs(candidate.y - Pos.y);
            if (preferContested && w.IsTileContested(candidate.x, candidate.y))
                score -= 2f;

            var reservationKey = BuildBuildReservationKey(candidate.x, candidate.y);
            score += w.GetSoftReservationCount(reservationKey) * 1.5f;

            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best ?? candidates[_rng.Next(candidates.Count)];
    }

    private (int x, int y)? FindRaidTarget(World w, bool preferTowerTargets)
    {
        var enemy = FindNearestHostilePerson(w, radius: 8);
        if (enemy != null)
        {
            RememberHostileContact(w, enemy.Value.person);
            return enemy.Value.person.Pos;
        }

        var nearestContested = FindNearestContestedTile(w, radius: 12);
        if (nearestContested != null)
        {
            int owner = w.GetTileOwnerColonyId(nearestContested.Value.x, nearestContested.Value.y);
            if (owner >= 0 && owner != _home.Id)
                return nearestContested;
        }

        var structure = FindNearestEnemyStructure(w, radius: 10, prioritizeSiege: true, preferTowerTargets: preferTowerTargets);
        if (structure != null)
            return structure.Value.structure.Pos;
        enemy = FindNearestHostilePerson(w, radius: 10);
        if (enemy != null)
            RememberHostileContact(w, enemy.Value.person);

        return enemy?.person.Pos;
    }

    private float ComputeStructureRaidDamage(World w, WorldSim.Simulation.Defense.DefensiveStructure structure)
    {
        var baseDamage = StructureRaidDamage * Math.Max(1f, w.SiegeDamageMultiplier);
        var roleBonus = Profession == Profession.Hunter ? 1.12f : 1f;
        var strengthBonus = 1f + (Strength / 120f);
        var techBonus = _home.UnlockedTechs.Contains("siege_craft") ? 1.15f : 1f;
        var targetMitigation = structure.Kind switch
        {
            WorldSim.Simulation.Defense.DefensiveStructureKind.WoodWall => 1.05f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.Gate => 1.0f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.StoneWall => 0.86f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.ReinforcedWall => 0.78f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.Watchtower => 0.9f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.ArrowTower => 0.86f,
            WorldSim.Simulation.Defense.DefensiveStructureKind.CatapultTower => 0.82f,
            _ => 1f
        };

        return Math.Max(6f, baseDamage * roleBonus * strengthBonus * techBonus * targetMitigation);
    }

    private (WorldSim.Simulation.Defense.DefensiveStructure structure, int dist)? FindNearestEnemyStructure(
        World w,
        int radius,
        bool prioritizeSiege = false,
        bool preferTowerTargets = false)
    {
        WorldSim.Simulation.Defense.DefensiveStructure? nearest = null;
        float best = float.MaxValue;
        foreach (var structure in w.DefensiveStructures)
        {
            if (structure.IsDestroyed || structure.Owner == _home || structure.Owner.Faction == _home.Faction)
                continue;

            int dist = Math.Abs(structure.Pos.x - Pos.x) + Math.Abs(structure.Pos.y - Pos.y);
            if (dist > radius)
                continue;

            float score = dist;
            if (prioritizeSiege)
            {
                int priority = structure.Kind switch
                {
                    WorldSim.Simulation.Defense.DefensiveStructureKind.ArrowTower or WorldSim.Simulation.Defense.DefensiveStructureKind.CatapultTower or WorldSim.Simulation.Defense.DefensiveStructureKind.Watchtower => preferTowerTargets ? 0 : 2,
                    WorldSim.Simulation.Defense.DefensiveStructureKind.Gate => preferTowerTargets ? 1 : 0,
                    WorldSim.Simulation.Defense.DefensiveStructureKind.WoodWall or WorldSim.Simulation.Defense.DefensiveStructureKind.StoneWall or WorldSim.Simulation.Defense.DefensiveStructureKind.ReinforcedWall => preferTowerTargets ? 2 : 1,
                    _ => 3
                };

                score += (priority * 100f) + (structure.Hp / Math.Max(1f, structure.MaxHp));
            }

            if (score < best)
            {
                best = score;
                nearest = structure;
            }
        }

        if (nearest == null)
            return null;

        var finalDist = Math.Abs(nearest.Pos.x - Pos.x) + Math.Abs(nearest.Pos.y - Pos.y);
        return (nearest, finalDist);
    }

    private void ApplyRaidSuccess(World w, Colony enemyColony)
    {
        if (enemyColony == _home)
            return;

        int foodLoot = Math.Min(3, enemyColony.Stock[Resource.Food]);
        int woodLoot = Math.Min(2, enemyColony.Stock[Resource.Wood]);

        enemyColony.Stock[Resource.Food] -= foodLoot;
        enemyColony.Stock[Resource.Wood] -= woodLoot;
        _home.Stock[Resource.Food] += foodLoot;
        _home.Stock[Resource.Wood] += woodLoot;

        var stance = w.GetFactionStance(_home.Faction, enemyColony.Faction);
        if (stance < WorldSim.Simulation.Diplomacy.Stance.Hostile)
            w.SetFactionStance(_home.Faction, enemyColony.Faction, WorldSim.Simulation.Diplomacy.Stance.Hostile);
        else if (stance == WorldSim.Simulation.Diplomacy.Stance.Hostile)
            w.SetFactionStance(_home.Faction, enemyColony.Faction, WorldSim.Simulation.Diplomacy.Stance.War);

        w.RegisterRaidImpact(_home.Faction, enemyColony.Faction, pressureBoost: 65d);

        w.AddExternalEvent($"{_home.Name} raid looted {foodLoot} food and {woodLoot} wood");
    }

    private (Person person, int dist)? FindNearestHostilePerson(World w, int radius)
    {
        Person? nearest = null;
        int bestDist = int.MaxValue;
        foreach (var person in w._people)
        {
            if (person == this || person.Health <= 0f || person.Home == _home)
                continue;

            var stance = w.GetFactionStance(_home.Faction, person.Home.Faction);
            if (stance < WorldSim.Simulation.Diplomacy.Stance.Hostile)
                continue;

            int dist = Math.Abs(person.Pos.x - Pos.x) + Math.Abs(person.Pos.y - Pos.y);
            if (dist <= radius && dist < bestDist)
            {
                bestDist = dist;
                nearest = person;
            }
        }

        if (nearest == null)
            return null;

        return (nearest, bestDist);
    }

    internal bool HasCombatFollowThroughIntent(int currentTick)
    {
        return Current is Job.Fight or Job.RaidBorder or Job.AttackStructure
            || HasRecentCombatIntent(currentTick);
    }

    internal bool HasRecentCombatIntent(int currentTick)
    {
        bool recentHostile = currentTick - _recentHostileContactTick <= RecentHostileMemoryTicks;
        bool recentCombat = LastCombatTick >= 0 && currentTick - LastCombatTick <= RecentHostileMemoryTicks;
        return recentHostile || recentCombat;
    }

    private static int GetCombatChaseRadius(World world, int baseRadius)
        => baseRadius + (world.IsLargeCombatTopology ? 2 : 0);

    private static int GetRecentHostilePursuitRadius(World world)
        => RecentHostilePursuitRadius + (world.IsLargeCombatTopology ? 2 : 0);

    private bool TryAttackOrPursueHostilePerson(World w, int radius, bool switchToFight, string pursuitCause)
    {
        var hostilePerson = FindNearestHostilePerson(w, radius);
        if (hostilePerson == null)
            return false;

        w.ReportContactHostileSensed(this);
        RememberHostileContact(w, hostilePerson.Value.person);
        if (switchToFight)
        {
            Current = Job.Fight;
            _doingJob = Math.Max(_doingJob, 1);
        }

        int hostileDist = hostilePerson.Value.dist;
        if (hostileDist > 1)
        {
            w.ReportContactPursueStart(this);
            DebugDecisionCause = pursuitCause;
            DebugTargetKey = $"move:{hostilePerson.Value.person.Pos.x}:{hostilePerson.Value.person.Pos.y}";
            _trackNoProgressForCurrentMove = true;
            _noProgressTrackContext = "combat";
            MoveTowards(w, hostilePerson.Value.person.Pos, 1);
            return true;
        }

        w.ReportContactAdjacentContact(this);
        EngageHostilePerson(w, hostilePerson.Value.person);
        return true;
    }

    private bool TryPursueRecentHostile(World w, string debugCause = "pursue_recent_hostile")
    {
        if (!TryGetRecentHostilePursuitTarget(w, out var target))
            return false;

        DebugDecisionCause = debugCause;
        DebugTargetKey = $"move:{target.x}:{target.y}";
        _trackNoProgressForCurrentMove = true;
        _noProgressTrackContext = "combat";
        MoveTowards(w, target, 1);
        return true;
    }

    private bool TryGetRecentHostilePursuitTarget(World w, out (int x, int y) target)
    {
        if (!HasRecentCombatIntent(w.CurrentTick))
        {
            target = default;
            return false;
        }

        if (_recentHostileActorId > 0)
        {
            var actor = w._people.FirstOrDefault(person => person.Id == _recentHostileActorId && person.Health > 0f);
            if (actor != null && actor.Home != _home && w.GetFactionStance(_home.Faction, actor.Home.Faction) >= WorldSim.Simulation.Diplomacy.Stance.Hostile)
            {
                _recentHostilePos = actor.Pos;
                var actorDistance = Math.Abs(actor.Pos.x - Pos.x) + Math.Abs(actor.Pos.y - Pos.y);
                if (actorDistance <= GetRecentHostilePursuitRadius(w))
                {
                    target = actor.Pos;
                    return true;
                }
            }
        }

        var distance = Math.Abs(_recentHostilePos.x - Pos.x) + Math.Abs(_recentHostilePos.y - Pos.y);
        if (distance == 0 || distance > GetRecentHostilePursuitRadius(w))
        {
            target = default;
            return false;
        }

        target = _recentHostilePos;
        return true;
    }

    private void RememberHostileContact(World w, Person hostile)
    {
        _recentHostileActorId = hostile.Id;
        _recentHostilePos = hostile.Pos;
        _recentHostileContactTick = w.CurrentTick;
    }

    private void EngageHostilePerson(World w, Person enemy)
    {
        RememberHostileContact(w, enemy);
        w.ReportCombatEngagement();
        var myPower = Strength + (Defense / 2f);
        var enemyPower = enemy.Strength + (enemy.Defense / 2f);
        var duelWinChance = Math.Clamp(0.40f + ((myPower - enemyPower) / 50f), 0.15f, 0.9f);
        if (_rng.NextDouble() < duelWinChance)
            enemy.ApplyCombatDamage(w, ScaleOutgoingCombatDamage(w, Math.Max(2f, Strength * 0.8f)), "FactionCombat");
        else
            ApplyCombatDamage(w, enemy.ScaleOutgoingCombatDamage(w, Math.Max(1.5f, enemy.Strength * 0.65f)), "FactionCombat");
    }

    private (int x, int y)? FindNearestContestedTile(World w, int radius)
    {
        (int x, int y)? nearest = null;
        int bestDist = int.MaxValue;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int dist = Math.Abs(dx) + Math.Abs(dy);
                if (dist > radius)
                    continue;

                int x = Pos.x + dx;
                int y = Pos.y + dy;
                if (!w.IsTileContested(x, y))
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = (x, y);
                }
            }
        }

        return nearest;
    }

    (int x, int y)? FindNearestThreat(World w, int radius)
    {
        (int x, int y)? bestPos = null;
        int bestDist = int.MaxValue;

        foreach (var animal in w._animals)
        {
            if (animal is not Predator predator || !predator.IsAlive)
                continue;

            int dist = Math.Abs(predator.Pos.x - Pos.x) + Math.Abs(predator.Pos.y - Pos.y);
            if (dist <= radius && dist < bestDist)
            {
                bestDist = dist;
                bestPos = predator.Pos;
            }
        }

        foreach (var person in w._people)
        {
            if (person == this || person.Health <= 0f || person.Home == _home)
                continue;

            int dist = Math.Abs(person.Pos.x - Pos.x) + Math.Abs(person.Pos.y - Pos.y);
            if (dist <= radius && dist < bestDist)
            {
                bestDist = dist;
                bestPos = person.Pos;
            }
        }

        return bestPos;
    }

    void TryConsumeToolCharge()
    {
        if (_home.ToolCharges > 0)
            _home.ToolCharges--;
    }

    public void ApplyDamage(float amount, string source)
    {
        if (amount <= 0f || Health <= 0f)
            return;

        Health -= amount;
        EnterCombat(world: null);
        if (Health <= 0f)
        {
            LastDeathReason = source.Contains("Predator", StringComparison.OrdinalIgnoreCase)
                ? PersonDeathReason.Predator
                : PersonDeathReason.Other;
        }
    }

    public void ApplyCombatDamage(World world, float amount, string source)
    {
        if (amount <= 0f || Health <= 0f)
            return;

        bool isFactionCombat = string.Equals(source, "FactionCombat", StringComparison.Ordinal);
        float adjusted = amount * GetIncomingCombatDamageMultiplier(world);
        Health -= adjusted;
        EnterCombat(world);
        ApplyMoraleDelta(-(adjusted * 0.08f));
        if (isFactionCombat)
            world.ReportContactFactionCombatDamage();
        if (Health <= 0f)
        {
            LastDeathReason = PersonDeathReason.Combat;
            if (isFactionCombat)
                world.ReportContactFactionCombatDeath();
        }
    }

    private void EnterCombat(World? world)
    {
        IsInCombat = true;
        _combatMarkerSeconds = 1.25f;
        LastCombatTick = world?.CurrentTick ?? (LastCombatTick + 1);
    }

    public void SetCombatAssignment(int? groupId, int? battleId, Formation formation, bool isCommander)
    {
        ActiveCombatGroupId = groupId ?? -1;
        ActiveBattleId = battleId ?? -1;
        AssignedFormation = formation;
        IsCombatCommander = isCommander;

        if (!isCommander)
        {
            CommanderIntelligence = 0;
            CommanderMoraleStabilityBonus = 0f;
            return;
        }

        CommanderIntelligence = Intelligence;
        CommanderMoraleStabilityBonus = Math.Clamp(Intelligence / 30f, 0f, 0.45f);
    }

    public void ApplyMoraleDelta(float delta)
    {
        var adjusted = delta;
        if (delta < 0f)
            adjusted *= (1f - CommanderMoraleStabilityBonus);
        else if (delta > 0f)
            adjusted *= (1f + (CommanderMoraleStabilityBonus * 0.5f));

        CombatMorale = Math.Clamp(CombatMorale + adjusted, 0f, 100f);
    }

    public bool BeginRouting(int ticks, (int x, int y)? origin = null)
    {
        bool wasRouting = IsRouting;
        RoutingTicksRemaining = Math.Max(RoutingTicksRemaining, ticks);
        IsRouting = RoutingTicksRemaining > 0;
        _routingOrigin = origin;
        if (IsRouting)
            Current = Job.Flee;

        SetCombatAssignment(null, null, AssignedFormation, isCommander: false);
        return !wasRouting && IsRouting;
    }

    public void MarkCombatPresence(World world)
    {
        EnterCombat(world);
    }

    bool HasNearbyOwnHouse(World w, int radius)
    {
        int r = Math.Max(0, radius);
        foreach (var h in w.Houses)
        {
            if (h.Owner != _home)
                continue;

            int dist = Math.Abs(h.Pos.x - Pos.x) + Math.Abs(h.Pos.y - Pos.y);
            if (dist <= r)
                return true;
        }

        return false;
    }

    bool TryMoveTowardsNearestResource(World w, int searchRadius, params Resource[] desired)
    {
        (int x, int y)? bestPos = null;
        int bestDist = int.MaxValue;
        float bestScore = float.MaxValue;
        Resource bestType = Resource.None;

        bool Wants(Resource r)
        {
            if (desired == null || desired.Length == 0) return false;
            for (int i = 0; i < desired.Length; i++)
                if (desired[i] == r) return true;
            return false;
        }

        for (int r = 1; r <= searchRadius; r++)
        {
            // Manhattan-gyűrű bejárása
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = Pos.x + dx;
                    int ny = Pos.y + dy;
                    if (nx < 0 || ny < 0 || nx >= w.Width || ny >= w.Height) continue;
                    int md = Math.Abs(dx) + Math.Abs(dy);
                    if (md > r) continue;

                    var node = w.GetTile(nx, ny).Node;
                    if (node == null || node.Amount <= 0) continue;
                    if (!Wants(node.Type)) continue;

                    string reservationKey = BuildResourceReservationKey(node.Type, nx, ny);
                    int reserved = w.GetSoftReservationCount(reservationKey);
                    float score = md + (reserved * 1.25f);

                    if (score < bestScore || (Math.Abs(score - bestScore) < 0.001f && md < bestDist))
                    {
                        bestScore = score;
                        bestDist = md;
                        bestPos = (nx, ny);
                        bestType = node.Type;
                    }
                }
            }
            if (bestPos != null) break; // legközelebbi megvan
        }

        if (bestPos == null) return false;

        // Ha már rajta állunk
        if (bestDist == 0)
        {
            var reservationKey = BuildResourceReservationKey(bestType, bestPos.Value.x, bestPos.Value.y);
            w.ReserveSoftTarget(reservationKey);
            DebugDecisionCause = "gather";
            DebugTargetKey = reservationKey;
            _protectFromDissipationThisTick = true;

            if (bestType == Resource.Wood)
            {
                Current = Job.GatherWood;
                _doingJob = ComputeTicks(WoodWorkTime, w, isHeavyWork: true);
            }
            else if (bestType == Resource.Stone)
            {
                Current = Job.GatherStone;
                _doingJob = ComputeTicks(StoneWorkTime, w, isHeavyWork: true);
            }
            else if (bestType == Resource.Food)
            {
                Current = Job.GatherFood;
                _doingJob = ComputeTicks(FoodWorkTime, w, isHeavyWork: true);
            }
            else if (bestType == Resource.Iron)
            {
                Current = Job.GatherIron;
                _doingJob = ComputeTicks(IronWorkTime, w, isHeavyWork: true);
            }
            else if (bestType == Resource.Gold)
            {
                Current = Job.GatherGold;
                _doingJob = ComputeTicks(GoldWorkTime, w, isHeavyWork: true);
            }
            return true;
        }

        // Step toward target
        var moveReservationKey = BuildResourceReservationKey(bestType, bestPos.Value.x, bestPos.Value.y);
        w.ReserveSoftTarget(moveReservationKey);
        DebugDecisionCause = "move_to_resource";
        DebugTargetKey = moveReservationKey;
        _trackNoProgressForCurrentMove = true;
        _noProgressTrackContext = "resource";
        MoveTowards(w, bestPos.Value, 1);
        return true;
    }

    private static string BuildResourceReservationKey(Resource resource, int x, int y)
        => $"resource:{resource}:{x}:{y}";

    bool TryHuntNearbyHerbivore(World w, int range)
    {
        Herbivore? nearest = null;
        int best = int.MaxValue;

        foreach (var animal in w._animals)
        {
            if (animal is not Herbivore herb || !herb.IsAlive)
                continue;

            int dist = Math.Abs(herb.Pos.x - Pos.x) + Math.Abs(herb.Pos.y - Pos.y);
            if (dist > range || dist >= best)
                continue;

            nearest = herb;
            best = dist;
        }

        if (nearest == null)
            return false;

        nearest.IsAlive = false;
        Emotions["Hope"] = Math.Clamp(Emotions.GetValueOrDefault("Hope", 0f) + 2f, -100f, 100f);
        return true;
    }

    void MoveTowards(World w, (int x, int y) target, int maxStep)
    {
        int remaining = ResolveMoveStepBudget(maxStep);
        if (remaining <= 0)
            return;

        var startPos = Pos;
        int cx = Pos.x, cy = Pos.y;
        var grid = new NavigationGrid(w);

        while (remaining-- > 0 && (cx != target.x || cy != target.y))
        {
            if (_unstickStepsRemaining > 0)
            {
                if (TryUnstickStep(w, ref cx, ref cy))
                    _unstickStepsRemaining--;
                else
                    _unstickStepsRemaining = 0;
                continue;
            }

            var topologyVersion = grid.TopologyVersion;
            if (!_pathCache.IsValid(target, topologyVersion))
                BuildPathCache(grid, (cx, cy), target, topologyVersion);

            var next = _pathCache.PeekNext();
            if (next.HasValue)
            {
                if (w.IsMovementBlocked(next.Value.x, next.Value.y, _home.Id))
                {
                    _pathCache.Invalidate();
                    BuildPathCache(grid, (cx, cy), target, topologyVersion);
                    next = _pathCache.PeekNext();
                }

                if (next.HasValue && !w.IsMovementBlocked(next.Value.x, next.Value.y, _home.Id))
                {
                    cx = next.Value.x;
                    cy = next.Value.y;
                    _pathCache.Advance();
                    continue;
                }
            }

            int dx = target.x - cx;
            int dy = target.y - cy;

            int nx = cx, ny = cy;
            if (Math.Abs(dx) >= Math.Abs(dy))
                nx += Math.Sign(dx);
            else
                ny += Math.Sign(dy);

            nx = Math.Clamp(nx, 0, w.Width - 1);
            ny = Math.Clamp(ny, 0, w.Height - 1);

            if (!w.IsMovementBlocked(nx, ny, _home.Id))
            {
                cx = nx;
                cy = ny;
                _pathCache.Invalidate();
            }
            else
            {
                BuildPathCache(grid, (cx, cy), target, topologyVersion);
                next = _pathCache.PeekNext();
                if (next.HasValue && !w.IsMovementBlocked(next.Value.x, next.Value.y, _home.Id))
                {
                    cx = next.Value.x;
                    cy = next.Value.y;
                    _pathCache.Advance();
                }
                else
                {
                    break;
                }
            }
        }

        Pos = (cx, cy);

        var noProgressContext = _noProgressTrackContext;
        if (noProgressContext == "move" && Current is Job.Fight or Job.RaidBorder or Job.AttackStructure)
            noProgressContext = "combat";

        var shouldTrackNoProgress = ShouldTrackNoProgressForCurrentMove();
        _trackNoProgressForCurrentMove = false;
        _noProgressTrackContext = "move";
        if (!shouldTrackNoProgress)
        {
            _noProgressStreak = 0;
            return;
        }

        if (Pos == startPos)
        {
            _noProgressStreak++;
            if (_noProgressStreak >= NoProgressThreshold)
            {
                _noProgressStreak = 0;
                _backoffTicksRemaining = Math.Max(_backoffTicksRemaining, NoProgressBackoffTicks);
                _pathCache.Invalidate();
                _unstickStepsRemaining = Math.Max(_unstickStepsRemaining, UnstickSteps);
                DebugDecisionCause = $"no_progress_backoff:{noProgressContext}";
                DebugTargetKey = $"move:{target.x}:{target.y}";
                Current = Job.Idle;
                _doingJob = 0;
                w.ReportNoProgressBackoff(noProgressContext);

                if (noProgressContext is "resource" or "build")
                    _suppressPeacefulActionsDuringBackoff = true;
                if (noProgressContext == "build")
                    ResetBuildSiteState(w);
            }
        }
        else
        {
            _noProgressStreak = 0;
        }
    }

    int ResolveMoveStepBudget(int baseStep)
    {
        if (baseStep <= 0)
            return 0;

        var speedMultiplier = Math.Max(0f, _home.MovementSpeedMultiplier);
        if (speedMultiplier <= 0f)
            return 0;

        _movementStepCarry += baseStep * speedMultiplier;
        var resolved = (int)MathF.Floor(_movementStepCarry);
        if (resolved > 0)
            _movementStepCarry -= resolved;

        return resolved;
    }

    private bool ShouldTrackNoProgressForCurrentMove()
    {
        if (_trackNoProgressForCurrentMove)
            return true;

        return Current is Job.Fight or Job.RaidBorder or Job.AttackStructure;
    }

    void BuildPathCache(NavigationGrid grid, (int x, int y) start, (int x, int y) target, int topologyVersion)
    {
        var path = NavigationPathfinder.FindPath(
            grid,
            start,
            target,
            moverColonyId: _home.Id,
            maxExpansions: PathMaxExpansions,
            out var budgetExceeded);

        if (budgetExceeded)
        {
            _pathCache.Invalidate();
            _unstickStepsRemaining = UnstickSteps;
            return;
        }

        if (path.Count <= 1)
        {
            _pathCache.Invalidate();
            return;
        }

        var horizon = Math.Min(path.Count, PathCacheHorizon + 1);
        _pathCache.Set(target, topologyVersion, path.Take(horizon).ToList());
    }

    bool TryUnstickStep(World w, ref int cx, ref int cy)
    {
        var candidates = new List<(int x, int y)>
        {
            (cx + 1, cy),
            (cx - 1, cy),
            (cx, cy + 1),
            (cx, cy - 1)
        };

        foreach (var candidate in candidates.OrderBy(_ => _rng.Next()))
        {
            int nx = Math.Clamp(candidate.x, 0, w.Width - 1);
            int ny = Math.Clamp(candidate.y, 0, w.Height - 1);
            if (w.IsMovementBlocked(nx, ny, _home.Id))
                continue;

            cx = nx;
            cy = ny;
            return true;
        }

        return false;
    }

    void Wander(World w)
    {
        int moveDistance = Math.Max(4, (int)MathF.Ceiling(4f * Math.Max(1f, _home.MovementSpeedMultiplier)));
        int tries = 8;
        for (int i = 0; i < tries; i++)
        {
            int nx = Math.Clamp(Pos.x + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Width - 1);
            int ny = Math.Clamp(Pos.y + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Height - 1);
            if (!w.IsMovementBlocked(nx, ny, _home.Id))
            {
                Pos = (nx, ny);
                return;
            }
        }
        // stay if all around is water
    }
}
