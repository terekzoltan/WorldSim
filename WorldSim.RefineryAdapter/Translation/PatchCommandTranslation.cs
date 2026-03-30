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

                    ValidateSeverityTier(addStoryBeat.Severity, effects.Count);

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

    private static void ValidateSeverityTier(string? declaredSeverityRaw, int effectCount)
    {
        if (effectCount > 3)
        {
            throw new InvalidOperationException(
                $"Story beat effect count {effectCount} exceeds S3-A limit (max 3 effects)."
            );
        }

        var inferredSeverity = effectCount switch
        {
            0 => "minor",
            <= 2 => "major",
            _ => "epic"
        };

        if (string.IsNullOrWhiteSpace(declaredSeverityRaw))
            return;

        var declaredSeverity = declaredSeverityRaw.Trim().ToLowerInvariant();
        if (declaredSeverity is not ("minor" or "major" or "epic"))
        {
            throw new InvalidOperationException(
                $"Unsupported story beat severity '{declaredSeverityRaw}'. Expected one of: minor, major, epic."
            );
        }

        if (!string.Equals(declaredSeverity, inferredSeverity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Story beat severity mismatch: declared '{declaredSeverity}', inferred '{inferredSeverity}' from effect count {effectCount}."
            );
        }
    }
}

public sealed class RuntimePatchCommandExecutor
{
    public void ValidateDirectorBatch(SimulationRuntime runtime, IReadOnlyList<RuntimePatchCommand> commands)
    {
        var snapshot = runtime.GetSnapshot().Director;
        var activeBeats = new HashSet<string>(
            snapshot.ActiveBeats.Select(beat => beat.BeatId),
            StringComparer.Ordinal);
        var virtualMajorCooldown = snapshot.MajorBeatCooldownRemainingTicks;
        var virtualEpicCooldown = snapshot.EpicBeatCooldownRemainingTicks;

        foreach (var command in commands)
        {
            switch (command)
            {
                case ApplyStoryBeatRuntimeCommand storyBeat:
                    runtime.ValidateStoryBeat(storyBeat.BeatId, storyBeat.Text, storyBeat.DurationTicks, storyBeat.Effects);
                    if (activeBeats.Contains(storyBeat.BeatId))
                        continue;

                    var severity = InferStorySeverity(storyBeat.Effects.Count);
                    if (severity == DirectorBeatSeverity.Major && virtualMajorCooldown > 0)
                    {
                        throw new InvalidOperationException(
                            $"Cannot apply story beat '{storyBeat.BeatId}': Major beat cooldown active ({virtualMajorCooldown} ticks)"
                        );
                    }

                    if (severity == DirectorBeatSeverity.Epic && virtualEpicCooldown > 0)
                    {
                        throw new InvalidOperationException(
                            $"Cannot apply story beat '{storyBeat.BeatId}': Epic beat cooldown active ({virtualEpicCooldown} ticks)"
                        );
                    }

                    activeBeats.Add(storyBeat.BeatId);
                    if (severity == DirectorBeatSeverity.Major)
                        virtualMajorCooldown = Math.Max(virtualMajorCooldown, 20);
                    else if (severity == DirectorBeatSeverity.Epic)
                        virtualEpicCooldown = Math.Max(virtualEpicCooldown, 40);
                    break;
                case ApplyColonyDirectiveRuntimeCommand directive:
                    runtime.ValidateColonyDirective(
                        directive.ColonyId,
                        directive.Directive,
                        directive.DurationTicks,
                        directive.Biases);
                    break;
                case UnlockTechRuntimeCommand:
                    throw new InvalidOperationException(
                        "Director batch validation received non-director command 'UnlockTechRuntimeCommand'."
                    );
                default:
                    throw new NotSupportedException(
                        $"Unsupported runtime patch command: {command.GetType().Name}"
                    );
            }
        }
    }

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

    private static DirectorBeatSeverity InferStorySeverity(int effectCount)
    {
        if (effectCount < 0)
            throw new InvalidOperationException($"Cannot infer beat severity: invalid effect count {effectCount}.");
        if (effectCount > 3)
            throw new InvalidOperationException($"Cannot apply story beat: effect count {effectCount} exceeds S3-A cap (max 3).");

        return effectCount switch
        {
            0 => DirectorBeatSeverity.Minor,
            <= 2 => DirectorBeatSeverity.Major,
            _ => DirectorBeatSeverity.Epic
        };
    }
}
