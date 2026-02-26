using System;
using System.Collections.Generic;
using System.Linq;

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

    // Combat extension minimum (Phase 0 ready)
    Fight,
    Flee,
    GuardColony,
    PatrolBorder
}
public enum Profession { Generalist, Lumberjack, Miner, Farmer, Hunter, Builder }
public enum PersonDeathReason { None, OldAge, Starvation, Predator, Other }

public class Person
{
    public (int x, int y) Pos;
    public Job Current = Job.Idle;
    public float Health = 100;  
    public float Defense = 0f;
    public float Age = 0;
    public float Stamina { get; private set; } = 100f;
    public PersonRole Roles { get; set; } = PersonRole.None;
    public Profession Profession { get; internal set; }
    public PersonDeathReason LastDeathReason { get; private set; } = PersonDeathReason.None;
    public int Strength, Intelligence;
    public Colony Home => _home;

    Colony _home;
    Random _rng = new();

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
    const float AgingTickDivisor = 90f;

    int _doingJob = 0; // csinalni hogy ideig dolgozzon ne instant

    // Idle loitering → wander only after some time doing nothing
    float _idleTimeSeconds = 0f;
    float _loiterThresholdSeconds; // randomized per person
    (int x, int y) _lastPos;

    private Person(Colony home, (int, int) pos, bool newborn, RuntimeNpcBrain brain)
    {
        _home = home;
        _brain = brain;
        Pos = pos;
        _lastPos = pos;
        Strength = _rng.Next(3, 11);
        Intelligence = _rng.Next(3, 11);
        Age = newborn ? 0f : 18f + (float)_rng.NextDouble() * 22f;
        Profession = PickInitialProfession(home);

        // Needs/Emotions baseline-ok
        Needs["Hunger"] = 20f; // 0..100, kisebb = jobb (jóllakott)
        Emotions["Happy"] = 0f;
        Emotions["Hope"] = 0f;

        _loiterThresholdSeconds = 2.5f + (float)_rng.NextDouble() * 2.5f; // 2.5..5.0s
    }

    public static Person Spawn(Colony home, (int, int) pos, RuntimeNpcBrain brain)
        => new Person(home, pos, newborn: false, brain);

    public static Person SpawnWithBonus(Colony home, (int, int) pos, World world, RuntimeNpcBrain brain)
    {
        var person = new Person(home, pos, newborn: true, brain);
        person.Strength = Math.Min(20, person.Strength + world.StrengthBonus);
        person.Intelligence = Math.Min(20, person.Intelligence + world.IntelligenceBonus);
        person.Health += world.HealthBonus;
        return person;
    }

