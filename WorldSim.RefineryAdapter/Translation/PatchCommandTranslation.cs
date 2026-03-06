using System;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Translation;

public sealed class PatchCommandTranslator
{
    public IReadOnlyList<RuntimePatchCommand> Translate(PatchResponse response)
    {
        var commands = new List<RuntimePatchCommand>(response.Patch.Count);

        foreach (var op in response.Patch)
        {
            switch (op)
            {
                case AddTechOp addTech:
                    commands.Add(new UnlockTechRuntimeCommand(addTech.TechId));
                    break;
                case AddStoryBeatOp addStoryBeat:
                    var effects = new List<DirectorDomainModifierSpec>();
                    if (addStoryBeat.Effects != null)
                    {
                        foreach (var effect in addStoryBeat.Effects)
                        {
                            if (!string.Equals(effect.Type, "domain_modifier", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException($"Unsupported effect type '{effect.Type}' in addStoryBeat.");
                            if (effect.DurationTicks <= 0)
                                throw new InvalidOperationException("Effect durationTicks must be > 0 in addStoryBeat.");
                            effects.Add(new DirectorDomainModifierSpec(effect.Domain, effect.Modifier, effect.DurationTicks));
                        }
                    }

                    commands.Add(new ApplyStoryBeatRuntimeCommand(
                        addStoryBeat.BeatId,
                        addStoryBeat.Text,
                        addStoryBeat.DurationTicks,
                        effects
                    ));
                    break;
                case SetColonyDirectiveOp setColonyDirective:
                    var biases = new List<DirectorGoalBiasSpec>();
                    if (setColonyDirective.Biases != null)
                    {
                        foreach (var bias in setColonyDirective.Biases)
                        {
                            if (!string.Equals(bias.Type, "goal_bias", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException($"Unsupported bias type '{bias.Type}' in setColonyDirective.");
                            biases.Add(new DirectorGoalBiasSpec(bias.GoalCategory, bias.Weight, bias.DurationTicks));
                        }
                    }

                    commands.Add(new ApplyColonyDirectiveRuntimeCommand(
                        setColonyDirective.ColonyId,
                        setColonyDirective.Directive,
                        setColonyDirective.DurationTicks,
                        biases
                    ));
                    break;
                default:
                    throw new NotSupportedException(
                        $"Adapter supports addTech/addStoryBeat/setColonyDirective only. Unsupported op: {op.GetType().Name}"
                    );
            }
        }

        return commands;
    }
}

public sealed class RuntimePatchCommandExecutor
{
    public void Execute(SimulationRuntime runtime, IReadOnlyList<RuntimePatchCommand> commands)
    {
        foreach (var command in commands)
        {
            switch (command)
            {
                case UnlockTechRuntimeCommand unlockTech:
                    if (!runtime.IsKnownTech(unlockTech.TechId))
                    {
                        throw new InvalidOperationException(
                            "Cannot apply addTech: unknown techId '" + unlockTech.TechId +
                            "'. loadedTechCount=" + runtime.LoadedTechCount +
                            ". TODO: add Java->C# tech ID mapping layer for cross-project IDs."
                        );
                    }

                    runtime.UnlockTechForPrimaryColony(unlockTech.TechId);
                    break;
                case ApplyStoryBeatRuntimeCommand storyBeat:
                    runtime.ApplyStoryBeat(storyBeat.BeatId, storyBeat.Text, storyBeat.DurationTicks, storyBeat.Effects);
                    break;
                case ApplyColonyDirectiveRuntimeCommand directive:
                    runtime.ApplyColonyDirective(
                        directive.ColonyId,
                        directive.Directive,
                        directive.DurationTicks,
                        directive.Biases
                    );
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported runtime patch command: {command.GetType().Name}"
                    );
            }
        }
    }
}
