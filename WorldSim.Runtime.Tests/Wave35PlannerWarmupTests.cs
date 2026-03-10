using System;
using System.Collections.Generic;
using WorldSim.AI;
using WorldSim.Simulation;
using Xunit;

namespace WorldSim.Runtime.Tests;

public class Wave35PlannerWarmupTests
{
    [Fact]
    public void PeacefulFallback_UsesSingleAiThinkPerTick()
    {
        var countingBrain = new CountingBrain(NpcCommand.Idle);
        var world = new World(
            width: 20,
            height: 20,
            initialPop: 8,
            brainFactory: _ => new RuntimeNpcBrain(countingBrain),
            randomSeed: 351)
        {
            EnableCombatPrimitives = true,
            EnableDiplomacy = true
        };

        world._animals.Clear();
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
                world.GetTile(x, y).ReplaceNode(null);
        }

        var actor = world._people[0];
        actor.Needs["Hunger"] = 18f;
        actor.Profession = Profession.Generalist;

        var births = new List<Person>();
        var ok = actor.Update(world, dt: 0.25f, births);

        Assert.True(ok);
        Assert.Equal(1, countingBrain.Calls);
        Assert.NotNull(actor.LastAiDecision);
        Assert.Equal(1L, actor.LastAiDecision!.Sequence);
    }

    private sealed class CountingBrain : INpcDecisionBrain
    {
        private readonly NpcCommand _command;
        public int Calls { get; private set; }

        public CountingBrain(NpcCommand command)
        {
            _command = command;
        }

        public AiDecisionResult Think(in NpcAiContext context)
        {
            Calls++;
            var trace = new AiDecisionTrace(
                SelectedGoal: "Count",
                PlannerName: "Count",
                PolicyName: "Count",
                PlanLength: 1,
                PlanPreview: new[] { _command },
                PlanCost: 1,
                ReplanReason: "Count",
                MethodName: "Count",
                GoalScores: Array.Empty<GoalScoreEntry>());
            return new AiDecisionResult(_command, trace);
        }
    }
}
