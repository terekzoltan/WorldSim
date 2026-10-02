using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSim.SmrLab.Artifacts;
using WorldSim.SmrLab.ManualRuns;

namespace WorldSim.SmrLab.Tests;

public sealed class ManualRunTests
{
    [Fact]
    public void RunnerInput_UsesCaseSensitiveConstructorNamesAndAllIsolatedControls()
    {
        var request = Request();
        var previous = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_COMPARE");
        try
        {
            Environment.SetEnvironmentVariable("WORLDSIM_SCENARIO_COMPARE", "true");
            var environment = ManualRunContract.EnvironmentFor(request, "C:/lab/private");
            using var json = JsonDocument.Parse(environment["WORLDSIM_SCENARIO_CONFIGS_JSON"]);
            var item = Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal("lab_small", item.GetProperty("Name").GetString());
            Assert.False(item.TryGetProperty("name", out _));
            Assert.Equal(40, item.GetProperty("Ticks").GetInt32());
            Assert.Equal(.25f, item.GetProperty("Dt").GetSingle());
            Assert.Equal(32, item.GetProperty("Width").GetInt32());
            Assert.Equal(20, item.GetProperty("Height").GetInt32());
            Assert.Equal(12, item.GetProperty("InitialPop").GetInt32());
            foreach (var key in new[] { "EnableCombatPrimitives", "EnableDiplomacy", "StoneBuildingsEnabled", "EnablePredatorHumanAttacks" })
                Assert.False(item.GetProperty(key).GetBoolean());
            Assert.True(item.GetProperty("EnableSiege").GetBoolean());
            Assert.Equal(1, item.GetProperty("BirthRateMultiplier").GetSingle());
            Assert.Equal(1, item.GetProperty("MovementSpeedMultiplier").GetSingle());
            Assert.Equal("false", environment["WORLDSIM_SCENARIO_ASSERT"]);
            foreach (var name in new[] { "COMPARE", "PERF", "ANOMALY_FAIL", "DELTA_FAIL", "PERF_FAIL" })
                Assert.Equal("false", environment["WORLDSIM_SCENARIO_" + name]);
            Assert.DoesNotContain("WORLDSIM_SCENARIO_TICKS", environment.Keys);
            Assert.DoesNotContain("WORLDSIM_SCENARIO_DT", environment.Keys);
            Assert.Equal("Headless", environment["WORLDSIM_VISUAL_PROFILE"]);
            Assert.Equal("10", environment["WORLDSIM_SCENARIO_SAMPLE_EVERY"]);
            var other = ManualRunContract.EnvironmentFor(request with { Preset = "lab_default", Ticks = 50, Mode = "assert" }, "C:/other");
            using var otherJson = JsonDocument.Parse(other["WORLDSIM_SCENARIO_CONFIGS_JSON"]);
            Assert.Equal(64, otherJson.RootElement[0].GetProperty("Width").GetInt32());
            Assert.Equal(40, otherJson.RootElement[0].GetProperty("Height").GetInt32());
            Assert.Equal(24, otherJson.RootElement[0].GetProperty("InitialPop").GetInt32());
            Assert.Equal(50, otherJson.RootElement[0].GetProperty("Ticks").GetInt32());
            Assert.Equal("true", other["WORLDSIM_SCENARIO_ASSERT"]);
        }
        finally { Environment.SetEnvironmentVariable("WORLDSIM_SCENARIO_COMPARE", previous); }
    }

