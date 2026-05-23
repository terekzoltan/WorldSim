using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class Wave9CampaignSupplyEvidenceTests
{
    [Fact]
    public void RunResults_ContainWave9TelemetryBlock()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "901",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("army-supply", "army_supply_depletion", ticks: 8)
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        AssertWave9BlockShape(run.GetProperty("wave9"));

        var perRunPath = Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json").Single();
        using var perRun = ReadJson(perRunPath);
        AssertWave9BlockShape(perRun.RootElement.GetProperty("wave9"));
        Assert.Equal("deterministic_probe", run.GetProperty("wave9").GetProperty("evidenceKind").GetString());
        Assert.Equal("not_tick_sampled", run.GetProperty("wave9").GetProperty("timelineSemantics").GetString());
        Assert.True(run.GetProperty("wave9").GetProperty("outOfSupplyTicks").GetInt32() > 0, $"No out-of-supply evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void Drilldown_TimelineLeavesWave9ProbeCountersDefaultUnlessTickSampled()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "902",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("carrier", "carrier_resupply", ticks: 8),
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "1"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var samples = timeline.RootElement.EnumerateArray().ToArray();
        Assert.True(samples.Length >= 2, $"Expected multiple timeline samples\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.All(samples, sample => AssertWave9TimelineDefault(sample.GetProperty("wave9")));

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var runWave9 = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("wave9");
        Assert.Equal("deterministic_probe", runWave9.GetProperty("evidenceKind").GetString());
        Assert.Equal("not_tick_sampled", runWave9.GetProperty("timelineSemantics").GetString());
        Assert.True(runWave9.GetProperty("memberInventoryConsumed").GetInt32() > 0, $"No run-level carrier evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void Compare_OldBaselineWithoutWave9Block_StillParses()
    {
        var baselineArtifact = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "903",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.Equal(0, baselineExit);

        var oldBaselinePath = RemoveWave9BlocksFromSummary(Path.Combine(baselineArtifact, "summary.json"));

        var compareArtifact = CreateArtifactDir();
        var compareExit = RunScenarioRunner(
            compareArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "903",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = oldBaselinePath
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, compareExit);
        Assert.True(File.Exists(Path.Combine(compareArtifact, "compare.json")), $"Missing compare artifact\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void InvalidWave9Scenario_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("bad-wave9", "not-a-wave9-lane", ticks: 8)
            },
            out _,
            out var stderr);

        Assert.Equal(3, exitCode);
        Assert.Contains("Wave9Scenario", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wave9ScenarioAliasMatrix_NormalizesToCanonicalUnderscoreArtifactValues()
    {
        var artifactDir = CreateArtifactDir();
        var aliases = new (string Name, string Raw, string Canonical, int Ticks, float Dt)[]
        {
            ("army-canonical", "army_supply_depletion", "army_supply_depletion", 8, 0.25f),
            ("army-hyphen", "army-supply-depletion", "army_supply_depletion", 8, 0.25f),
            ("carrier-canonical", "carrier_resupply", "carrier_resupply", 8, 0.25f),
            ("carrier-hyphen", "carrier-resupply", "carrier_resupply", 8, 0.25f),
            ("foraging-canonical", "campaign_foraging", "campaign_foraging", 8, 0.25f),
            ("foraging-closeout", "foraging-extension", "campaign_foraging", 8, 0.25f),
            ("foraging-hyphen", "campaign-foraging", "campaign_foraging", 8, 0.25f),
            ("campaign-canonical", "campaign_assembly_march_encounter", "campaign_assembly_march_encounter", 160, 1f),
            ("campaign-hyphen", "campaign-assembly-march-encounter", "campaign_assembly_march_encounter", 160, 1f)
        };
        var configJson = "[" + string.Join(",", aliases.Select(alias => ConfigObject(alias.Name, alias.Raw, alias.Ticks, alias.Dt))) + "]";
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "904",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var runs = summary.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal(aliases.Length, runs.Length);
        foreach (var alias in aliases)
        {
            var wave9 = Wave9ForConfig(runs, alias.Name);
            Assert.Equal(alias.Canonical, wave9.GetProperty("wave9Scenario").GetString());
            Assert.Equal("deterministic_probe", wave9.GetProperty("evidenceKind").GetString());
            Assert.Equal("not_tick_sampled", wave9.GetProperty("timelineSemantics").GetString());
        }

        var campaign = Wave9ForConfig(runs, "campaign-hyphen");
        Assert.True(campaign.GetProperty("campaignLaunches").GetInt32() > 0, $"No campaign launch evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(campaign.GetProperty("assemblyCompletedCount").GetInt32() > 0, $"No assembly evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(campaign.GetProperty("campaignRouteProgress").GetInt32() > 0, $"No march evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(campaign.GetProperty("campaignEncounterCount").GetInt32() > 0, $"No encounter evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void DeterministicWave9Lanes_ProduceDedicatedNonGenericEvidence()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[" + string.Join(",", new[]
        {
            ConfigObject("army-supply", "army_supply_depletion", ticks: 8),
            ConfigObject("carrier", "carrier_resupply", ticks: 8),
            ConfigObject("foraging", "campaign_foraging", ticks: 8),
            ConfigObject("campaign", "campaign_assembly_march_encounter", ticks: 160, dt: 1f)
        }) + "]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "905",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var runs = summary.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal(4, runs.Length);

        var armySupply = Wave9For(runs, "army_supply_depletion");
        AssertDeterministicProbeMetadata(armySupply);
        Assert.True(armySupply.GetProperty("outOfSupplyTicks").GetInt32() > 0, $"No army supply depletion evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(armySupply.GetProperty("supplyAttritionEvents").GetInt32() > 0);

        var carrier = Wave9For(runs, "carrier_resupply");
        AssertDeterministicProbeMetadata(carrier);
        Assert.True(carrier.GetProperty("carrierAssignments").GetInt32() > 0);
        Assert.True(carrier.GetProperty("carrierSupplyApplications").GetInt32() > 0);
        Assert.Equal(carrier.GetProperty("carrierSupplyApplications").GetInt32(), carrier.GetProperty("carrierDeliveries").GetInt32());
        Assert.True(carrier.GetProperty("memberInventoryConsumed").GetInt32() > 0);
        Assert.True(carrier.GetProperty("rationPoolConsumed").GetInt32() > 0);

        var foraging = Wave9For(runs, "campaign_foraging");
        AssertDeterministicProbeMetadata(foraging);
        Assert.True(foraging.GetProperty("campaignForageAttempts").GetInt32() > 0);
        Assert.True(foraging.GetProperty("campaignForageSuccesses").GetInt32() > 0);
        Assert.True(foraging.GetProperty("campaignForageFoodGained").GetInt32() > 0);
        Assert.True(foraging.GetProperty("campaignForageCapReached").GetInt32() > 0);

        var campaign = Wave9For(runs, "campaign_assembly_march_encounter");
        AssertDeterministicProbeMetadata(campaign);
        Assert.True(campaign.GetProperty("campaignLaunches").GetInt32() > 0);
        Assert.True(campaign.GetProperty("assemblyStartedCount").GetInt32() > 0);
        Assert.True(campaign.GetProperty("marchStartedCount").GetInt32() > 0);
        Assert.True(campaign.GetProperty("campaignEncounterCount").GetInt32() > 0);
    }

    private static JsonElement Wave9For(JsonElement[] runs, string scenario)
        => runs.Select(run => run.GetProperty("wave9"))
            .Single(wave9 => string.Equals(wave9.GetProperty("wave9Scenario").GetString(), scenario, StringComparison.Ordinal));

    private static JsonElement Wave9ForConfig(JsonElement[] runs, string configName)
        => runs.Single(run => string.Equals(run.GetProperty("configName").GetString(), configName, StringComparison.Ordinal))
            .GetProperty("wave9");

    private static void AssertWave9BlockShape(JsonElement wave9)
    {
        Assert.True(wave9.TryGetProperty("wave9Scenario", out _));
        Assert.True(wave9.TryGetProperty("evidenceKind", out _));
        Assert.True(wave9.TryGetProperty("timelineSemantics", out _));
        Assert.True(wave9.TryGetProperty("activeCampaigns", out _));
        Assert.True(wave9.TryGetProperty("activeArmies", out _));
        Assert.True(wave9.TryGetProperty("totalArmyMembers", out _));
        Assert.True(wave9.TryGetProperty("supplySourceMode", out _));
        Assert.True(wave9.TryGetProperty("memberInventoryConsumed", out _));
        Assert.True(wave9.TryGetProperty("rationPoolConsumed", out _));
        Assert.True(wave9.TryGetProperty("carrierAssignments", out _));
        Assert.True(wave9.TryGetProperty("carrierDeliveries", out _));
        Assert.True(wave9.TryGetProperty("carrierSupplyApplications", out _));
        Assert.True(wave9.TryGetProperty("campaignForageAttempts", out _));
        Assert.True(wave9.TryGetProperty("campaignForageFoodGained", out _));
        Assert.True(wave9.TryGetProperty("campaignAssemblingTicks", out _));
        Assert.True(wave9.TryGetProperty("campaignMarchingTicks", out _));
        Assert.True(wave9.TryGetProperty("campaignEncounterTicks", out _));
        Assert.True(wave9.TryGetProperty("campaignRouteProgress", out _));
        Assert.True(wave9.TryGetProperty("campaignEncounterCount", out _));
    }

    private static void AssertDeterministicProbeMetadata(JsonElement wave9)
    {
        Assert.Equal("deterministic_probe", wave9.GetProperty("evidenceKind").GetString());
        Assert.Equal("not_tick_sampled", wave9.GetProperty("timelineSemantics").GetString());
    }

    private static void AssertWave9TimelineDefault(JsonElement wave9)
    {
        Assert.Equal("none", wave9.GetProperty("wave9Scenario").GetString());
        Assert.Equal("not_configured", wave9.GetProperty("evidenceKind").GetString());
        Assert.Equal("not_sampled", wave9.GetProperty("timelineSemantics").GetString());
        Assert.Equal("none", wave9.GetProperty("supplySourceMode").GetString());
        Assert.Equal(0, wave9.GetProperty("memberInventoryConsumed").GetInt32());
        Assert.Equal(0, wave9.GetProperty("rationPoolConsumed").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignForageAttempts").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignForageFoodGained").GetInt32());
        Assert.Equal("none", wave9.GetProperty("campaignPhase").GetString());
        Assert.Equal(0, wave9.GetProperty("campaignAssemblingTicks").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignMarchingTicks").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignEncounterTicks").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignRouteProgress").GetInt32());
        Assert.Equal(0, wave9.GetProperty("campaignEncounterCount").GetInt32());
    }

    private static string RemoveWave9BlocksFromSummary(string summaryPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
            run.Remove("wave9");

        var patchedPath = Path.Combine(Path.GetTempPath(), $"worldsim-baseline-no-wave9-{Guid.NewGuid():N}.json");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
    }

    private static string ConfigJson(string name, string wave9Scenario, int ticks, float dt = 0.25f)
        => "[" + ConfigObject(name, wave9Scenario, ticks, dt) + "]";

    private static string ConfigObject(string name, string wave9Scenario, int ticks, float dt = 0.25f)
        => $$"""
           {"Name":"{{name}}","Width":32,"Height":32,"InitialPop":24,"Ticks":{{ticks}},"Dt":{{dt.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"EnableCombatPrimitives":false,"EnableDiplomacy":false,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave9Scenario":"{{wave9Scenario}}"}
           """;

    private static int RunScenarioRunner(
        string artifactDir,
        IReadOnlyDictionary<string, string> env,
        out string stdout,
        out string stderr)
    {
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "WorldSim.ScenarioRunner", "WorldSim.ScenarioRunner.csproj");
        var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["WORLDSIM_SCENARIO_TICKS"] = "8";
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_MODE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_ASSERT");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_COMPARE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_PERF_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DELTA_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_BASELINE_PATH");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DRILLDOWN");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DRILLDOWN_TOP");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_SAMPLE_EVERY");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_CONFIGS_JSON");
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_LANE");

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);
        stdout = result.Stdout;
        stderr = result.Stderr;
        return result.ExitCode;
    }

    private static JsonDocument ReadJson(string path)
        => JsonDocument.Parse(File.ReadAllText(path));

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-wave9-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "WorldSim.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate WorldSim.sln");
    }
}
