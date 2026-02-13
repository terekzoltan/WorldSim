using WorldSimRefineryClient.Contracts;

namespace WorldSimRefineryClient.Apply;

public sealed class PatchApplier
{
    public PatchApplyResult Apply(SimulationPatchState state, PatchResponse response, PatchApplyOptions? options = null)
    {
        options ??= new PatchApplyOptions();

        if (!string.Equals(response.SchemaVersion, "v1", StringComparison.Ordinal))
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
}
