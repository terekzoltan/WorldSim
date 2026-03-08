using System;
using System.Collections.Generic;
using System.Linq;
using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed class RuntimeNpcBrain
{
    private const int ThreatSenseRadius = 4;
    private const int ContestedSenseRadius = 2;
    private readonly INpcDecisionBrain _brain;
    private float _simulationTimeSeconds;
    private long _decisionSequence;

    public NpcPlannerMode PlannerMode { get; }
    public string PolicyName { get; }
    public RuntimeAiDecision? LastDecision { get; private set; }

    public RuntimeNpcBrain(NpcPlannerMode plannerMode, string policyName)
    {
        PlannerMode = plannerMode;
        PolicyName = policyName;

        IPlanner planner = plannerMode switch
        {
            NpcPlannerMode.Simple => new SimplePlanner(),
            NpcPlannerMode.Htn => new HtnPlanner(),
            _ => new GoapPlanner()
        };

        _brain = new UtilityGoapBrain(planner, GoalLibrary.CreateDefaultGoals(), policyName);
    }

    public RuntimeNpcBrain(NpcPlannerMode plannerMode)
        : this(plannerMode, $"Global:{plannerMode}")
    {
    }

    public RuntimeNpcBrain(INpcDecisionBrain? brain = null)
    {
        PlannerMode = NpcPlannerMode.Goap;
        PolicyName = "Custom";
        _brain = brain ?? new UtilityGoapBrain(new GoapPlanner(), GoalLibrary.CreateDefaultGoals(), PolicyName);
    }

    public Job Think(Person actor, World world, float dt)
    {
        _simulationTimeSeconds += Math.Max(0f, dt);
        var context = BuildContext(actor, world);
        var decision = _brain.Think(context);
        return RecordDecision(actor, decision.Command, decision.Trace);
    }

    private Job RecordDecision(Person actor, NpcCommand command, AiDecisionTrace trace)
    {
        var job = command switch
        {
            NpcCommand.GatherWood => Job.GatherWood,
            NpcCommand.GatherStone => Job.GatherStone,
            NpcCommand.GatherIron => Job.GatherIron,
            NpcCommand.GatherGold => Job.GatherGold,
            NpcCommand.GatherFood => Job.GatherFood,
            NpcCommand.EatFood => Job.EatFood,
            NpcCommand.Rest => Job.Rest,
            NpcCommand.BuildHouse => Job.BuildHouse,
            NpcCommand.CraftTools => Job.CraftTools,
            NpcCommand.BuildWall => Job.BuildWall,
            NpcCommand.BuildWatchtower => Job.BuildWatchtower,
            NpcCommand.RaidBorder => Job.RaidBorder,
            NpcCommand.AttackStructure => Job.AttackStructure,
            NpcCommand.Fight => Job.Fight,
            NpcCommand.Flee => Job.Flee,
            _ => Job.Idle
        };

        _decisionSequence++;
        LastDecision = new RuntimeAiDecision(
            Sequence: _decisionSequence,
            ColonyId: actor.Home.Id,
            X: actor.Pos.x,
            Y: actor.Pos.y,
            Command: command,
            Job: job,
            Trace: trace);

        return job;
    }

    private NpcAiContext BuildContext(Person actor, World world)
    {
        var hunger = actor.Needs.TryGetValue("Hunger", out var value) ? value : 20f;
        var colonyPopulation = world._people.Count(person => person.Home == actor.Home && person.Health > 0f);
        var colonyId = actor.Home.Id;
        var warState = world.GetColonyWarState(colonyId);
        var isWarStance = warState == ColonyWarState.War;
        var isHostileStance = warState == ColonyWarState.Tense || isWarStance;
        var isContestedTile = world.IsTileContested(actor.Pos.x, actor.Pos.y);
        var hasContestedTilesNearby = HasContestedTilesNearby(world, actor.Pos, ContestedSenseRadius);
        var nearbyPredators = world._animals.Count(animal =>
            animal is Predator predator
            && predator.IsAlive
            && Manhattan(actor.Pos, predator.Pos) <= ThreatSenseRadius);
        var nearbyHostiles = world._people.Count(other =>
            other != actor
            && other.Health > 0f
            && IsEnemyFaction(world, actor, other)
            && Manhattan(actor.Pos, other.Pos) <= ThreatSenseRadius);
        var nearbyEnemies = nearbyHostiles;
        var hostileProximityScore = Math.Clamp(nearbyEnemies / 4f, 0f, 1f);
        var resourceCrowdPressure = ComputeResourceCrowdPressure(world, actor.Pos, radius: 5);
        var buildCrowdPressure = ComputeBuildCrowdPressure(world, actor.Home.Origin, actor.Home.Id);
        var retreatCrowdPressure = ComputeRetreatCrowdPressure(world, actor.Home.Origin, actor.Home.Id);
        var localThreatScore = ComputeThreatScore(
            nearbyPredators,
            nearbyHostiles,
            nearbyEnemies,
            isContestedTile,
            hasContestedTilesNearby,
            isHostileStance,
            isWarStance);
        var isWarriorRole = IsWarriorRole(world, actor, colonyId, isHostileStance);

        return new NpcAiContext(
            SimulationTimeSeconds: _simulationTimeSeconds,
            Hunger: hunger,
            Stamina: actor.Stamina,
            HomeWood: actor.Home.Stock[Resource.Wood],
            HomeStone: actor.Home.Stock[Resource.Stone],
            HomeIron: actor.Home.Stock[Resource.Iron],
            HomeGold: actor.Home.Stock[Resource.Gold],
            HomeFood: actor.Home.Stock[Resource.Food],
            HomeHouseCount: actor.Home.HouseCount,
            HouseWoodCost: actor.Home.HouseWoodCost,
            ColonyPopulation: colonyPopulation,
            HouseCapacity: world.HouseCapacity,
            StoneBuildingsEnabled: world.StoneBuildingsEnabled,
            CanBuildWithStone: actor.Home.CanBuildWithStone,
            HouseStoneCost: actor.Home.HouseStoneCost,
            Health: Math.Clamp(actor.Health, 0f, 1000f),
            Strength: Math.Max(0, actor.Strength),
            Defense: Math.Max(0, actor.Defense),
            NearbyPredators: nearbyPredators,
            NearbyHostilePeople: nearbyHostiles,
            BiasFarming: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Farming),
            BiasGathering: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Gathering),
            BiasBuilding: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Building),
            BiasCrafting: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Crafting),
            BiasRest: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Rest),
            BiasSocial: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Social),
            BiasMilitary: (float)world.GetEffectiveGoalBias(colonyId, GoalBiasCategories.Military),
            IsWarStance: isWarStance,
            IsHostileStance: isHostileStance,
            IsContestedTile: isContestedTile,
            HasContestedTilesNearby: hasContestedTilesNearby,
            IsWarriorRole: isWarriorRole,
            NearbyEnemyCount: nearbyEnemies,
            HostileProximityScore: hostileProximityScore,
            LocalThreatScore: localThreatScore,
            ResourceCrowdPressure: resourceCrowdPressure,
            BuildCrowdPressure: buildCrowdPressure,
            RetreatCrowdPressure: retreatCrowdPressure);
    }

    private static int Manhattan((int x, int y) a, (int x, int y) b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    private static bool IsEnemyFaction(World world, Person actor, Person other)
    {
        if (other.Home == actor.Home)
            return false;

        var stance = world.GetFactionStance(actor.Home.Faction, other.Home.Faction);
        return stance >= WorldSim.Simulation.Diplomacy.Stance.Hostile;
    }

    private static bool HasContestedTilesNearby(World world, (int x, int y) pos, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                var x = pos.x + dx;
                var y = pos.y + dy;
                if (world.IsTileContested(x, y))
                    return true;
            }
        }

        return false;
    }

    private static float ComputeThreatScore(
        int nearbyPredators,
        int nearbyHostiles,
        int nearbyEnemies,
        bool isContestedTile,
        bool hasContestedTilesNearby,
        bool isHostileStance,
        bool isWarStance)
    {
        var score = 0f;
        score += Math.Clamp(nearbyPredators / 3f, 0f, 1f) * 0.45f;
        score += Math.Clamp(nearbyHostiles / 4f, 0f, 1f) * 0.55f;
        score += Math.Clamp(nearbyEnemies / 4f, 0f, 1f) * 0.35f;
        if (hasContestedTilesNearby)
            score += 0.15f;
        if (isContestedTile)
            score += 0.2f;
        if (isHostileStance)
            score += 0.1f;
        if (isWarStance)
            score += 0.15f;
        return Math.Clamp(score, 0f, 1f);
    }

    private static float ComputeResourceCrowdPressure(World world, (int x, int y) pos, int radius)
    {
        var reservations = new List<int>(16);
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var x = pos.x + dx;
                var y = pos.y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                    continue;

                var node = world.GetTile(x, y).Node;
                if (node == null || node.Amount <= 0)
                    continue;

                var key = $"resource:{node.Type}:{x}:{y}";
                reservations.Add(world.GetSoftReservationCount(key));
            }
        }

        if (reservations.Count == 0)
            return 0f;

        reservations.Sort();
        int sample = Math.Min(4, reservations.Count);
        float avg = 0f;
        for (int i = 0; i < sample; i++)
            avg += reservations[i];
        avg /= sample;
        return Math.Clamp(avg / 4f, 0f, 1f);
    }

    private static float ComputeBuildCrowdPressure(World world, (int x, int y) origin, int colonyId)
    {
        var reservations = new List<int>(24);
        for (int radius = 2; radius <= 6; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                        continue;

                    var x = origin.x + dx;
                    var y = origin.y + dy;
                    if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                        continue;
                    if (world.GetTile(x, y).Ground == Ground.Water)
                        continue;
                    if (world.IsMovementBlocked(x, y, colonyId))
                        continue;

                    reservations.Add(world.GetSoftReservationCount($"build:{x}:{y}"));
                }
            }
        }

        if (reservations.Count == 0)
            return 0f;

        reservations.Sort();
        int sample = Math.Min(6, reservations.Count);
        float avg = 0f;
        for (int i = 0; i < sample; i++)
            avg += reservations[i];
        avg /= sample;
        return Math.Clamp(avg / 4f, 0f, 1f);
    }

    private static float ComputeRetreatCrowdPressure(World world, (int x, int y) origin, int colonyId)
    {
        var reservations = new List<int>(24);
        for (int radius = 2; radius <= 8; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) != radius)
                        continue;

                    var x = origin.x + dx;
                    var y = origin.y + dy;
                    if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                        continue;
                    if (world.GetTile(x, y).Ground == Ground.Water)
                        continue;
                    if (world.IsMovementBlocked(x, y, colonyId))
                        continue;

                    reservations.Add(world.GetSoftReservationCount($"retreat:{x}:{y}"));
                }
            }
        }

        if (reservations.Count == 0)
            return 0f;

        reservations.Sort();
        int sample = Math.Min(8, reservations.Count);
        float avg = 0f;
        for (int i = 0; i < sample; i++)
            avg += reservations[i];
        avg /= sample;
        return Math.Clamp(avg / 4f, 0f, 1f);
    }

    private static bool IsWarriorRole(World world, Person actor, int colonyId, bool isHostileStance)
    {
        if (actor.Profession == Profession.Hunter)
            return true;

        if (!isHostileStance)
            return false;

        var warriorCount = world.GetColonyWarriorCount(colonyId);
        if (warriorCount <= 0)
            return false;

        var myPower = actor.Strength + actor.Defense;
        var strongerCount = world._people.Count(other =>
            other.Home == actor.Home
            && other != actor
            && other.Health > 0f
            && (other.Strength + other.Defense) > myPower);
        return strongerCount < warriorCount;
    }
}
