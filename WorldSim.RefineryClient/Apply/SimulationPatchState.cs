namespace WorldSimRefineryClient.Apply;

public sealed class SimulationPatchState
{
    public HashSet<string> AppliedOpIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TechIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EventIds { get; } = new(StringComparer.Ordinal);

    public static SimulationPatchState CreateBaseline()
    {
        var state = new SimulationPatchState();
        state.TechIds.Add("POLICY_GLOBAL");
        return state;
    }
}
