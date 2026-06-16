using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class Wave10CampaignEvidenceTests
{
    [Fact]
    public void RunResults_ContainNonNullDefaultSafeWave10Block()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "1001",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_TICKS"] = "8"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        var wave10 = run.GetProperty("wave10");
        Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
        Assert.Equal("not_configured", wave10.GetProperty("proofType").GetString());
        Assert.Equal("not_applicable", wave10.GetProperty("evidenceStatus").GetString());
        Assert.Equal("not_sampled", wave10.GetProperty("timelineSemantics").GetString());
        Assert.Equal("main_world_run_has_no_campaign_runtime", wave10.GetProperty("reasonCode").GetString());
        Assert.Empty(wave10.GetProperty("nonClaims").EnumerateArray());
        Assert.Equal(0, wave10.GetProperty("campaignLaunches").GetInt32());

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.False(manifest.RootElement.GetProperty("wave10Enabled").GetBoolean());
        Assert.Equal(0, manifest.RootElement.GetProperty("wave10RunCount").GetInt32());
        Assert.Empty(manifest.RootElement.GetProperty("wave10LaneNames").EnumerateArray());
        Assert.Empty(manifest.RootElement.GetProperty("wave10ProofTypes").EnumerateArray());
    }

    [Fact]
    public void Wave10ProbeEvidence_IsSeparatedFromNormalRunsAndDrivesManifest()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[" + string.Join(",", new[]
        {
            ConfigObject("manual", "manual-operator-launch", ticks: 8),
            ConfigObject("organic", "organic_campaign_launch", ticks: 8),
            ConfigObject("siege", "siege-unit-breach", ticks: 8),
            ConfigObject("multi", "multi-front-bounded", ticks: 8),
            ConfigObject("resolution", "campaign-siege-resolution", ticks: 8),
            ConfigObject("supply", "supply-line-convoy", ticks: 8),
            ConfigObject("forward", "forward-base-long-campaign", ticks: 8),
            ConfigObject("scout", "scout-intel-campaign-choice", ticks: 8)
        }) + "]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "1002",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var runs = summary.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal(8, runs.Length);

        foreach (var run in runs)
        {
            var wave10 = run.GetProperty("wave10");
            AssertWave10BlockShape(wave10);
            Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
            Assert.Equal("not_configured", wave10.GetProperty("proofType").GetString());
            Assert.Equal("not_applicable", wave10.GetProperty("evidenceStatus").GetString());
            Assert.Equal(0, wave10.GetProperty("campaignLaunches").GetInt32());
            Assert.Equal(0, wave10.GetProperty("siegeUnitsSpawned").GetInt32());
        }

        var probesPath = Path.Combine(artifactDir, "wave10-probes.json");
        Assert.True(File.Exists(probesPath), $"Missing wave10-probes.json\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var probes = ReadJson(probesPath);
        var probeItems = probes.RootElement.EnumerateArray().ToArray();
        Assert.Equal(8, probeItems.Length);

        var requiredLanes = new[]
        {
            "manual_operator_launch",
            "organic_campaign_launch",
            "campaign_siege_resolution",
            "supply_line_convoy",
            "forward_base_long_campaign",
            "scout_intel_campaign_choice",
            "siege_unit_breach",
            "multi_front_bounded"
        };
        var probeLanes = probeItems.Select(probe => probe.GetProperty("wave10Scenario").GetString()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Assert.Equal(requiredLanes.OrderBy(value => value, StringComparer.Ordinal).ToArray(), probeLanes);

        var manual = ProbeFor(probeItems, "manual_operator_launch").GetProperty("telemetry");
        Assert.Equal("manual_operator", manual.GetProperty("proofType").GetString());
        Assert.Equal("positive", manual.GetProperty("evidenceStatus").GetString());
        Assert.True(manual.GetProperty("campaignLaunches").GetInt32() > 0, $"No manual launch evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var multi = ProbeFor(probeItems, "multi_front_bounded").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", multi.GetProperty("proofType").GetString());
        Assert.Equal("positive", multi.GetProperty("evidenceStatus").GetString());
        Assert.True(multi.GetProperty("activeCampaigns").GetInt32() > 1, $"No active multi-front proof\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(multi.GetProperty("maxActiveCampaignsForAnyFaction").GetInt32() > 1, $"Expected active multi-front owner proof\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.Contains("not organic launch proof", string.Join(" | ", multi.GetProperty("nonClaims").EnumerateArray().Select(value => value.GetString())));

        var organic = ProbeFor(probeItems, "organic_campaign_launch").GetProperty("telemetry");
        Assert.Equal("organic", organic.GetProperty("proofType").GetString());
        AssertPositive(organic);
        Assert.True(organic.GetProperty("campaignLaunches").GetInt32() > 0, $"No organic launch evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var resolution = ProbeFor(probeItems, "campaign_siege_resolution").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", resolution.GetProperty("proofType").GetString());
        AssertPositive(resolution);
        Assert.True(resolution.GetProperty("campaignSiegesEntered").GetInt32() > 0, $"No campaign siege entry evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(resolution.GetProperty("resolvedCampaigns").GetInt32() > 0 || resolution.GetProperty("siegePressureTicks").GetInt32() > 0, $"No campaign siege pressure/resolution evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var supply = ProbeFor(probeItems, "supply_line_convoy").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", supply.GetProperty("proofType").GetString());
        AssertPositive(supply);
        Assert.True(supply.GetProperty("convoysSpawned").GetInt32() > 0, $"No convoy spawn evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(HasAnyConvoyOutcome(supply), $"No convoy request-bound outcome evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var forward = ProbeFor(probeItems, "forward_base_long_campaign").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", forward.GetProperty("proofType").GetString());
        AssertPositive(forward);
        Assert.True(forward.GetProperty("forwardBasesEstablished").GetInt32() > 0, $"No forward-base establishment evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(
            forward.GetProperty("forwardBaseRestTicks").GetInt32() > 0
            || forward.GetProperty("forwardBasesExpired").GetInt32() > 0
            || forward.GetProperty("forwardBasesAbandoned").GetInt32() > 0,
            $"No forward-base lifecycle evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var scout = ProbeFor(probeItems, "scout_intel_campaign_choice").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", scout.GetProperty("proofType").GetString());
        AssertPositive(scout);
        Assert.True(scout.GetProperty("scoutIntelObserved").GetInt32() > 0, $"No scout-intel observation evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(scout.GetProperty("freshScoutIntel").GetInt32() > 0, $"No fresh scout-intel evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(scout.GetProperty("campaignTargetsWithScoutIntel").GetInt32() > 0, $"No campaign target-with-intel evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var siegeUnit = ProbeFor(probeItems, "siege_unit_breach").GetProperty("telemetry");
        Assert.Equal("deterministic_probe", siegeUnit.GetProperty("proofType").GetString());
        AssertPositive(siegeUnit);
        Assert.True(siegeUnit.GetProperty("siegeUnitsSpawned").GetInt32() > 0, $"No siege-unit spawn evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(siegeUnit.GetProperty("siegeUnitActionTicks").GetInt32() > 0 || siegeUnit.GetProperty("siegePressureTicks").GetInt32() > 0, $"No siege-unit action evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        foreach (var probe in probeItems)
        {
            Assert.False(string.IsNullOrWhiteSpace(probe.GetProperty("probeKey").GetString()));
            Assert.Equal("simulation_runtime_probe", probe.GetProperty("runtimeSource").GetString());
            var telemetry = probe.GetProperty("telemetry");
            Assert.Equal("simulation_runtime_probe", telemetry.GetProperty("runtimeSource").GetString());
            if (telemetry.GetProperty("evidenceStatus").GetString() == "proof_unavailable")
            {
                Assert.NotEmpty(telemetry.GetProperty("nonClaims").EnumerateArray());
                Assert.NotEqual("lane_not_configured", telemetry.GetProperty("reasonCode").GetString());
            }
        }

        var summaryProofTypes = probeItems
            .Select(probe => probe.GetProperty("telemetry").GetProperty("proofType").GetString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        var manifestProofTypes = manifest.RootElement.GetProperty("wave10ProofTypes")
            .EnumerateArray()
            .Select(value => value.GetString())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(summaryProofTypes, manifestProofTypes);
        Assert.Equal(8, manifest.RootElement.GetProperty("wave10RunCount").GetInt32());

        var summaryProbe = summary.RootElement.GetProperty("wave10ProbeEvidence");
        Assert.True(summaryProbe.GetProperty("enabled").GetBoolean());
        Assert.Equal("wave10-probes.json", summaryProbe.GetProperty("artifactFile").GetString());
        Assert.Equal(8, summaryProbe.GetProperty("probeCount").GetInt32());

        foreach (var runPath in Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json"))
        {
            using var perRun = ReadJson(runPath);
            var wave10 = perRun.RootElement.GetProperty("wave10");
            AssertWave10BlockShape(wave10);
            Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
            Assert.Equal("not_configured", wave10.GetProperty("proofType").GetString());
            Assert.Equal(0, wave10.GetProperty("campaignLaunches").GetInt32());
        }
    }

    [Fact]
    public void Wave10AssertEnabledCloseoutProfile_UsesStableMainWorldRunSettings()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_MODE"] = "assert",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("forward", "forward-base-long-campaign", ticks: 8)
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        Assert.Equal(64, run.GetProperty("width").GetInt32());
        Assert.Equal(40, run.GetProperty("height").GetInt32());
        Assert.Equal(24, run.GetProperty("initialPop").GetInt32());
        Assert.Equal(300, run.GetProperty("ticks").GetInt32());
        Assert.False(run.GetProperty("enableCombatPrimitives").GetBoolean());
        Assert.False(run.GetProperty("enableDiplomacy").GetBoolean());
        Assert.Equal(1, run.GetProperty("birthRateMultiplier").GetSingle());

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(0, manifest.RootElement.GetProperty("assertionFailures").GetInt32());

        using var probes = ReadJson(Path.Combine(artifactDir, "wave10-probes.json"));
        var forward = probes.RootElement.EnumerateArray().Single().GetProperty("telemetry");
        Assert.Equal("positive", forward.GetProperty("evidenceStatus").GetString());
    }

    [Fact]
    public void Wave10MultiFrontBounded_Goap303_AssertEnabled_IsNotProofUnavailable()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_MODE"] = "assert",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_SEEDS"] = "303",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "goap",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("multi", "multi-front-bounded", ticks: 8)
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var probeSummary = summary.RootElement.GetProperty("wave10ProbeEvidence");
        Assert.Empty(probeSummary.GetProperty("unavailableLaneNames").EnumerateArray());

        using var probes = ReadJson(Path.Combine(artifactDir, "wave10-probes.json"));
        var telemetry = probes.RootElement.EnumerateArray().Single().GetProperty("telemetry");
        Assert.Equal("positive", telemetry.GetProperty("evidenceStatus").GetString());
        Assert.True(
            telemetry.GetProperty("campaignLaunchBlockedByCap").GetInt32() > 0
            || telemetry.GetProperty("campaignLaunchBlockedByPairCap").GetInt32() > 0
            || telemetry.GetProperty("maxActiveCampaignsForAnyFaction").GetInt32() > 1,
            $"Expected bounded multi-front proof\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void Wave10ManualOperatorLifecycle_ExportsRuntimeBackedMainRunTelemetryAndSampledTimeline()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = LifecycleConfigJson("manual-lifecycle", "manual-operator-campaign-lifecycle", ticks: 80, manualLaunchTick: 1),
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "20"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(artifactDir, "wave10-probes.json")), "Lifecycle main-run truth must not be exported as side-probe evidence.");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        var wave10 = run.GetProperty("wave10");
        AssertWave10BlockShape(wave10);
        Assert.Equal("manual_operator_campaign_lifecycle", wave10.GetProperty("wave10Scenario").GetString());
        Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
        Assert.Equal("manual_operator", wave10.GetProperty("proofType").GetString());
        Assert.Equal("tick_sampled", wave10.GetProperty("timelineSemantics").GetString());
        Assert.Equal(1, wave10.GetProperty("manualLaunchAttemptTick").GetInt64());
        Assert.True(wave10.GetProperty("manualLaunchAttempted").GetBoolean());
        Assert.True(wave10.GetProperty("manualLaunchSucceeded").GetBoolean(), $"Manual lifecycle launch failed\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.Equal("Created", wave10.GetProperty("manualLaunchStatus").GetString());
        Assert.True(wave10.GetProperty("campaignLaunches").GetInt32() > 0, $"No campaign lifecycle after manual launch\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(wave10.GetProperty("firstCampaignLaunchTick").GetInt64() <= 1);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.True(manifest.RootElement.GetProperty("wave10Enabled").GetBoolean());
        Assert.Equal(1, manifest.RootElement.GetProperty("wave10RunCount").GetInt32());
        Assert.Contains("manual_operator_campaign_lifecycle", manifest.RootElement.GetProperty("wave10LaneNames").EnumerateArray().Select(value => value.GetString()));

        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));
        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var samples = timeline.RootElement.EnumerateArray().ToArray();
        Assert.NotEmpty(samples);
        Assert.All(samples, sample =>
        {
            var sampleWave10 = sample.GetProperty("wave10");
            Assert.Equal("manual_operator_campaign_lifecycle", sampleWave10.GetProperty("wave10Scenario").GetString());
            Assert.Equal("main_world_run", sampleWave10.GetProperty("runtimeSource").GetString());
            Assert.Equal("tick_sampled", sampleWave10.GetProperty("timelineSemantics").GetString());
        });
    }

    [Fact]
    public void Wave10HostileLifecycle_UsesOrganicMainRunTelemetryWithoutProbeArtifact()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = LifecycleConfigJson("hostile-lifecycle", "organic-hostile-campaign-lifecycle", ticks: 80),
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "20"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(artifactDir, "wave10-probes.json")), "Hostile lifecycle is main-run truth, not side-probe evidence.");
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var wave10 = summary.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("wave10");
        Assert.Equal("organic_hostile_campaign_lifecycle", wave10.GetProperty("wave10Scenario").GetString());
        Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
        Assert.Equal("organic", wave10.GetProperty("proofType").GetString());
        Assert.Equal("tick_sampled", wave10.GetProperty("timelineSemantics").GetString());

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.True(manifest.RootElement.GetProperty("wave10Enabled").GetBoolean());
        Assert.Equal(1, manifest.RootElement.GetProperty("wave10RunCount").GetInt32());
        Assert.Contains("organic_hostile_campaign_lifecycle", manifest.RootElement.GetProperty("wave10LaneNames").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void InvalidWave10Scenario_ReturnsConfigError()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("bad-wave10", "not-a-wave10-lane", ticks: 8)
            },
            out _,
            out var stderr);

        Assert.Equal(3, exitCode);
        Assert.Contains("Wave10Scenario", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_OldBaselineWithoutWave10Block_StillParses()
    {
        var baselineArtifact = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "1003",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.Equal(0, baselineExit);

        var oldBaselinePath = RemoveWave10BlocksFromSummary(Path.Combine(baselineArtifact, "summary.json"));

        var compareArtifact = CreateArtifactDir();
        var compareExit = RunScenarioRunner(
            compareArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "1003",
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
    public void Drilldown_TimelineContainsCanonicalWave10Semantics()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "1004",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = ConfigJson("manual", "manual_operator_launch", ticks: 8),
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
        Assert.All(samples, sample =>
        {
            var wave10 = sample.GetProperty("wave10");
            Assert.Equal("main_world_run", wave10.GetProperty("runtimeSource").GetString());
            Assert.Equal("not_configured", wave10.GetProperty("proofType").GetString());
            Assert.Equal("not_sampled", wave10.GetProperty("timelineSemantics").GetString());
            Assert.Equal(0, wave10.GetProperty("activeCampaigns").GetInt32());
        });
    }

    private static JsonElement Wave10ForConfig(JsonElement[] runs, string configName)
        => runs.Single(run => string.Equals(run.GetProperty("configName").GetString(), configName, StringComparison.Ordinal))
            .GetProperty("wave10");

    private static JsonElement ProbeFor(JsonElement[] probes, string scenario)
        => probes.Single(probe => string.Equals(probe.GetProperty("wave10Scenario").GetString(), scenario, StringComparison.Ordinal));

    private static void AssertPositive(JsonElement telemetry)
    {
        Assert.Equal("positive", telemetry.GetProperty("evidenceStatus").GetString());
        Assert.Equal("none", telemetry.GetProperty("reasonCode").GetString());
    }

    private static bool HasAnyConvoyOutcome(JsonElement telemetry)
        => telemetry.GetProperty("convoysDelivered").GetInt32() > 0
           || telemetry.GetProperty("convoysFailed").GetInt32() > 0
           || telemetry.GetProperty("convoyThrottleBlocks").GetInt32() > 0
           || telemetry.GetProperty("convoyCapBlocks").GetInt32() > 0
           || telemetry.GetProperty("convoyHomeDefenseBlocks").GetInt32() > 0
           || telemetry.GetProperty("convoyRouteBudgetExhausted").GetInt32() > 0;

    private static void AssertUnavailableRoute(JsonElement[] probes, string scenario, string reasonCode, string route)
    {
        var telemetry = ProbeFor(probes, scenario).GetProperty("telemetry");
        Assert.Equal("proof_unavailable", telemetry.GetProperty("evidenceStatus").GetString());
        Assert.Equal(reasonCode, telemetry.GetProperty("reasonCode").GetString());
        var nonClaims = telemetry.GetProperty("nonClaims").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        Assert.Contains(nonClaims, value => value.Contains(route, StringComparison.Ordinal));
    }

    private static void AssertWave10BlockShape(JsonElement wave10)
    {
        Assert.True(wave10.TryGetProperty("wave10Scenario", out _));
        Assert.True(wave10.TryGetProperty("runtimeSource", out _));
        Assert.True(wave10.TryGetProperty("proofType", out _));
        Assert.True(wave10.TryGetProperty("evidenceStatus", out _));
        Assert.True(wave10.TryGetProperty("timelineSemantics", out _));
        Assert.True(wave10.TryGetProperty("reasonCode", out _));
        Assert.True(wave10.TryGetProperty("nonClaims", out _));
        Assert.True(wave10.TryGetProperty("campaignLaunches", out _));
        Assert.True(wave10.TryGetProperty("activeCampaigns", out _));
        Assert.True(wave10.TryGetProperty("resolvedCampaigns", out _));
        Assert.True(wave10.TryGetProperty("siegeUnitsSpawned", out _));
        Assert.True(wave10.TryGetProperty("campaignLaunchBlockedByCap", out _));
        Assert.True(wave10.TryGetProperty("campaignLaunchBlockedByPairCap", out _));
        Assert.True(wave10.TryGetProperty("maxActiveCampaignsForAnyFaction", out _));
        Assert.True(wave10.TryGetProperty("firstCampaignLaunchTick", out _));
        Assert.True(wave10.TryGetProperty("firstAssemblyTick", out _));
        Assert.True(wave10.TryGetProperty("firstMarchTick", out _));
        Assert.True(wave10.TryGetProperty("firstEncounterTick", out _));
        Assert.True(wave10.TryGetProperty("firstSiegeTick", out _));
        Assert.True(wave10.TryGetProperty("firstResolutionTick", out _));
        Assert.True(wave10.TryGetProperty("longestUnresolvedCampaignAgeTicks", out _));
        Assert.True(wave10.TryGetProperty("manualLaunchAttemptTick", out _));
        Assert.True(wave10.TryGetProperty("manualLaunchAttempted", out _));
        Assert.True(wave10.TryGetProperty("manualLaunchSucceeded", out _));
        Assert.True(wave10.TryGetProperty("manualLaunchStatus", out _));
    }

    private static string RemoveWave10BlocksFromSummary(string summaryPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
            run.Remove("wave10");
        node.Remove("wave10ProbeEvidence");

        var patchedPath = Path.Combine(Path.GetTempPath(), $"worldsim-baseline-no-wave10-{Guid.NewGuid():N}.json");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
    }

    private static string ConfigJson(string name, string wave10Scenario, int ticks, float dt = 0.25f)
        => "[" + ConfigObject(name, wave10Scenario, ticks, dt) + "]";

    private static string ConfigObject(string name, string wave10Scenario, int ticks, float dt = 0.25f)
        => $$"""
           {"Name":"{{name}}","Width":32,"Height":32,"InitialPop":24,"Ticks":{{ticks}},"Dt":{{dt.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"EnableCombatPrimitives":true,"EnableDiplomacy":true,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave10Scenario":"{{wave10Scenario}}"}
           """;

    private static string LifecycleConfigJson(string name, string wave10Scenario, int ticks, int? manualLaunchTick = null)
    {
        var launchTickJson = manualLaunchTick.HasValue ? $",\"Wave10ManualLaunchTick\":{manualLaunchTick.Value}" : string.Empty;
        return $$"""
            [{"Name":"{{name}}","Width":40,"Height":40,"InitialPop":80,"Ticks":{{ticks}},"Dt":0.25,"EnableCombatPrimitives":true,"EnableDiplomacy":true,"StoneBuildingsEnabled":false,"BirthRateMultiplier":0.0,"MovementSpeedMultiplier":1.0,"EnableSiege":true,"Wave10Scenario":"{{wave10Scenario}}"{{launchTickJson}}}]
            """;
    }

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
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-wave10-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WorldSim.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing WorldSim.sln");
    }
}