    private static Profession PickInitialProfession(Colony colony)
    {
        var roll = Random.Shared.NextDouble();
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

        float hunger = Needs.GetValueOrDefault("Hunger", 0f);

        // Critical hunger preemption happens before starvation damage to avoid dying with available food.
        if (hunger >= 78f && _home.Stock[Resource.Food] > 0)
        {
            if (hunger >= 96f)
            {
                _home.Stock[Resource.Food] -= 1;
                Needs["Hunger"] = Math.Max(0f, hunger - 28f);
                Stamina = Math.Clamp(Stamina + 8f, 0f, 100f);
                Health = Math.Min(100f + w.HealthBonus, Health + 0.8f);
                Current = Job.Idle;
                _doingJob = 0;
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
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
            // Last-chance starvation rescue: consume food if available before declaring death.
            if (hunger >= 85f && _home.Stock[Resource.Food] > 0)
            {
                _home.Stock[Resource.Food] -= 1;
                Needs["Hunger"] = Math.Max(0f, hunger - 30f);
                Health = Math.Max(1f, Health + 1.2f);
                Current = Job.Idle;
                _doingJob = 0;
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            LastDeathReason = hunger >= 85f ? PersonDeathReason.Starvation : PersonDeathReason.Other;
            return false;
        }

        if (Age < 16f)
        {
            if (hunger >= 68f && _home.Stock[Resource.Food] > 0)
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
            births.Add(Person.SpawnWithBonus(_home, Pos, w, w.CreateNpcBrain(_home)));
        }

        int reserveBonus = _home.FoodReserveBonus;
        float foodPerPerson = colonyFoodStock / (float)Math.Max(1, colonyPop);
        bool emergencyFood = colonyFoodStock <= Math.Max(5 + reserveBonus, colonyPop + 1 + reserveBonus);
        bool veryLowFood = colonyFoodStock <= Math.Max(2 + reserveBonus / 2, colonyPop / 2 + reserveBonus / 2);
        bool abundantFood = foodPerPerson >= 20f;

        if (_doingJob > 0 && Current != Job.Idle)
        {
            // working → not idle
            _idleTimeSeconds = 0f;

            _doingJob--;
            if (_doingJob <= 0)
            {
                switch (Current)
                {
                    case Job.GatherWood:
                        if (w.TryHarvest(Pos, Resource.Wood, 1))
                            _home.Stock[Resource.Wood] += GetGatherAmount(Resource.Wood, w.WoodYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherStone:
                        if (w.TryHarvest(Pos, Resource.Stone, 1))
                            _home.Stock[Resource.Stone] += GetGatherAmount(Resource.Stone, w.StoneYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherIron:
                        if (w.TryHarvest(Pos, Resource.Iron, 1))
                            _home.Stock[Resource.Iron] += GetGatherAmount(Resource.Iron, w.IronYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.GatherGold:
                        if (w.TryHarvest(Pos, Resource.Gold, 1))
                            _home.Stock[Resource.Gold] += GetGatherAmount(Resource.Gold, w.GoldYield);
                        else
                            Wander(w);
                        TryConsumeToolCharge();
                        break;

                    case Job.BuildHouse:
                        // Szükséges házak száma, de legalább annyi, mint a már meglévő házak
                        int maxHouses = Math.Max(_home.HouseCount, (int)Math.Ceiling((colonyPop + 3) / (double)w.HouseCapacity));
                        if (_home.HouseCount < maxHouses)
                        {
                            if (w.StoneBuildingsEnabled && _home.CanBuildWithStone && _home.Stock[Resource.Stone] >= _home.HouseStoneCost)
                            {
                                _home.Stock[Resource.Stone] -= _home.HouseStoneCost;
                                _home.HouseCount++;
                                w.AddHouse(_home, Pos);
                            }
                            else if (_home.Stock[Resource.Wood] >= _home.HouseWoodCost)
                            {
                                _home.Stock[Resource.Wood] -= _home.HouseWoodCost;
                                _home.HouseCount++;
                                w.AddHouse(_home, Pos);
                            }
                        }
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

                    case Job.GatherFood:
                        if (w.TryHarvest(Pos, Resource.Food, 1))
                            _home.Stock[Resource.Food] += GetGatherAmount(Resource.Food, w.FoodYield);
                        else if (veryLowFood && TryHuntNearbyHerbivore(w, 1))
                            _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(Resource.Food, w.FoodYield));
                        else
                            Wander(w);
                        break;

                    case Job.EatFood:
                        if (_home.Stock[Resource.Food] > 0)
                        {
                            _home.Stock[Resource.Food] -= 1;
                            Needs["Hunger"] = Math.Max(0f, Needs.GetValueOrDefault("Hunger", 30f) - 35f);
                            Stamina = Math.Clamp(Stamina + 12f, 0f, 100f);
                            Health = Math.Min(100f + w.HealthBonus, Health + 1.5f);
                        }
                        break;

                    case Job.Rest:
                        float restGain = HasNearbyOwnHouse(w, radius: 2) ? 30f : 22f;
                        Stamina = Math.Clamp(Stamina + restGain, 0f, 100f);
                        break;

                    case Job.Fight:
                    case Job.Flee:
                    case Job.GuardColony:
                    case Job.PatrolBorder:
                        // Pre-Phase0 placeholder: concrete combat behavior is owned by Track B combat primitives.
                        break;
                }

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

            if (_home.Stock[Resource.Food] > 0 && localHunger >= 86f)
            {
                Current = Job.EatFood;
                _doingJob = 1;
                _idleTimeSeconds = 0f;
                _lastPos = Pos;
                return true;
            }

            if (abundantFood && localHunger < 58f)
                veryLowFood = false;

            if (localHunger >= eatNowThreshold && _home.Stock[Resource.Food] > 0)
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
                    _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(Resource.Food, w.FoodYield + 1));
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
            if (TryMoveTowardsNearestResource(w, searchRadius: 2, Resource.Wood, Resource.Stone, Resource.Iron, Resource.Gold))
            {
                _idleTimeSeconds = 0f; // movement → not idle
                _lastPos = Pos;
                return true;
            }

            // 3) Utility-goal fallback → pick a job (e.g., BuildHouse if feasible)
            var next = _brain.Think(this, w, dt);
            if (next != Job.Idle)
            {
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
                    Job.Fight       => 1,
                    Job.Flee        => 1,
                    Job.GuardColony => 1,
                    Job.PatrolBorder => 1,
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

        float effectiveSpeed = Math.Max(0.2f, w.WorkEfficiencyMultiplier * _home.ColonyWorkMultiplier * staminaFactor * professionSpeed * toolSpeed);
        return Math.Max(1, (int)MathF.Ceiling(baseTicks / effectiveSpeed));
    }

    int GetGatherAmount(Resource res, int baseYield)
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

        return Math.Max(1, (int)MathF.Round(baseYield * colonyMultiplier * professionMultiplier));
    }

    bool TryProfessionDirectedAction(World w, bool veryLowFood)
    {
        switch (Profession)
        {
            case Profession.Builder:
                if (_home.ToolCharges <= 2 && _home.Stock[Resource.Iron] >= 2 && _home.Stock[Resource.Wood] >= 2)
                {
                    Current = Job.CraftTools;
                    _doingJob = ComputeTicks(CraftToolsTime, w, isHeavyWork: true);
                    return true;
                }

                if (_home.Stock[Resource.Wood] >= _home.HouseWoodCost / 2)
                {
                    Current = Job.BuildHouse;
                    _doingJob = ComputeTicks(BuildHouseTime, w, isHeavyWork: true);
                    return true;
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
                    _home.Stock[Resource.Food] += Math.Max(1, GetGatherAmount(Resource.Food, w.FoodYield + 1));
                    return true;
                }
                break;
        }

        return false;
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
        if (Health <= 0f)
        {
            LastDeathReason = source.Contains("Predator", StringComparison.OrdinalIgnoreCase)
                ? PersonDeathReason.Predator
                : PersonDeathReason.Other;
        }
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

                    if (md < bestDist)
                    {
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
        MoveTowards(w, bestPos.Value, (int)_home.MovementSpeedMultiplier);
        return true;
    }

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
        int remaining = Math.Max(1, maxStep);
        int cx = Pos.x, cy = Pos.y;

        while (remaining-- > 0 && (cx != target.x || cy != target.y))
        {
            int dx = target.x - cx;
            int dy = target.y - cy;

            int nx = cx, ny = cy;
            if (Math.Abs(dx) >= Math.Abs(dy))
                nx += Math.Sign(dx);
            else
                ny += Math.Sign(dy);

            nx = Math.Clamp(nx, 0, w.Width - 1);
            ny = Math.Clamp(ny, 0, w.Height - 1);

            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                cx = nx;
                cy = ny;
            }
            else
            {
                break;
            }
        }

        Pos = (cx, cy);
    }

    void Wander(World w)
    {
        int moveDistance = (int)_home.MovementSpeedMultiplier;
        int tries = 8;
        for (int i = 0; i < tries; i++)
        {
            int nx = Math.Clamp(Pos.x + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Width - 1);
            int ny = Math.Clamp(Pos.y + _rng.Next(-moveDistance, moveDistance + 1), 0, w.Height - 1);
            if (w.GetTile(nx, ny).Ground != Ground.Water)
            {
                Pos = (nx, ny);
                return;
            }
        }
        // stay if all around is water
    }
}