    [Fact]
    public async Task DuplicateConcurrentAndRestart_FailClosedWithoutSecondLaunch()
    {
        using var fixture = new Harness();
        var request = Request();
        var first = fixture.Manager.Start(request);
        Assert.Equal(202, first.Status);
        Assert.Equal(200, fixture.Manager.Start(request).Status);
        Assert.Equal(409, fixture.Manager.Start(request with { Ticks = 41 }).Status);
        Assert.Equal(409, fixture.Manager.Start(Request() with { IntentId = Guid.NewGuid() }).Status);
        await fixture.Runner.WaitForLaunch();
        Assert.Equal(1, fixture.Runner.Launches);
        Assert.True(fixture.Runner.SawFlushedStartingJournal);
        Assert.Empty(fixture.Store.ListBundles());
        fixture.Manager.Dispose();
        using var resumed = fixture.OpenManager();
        Assert.Contains("no proven terminal", resumed.Blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, resumed.Start(Request() with { IntentId = Guid.NewGuid() }).Status);
        Assert.Equal(200, resumed.Start(request).Status);
        fixture.Runner.Complete(new ManualProcessResult("EXITED", 0, "synthetic"));
        await fixture.WaitForTerminal();
        Assert.Equal(1, fixture.Runner.Launches);
    }

    [Theory]
    [InlineData(0, true, "COMPLETE", true, false)]
    [InlineData(0, true, "PARTIAL", true, true)]
    [InlineData(2, true, "COMPLETE", true, false)]
    [InlineData(4, true, "COMPLETE", true, false)]
    [InlineData(3, true, "COMPLETE", true, false)]
    [InlineData(3, false, "UNKNOWN", false, false)]
    public async Task TerminalProcessAndArtifactAxes_AreIndependent(int exit, bool valid, string expected, bool published, bool missingRunFile)
    {
        using var fixture = new Harness();
        var request = Request();
        Assert.Equal(202, fixture.Manager.Start(request).Status);
        await fixture.Runner.WaitForLaunch();
        if (valid) fixture.WriteSynthetic(request, exit, missingRunFile);
        fixture.Runner.Complete(new ManualProcessResult("EXITED", exit, "synthetic"));
        var result = await fixture.WaitForTerminal();
        Assert.Equal("EXITED", result.ProcessStatus);
        Assert.Equal(exit, result.ExitCode);
        Assert.Equal(expected, result.ArtifactStatus);
        Assert.Equal(published, result.BundleId is not null);
        Assert.Equal(published ? "PUBLISHED" : "NO_BUNDLE", result.PublicationStatus);
        if (exit == 2) Assert.Equal(1, result.AssertionFailures);
        if (exit == 4) Assert.Equal(1, result.AnomalyCount);
        if (published) Assert.Single(fixture.Store.ListBundles());
        else Assert.Empty(fixture.Store.ListBundles());
    }

