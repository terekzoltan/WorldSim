using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSim.SmrLab.Artifacts;

namespace WorldSim.SmrLab.Tests;

public sealed class ArtifactStoreTests
{
    private const string Old = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001";
    private const string New = "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001";

    [Fact]
    public void RealOldAndNewBundles_ImportAllRunsAndSourceSamples()
    {
        // Approved read-only fixture roots; no ScenarioRunner invocation or fixture copying.
        Assert.True(File.Exists(Path.Combine(Old, "manifest.json")) && File.Exists(Path.Combine(Old, "drilldown", "index.json")));
        Assert.True(File.Exists(Path.Combine(New, "manifest.json")) && File.Exists(Path.Combine(New, "drilldown", "index.json")));
        var store = new ArtifactStore(Workspace(), [Old, New]);
        var bundles = store.ListBundles();
        var old = Assert.Single(bundles, b => b.Root == "all-around-smoke-001");
        var newer = Assert.Single(bundles, b => b.Root == "all-around-smoke-wave10-001");
        Assert.True(old.Status == "COMPLETE", string.Join("; ", old.Limitations));
        Assert.True(newer.Status == "COMPLETE", string.Join("; ", newer.Limitations));
        Assert.Equal("smr/v1", old.Format);
        Assert.Equal("smr/v1", newer.Format);
        Assert.Equal(27, old.DeclaredRuns);
        Assert.Equal(9, newer.DeclaredRuns);
        Assert.Equal(27, old.Runs.Count);
        Assert.Equal(9, newer.Runs.Count);
        Assert.Equal("UNKNOWN", old.Provenance);
        Assert.Equal("UNKNOWN", newer.Provenance);

        var oldListing = Assert.Single(old.Runs, r => r.Config == "standard-default" && r.Planner == "Goap" && r.Seed == "202");
        var newListing = Assert.Single(newer.Runs, r => r.Config == "default" && r.Planner == "Goap" && r.Seed == "101");
        Assert.Equal("UNKNOWN", oldListing.Fields["visualLane"]);
        Assert.Equal("UNKNOWN", oldListing.Fields["enableSiege"]);
        Assert.Equal("not retained", Assert.Single(old.Runs, r => r.Config == "medium-default" && r.Seed == "101" && r.Planner == "Simple").Timeline);
        Assert.Equal("available", oldListing.Timeline);
        var oldDetail = store.GetRun(old.Id, oldListing.Id)!;
        var newDetail = store.GetRun(newer.Id, newListing.Id)!;
        Assert.Equal(25, oldDetail.SampleEvery);
        Assert.Equal(25, newDetail.SampleEvery);
        Assert.Equal(new LabSample(1, 72, 0, null, null, null), oldDetail.Samples[0]);
        Assert.Equal(25, oldDetail.Samples[1].Tick);
        Assert.Equal(101d, oldDetail.Samples[1].Food);
        Assert.Equal(1, newDetail.Samples[0].Tick);
        Assert.Equal(24d, newDetail.Samples[0].People);
        Assert.Equal(0d, newDetail.Samples[0].Food);
        Assert.NotEmpty(oldDetail.Events);
        Assert.Empty(newDetail.Events);

        var comparison = store.Compare(old.Id, oldListing.Id, newer.Id, newListing.Id)!;
        Assert.Contains(comparison.Differences, d => d.Field == "source");
        Assert.Contains(comparison.Differences, d => d.Field == "width" && d.Left == "192" && d.Right == "64");
        var report = store.Report(comparison);
        Assert.Contains(oldDetail.Source, report);
        Assert.Contains(newDetail.Source, report);
        Assert.Contains("| Left | COMPLETE |", report);
        Assert.Contains("| Right | COMPLETE |", report);
        Assert.Contains("Assertion failures", report);
        Assert.Contains("Assertion skips", report);
        Assert.Contains("Run anomalies", report);
        Assert.Contains("Bundle-level anomalies", report);
        Assert.Contains("People", report);
        Assert.Contains("Remaining uncertainty", report);
    }

    [Fact]
    public void SameRunKey_DifferentFlagsAndHorizon_AreNotIdentical()
    {
        using var fixture = new SyntheticFixture();
        fixture.Create("a", ticks: 5, combat: false);
        fixture.Create("b", ticks: 20, combat: true);
        var store = new ArtifactStore(Workspace(), [fixture.Directory]);
        var bundles = store.ListBundles();
        var difference = store.Compare(bundles[0].Id, "0", bundles[1].Id, "0")!;
        Assert.Contains(difference.Differences, d => d.Field == "ticks" && d.Left == "5" && d.Right == "20");
        Assert.Contains(difference.Differences, d => d.Field == "enableCombatPrimitives" && d.Left == "false" && d.Right == "true");
    }

