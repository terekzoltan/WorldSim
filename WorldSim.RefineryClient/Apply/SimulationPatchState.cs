namespace WorldSimRefineryClient.Apply;

public sealed class SimulationPatchState
{
    public HashSet<string> AppliedOpIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TechIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EventIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> StoryBeatIds { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, string> ColonyDirectives { get; } = new();

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