    [Theory]
    [InlineData("LAUNCH_ERROR")]
    [InlineData("TIMEOUT")]
    public async Task LaunchErrorAndTimeout_DoNotInventMeasurements(string processStatus)
    {
        using var fixture = new Harness();
        Assert.Equal(202, fixture.Manager.Start(Request()).Status);
        await fixture.Runner.WaitForLaunch();
        fixture.Runner.Complete(new ManualProcessResult(processStatus, null, "synthetic"));
        var result = await fixture.WaitForTerminal();
        Assert.Equal(processStatus, result.ProcessStatus);
        Assert.Null(result.AssertionFailures);
        Assert.Null(result.BundleId);
        Assert.Equal("NO_BUNDLE", result.PublicationStatus);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, null)]
    public void MalformedExitedJournal_MissingTimeOrCode_BlocksWithoutDeletingEvidence(bool hasTime, int? exitCode)
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var request = Request();
        var stage = fixture.Stage(request);
        Directory.CreateDirectory(stage);
        var retained = Path.Combine(stage, "retained.txt");
        File.WriteAllText(retained, "evidence");
        fixture.WriteJournal(request, "EXITED", exitCode, hasTime ? DateTimeOffset.UtcNow : null);
        using var recovered = fixture.OpenManager();
        Assert.NotNull(recovered.Blocker);
        Assert.Equal(409, recovered.Start(Request()).Status);
        Assert.Equal(0, fixture.Runner.Launches);
        Assert.Equal("evidence", File.ReadAllText(retained));
    }

    [Theory]
    [InlineData("EXITED", 0)]
    [InlineData("LAUNCH_ERROR", null)]
    [InlineData("TIMEOUT", null)]
    public async Task ValidTerminalJournal_DoesNotTreatLegitimateNullCodeAsMalformed(string status, int? exitCode)
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var previous = Request();
        Directory.CreateDirectory(fixture.Stage(previous));
        fixture.WriteJournal(previous, status, exitCode, DateTimeOffset.UtcNow);
        using var recovered = fixture.OpenManager();
        Assert.Null(recovered.Blocker);
        Assert.Equal("NO_BUNDLE", recovered.Get(previous.IntentId.ToString("N"))!.PublicationStatus);
        Assert.Equal(202, recovered.Start(Request()).Status);
        await fixture.Runner.WaitForLaunch();
        fixture.Runner.Complete(new ManualProcessResult("LAUNCH_ERROR", null, "synthetic"));
        await fixture.WaitForTerminal(recovered);
        Assert.Equal(1, fixture.Runner.Launches);
    }

    [Fact]
    public async Task PublicationWindow_BlocksDifferentIntentAtApi_ThenPublishesWithoutSecondLaunch()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new Harness(async () => { reached.TrySetResult(); await release.Task; });
        await using var app = LabHost.Create(fixture.Store, 0, fixture.Manager);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(LabHost.BoundAddress(app)) };
        var options = await client.GetFromJsonAsync<JsonElement>("/api/manual/options");
        var token = options.GetProperty("token").GetString()!;
        var request = Request();
        async Task<HttpResponseMessage> Post(ManualRunRequest body)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/manual/jobs") { Content = JsonContent.Create(body) };
            message.Headers.TryAddWithoutValidation("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
            message.Headers.Add("X-Lab-Token", token);
            return await client.SendAsync(message);
        }
        try
        {
            using (var first = await Post(request)) Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            await fixture.Runner.WaitForLaunch();
            fixture.WriteSynthetic(request, 0);
            fixture.Runner.Complete(new ManualProcessResult("EXITED", 0, "synthetic"));
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var pending = await client.GetFromJsonAsync<ManualJob>($"/api/manual/jobs/{request.IntentId:N}");
            Assert.NotNull(pending!.FinishedAt);
            Assert.Equal("PENDING", pending.PublicationStatus);
            Assert.Null(pending.BundleId);
            var journal = JsonSerializer.Deserialize<ManualJob>(File.ReadAllText(fixture.Journal(request)))!;
            Assert.Equal("PENDING", journal.PublicationStatus);
            using (var other = await Post(Request())) Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
            using (var duplicate = await Post(request)) Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
            Assert.Equal(1, fixture.Runner.Launches);
        }
        finally { release.TrySetResult(); }
        var settled = await fixture.WaitForTerminal();
        Assert.Equal("PUBLISHED", settled.PublicationStatus);
        Assert.NotNull(settled.BundleId);
        Assert.Single(fixture.Store.ListBundles());
        await app.StopAsync();
    }

    [Fact]
    public async Task RestartFromDurablePublicationWindow_ReconcilesWithoutReplaying()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var original = new Harness(async () => { reached.TrySetResult(); await release.Task; });
        using var restarted = new Harness();
        var request = Request();
        try
        {
            Assert.Equal(202, original.Manager.Start(request).Status);
            await original.Runner.WaitForLaunch();
            original.WriteSynthetic(request, 0);
            original.Runner.Complete(new ManualProcessResult("EXITED", 0, "synthetic"));
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // A separate private root preserves exactly the durable terminal journal
            // and staging snapshot at the crash window, without racing the live fake.
            restarted.Manager.Dispose();
            CopyBundle(original.Stage(request), restarted.Stage(request));
            File.Copy(original.Journal(request), restarted.Journal(request));
            using var recovered = restarted.OpenManager();
            Assert.Null(recovered.Blocker);
            var published = recovered.Get(request.IntentId.ToString("N"))!;
            Assert.Equal("PUBLISHED", published.PublicationStatus);
            Assert.NotNull(published.BundleId);
            Assert.Equal(200, recovered.Start(request).Status);
            Assert.Equal(0, restarted.Runner.Launches);
        }
        finally { release.TrySetResult(); }
        await original.WaitForTerminal();
    }

    [Fact]
    public void OldJournalAndUncertainPublication_NeverInventAnOpenLink()
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var legacy = Request();
        Directory.CreateDirectory(fixture.Stage(legacy));
        fixture.WriteJournal(legacy, "EXITED", 0, DateTimeOffset.UtcNow, omitPublicationField: true);
        using (var recovered = fixture.OpenManager())
        {
            var job = recovered.Get(legacy.IntentId.ToString("N"))!;
            Assert.Equal("NO_BUNDLE", job.PublicationStatus);
            Assert.Null(job.BundleId);
        }
        var uncertain = Request();
        fixture.WriteJournal(uncertain, "EXITED", 0, DateTimeOffset.UtcNow, "PENDING");
        using var blocked = fixture.OpenManager();
        Assert.Equal("UNCERTAIN", blocked.Get(uncertain.IntentId.ToString("N"))!.PublicationStatus);
        Assert.Null(blocked.Get(uncertain.IntentId.ToString("N"))!.BundleId);
        Assert.Equal(409, blocked.Start(Request()).Status);
        Assert.Equal(200, blocked.Start(uncertain).Status);
        Assert.Equal(0, fixture.Runner.Launches);
    }

    [Fact]
    public void LegacyPublishedJournal_ReconcilesVerifiedBundleWithoutRewritingRetainedJournal()
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var request = Request();
        Directory.CreateDirectory(fixture.Stage(request));
        fixture.WriteSynthetic(request, 0);
        fixture.WriteJournal(request, "EXITED", 0, DateTimeOffset.UtcNow, omitPublicationField: true);
        var retained = File.ReadAllText(fixture.Journal(request));
        Directory.Move(fixture.Stage(request), Path.Combine(fixture.Root, "published", request.IntentId.ToString("N")));
        using var recovered = fixture.OpenManager();
        var job = recovered.Get(request.IntentId.ToString("N"))!;
        Assert.Null(recovered.Blocker);
        Assert.Equal("PUBLISHED", job.PublicationStatus);
        Assert.NotNull(job.BundleId);
        Assert.Equal(retained, File.ReadAllText(fixture.Journal(request)));
        Assert.Equal(0, fixture.Runner.Launches);
    }

    [Fact]
    public void LegacyPublishedJournal_MismatchedRunSettings_FailsClosedWithoutTouchingBundle()
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var request = Request();
        Directory.CreateDirectory(fixture.Stage(request));
        fixture.WriteSynthetic(request, 0);
        // Keep the bundle structurally readable while its run identity differs
        // from the retained terminal journal. Neither raw file may be rewritten.
        foreach (var file in new[] { "summary.json", Path.Combine("runs", "same-key.json") })
        {
            var path = Path.Combine(fixture.Stage(request), file);
            var original = File.ReadAllText(path);
            var mismatched = original.Replace("\"seed\":101", "\"seed\":202", StringComparison.Ordinal);
            Assert.NotEqual(original, mismatched);
            File.WriteAllText(path, mismatched);
        }
        fixture.WriteJournal(request, "EXITED", 0, DateTimeOffset.UtcNow, omitPublicationField: true);
        var published = Path.Combine(fixture.Root, "published", request.IntentId.ToString("N"));
        Directory.Move(fixture.Stage(request), published);
        var parseable = Assert.Single(fixture.Store.ListBundles());
        Assert.Equal("smr/v1", parseable.Format);
        Assert.Equal("202", Assert.Single(parseable.Runs).Seed);
        var retained = Directory.EnumerateFiles(published, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(published, file), File.ReadAllBytes);

        using var recovered = fixture.OpenManager();
        var job = recovered.Get(request.IntentId.ToString("N"))!;
        Assert.Equal("UNCERTAIN", job.PublicationStatus);
        Assert.Null(job.BundleId);
        Assert.NotNull(recovered.Blocker);
        Assert.Equal(409, recovered.Start(Request()).Status);
        Assert.Equal(200, recovered.Start(request).Status);
        Assert.Equal(0, fixture.Runner.Launches);
        Assert.True(Directory.Exists(published));
        foreach (var (file, bytes) in retained)
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(published, file)));
    }

    [Theory]
    [InlineData("seedCount", "\"1\"")]
    [InlineData("plannerCount", "null")]
    [InlineData("configCount", "{}")]
    [InlineData("exitCode", "2147483648")]
    [InlineData("anomalyCount", "[]")]
    public void LegacyPublishedJournal_IndexedBundleWithMalformedTypedManifest_FailsClosed(
        string field, string malformedJson)
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var request = Request();
        Directory.CreateDirectory(fixture.Stage(request));
        fixture.WriteSynthetic(request, 0);
        var manifestPath = Path.Combine(fixture.Stage(request), "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest[field] = JsonNode.Parse(malformedJson);
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        fixture.WriteJournal(request, "EXITED", 0, DateTimeOffset.UtcNow, omitPublicationField: true);
        var published = Path.Combine(fixture.Root, "published", request.IntentId.ToString("N"));
        Directory.Move(fixture.Stage(request), published);

        // Prove the reader still indexes this exact published fixture, so recovery
        // must reach Inspect's stricter typed-read boundary rather than fail earlier.
        var indexed = Assert.Single(fixture.Store.ListBundles());
        Assert.Equal("smr/v1", indexed.Format);
        Assert.Single(indexed.Runs);
        var retained = Directory.EnumerateFiles(published, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(published, file), File.ReadAllBytes);

        using var recovered = fixture.OpenManager();
        var job = recovered.Get(request.IntentId.ToString("N"))!;
        Assert.Equal("UNCERTAIN", job.PublicationStatus);
        Assert.Null(job.BundleId);
        Assert.NotNull(recovered.Blocker);
        Assert.Equal(409, recovered.Start(Request()).Status);
        Assert.Equal(200, recovered.Start(request).Status);
        Assert.Equal(0, fixture.Runner.Launches);
        Assert.True(Directory.Exists(published));
        foreach (var (file, bytes) in retained)
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(published, file)));
    }

    [Fact]
    public async Task HttpMutation_GuardsOriginTokenAndTypedBodyBeforeSpawn()
    {
        using var fixture = new Harness();
        await using var app = LabHost.Create(fixture.Store, 0, fixture.Manager);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(LabHost.BoundAddress(app)) };
        var options = await client.GetFromJsonAsync<JsonElement>("/api/manual/options");
        var token = options.GetProperty("token").GetString()!;
        var payload = new { intentId = Guid.NewGuid(), profile = "core", preset = "lab_small", seed = 101,
            planner = "Simple", ticks = 40, mode = "standard" };
        async Task<HttpStatusCode> Post(object body, string? origin, string? header)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/manual/jobs")
                { Content = JsonContent.Create(body) };
            if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
            if (header is not null) request.Headers.Add("X-Lab-Token", header);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }
        Assert.Equal(HttpStatusCode.Forbidden, await Post(payload, null, token));
        Assert.Equal(HttpStatusCode.Forbidden, await Post(payload, "https://foreign.example", token));
        Assert.Equal(HttpStatusCode.Forbidden, await Post(payload, client.BaseAddress!.GetLeftPart(UriPartial.Authority), null));
        Assert.Equal(HttpStatusCode.BadRequest, await Post(new { payload.intentId, payload.profile, payload.preset,
            payload.seed, payload.planner, payload.ticks, payload.mode, executable = "evil" },
            client.BaseAddress!.GetLeftPart(UriPartial.Authority), token));
        Assert.Equal(0, fixture.Runner.Launches);
        Assert.Empty(fixture.Manager.List());
        Assert.Equal(HttpStatusCode.Accepted, await Post(payload, client.BaseAddress!.GetLeftPart(UriPartial.Authority), token));
        await fixture.Runner.WaitForLaunch();
        fixture.Runner.Complete(new ManualProcessResult("EXITED", 3, "synthetic"));
        await fixture.WaitForTerminal();
        await app.StopAsync();
    }

    [Fact]
    public void CorruptOrMissingJournal_AndFullPublishedRoot_BlockStarts()
    {
        using var fixture = new Harness();
        fixture.Manager.Dispose();
        var stage = Path.Combine(fixture.Root, ".staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        using (var recovered = fixture.OpenManager())
            Assert.Equal(409, recovered.Start(Request()).Status);
        Directory.Delete(stage);
        var journal = Path.Combine(fixture.Root, ".jobs", Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(journal, "{bad");
        using (var recovered = fixture.OpenManager())
            Assert.Equal(409, recovered.Start(Request()).Status);
        File.Delete(journal);
        for (var i = 0; i < LabLimits.MaxBundles; i++)
            Directory.CreateDirectory(Path.Combine(fixture.Root, "published", $"retained-{i}"));
        // Missing journals on published entries fail closed before capacity can even be bypassed.
        using (var recovered = fixture.OpenManager())
            Assert.Equal(409, recovered.Start(Request()).Status);
    }

    [Fact]
    public void FullPublishedRoot_RejectsBeforeSpawningAndRetainsEvidence()
    {
        using var fixture = new Harness();
        for (var i = 0; i < LabLimits.MaxBundles; i++)
            Directory.CreateDirectory(Path.Combine(fixture.Root, "published", $"retained-{i}"));
        var result = fixture.Manager.Start(Request());
        Assert.Equal(409, result.Status);
        Assert.Contains("32-bundle", result.Error);
        Assert.Equal(0, fixture.Runner.Launches);
        Assert.Equal(32, Directory.EnumerateDirectories(Path.Combine(fixture.Root, "published")).Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RestartLiveMatchingPidVersusReusedPid_BothBlockWithoutReplaying(bool matching)
    {
        using var fixture = new Harness();
        var request = Request();
        Assert.Equal(202, fixture.Manager.Start(request).Status);
        await fixture.Runner.WaitForLaunch();
        fixture.Manager.Dispose();
        var journal = Path.Combine(fixture.Root, ".jobs", request.IntentId.ToString("N") + ".json");
        var original = JsonSerializer.Deserialize<ManualJob>(File.ReadAllText(journal))!;
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        var age = matching ? TimeSpan.Zero : TimeSpan.FromMinutes(-10);
        File.WriteAllText(journal, JsonSerializer.Serialize(original with
        {
            ProcessId = current.Id,
            ProcessStartedAt = new DateTimeOffset(current.StartTime.ToUniversalTime()).Add(age)
        }));
        using var resumed = fixture.OpenManager();
        Assert.Contains(matching ? "matching live child" : "no proven terminal", resumed.Blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(409, resumed.Start(Request()).Status);
        Assert.Equal(1, fixture.Runner.Launches);
        fixture.Runner.Complete(new ManualProcessResult("EXITED", 3, "synthetic"));
        await fixture.WaitForTerminal();
    }

    [Fact]
    public async Task OfflineParity_IgnoresOnlyGeneratedIdentityAndTimingNotMeasuredFood()
    {
        using var fixture = new Harness();
        var request = Request();
        Assert.Equal(202, fixture.Manager.Start(request).Status);
        await fixture.Runner.WaitForLaunch();
        fixture.WriteSynthetic(request, 0);
        var control = Path.Combine(fixture.Root, "parity-control");
        CopyBundle(Path.Combine(fixture.Root, ".staging", request.IntentId.ToString("N")), control);
        File.WriteAllText(Path.Combine(control, "manifest.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "smr/v1", totalRuns = 1, seedCount = 1, plannerCount = 1,
            configCount = 1, exitCode = 0, anomalyCount = 0, runId = "different-generated-id",
            artifactDir = control
        }));
        fixture.Runner.Complete(new ManualProcessResult("EXITED", 0, "synthetic"));
        var terminal = await fixture.WaitForTerminal();
        var published = Path.Combine(fixture.Root, "published", terminal.Id);
        Assert.Empty(ManualParity.Compare(published, control));
        File.WriteAllText(Path.Combine(control, "summary.json"), File.ReadAllText(Path.Combine(control, "summary.json")).Replace("\"food\":5", "\"food\":6"));
        Assert.Contains(ManualParity.Compare(published, control), d => d.Contains("food", StringComparison.Ordinal));
    }

    private static void CopyBundle(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        Directory.CreateDirectory(Path.Combine(destination, "runs"));
        foreach (var file in Directory.EnumerateFiles(Path.Combine(source, "runs")))
            File.Copy(file, Path.Combine(destination, "runs", Path.GetFileName(file)));
    }

    private static ManualRunRequest Request() => new(Guid.NewGuid(), "core", "lab_small", 101, "Simple", 40, "standard");

    private sealed class Harness : IDisposable
    {
        public string Root { get; } = Path.Combine(ArtifactStoreTests.Workspace(), ".artifacts", "smr", "lab-b-tests-" + Guid.NewGuid().ToString("N"));
        public FakeRunner Runner { get; } = new();
        public ArtifactStore Store { get; }
        public ManualRunManager Manager { get; }

        public Harness(Func<Task>? beforePublication = null)
        {
            var workspace = ArtifactStoreTests.Workspace();
            var dll = Path.Combine(workspace, "WorldSim.ScenarioRunner", "bin", "Debug", "net8.0", "WorldSim.ScenarioRunner.dll");
            Assert.True(File.Exists(dll), "Build the ScenarioRunner DLL once before focused Lab tests; no simulation is launched.");
            Manager = new ManualRunManager(workspace, Root, dll, Runner, beforePublication);
            Store = new ArtifactStore(workspace, [Manager.PublishedRoot]);
            Manager.AttachStore(Store);
        }

        public ManualRunManager OpenManager()
        {
            var workspace = ArtifactStoreTests.Workspace();
            var dll = Path.Combine(workspace, "WorldSim.ScenarioRunner", "bin", "Debug", "net8.0", "WorldSim.ScenarioRunner.dll");
            var manager = new ManualRunManager(workspace, Root, dll, Runner);
            manager.AttachStore(Store);
            return manager;
        }

        public string Stage(ManualRunRequest request) => Path.Combine(Root, ".staging", request.IntentId.ToString("N"));
        public string Journal(ManualRunRequest request) => Path.Combine(Root, ".jobs", request.IntentId.ToString("N") + ".json");

        public void WriteJournal(ManualRunRequest request, string status, int? exitCode,
            DateTimeOffset? finishedAt, string? publicationStatus = null, bool omitPublicationField = false)
        {
            var job = new ManualJob(request.IntentId.ToString("N"), request, status, exitCode,
                "UNKNOWN", null, null, null, null, null, DateTimeOffset.UtcNow,
                finishedAt, null, "Synthetic retained terminal journal.", Manager.RunnerHash,
                ManualRunContract.EnvironmentFor(request, Stage(request)), Manager.CheckoutHead, publicationStatus);
            var json = JsonNode.Parse(JsonSerializer.Serialize(job))!.AsObject();
            if (omitPublicationField) json.Remove("PublicationStatus");
            File.WriteAllText(Journal(request), json.ToJsonString());
        }

        public void WriteSynthetic(ManualRunRequest request, int exit, bool missingRunFile = false)
        {
            var stage = Path.Combine(Root, ".staging", request.IntentId.ToString("N"));
            var run = new { configName = "lab_small", plannerMode = "Simple", seed = 101, visualLane = "Headless",
                width = 32, height = 20, initialPop = 12, ticks = 40, dt = .25,
                enableCombatPrimitives = false, enableDiplomacy = false, enableSiege = true,
                stoneBuildingsEnabled = false, enablePredatorHumanAttacks = false,
                birthRateMultiplier = 1, movementSpeedMultiplier = 1, people = 12, food = 5 };
            File.WriteAllText(Path.Combine(stage, "summary.json"), JsonSerializer.Serialize(new { runs = new[] { run } }));
            File.WriteAllText(Path.Combine(stage, "manifest.json"), JsonSerializer.Serialize(new { schemaVersion = "smr/v1",
                totalRuns = 1, seedCount = 1, plannerCount = 1, configCount = 1, exitCode = exit, anomalyCount = exit == 4 ? 1 : 0 }));
            File.WriteAllText(Path.Combine(stage, "assertions.json"), exit == 2
                ? "[{\"invariantId\":\"SURV-01\",\"runKey\":\"same-key\",\"passed\":false,\"skipped\":false,\"message\":\"synthetic failure\"}]"
                : "[]");
            File.WriteAllText(Path.Combine(stage, "anomalies.json"), exit == 4
                ? "[{\"id\":\"ANOM-SYNTHETIC\",\"runKey\":\"same-key\",\"message\":\"synthetic anomaly\"}]"
                : "[]");
            File.WriteAllText(Path.Combine(stage, "run.log"), "synthetic only");
            var runs = Path.Combine(stage, "runs"); Directory.CreateDirectory(runs);
            if (!missingRunFile) File.WriteAllText(Path.Combine(runs, "same-key.json"), JsonSerializer.Serialize(run));
        }

        public async Task<ManualJob> WaitForTerminal(ManualRunManager? manager = null)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!deadline.IsCancellationRequested)
            {
                var job = (manager ?? Manager).List().OrderByDescending(j => j.CreatedAt).First();
                if (job.FinishedAt is not null && job.PublicationStatus is "PUBLISHED" or "NO_BUNDLE" or "UNCERTAIN")
                    return job;
                await Task.Delay(20, deadline.Token);
            }
            throw new TimeoutException("Synthetic child did not finish.");
        }

        public void Dispose()
        {
            Manager.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FakeRunner : IManualRunner
    {
        private readonly TaskCompletionSource<ManualProcessResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _launch = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Launches { get; private set; }
        public bool SawFlushedStartingJournal { get; private set; }
        public Task WaitForLaunch() => _launch.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public void Complete(ManualProcessResult result) => _completion.TrySetResult(result);
        public async Task<ManualProcessResult> RunAsync(ManualRunInvocation invocation,
            Action<int, DateTimeOffset> started, CancellationToken cancellationToken)
        {
            Launches++;
            var root = Directory.GetParent(invocation.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"])!.Parent!.FullName;
            var journal = Path.Combine(root, ".jobs", invocation.JobId + ".json");
            SawFlushedStartingJournal = File.Exists(journal) &&
                JsonSerializer.Deserialize<ManualJob>(File.ReadAllText(journal))?.ProcessStatus == "STARTING";
            if (!SawFlushedStartingJournal) throw new InvalidDataException("Fake child saw no durable pre-spawn journal.");
            started(0, DateTimeOffset.UtcNow);
            _launch.TrySetResult(true);
            return await _completion.Task.WaitAsync(cancellationToken);
        }
    }
}