    [Fact]
    public void CorruptPartialAndUnknownFormats_DoNotBreakOtherBundles()
    {
        using var fixture = new SyntheticFixture();
        fixture.Create("good", ticks: 5, combat: false);
        File.WriteAllText(Path.Combine(fixture.CreateDirectory("broken"), "manifest.json"), "{oops");
        var partial = fixture.CreateDirectory("partial");
        File.WriteAllText(Path.Combine(partial, "manifest.json"), "{\"schemaVersion\":\"smr/v1\",\"totalRuns\":1}");
        var unknown = fixture.CreateDirectory("unknown");
        File.WriteAllText(Path.Combine(unknown, "manifest.json"), "{\"schemaVersion\":\"smr/v999\"}");
        var bundles = new ArtifactStore(Workspace(), [fixture.Directory]).ListBundles();
        Assert.Contains(bundles, b => b.Status == "COMPLETE" && b.Runs.Count == 1);
        Assert.Contains(bundles, b => b.Status == "PARTIAL" && b.Limitations.Any());
        Assert.Contains(bundles, b => b.Status == "UNKNOWN_FORMAT");
    }

    [Fact]
    public void RootRestriction_UntrustedArtifactPathAndMarkdownEscaping()
    {
        using var fixture = new SyntheticFixture();
        Assert.Throws<ArgumentException>(() => new ArtifactStore(Workspace(), ["relative/bundle"]));
        fixture.Create("safe", ticks: 5, combat: false, config: "<script>alert(1)</script>|\nheading");
        fixture.Create("other", ticks: 6, combat: false);
        // Existing manifest's absolute artifactDir is untrusted and ignored.
        var store = new ArtifactStore(Workspace(), [fixture.Directory]);
        var bundles = store.ListBundles();
        var pair = store.Compare(bundles[1].Id, "0", bundles[0].Id, "0")!;
        var report = store.Report(pair);
        Assert.DoesNotContain("C:/not/an/approved/root", report);
        Assert.DoesNotContain("<script>", report);
        Assert.Contains("&lt;script&gt;", report);
        Assert.Contains("\\|", report);
        Assert.Null(store.GetRun("../../outside", "0"));
        Assert.Null(store.GetRun(bundles[0].Id, "../manifest.json"));
    }

    [Fact]
    public void UnselectedRoot_CannotBeReachedThroughOpaqueIdsOrManifestPath()
    {
        using var fixture = new SyntheticFixture();
        var selected = fixture.Create("selected", ticks: 5, combat: false);
        fixture.Create("unselected", ticks: 6, combat: false);
        var store = new ArtifactStore(Workspace(), [selected]);
        var bundle = Assert.Single(store.ListBundles());
        Assert.Equal("selected", Assert.Single(store.Roots).Name);
        Assert.Null(store.GetBundle("unselected"));
        Assert.Null(store.GetRun("../unselected", "0"));
        Assert.DoesNotContain("C:/not/an/approved/root", store.Report(store.Compare(bundle.Id, "0", bundle.Id, "0")!));
    }

