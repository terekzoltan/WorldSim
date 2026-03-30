namespace WorldSimRefineryClient.Apply;

public sealed class SimulationPatchState
{
    public HashSet<string> AppliedOpIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TechIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EventIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> StoryBeatIds { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, string> ColonyDirectives { get; } = new();

    public SimulationPatchState Clone()
    {
        var clone = new SimulationPatchState();
        clone.CopyFrom(this);
        return clone;
    }

    public void CopyFrom(SimulationPatchState source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        AppliedOpIds.Clear();
        AppliedOpIds.UnionWith(source.AppliedOpIds);

        TechIds.Clear();
        TechIds.UnionWith(source.TechIds);

        EventIds.Clear();
        EventIds.UnionWith(source.EventIds);

        StoryBeatIds.Clear();
        StoryBeatIds.UnionWith(source.StoryBeatIds);

        ColonyDirectives.Clear();
        foreach (var pair in source.ColonyDirectives)
        {
            ColonyDirectives[pair.Key] = pair.Value;
        }
    }

    public bool SetColonyDirective(int colonyId, string directive)
    {
        if (ColonyDirectives.TryGetValue(colonyId, out var existing)
            && string.Equals(existing, directive, StringComparison.Ordinal))
        {
            return false;
        }

        ColonyDirectives[colonyId] = directive;
        return true;
    }

    public static SimulationPatchState CreateBaseline()
    {
        var state = new SimulationPatchState();
        state.TechIds.Add("POLICY_GLOBAL");
        return state;
    }
}
