using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;

namespace WorldSimRefineryClient.Apply;

public sealed class PatchApplier
{
    private static readonly string[] SupportedTreatyKinds =
    {
        "ceasefire",
        "peace_talks"
    };

    private const int MinFactionId = 0;
    private const int MaxFactionId = 3;

    public PatchApplyResult Apply(SimulationPatchState state, PatchResponse response, PatchApplyOptions? options = null)
    {
        options ??= new PatchApplyOptions();

        if (!string.Equals(response.SchemaVersion, PatchContract.SchemaVersion, StringComparison.Ordinal))
        {
            throw new PatchApplyException($"Unsupported schemaVersion '{response.SchemaVersion}'.");
        }

        var applied = 0;
        var deduped = 0;
        var noOp = 0;

        foreach (var op in response.Patch)
        {
            if (string.IsNullOrWhiteSpace(op.OpId))
            {
                throw new PatchApplyException("Patch operation is missing required opId.");
            }

            if (!state.AppliedOpIds.Add(op.OpId))
            {
                deduped++;
                continue;
            }

            var changed = false;
            switch (op)
            {
                case AddTechOp addTech:
                    changed = state.TechIds.Add(addTech.TechId);
                    break;
                case TweakTechOp tweakTech:
                    if (!state.TechIds.Contains(tweakTech.TechId))
                    {
                        throw new PatchApplyException($"tweakTech requires existing tech '{tweakTech.TechId}'.");
                    }
                    changed = true;
                    break;
                case AddWorldEventOp addWorldEvent:
                    changed = state.EventIds.Add(addWorldEvent.EventId);
                    break;
                case AddStoryBeatOp addStoryBeat:
                    changed = state.StoryBeatIds.Add(addStoryBeat.BeatId);
                    break;
                case SetColonyDirectiveOp setColonyDirective:
                    changed = state.SetColonyDirective(setColonyDirective.ColonyId, setColonyDirective.Directive);
                    break;
                case DeclareWarOp declareWar:
                    ValidateFactionId(declareWar.AttackerFactionId, "declareWar.attackerFactionId");
                    ValidateFactionId(declareWar.DefenderFactionId, "declareWar.defenderFactionId");
                    if (declareWar.AttackerFactionId == declareWar.DefenderFactionId)
                    {
                        throw new PatchApplyException("declareWar requires attackerFactionId != defenderFactionId.");
                    }

                    changed = state.RegisterDeclaredWar(declareWar.AttackerFactionId, declareWar.DefenderFactionId);
                    break;
                case ProposeTreatyOp proposeTreaty:
                    ValidateFactionId(proposeTreaty.ProposerFactionId, "proposeTreaty.proposerFactionId");
                    ValidateFactionId(proposeTreaty.ReceiverFactionId, "proposeTreaty.receiverFactionId");
                    if (proposeTreaty.ProposerFactionId == proposeTreaty.ReceiverFactionId)
                    {
                        throw new PatchApplyException("proposeTreaty requires proposerFactionId != receiverFactionId.");
                    }

                    var treatyKind = NormalizeTreatyKind(proposeTreaty.TreatyKind);
                    changed = state.RegisterTreatyProposal(
                        proposeTreaty.ProposerFactionId,
                        proposeTreaty.ReceiverFactionId,
                        treatyKind);
                    break;
                default:
                    if (options.StrictMode)
                    {
                        throw new PatchApplyException($"Unknown patch op type '{op.GetType().Name}'.");
                    }
                    changed = false;
                    break;
            }

            if (changed)
            {
                applied++;
            }
            else
            {
                noOp++;
            }
        }

        return new PatchApplyResult(applied, deduped, noOp);
    }

    private static void ValidateFactionId(int factionId, string fieldName)
    {
        if (factionId < MinFactionId || factionId > MaxFactionId)
        {
            throw new PatchApplyException(
                $"{fieldName} out of range: {factionId} (current valid faction ids: {MinFactionId}..{MaxFactionId}).");
        }
    }

    private static string NormalizeTreatyKind(string treatyKindRaw)
    {
        var treatyKind = treatyKindRaw?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(treatyKind))
        {
            throw new PatchApplyException(
                "proposeTreaty.treatyKind is required. Expected one of: ceasefire, peace_talks.");
        }

        if (Array.IndexOf(SupportedTreatyKinds, treatyKind) < 0)
        {
            throw new PatchApplyException(
                $"Unsupported proposeTreaty.treatyKind '{treatyKindRaw}'. Expected one of: ceasefire, peace_talks.");
        }

        return treatyKind;
    }
}
