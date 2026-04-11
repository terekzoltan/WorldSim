using System;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSim.Runtime;
using WorldSim.Simulation;

namespace WorldSim.RefineryAdapter.Translation;

public sealed class PatchCommandTranslator
{
    private static readonly string[] SupportedTreatyKinds =
    {
        "ceasefire",
        "peace_talks"
    };

    private static readonly int MaxFactionId = Enum.GetValues(typeof(Faction)).Length - 1;

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
                    var effects = BuildDomainModifierSpecs(addStoryBeat.Effects, "addStoryBeat");

                    DirectorCausalChainSpec? causalChain = null;
                    if (addStoryBeat.CausalChain != null)
                    {
                        causalChain = BuildCausalChainSpec(addStoryBeat.CausalChain, addStoryBeat.BeatId);
                    }

                    ValidateSeverityTier(addStoryBeat.Severity, effects.Count);

                    commands.Add(new ApplyStoryBeatRuntimeCommand(
                        addStoryBeat.BeatId,
                        addStoryBeat.Text,
                        addStoryBeat.DurationTicks,
                        effects,
                        causalChain
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
                case DeclareWarOp declareWar:
                    var attacker = MapFactionId(declareWar.AttackerFactionId, "declareWar.attackerFactionId");
                    var defender = MapFactionId(declareWar.DefenderFactionId, "declareWar.defenderFactionId");
                    if (attacker == defender)
                    {
                        throw new InvalidOperationException(
                            "declareWar requires attackerFactionId != defenderFactionId.");
                    }

                    commands.Add(new DeclareWarRuntimeCommand(attacker, defender, declareWar.Reason));
                    break;
                case ProposeTreatyOp proposeTreaty:
                    var proposer = MapFactionId(proposeTreaty.ProposerFactionId, "proposeTreaty.proposerFactionId");
                    var receiver = MapFactionId(proposeTreaty.ReceiverFactionId, "proposeTreaty.receiverFactionId");
                    if (proposer == receiver)
                    {
                        throw new InvalidOperationException(
                            "proposeTreaty requires proposerFactionId != receiverFactionId.");
                    }

                    var treatyKind = NormalizeTreatyKind(proposeTreaty.TreatyKind);
                    commands.Add(new ProposeTreatyRuntimeCommand(proposer, receiver, treatyKind, proposeTreaty.Note));
                    break;
                default:
                    throw new NotSupportedException(
                        $"Adapter supports addTech/addStoryBeat/setColonyDirective/declareWar/proposeTreaty only. Unsupported op: {op.GetType().Name}"
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

    private static List<DirectorDomainModifierSpec> BuildDomainModifierSpecs(
        IReadOnlyList<EffectEntry>? effects,
        string context)
    {
        var specs = new List<DirectorDomainModifierSpec>();
        if (effects == null)
            return specs;

        foreach (var effect in effects)
        {
            if (!string.Equals(effect.Type, "domain_modifier", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported effect type '{effect.Type}' in {context}.");
            }

            if (effect.DurationTicks <= 0)
            {
                throw new InvalidOperationException($"Effect durationTicks must be > 0 in {context}.");
            }

            specs.Add(new DirectorDomainModifierSpec(effect.Domain, effect.Modifier, effect.DurationTicks));
        }

        return specs;
    }

    private static DirectorCausalChainSpec BuildCausalChainSpec(CausalChainEntry chain, string parentBeatId)
    {
        if (!string.Equals(chain.Type, "causal_chain", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported causalChain type '{chain.Type}' in addStoryBeat '{parentBeatId}'."
            );
        }

        var followUpEffects = BuildDomainModifierSpecs(chain.FollowUpBeat.Effects, "causalChain.followUpBeat");
        ValidateSeverityTier(chain.FollowUpBeat.Severity, followUpEffects.Count);

        return new DirectorCausalChainSpec(
            new DirectorCausalConditionSpec(
                chain.Condition.Metric,
                chain.Condition.Operator,
                chain.Condition.Threshold),
            new DirectorFollowUpBeatSpec(
                chain.FollowUpBeat.BeatId,
                chain.FollowUpBeat.Text,
                chain.FollowUpBeat.DurationTicks,
                followUpEffects),
            chain.WindowTicks,
            chain.MaxTriggers);
    }

    private static Faction MapFactionId(int factionId, string fieldName)
    {
        if (factionId < 0 || factionId > MaxFactionId)
        {
            throw new InvalidOperationException(
                $"{fieldName} out of range: {factionId} (current valid faction ids: 0..{MaxFactionId}).");
        }

        return (Faction)factionId;
    }

    private static string NormalizeTreatyKind(string treatyKindRaw)
    {
        var treatyKind = treatyKindRaw?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(treatyKind))
        {
            throw new InvalidOperationException(
                "proposeTreaty.treatyKind is required. Expected one of: ceasefire, peace_talks.");
        }

        if (Array.IndexOf(SupportedTreatyKinds, treatyKind) < 0)
        {
            throw new InvalidOperationException(
                $"Unsupported proposeTreaty.treatyKind '{treatyKindRaw}'. Expected one of: ceasefire, peace_talks.");
        }

        return treatyKind;
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
                    runtime.ValidateStoryBeat(storyBeat.BeatId, storyBeat.Text, storyBeat.DurationTicks, storyBeat.Effects, storyBeat.CausalChain);
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
                        virtualMajorCooldown = Math.Max(virtualMajorCooldown, DirectorState.MajorBeatCooldownTicks);
                    else if (severity == DirectorBeatSeverity.Epic)
                        virtualEpicCooldown = Math.Max(virtualEpicCooldown, DirectorState.EpicBeatCooldownTicks);
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
                case DeclareWarRuntimeCommand declareWar:
                    runtime.ValidateDeclareWar(declareWar.Attacker, declareWar.Defender);
                    break;
                case ProposeTreatyRuntimeCommand treaty:
                    runtime.ValidateProposeTreaty(treaty.Proposer, treaty.Receiver, treaty.TreatyKind);
                    break;
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
                    runtime.ApplyStoryBeat(storyBeat.BeatId, storyBeat.Text, storyBeat.DurationTicks, storyBeat.Effects, storyBeat.CausalChain);
                    break;
                case ApplyColonyDirectiveRuntimeCommand directive:
                    runtime.ApplyColonyDirective(
                        directive.ColonyId,
                        directive.Directive,
                        directive.DurationTicks,
                        directive.Biases
                    );
                    break;
                case DeclareWarRuntimeCommand declareWar:
                    runtime.DeclareWar(declareWar.Attacker, declareWar.Defender, declareWar.Reason);
                    break;
                case ProposeTreatyRuntimeCommand treaty:
                    runtime.ProposeTreaty(treaty.Proposer, treaty.Receiver, treaty.TreatyKind, treaty.Note);
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
