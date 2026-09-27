namespace WorldSim.SmrLab.Artifacts;

public static class LabLimits
{
    public const int MaxRoots = 8;
    public const int MaxBundles = 32;
    public const int MaxRuns = 128;
    public const int MaxSamples = 512;
    public const int MaxEvents = 1024;
    public const long MaxFileBytes = 4 * 1024 * 1024;
    public const long MaxReadBytes = 32 * 1024 * 1024;
}

public sealed record LabRoot(int Id, string Name);

public sealed record LabItem(string Id, string Kind, string Message, string? RunKey = null);

public sealed record LabSample(int Tick, double? People, double? Food, double? Herbivores, double? Predators, double? ActiveFoodNodes);

public sealed record LabRun(
    string Id, string Source, string Config, string Planner, string Seed,
    IReadOnlyDictionary<string, string> Fields, int? AssertionFailures, int? AssertionSkips,
    IReadOnlyList<LabItem> Assertions, IReadOnlyList<LabItem> Anomalies,
    string Timeline, int? SampleEvery, IReadOnlyList<LabSample> Samples,
    IReadOnlyList<LabItem> Events, IReadOnlyList<string> Limitations);

public sealed record LabBundle(
    string Id, string Root, string Status, string Format, string? RunId,
    string Provenance, int? DeclaredRuns, int? ExitCode,
    IReadOnlyList<LabRun> Runs, IReadOnlyList<LabItem> Anomalies,
    IReadOnlyList<string> Limitations, string Source);

public sealed record LabDifference(string Field, string Left, string Right);

public sealed record LabOutcome(string BundleStatus, int? AssertionFailures, int? AssertionSkips,
    int? RunAnomalies, int? BundleAnomalies, string People, string Food,
    IReadOnlyList<LabItem> RunAnomalyItems, IReadOnlyList<LabItem> BundleAnomalyItems);

public sealed record LabComparison(LabRun Left, LabRun Right, IReadOnlyList<LabDifference> Differences,
    string Inference, IReadOnlyList<string> Limitations, LabOutcome LeftOutcome, LabOutcome RightOutcome);
