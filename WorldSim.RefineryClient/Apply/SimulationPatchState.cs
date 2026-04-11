namespace WorldSimRefineryClient.Apply;

public sealed class SimulationPatchState
{
    public HashSet<string> AppliedOpIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TechIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> EventIds { get; } = new(StringComparer.Ordinal);
    public HashSet<string> StoryBeatIds { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, string> ColonyDirectives { get; } = new();
    public HashSet<(int LeftFactionId, int RightFactionId)> DeclaredWars { get; } = new();
    public HashSet<(int ProposerFactionId, int ReceiverFactionId, string TreatyKind)> TreatyProposals { get; } = new();

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

        DeclaredWars.Clear();
        DeclaredWars.UnionWith(source.DeclaredWars);

        TreatyProposals.Clear();
        TreatyProposals.UnionWith(source.TreatyProposals);
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

    public bool RegisterDeclaredWar(int attackerFactionId, int defenderFactionId)
    {
        var pair = NormalizeFactionPair(attackerFactionId, defenderFactionId);
        return DeclaredWars.Add(pair);
    }

    public bool RegisterTreatyProposal(int proposerFactionId, int receiverFactionId, string treatyKind)
    {
        return TreatyProposals.Add((proposerFactionId, receiverFactionId, NormalizeTreatyKindKey(treatyKind)));
    }

    private static (int LeftFactionId, int RightFactionId) NormalizeFactionPair(int first, int second)
        => first <= second ? (first, second) : (second, first);

    private static string NormalizeTreatyKindKey(string treatyKind)
    {
        if (string.IsNullOrWhiteSpace(treatyKind))
            throw new ArgumentException("treatyKind is required.", nameof(treatyKind));

        return treatyKind.Trim().ToLowerInvariant();
    }

    public static SimulationPatchState CreateBaseline()
    {
        var state = new SimulationPatchState();
        state.TechIds.Add("POLICY_GLOBAL");
        return state;
    }
}
