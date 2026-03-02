using System;
using System.Linq;
using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed class RuntimeNpcBrain
{
    private const int ThreatSenseRadius = 4;
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
        var nearbyPredators = world._animals.Count(animal =>
            animal is Predator predator
            && predator.IsAlive
            && Manhattan(actor.Pos, predator.Pos) <= ThreatSenseRadius);
        var nearbyHostiles = world._people.Count(other =>
            other != actor
            && other.Health > 0f
            && other.Home != actor.Home
            && Manhattan(actor.Pos, other.Pos) <= ThreatSenseRadius);

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
            NearbyHostilePeople: nearbyHostiles);
    }

    private static int Manhattan((int x, int y) a, (int x, int y) b)
        => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
}