    [Fact]
    public void VersionedCustomPayload_DispatchesWithoutInventedE11HResults()
    {
        using var fixture = new SyntheticFixture();
        var directory = fixture.CreateDirectory("synthetic-e11h");
        File.WriteAllText(Path.Combine(directory, "payload.json"),
            "{\"schemaVersion\":\"e11h_smr_recruitment_mortality_v1\"}");
        var bundle = Assert.Single(new ArtifactStore(Workspace(), [directory]).ListBundles());
        Assert.Equal("PARTIAL", bundle.Status);
        Assert.Empty(bundle.Runs);
        Assert.Contains(bundle.Limitations, item => item.Contains("real import unverified", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingOrCorruptRetainedTimeline_IsPartialNotAccepted()
    {
        using var fixture = new SyntheticFixture();
        var path = fixture.Create("timeline", ticks: 5, combat: false);
        var drilldown = Path.Combine(path, "drilldown");
        System.IO.Directory.CreateDirectory(drilldown);
        File.WriteAllText(Path.Combine(drilldown, "index.json"),
            "{\"runs\":[{\"runKey\":\"same-key\"}]}");
        var store = new ArtifactStore(Workspace(), [path]);
        Assert.Equal("PARTIAL", Assert.Single(store.ListBundles()).Status);
        System.IO.Directory.CreateDirectory(Path.Combine(drilldown, "same-key"));
        File.WriteAllText(Path.Combine(drilldown, "same-key", "timeline.json"), "{invalid");
        Assert.Equal("PARTIAL", Assert.Single(store.ListBundles()).Status);
        File.WriteAllText(Path.Combine(drilldown, "index.json"),
            "{\"runs\":[{\"runKey\":\"..\\\\outside\"}]}");
        Assert.Equal("PARTIAL", Assert.Single(store.ListBundles()).Status);
    }

    [Fact]
    public void PerFileBound_RejectsOversizedSummaryBeforeParsing()
    {
        using var fixture = new SyntheticFixture();
        var path = fixture.Create("oversize", ticks: 5, combat: false);
        File.WriteAllText(Path.Combine(path, "summary.json"), new string(' ', (int)LabLimits.MaxFileBytes + 1));
        var bundle = Assert.Single(new ArtifactStore(Workspace(), [path]).ListBundles());
        Assert.Equal("PARTIAL", bundle.Status);
        Assert.Empty(bundle.Runs);
        Assert.Contains(bundle.Limitations, item => item.Contains("exceeds file limit", StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestRunMismatch_AndMalformedAssertionItems_ArePartial()
    {
        using var fixture = new SyntheticFixture();
        var path = fixture.Create("mismatch", ticks: 5, combat: false);
        File.WriteAllText(Path.Combine(path, "manifest.json"), "{\"schemaVersion\":\"smr/v1\",\"totalRuns\":3}");
        File.WriteAllText(Path.Combine(path, "assertions.json"), "[null]");
        var bundle = Assert.Single(new ArtifactStore(Workspace(), [path]).ListBundles());
        Assert.Equal("PARTIAL", bundle.Status);
        Assert.Contains(bundle.Limitations, message => message.Contains("totalRuns", StringComparison.Ordinal));
        Assert.Contains(bundle.Limitations, message => message.Contains("Malformed assertion", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{\"skipped\":false}")]
    [InlineData("{\"passed\":\"true\",\"skipped\":false}")]
    [InlineData("{\"passed\":true}")]
    [InlineData("{\"passed\":true,\"skipped\":\"true\"}")]
    public void MalformedAssertionState_RemainsUnknownAndMakesBundlePartial(string state)
    {
        using var fixture = new SyntheticFixture();
        var dir = fixture.Create("malformed-assertion", ticks: 5, combat: false);
        var fields = state[1..^1];
        File.WriteAllText(Path.Combine(dir, "assertions.json"),
            "[{\"invariantId\":\"SURV-01\",\"runKey\":\"same-key\"," + fields + "}]");

        var bundle = Assert.Single(new ArtifactStore(Workspace(), [dir]).ListBundles());
        Assert.Equal("PARTIAL", bundle.Status);
        var run = Assert.Single(bundle.Runs);
        Assert.Equal("UNKNOWN", Assert.Single(run.Assertions).Kind);
        Assert.Null(run.AssertionFailures);
        Assert.Null(run.AssertionSkips);
        Assert.Contains(bundle.Limitations, issue => issue.Contains("assertion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidLegacyPassedAndSkipped_StaysSkipNotPass()
    {
        using var fixture = new SyntheticFixture();
        var dir = fixture.Create("skipped", ticks: 5, combat: false);
        File.WriteAllText(Path.Combine(dir, "assertions.json"),
            "[{\"invariantId\":\"COMB-01\",\"runKey\":\"same-key\",\"passed\":true,\"skipped\":true}]");
        var bundle = Assert.Single(new ArtifactStore(Workspace(), [dir]).ListBundles());
        Assert.Equal("COMPLETE", bundle.Status);
        var run = Assert.Single(bundle.Runs);
        Assert.Equal("SKIP", Assert.Single(run.Assertions).Kind);
        Assert.Equal(1, run.AssertionSkips);
        Assert.Equal(0, run.AssertionFailures);
    }

    [Theory]
    [InlineData("ticks", "20")]
    [InlineData("enableCombatPrimitives", "true")]
    [InlineData("people", "99")]
    public void SameIdentityConflictingRunFile_IsPartialAndCitesSummaryForDisplayedValues(string field, string newValue)
    {
        using var fixture = new SyntheticFixture();
        var dir = fixture.Create("contradictory-run", ticks: 5, combat: false);
        var file = Path.Combine(dir, "runs", "same-key.json");
        var payload = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        payload[field] = field == "enableCombatPrimitives" ? JsonValue.Create(bool.Parse(newValue)) : JsonValue.Create(int.Parse(newValue));
        File.WriteAllText(file, payload.ToJsonString());

        var bundle = Assert.Single(new ArtifactStore(Workspace(), [dir]).ListBundles());
        Assert.Equal("PARTIAL", bundle.Status);
        Assert.Contains(bundle.Limitations, issue => issue.Contains(field, StringComparison.Ordinal) &&
            issue.Contains("summary.json", StringComparison.Ordinal) && issue.Contains("same-key.json", StringComparison.Ordinal));
        var run = Assert.Single(bundle.Runs);
        Assert.EndsWith("summary.json#runs[0]", run.Source, StringComparison.Ordinal);
        Assert.Contains(run.Limitations, issue => issue.Contains(field, StringComparison.Ordinal));
        Assert.Equal("5", run.Fields["ticks"]);
        Assert.Equal("false", run.Fields["enableCombatPrimitives"]);
        Assert.Equal("2", run.Fields["people"]);
    }

    [Fact]
    public void PartialRunReport_PreservesMeasuredOutcomesSourcesAndUnknownCounts()
    {
        using var fixture = new SyntheticFixture();
        var first = fixture.Create("a", ticks: 5, combat: false);
        var second = fixture.Create("b", ticks: 6, combat: false);
        File.WriteAllText(Path.Combine(first, "assertions.json"),
            "[{\"invariantId\":\"SURV-01\",\"runKey\":\"same-key\",\"passed\":false,\"skipped\":false}]");
        File.WriteAllText(Path.Combine(first, "anomalies.json"),
            "[{\"id\":\"ANOM-RUN\",\"runKey\":\"same-key\"},{\"id\":\"ANOM-GLOBAL\"}]");
        File.WriteAllText(Path.Combine(second, "manifest.json"), "{\"schemaVersion\":\"smr/v1\",\"totalRuns\":2}");
        File.Delete(Path.Combine(second, "anomalies.json"));
        var store = new ArtifactStore(Workspace(), [fixture.Directory]);
        var bundles = store.ListBundles();
        var comparison = store.Compare(bundles[0].Id, "0", bundles[1].Id, "0")!;
        var report = store.Report(comparison);
        Assert.Contains("| Left | COMPLETE | 1 | 0 | 1 | 1 | 2 | 3 |", report);
        Assert.Contains("| Right | PARTIAL | 0 | 0 | UNKNOWN | UNKNOWN | 2 | 3 |", report);
        Assert.Contains("Left run anomaly: ANOM-RUN", report);
        Assert.Contains("Left unscoped bundle anomaly: ANOM-GLOBAL", report);
        Assert.Contains("Missing anomalies.json", report);
        Assert.Contains(comparison.Left.Source, report);
        Assert.Contains(comparison.Right.Source, report);
        Assert.Contains("Measured outcome is not Meta acceptance", report);
    }

    internal static string Workspace()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldSim.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("WorldSim.sln not found in isolated worktree.");
    }

    internal sealed class SyntheticFixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "lab-fixtures", Guid.NewGuid().ToString("N"));

        public string CreateDirectory(string name)
        {
            var path = Path.Combine(Directory, name);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public string Create(string name, int ticks, bool combat, string config = "case")
        {
            var dir = CreateDirectory(name);
            System.IO.Directory.CreateDirectory(Path.Combine(dir, "runs"));
            var entry = new { configName = config, plannerMode = "Simple", seed = 1, ticks,
                enableCombatPrimitives = combat, people = 2, food = 3 };
            var json = JsonSerializer.Serialize(entry);
            File.WriteAllText(Path.Combine(dir, "manifest.json"),
                "{\"schemaVersion\":\"smr/v1\",\"totalRuns\":1,\"artifactDir\":\"C:/not/an/approved/root\"}");
            File.WriteAllText(Path.Combine(dir, "summary.json"), "{\"runs\":[" + json + "]}");
            File.WriteAllText(Path.Combine(dir, "runs", "same-key.json"), json);
            File.WriteAllText(Path.Combine(dir, "assertions.json"), "[]");
            File.WriteAllText(Path.Combine(dir, "anomalies.json"), "[]");
            return dir;
        }

        public void Dispose() { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true); }
    }
}
