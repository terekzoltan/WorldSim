using System;
using System.Linq;
using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed class RuntimeNpcBrain
{
    private const int ThreatRadius = 4;
    private const int WarCadenceTicks = 10;
    private const int TerritoryCadenceTicks = 10;
    private const int WarriorCadenceTicks = 5;

    private readonly INpcDecisionBrain _brain;
    private float _simulationTimeSeconds;
    private long _decisionSequence;
    private int _contextTick;
    private NpcWarState _cachedWarState = NpcWarState.Peace;
    private bool _cachedTileContestedNearby;
    private int _cachedColonyWarriorCount;

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
        var nearbyPredators = world._animals.Count(animal =>
            animal is Predator predator
            && predator.IsAlive
            && Manhattan(actor.Pos, predator.Pos) <= ThreatRadius);
        var nearbyHostiles = world._people.Count(person =>
            person != actor
            && person.Health > 0f
            && person.Home != actor.Home
            && Manhattan(actor.Pos, person.Pos) <= ThreatRadius);

        _contextTick++;
        if (_contextTick == 1 || _contextTick % WarCadenceTicks == 0)
            _cachedWarState = ResolveWarState(world, nearbyPredators, nearbyHostiles);

        if (_contextTick == 1 || _contextTick % TerritoryCadenceTicks == 0)
            _cachedTileContestedNearby = ResolveTileContestedNearby(actor, world, nearbyHostiles);

        if (_contextTick == 1 || _contextTick % WarriorCadenceTicks == 0)
            _cachedColonyWarriorCount = world._people.Count(person =>
                person.Home == actor.Home
                && person.Health > 0f
                && IsWarrior(person, world));

        return new NpcAiContext(
            SimulationTimeSeconds: Round3(_simulationTimeSeconds),
            Hunger: ClampAndRound(hunger, 0f, 100f),
            Stamina: ClampAndRound(actor.Stamina, 0f, 100f),
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
            Health: ClampAndRound(actor.Health, 0f, 150f),
            Strength: Math.Clamp(actor.Strength, 0, 50),
            Defense: ClampAndRound(actor.Defense, 0f, 100f),
            NearbyPredators: nearbyPredators,
            NearbyHostilePeople: nearbyHostiles,
            WarState: _cachedWarState,
            TileContestedNearby: _cachedTileContestedNearby,
            IsWarrior: IsWarrior(actor, world),
            ColonyWarriorCount: _cachedColonyWarriorCount);
    }

    private static int Manhattan((int x, int y) a, (int x, int y) b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

    private static float ClampAndRound(float value, float min, float max)
        => MathF.Round(Math.Clamp(value, min, max), 3);

    private static float Round3(float value)
        => MathF.Round(value, 3);

    private static bool IsWarrior(Person person, World world)
    {
        if (person.Roles.HasFlag(PersonRole.Warrior))
            return true;

        var hasExplicitRoleAssignments = world._people.Any(p => p.Roles != PersonRole.None);
        if (hasExplicitRoleAssignments)
            return false;

        // Fallback until full role assignment lands (Track B contract v1).
        return person.Profession == Profession.Hunter;
    }

    private static NpcWarState ResolveWarState(World world, int nearbyPredators, int nearbyHostiles)
    {
        if (!world.EnableDiplomacy)
            return NpcWarState.Peace;

        if (world.EnableCombatPrimitives && nearbyHostiles > 0)
            return NpcWarState.War;

        if (nearbyHostiles > 0 || nearbyPredators > 0)
            return NpcWarState.Tense;

        return NpcWarState.Peace;
    }

    private static bool ResolveTileContestedNearby(Person actor, World world, int nearbyHostiles)
    {
        if (!world.EnableDiplomacy || nearbyHostiles <= 0)
            return false;

        // Territory system is not landed yet; hostile overlap is the current runtime contention source.
        return true;
    }
}
