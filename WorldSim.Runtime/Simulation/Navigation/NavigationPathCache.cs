namespace WorldSim.Simulation.Navigation;

public sealed class NavigationPathCache
{
    public (int x, int y) Target { get; private set; }
    public int TopologyVersion { get; private set; }
    public int NextIndex { get; private set; }
    public IReadOnlyList<(int x, int y)> Steps { get; private set; } = Array.Empty<(int x, int y)>();

    public bool HasPath => Steps.Count > 1 && NextIndex < Steps.Count;

    public void Set((int x, int y) target, int topologyVersion, IReadOnlyList<(int x, int y)> steps)
    {
        Target = target;
        TopologyVersion = topologyVersion;
        Steps = steps;
        NextIndex = Math.Min(1, steps.Count);
    }

    public bool IsValid((int x, int y) target, int topologyVersion)
        => HasPath && Target == target && TopologyVersion == topologyVersion;

    public (int x, int y)? PeekNext()
    {
        if (!HasPath)
            return null;
        return Steps[NextIndex];
    }

    public void Advance()
    {
        if (!HasPath)
            return;
        NextIndex++;
    }

    public void Invalidate()
    {
        Steps = Array.Empty<(int x, int y)>();
        NextIndex = 0;
    }
}
