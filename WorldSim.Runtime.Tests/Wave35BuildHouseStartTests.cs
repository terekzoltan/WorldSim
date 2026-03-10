using System;
using System.Linq;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave35BuildHouseStartTests
{
    [Fact]
    public void BuildHouse_DoesNotStart_WhenOnlyHalfWoodCostAvailable()
    {
        var world = CreateBuildHouseForcedWorld(seed: 1201);
        var colony = world._colonies[0];
        var actor = world._people.First(person => person.Home == colony);
        world._people = new System.Collections.Generic.List<Person> { actor };

        actor.Profession = Profession.Builder;
        actor.Needs["Hunger"] = 8f;
        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Stone] = 0;
        colony.Stock[Resource.Wood] = Math.Max(1, colony.HouseWoodCost / 2);

        world.Update(0.25f);

        Assert.NotEqual(Job.BuildHouse, actor.Current);
        Assert.Equal(0, colony.HouseCount);
        Assert.DoesNotContain(world.Houses, house => house.Owner == colony);
    }

    [Fact]
    public void BuildHouse_StartsAndCompletes_WhenFullyAffordableAndHousingNeeded()
    {
        var world = CreateBuildHouseForcedWorld(seed: 1202);
        var colony = world._colonies[0];
        var actor = world._people.First(person => person.Home == colony);
        world._people = new System.Collections.Generic.List<Person> { actor };

        actor.Profession = Profession.Builder;
        actor.Needs["Hunger"] = 8f;
        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Stone] = 0;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost;

        int beforeHouseCount = colony.HouseCount;
        int beforeHouseEntities = world.Houses.Count(house => house.Owner == colony);

        for (int i = 0; i < 48; i++)
            world.Update(0.25f);

        Assert.True(colony.HouseCount > beforeHouseCount);
        Assert.True(world.Houses.Count(house => house.Owner == colony) > beforeHouseEntities);
    }

    [Fact]
    public void BuildHouse_DoesNotStart_WhenHousingIsNotNeeded()
    {
        var world = CreateBuildHouseForcedWorld(seed: 1203);
        var colony = world._colonies[0];
        var actor = world._people.First(person => person.Home == colony);
        world._people = new System.Collections.Generic.List<Person> { actor };

        colony.HouseCount = 20;
        colony.Stock[Resource.Food] = 200;
        colony.Stock[Resource.Wood] = colony.HouseWoodCost * 2;
        actor.Needs["Hunger"] = 8f;

        world.Update(0.25f);

        Assert.NotEqual(Job.BuildHouse, actor.Current);
        Assert.Equal(20, colony.HouseCount);
    }

    private static World CreateBuildHouseForcedWorld(int seed)
    {
        return new World(
            width: 24,
            height: 20,
            initialPop: 12,
            brainFactory: _ => new RuntimeNpcBrain(new FixedCommandBrain(NpcCommand.BuildHouse)),
            randomSeed: seed)
        {
            EnableCombatPrimitives = false,
            EnableDiplomacy = false,
            StoneBuildingsEnabled = false
        };
    }

    private sealed class FixedCommandBrain : INpcDecisionBrain
    {
        private readonly NpcCommand _command;

        public FixedCommandBrain(NpcCommand command)
        {
            _command = command;
        }

        public AiDecisionResult Think(in NpcAiContext context)
        {
            var trace = new AiDecisionTrace(
                SelectedGoal: "Fixed",
                PlannerName: "Fixed",
                PolicyName: "Fixed",
                PlanLength: 1,
                PlanPreview: new[] { _command },
                PlanCost: 1,
                ReplanReason: "Fixed",
                MethodName: "Fixed",
                GoalScores: Array.Empty<GoalScoreEntry>());

            return new AiDecisionResult(_command, trace);
        }
    }
}
