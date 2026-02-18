using System;
using System.Linq;
using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed class RuntimeNpcBrain
{
    private readonly INpcDecisionBrain _brain;
    private float _simulationTimeSeconds;

    public RuntimeNpcBrain(INpcDecisionBrain? brain = null)
    {
        _brain = brain ?? new UtilityGoapBrain(new GoapPlanner(), GoalLibrary.CreateDefaultGoals());
    }

    public Job Think(Person actor, World world, float dt)
    {
        _simulationTimeSeconds += Math.Max(0f, dt);
        var context = BuildContext(actor, world);
        var command = _brain.Think(context);
        return command switch
        {
            NpcCommand.GatherWood => Job.GatherWood,
            NpcCommand.GatherStone => Job.GatherStone,
            NpcCommand.GatherFood => Job.GatherFood,
            NpcCommand.EatFood => Job.EatFood,
            NpcCommand.Rest => Job.Rest,
            NpcCommand.BuildHouse => Job.BuildHouse,
            _ => Job.Idle
        };
    }

    private NpcAiContext BuildContext(Person actor, World world)
    {
        var hunger = actor.Needs.TryGetValue("Hunger", out var value) ? value : 20f;
        var colonyPopulation = world._people.Count(person => person.Home == actor.Home && person.Health > 0f);

        return new NpcAiContext(
            SimulationTimeSeconds: _simulationTimeSeconds,
            Hunger: hunger,
            HomeWood: actor.Home.Stock[Resource.Wood],
            HomeStone: actor.Home.Stock[Resource.Stone],
            HomeFood: actor.Home.Stock[Resource.Food],
            HomeHouseCount: actor.Home.HouseCount,
            HouseWoodCost: actor.Home.HouseWoodCost,
            ColonyPopulation: colonyPopulation,
            HouseCapacity: world.HouseCapacity,
            StoneBuildingsEnabled: world.StoneBuildingsEnabled,
            CanBuildWithStone: actor.Home.CanBuildWithStone,
            HouseStoneCost: actor.Home.HouseStoneCost);
    }
}
