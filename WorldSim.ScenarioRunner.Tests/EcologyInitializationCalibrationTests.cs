using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class EcologyInitializationCalibrationTests : IDisposable
{
    private readonly List<string> _temporaryPaths = new();

    [Fact]
    public void EcologyInitialization_PairedPoliciesExposeRuntimeIdentityAndEffectiveInputs()
    {
        var execution = RunScenarioRunner(BuildPolicyPair("ecology_initialization", ticks: 1, predatorHumanAttacks: false));
        AssertSuccessful(execution);

        using var summary = ReadJson(Path.Combine(execution.ArtifactDir, "summary.json"));
        var runs = ReadRunsByConfig(summary);

        var legacy = runs["ecology_initialization__legacy_random"];
        AssertInitialAnimalIdentity(legacy, "legacy_random", "compare_override", expectedPreferredValues: false);

        var habitat = runs["ecology_initialization__habitat_aware"];
        AssertInitialAnimalIdentity(habitat, "habitat_aware", "runtime_options", expectedPreferredValues: true);
        var initialEcology = habitat.GetProperty("initialEcology");
        Assert.Equal(0, initialEcology.GetProperty("animalsOnWater").GetInt32());
        Assert.Equal(0, initialEcology.GetProperty("animalsOnMovementBlockedTiles").GetInt32());
        Assert.Equal(0, initialEcology.GetProperty("predatorsInPreyEmptyRegions").GetInt32());
    }

    [Fact]
    public void EcologyInitialization_OmittedPolicyPreservesRuntimeDefault()
    {
        var config = BuildConfig("ecology_initialization__runtime_default", ticks: 1, predatorHumanAttacks: false);
        var execution = RunScenarioRunner(SerializeConfigs(config));
        AssertSuccessful(execution);

        using var summary = ReadJson(Path.Combine(execution.ArtifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        var input = run.GetProperty("initialAnimalConfig");
        Assert.Equal(JsonValueKind.Null, input.GetProperty("requestedPolicy").ValueKind);
        Assert.Equal("habitat_aware", input.GetProperty("effectivePolicy").GetString());
        Assert.Equal("runtime_default", input.GetProperty("effectivePolicySource").GetString());
        Assert.Equal("runtime_default", run.GetProperty("initialEcology").GetProperty("initialAnimalPolicySource").GetString());
    }

    [Fact]
    public void EcologyInitialization_ValidOverridesReachRuntimeAndArtifactIdentity()
    {
        var legacy = BuildConfig("ecology_initialization__legacy_overrides", ticks: 1, predatorHumanAttacks: false);
        legacy["InitialAnimalPolicy"] = "legacy_random";
        legacy["InitialAnimalAreaTilesPerAnimal"] = 128;
        var habitat = BuildConfig("ecology_initialization__habitat_overrides", ticks: 1, predatorHumanAttacks: false);
        habitat["InitialAnimalPolicy"] = "habitat_aware";
        habitat["InitialAnimalAreaTilesPerAnimal"] = 128;
        habitat["InitialAnimalPreferredPersonOrColonyDistance"] = 9;
        habitat["InitialAnimalPreferredHerbivoreFoodRadius"] = 4;
        habitat["InitialAnimalPreferredPredatorPreyRadius"] = 8;

        var execution = RunScenarioRunner(SerializeConfigs(legacy, habitat), seeds: "511");
        AssertSuccessful(execution);

        using var summary = ReadJson(Path.Combine(execution.ArtifactDir, "summary.json"));
        var runs = ReadRunsByConfig(summary);
        var legacyInput = runs["ecology_initialization__legacy_overrides"].GetProperty("initialAnimalConfig");
        Assert.Equal(128, legacyInput.GetProperty("areaTilesPerAnimal").GetInt32());
        Assert.Equal(JsonValueKind.Null, legacyInput.GetProperty("preferredPersonOrColonyDistance").ValueKind);
        Assert.Equal(20, runs["ecology_initialization__legacy_overrides"]
            .GetProperty("initialEcology")
            .GetProperty("initialAnimalBudgetCeiling")
            .GetInt32());

        var habitatRun = runs["ecology_initialization__habitat_overrides"];
        var habitatInput = habitatRun.GetProperty("initialAnimalConfig");
        Assert.Equal(128, habitatInput.GetProperty("areaTilesPerAnimal").GetInt32());
        Assert.Equal(9, habitatInput.GetProperty("preferredPersonOrColonyDistance").GetInt32());
        Assert.Equal(4, habitatInput.GetProperty("preferredHerbivoreFoodRadius").GetInt32());
        Assert.Equal(8, habitatInput.GetProperty("preferredPredatorPreyRadius").GetInt32());
        var habitatInitial = habitatRun.GetProperty("initialEcology");
        Assert.Equal(20, habitatInitial.GetProperty("initialAnimalBudgetCeiling").GetInt32());
        Assert.Equal(9, habitatInitial.GetProperty("preferredPersonOrColonyDistance").GetInt32());
        Assert.Equal(4, habitatInitial.GetProperty("preferredHerbivoreFoodRadius").GetInt32());
        Assert.Equal(8, habitatInitial.GetProperty("preferredPredatorPreyRadius").GetInt32());
    }

    [Theory]
    [InlineData(" Legacy_Random ", "legacy_random")]
    [InlineData(" HABITAT_AWARE ", "habitat_aware")]
    public void EcologyInitialization_NormalizedPolicyRemainsDeterministic(string rawPolicy, string expectedPolicy)
    {
        var config = BuildConfig($"ecology_initialization__{expectedPolicy}", ticks: 1, predatorHumanAttacks: false);
        config["InitialAnimalPolicy"] = rawPolicy;
        config["InitialAnimalAreaTilesPerAnimal"] = 256;
        var json = SerializeConfigs(config);

        var first = RunScenarioRunner(json, seeds: "503");
        var second = RunScenarioRunner(json, seeds: "503");
        AssertSuccessful(first);
        AssertSuccessful(second);

        using var firstSummary = ReadJson(Path.Combine(first.ArtifactDir, "summary.json"));
        using var secondSummary = ReadJson(Path.Combine(second.ArtifactDir, "summary.json"));
        var firstRun = firstSummary.RootElement.GetProperty("runs").EnumerateArray().Single();
        var secondRun = secondSummary.RootElement.GetProperty("runs").EnumerateArray().Single();

        Assert.Equal(expectedPolicy, firstRun.GetProperty("initialAnimalConfig").GetProperty("requestedPolicy").GetString());
        Assert.Equal(expectedPolicy, firstRun.GetProperty("initialAnimalConfig").GetProperty("effectivePolicy").GetString());
        Assert.Equal(expectedPolicy, firstRun.GetProperty("initialEcology").GetProperty("initialAnimalPolicy").GetString());
        AssertJsonEqual(firstRun.GetProperty("initialAnimalConfig"), secondRun.GetProperty("initialAnimalConfig"));
        AssertJsonEqual(firstRun.GetProperty("initialEcology"), secondRun.GetProperty("initialEcology"));
        AssertJsonEqual(firstRun.GetProperty("ecology"), secondRun.GetProperty("ecology"));
    }

    [Theory]
    [InlineData("unknown_policy", "InitialAnimalPolicy must be")]
    [InlineData("zero_density", "InitialAnimalAreaTilesPerAnimal must be > 0")]
    [InlineData("negative_radius", "preferred distances and radii must be >= 0")]
    [InlineData("override_without_policy", "require an explicit InitialAnimalPolicy")]
    [InlineData("legacy_radius", "legacy_random does not use preferred")]
    public void EcologyInitialization_InvalidConfigReturnsConfigError(string invalidCase, string expectedMessage)
    {
        var config = BuildInvalidConfig(invalidCase);
        var execution = RunScenarioRunner(SerializeConfigs(config));

        Assert.Equal(3, execution.Process.ExitCode);
        Assert.Contains("config_error", execution.Process.Stdout + execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedMessage, execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoreRunIdentity_DuplicateEffectiveConfigNameFailsBeforeGeneratedArtifacts()
    {
        var first = BuildConfig("duplicate_core_identity", ticks: 1, predatorHumanAttacks: false);
        var second = BuildConfig("duplicate_core_identity", ticks: 1, predatorHumanAttacks: false);

        var execution = RunScenarioRunner(SerializeConfigs(first, second), seeds: "601");

        Assert.Equal(3, execution.Process.ExitCode);
        Assert.Contains("config_error", execution.Process.Stdout + execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate effective scenario config name 'duplicate_core_identity'", execution.Process.Stderr, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(execution.ArtifactDir));
    }

    [Fact]
    public void CoreRunIdentity_DuplicateSeedPreservesCallerOwnedArtifactDirectory()
    {
        const string sentinelName = "caller-owned.sentinel";
        const string sentinelContents = "preserve this caller-owned file";
        var config = BuildConfig("duplicate_seed_identity", ticks: 1, predatorHumanAttacks: false);

        var execution = RunScenarioRunner(
            SerializeConfigs(config),
            seeds: "607,607",
            arrangeArtifactDirectory: artifactDir =>
                File.WriteAllText(Path.Combine(artifactDir, sentinelName), sentinelContents));

        Assert.Equal(3, execution.Process.ExitCode);
        Assert.Contains("config_error", execution.Process.Stdout + execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate scenario seed '607'", execution.Process.Stderr, StringComparison.Ordinal);
        var entries = Directory.EnumerateFileSystemEntries(execution.ArtifactDir).ToArray();
        var sentinelPath = Assert.Single(entries);
        Assert.Equal(Path.Combine(execution.ArtifactDir, sentinelName), sentinelPath);
        Assert.Equal(sentinelContents, File.ReadAllText(sentinelPath));
        Assert.False(File.Exists(Path.Combine(execution.ArtifactDir, "summary.json")));
        Assert.False(File.Exists(Path.Combine(execution.ArtifactDir, "manifest.json")));
        Assert.False(Directory.Exists(Path.Combine(execution.ArtifactDir, "runs")));
        Assert.False(Directory.Exists(Path.Combine(execution.ArtifactDir, "drilldown")));
    }

    [Fact]
    public void Compare_DuplicateBaselineRunIdentityProducesCompleteConfigErrorBundle()
    {
        var config = BuildConfig("duplicate_baseline_identity", ticks: 1, predatorHumanAttacks: false);
        var configJson = SerializeConfigs(config);
        var baseline = RunScenarioRunner(configJson, seeds: "613");
        AssertSuccessful(baseline);

        var baselineNode = JsonNode.Parse(File.ReadAllText(Path.Combine(baseline.ArtifactDir, "summary.json")))?.AsObject()
            ?? throw new InvalidOperationException("Baseline summary is invalid.");
        var baselineRuns = baselineNode["runs"]?.AsArray()
            ?? throw new InvalidOperationException("Baseline summary has no runs.");
        baselineRuns.Add(baselineRuns[0]?.DeepClone());
        var baselinePath = CreateTemporaryFile("worldsim-step5c3-duplicate-baseline");
        File.WriteAllText(baselinePath, baselineNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var current = RunScenarioRunner(
            configJson,
            seeds: "613",
            additionalEnvironment: new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = baselinePath
            });

        var output = current.Process.Stdout + current.Process.Stderr;
        Assert.Equal(3, current.Process.ExitCode);
        Assert.Contains("baseline contains duplicate semantic run identity", current.Process.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("same key", output, StringComparison.OrdinalIgnoreCase);

        using var summary = ReadJson(Path.Combine(current.ArtifactDir, "summary.json"));
        Assert.Single(summary.RootElement.GetProperty("runs").EnumerateArray());
        Assert.Single(Directory.GetFiles(Path.Combine(current.ArtifactDir, "runs"), "*.json"));
        using var manifest = ReadJson(Path.Combine(current.ArtifactDir, "manifest.json"));
        Assert.Equal(3, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.False(File.Exists(Path.Combine(current.ArtifactDir, "compare.json")));
    }

    [Fact]
    public void EcologyInitialization_RuntimeBackedLifecycleRejectsInitialAnimalOverrides()
    {
        var config = BuildConfig("lifecycle_initial_policy_rejected", ticks: 8, predatorHumanAttacks: true);
        config["Wave10Scenario"] = "organic-campaign-lifecycle";
        config["InitialAnimalPolicy"] = "habitat_aware";
        var execution = RunScenarioRunner(SerializeConfigs(config));

        Assert.Equal(3, execution.Process.ExitCode);
        Assert.Contains("config_error", execution.Process.Stdout + execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime-backed Wave10 lifecycle", execution.Process.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void EcologyInitialization_NonCoreLaneRejectsInitialAnimalOverrides()
    {
        var config = BuildConfig("refinery_initial_policy_rejected", ticks: 1, predatorHumanAttacks: false);
        config["InitialAnimalPolicy"] = "legacy_random";
        var execution = RunScenarioRunner(SerializeConfigs(config), lane: "refinery_fixture");

        Assert.Equal(3, execution.Process.ExitCode);
        Assert.Contains("config_error", execution.Process.Stdout + execution.Process.Stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supported only on WORLDSIM_SCENARIO_LANE=core", execution.Process.Stderr, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(execution.ArtifactDir));
    }

    [Fact]
    public void EcologyInitialization_OldBaselineWithoutInitialAnimalConfigStillParses()
    {
        var config = BuildConfig("ecology_initialization__baseline_compat", ticks: 1, predatorHumanAttacks: false);
        config["InitialAnimalPolicy"] = "habitat_aware";
        var configJson = SerializeConfigs(config);
        var baseline = RunScenarioRunner(configJson, seeds: "509");
        AssertSuccessful(baseline);

        var baselineNode = JsonNode.Parse(File.ReadAllText(Path.Combine(baseline.ArtifactDir, "summary.json")))?.AsObject()
            ?? throw new InvalidOperationException("Baseline summary is invalid.");
        foreach (var run in baselineNode["runs"]?.AsArray().OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            run.Remove("initialAnimalConfig");
        var baselinePath = CreateTemporaryFile("worldsim-step5c3-baseline");
        File.WriteAllText(baselinePath, baselineNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var current = RunScenarioRunner(
            configJson,
            seeds: "509",
            additionalEnvironment: new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = baselinePath
            });
        AssertSuccessful(current);

        using var compare = ReadJson(Path.Combine(current.ArtifactDir, "compare.json"));
        Assert.Equal(1, compare.RootElement.GetProperty("matchedRunCount").GetInt32());
        Assert.Equal(0, compare.RootElement.GetProperty("totalFailureCount").GetInt32());
    }

    [Fact]
    public void EcologyEarlyContact_PolicyPairExportsEventsAndAllSixDrilldowns()
    {
        var execution = RunScenarioRunner(
            BuildPolicyPair("ecology_early_contact", ticks: 100, predatorHumanAttacks: true),
            seeds: "101,202,303",
            additionalEnvironment: new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "6",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "25"
            });
        AssertSuccessful(execution);

        using var summary = ReadJson(Path.Combine(execution.ArtifactDir, "summary.json"));
        var runs = summary.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal(6, runs.Length);
        var semanticIdentities = runs
            .Select(run => $"{run.GetProperty("configName").GetString()}|{run.GetProperty("plannerMode").GetString()}|{run.GetProperty("visualLane").GetString()}|{run.GetProperty("seed").GetInt32()}")
            .ToArray();
        Assert.Equal(6, semanticIdentities.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(6, Directory.GetFiles(Path.Combine(execution.ArtifactDir, "runs"), "*.json").Length);
        foreach (var seedGroup in runs.GroupBy(run => run.GetProperty("seed").GetInt32()))
        {
            Assert.Equal(2, seedGroup.Count());
            var pair = seedGroup.OrderBy(run => run.GetProperty("configName").GetString(), StringComparer.Ordinal).ToArray();
            AssertPairedRunIdentity(pair[0], pair[1]);
        }

        foreach (var run in runs)
        {
            var ecology = run.GetProperty("ecology");
            Assert.Equal("disabled", ecology.GetProperty("emergencyRescuePolicy").GetString());
            Assert.Equal(0, ecology.GetProperty("emergencyRescues").GetInt32());
            AssertNullableNumber(ecology, "firstPredatorHumanContactTick");
            AssertNullableNumber(ecology, "firstPredatorHuntTick");
            AssertNullableNumber(ecology, "firstHerbivoreGrazingTick");
            AssertNullableNumber(ecology, "firstPredatorDeathTick");
            AssertNullableNumber(ecology, "firstHerbivoreDeathTick");
            AssertNullableNumber(ecology, "firstPredatorBirthTick");
            AssertNullableNumber(ecology, "firstHerbivoreBirthTick");
        }

        using var manifest = ReadJson(Path.Combine(execution.ArtifactDir, "manifest.json"));
        Assert.Equal(6, manifest.RootElement.GetProperty("drilldownSelectedRuns").GetInt32());
        using var index = ReadJson(Path.Combine(execution.ArtifactDir, "drilldown", "index.json"));
        var runKeys = index.RootElement.GetProperty("runs")
            .EnumerateArray()
            .Select(run => run.GetProperty("runKey").GetString())
            .Where(runKey => runKey is not null)
            .Cast<string>()
            .ToArray();
        Assert.Equal(6, runKeys.Length);
        Assert.Equal(6, runKeys.Distinct(StringComparer.Ordinal).Count());
        foreach (var runKey in runKeys)
        {
            var drilldownDir = Path.Combine(execution.ArtifactDir, "drilldown", runKey);
            Assert.True(File.Exists(Path.Combine(drilldownDir, "timeline.json")), $"Missing timeline for {runKey}.");
            Assert.True(File.Exists(Path.Combine(drilldownDir, "replay.json")), $"Missing replay for {runKey}.");
            using var timeline = ReadJson(Path.Combine(drilldownDir, "timeline.json"));
            Assert.NotEmpty(timeline.RootElement.EnumerateArray());
        }
    }

    private static void AssertInitialAnimalIdentity(
        JsonElement run,
        string expectedPolicy,
        string expectedSource,
        bool expectedPreferredValues)
    {
        var input = run.GetProperty("initialAnimalConfig");
        Assert.Equal(expectedPolicy, input.GetProperty("requestedPolicy").GetString());
        Assert.Equal(expectedPolicy, input.GetProperty("effectivePolicy").GetString());
        Assert.Equal(expectedSource, input.GetProperty("effectivePolicySource").GetString());
        Assert.Equal(256, input.GetProperty("areaTilesPerAnimal").GetInt32());

        var initialEcology = run.GetProperty("initialEcology");
        Assert.Equal(expectedPolicy, initialEcology.GetProperty("initialAnimalPolicy").GetString());
        Assert.Equal(expectedSource, initialEcology.GetProperty("initialAnimalPolicySource").GetString());
        if (expectedPreferredValues)
        {
            Assert.Equal(7, input.GetProperty("preferredPersonOrColonyDistance").GetInt32());
            Assert.Equal(5, input.GetProperty("preferredHerbivoreFoodRadius").GetInt32());
            Assert.Equal(6, input.GetProperty("preferredPredatorPreyRadius").GetInt32());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, input.GetProperty("preferredPersonOrColonyDistance").ValueKind);
            Assert.Equal(JsonValueKind.Null, input.GetProperty("preferredHerbivoreFoodRadius").ValueKind);
            Assert.Equal(JsonValueKind.Null, input.GetProperty("preferredPredatorPreyRadius").ValueKind);
        }
    }

    private static void AssertPairedRunIdentity(JsonElement first, JsonElement second)
    {
        var equalProperties = new[]
        {
            "plannerMode", "seed", "visualLane", "width", "height", "initialPop", "ticks", "dt",
            "enableCombatPrimitives", "enableDiplomacy", "enableSiege", "stoneBuildingsEnabled",
            "birthRateMultiplier", "movementSpeedMultiplier", "enablePredatorHumanAttacks",
            "allowEmergencyRescueInAcceptance", "ecologyBalance"
        };
        foreach (var property in equalProperties)
            Assert.Equal(first.GetProperty(property).GetRawText(), second.GetProperty(property).GetRawText());

        Assert.NotEqual(first.GetProperty("configName").GetString(), second.GetProperty("configName").GetString());
        Assert.NotEqual(
            first.GetProperty("initialAnimalConfig").GetProperty("effectivePolicy").GetString(),
            second.GetProperty("initialAnimalConfig").GetProperty("effectivePolicy").GetString());
    }

    private static void AssertNullableNumber(JsonElement owner, string propertyName)
    {
        var value = owner.GetProperty(propertyName);
        Assert.True(
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Number,
            $"Expected {propertyName} to be null or number, got {value.ValueKind}.");
    }

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual)
    {
        var expectedNode = JsonNode.Parse(expected.GetRawText());
        var actualNode = JsonNode.Parse(actual.GetRawText());
        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode));
    }

    private static Dictionary<string, JsonElement> ReadRunsByConfig(JsonDocument summary)
        => summary.RootElement.GetProperty("runs")
            .EnumerateArray()
            .ToDictionary(
                run => run.GetProperty("configName").GetString()!,
                run => run.Clone(),
                StringComparer.Ordinal);

    private static JsonObject BuildInvalidConfig(string invalidCase)
    {
        var config = BuildConfig($"invalid_{invalidCase}", ticks: 1, predatorHumanAttacks: false);
        switch (invalidCase)
        {
            case "unknown_policy":
                config["InitialAnimalPolicy"] = "not_a_policy";
                break;
            case "zero_density":
                config["InitialAnimalPolicy"] = "habitat_aware";
                config["InitialAnimalAreaTilesPerAnimal"] = 0;
                break;
            case "negative_radius":
                config["InitialAnimalPolicy"] = "habitat_aware";
                config["InitialAnimalPreferredPredatorPreyRadius"] = -1;
                break;
            case "override_without_policy":
                config["InitialAnimalAreaTilesPerAnimal"] = 256;
                break;
            case "legacy_radius":
                config["InitialAnimalPolicy"] = "legacy_random";
                config["InitialAnimalPreferredHerbivoreFoodRadius"] = 5;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidCase), invalidCase, "Unknown invalid config case.");
        }

        return config;
    }

    private static string BuildPolicyPair(string prefix, int ticks, bool predatorHumanAttacks)
    {
        var legacy = BuildConfig($"{prefix}__legacy_random", ticks, predatorHumanAttacks);
        legacy["InitialAnimalPolicy"] = "legacy_random";
        legacy["InitialAnimalAreaTilesPerAnimal"] = 256;
        var habitat = BuildConfig($"{prefix}__habitat_aware", ticks, predatorHumanAttacks);
        habitat["InitialAnimalPolicy"] = "habitat_aware";
        habitat["InitialAnimalAreaTilesPerAnimal"] = 256;
        return SerializeConfigs(legacy, habitat);
    }

    private static JsonObject BuildConfig(string name, int ticks, bool predatorHumanAttacks)
        => new()
        {
            ["Name"] = name,
            ["Width"] = 64,
            ["Height"] = 40,
            ["InitialPop"] = 24,
            ["Ticks"] = ticks,
            ["Dt"] = 0.25,
            ["EnableCombatPrimitives"] = false,
            ["EnableDiplomacy"] = false,
            ["EnableSiege"] = true,
            ["StoneBuildingsEnabled"] = false,
            ["BirthRateMultiplier"] = 1.0,
            ["MovementSpeedMultiplier"] = 1.0,
            ["EnablePredatorHumanAttacks"] = predatorHumanAttacks,
            ["EmergencyRescuePolicy"] = "disabled"
        };

    private static string SerializeConfigs(params JsonObject[] configs)
    {
        var array = new JsonArray();
        foreach (var config in configs)
            array.Add(config);
        return array.ToJsonString();
    }

    private ScenarioExecution RunScenarioRunner(
        string configsJson,
        string seeds = "501",
        string lane = "core",
        IReadOnlyDictionary<string, string>? additionalEnvironment = null,
        Action<string>? arrangeArtifactDirectory = null)
    {
        var artifactDir = CreateArtifactDir();
        arrangeArtifactDirectory?.Invoke(artifactDir);
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "WorldSim.ScenarioRunner", "WorldSim.ScenarioRunner.csproj");
        var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var key in startInfo.Environment.Keys
                     .Where(key => key.StartsWith("WORLDSIM_SCENARIO_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(key);
        }
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        startInfo.Environment["WORLDSIM_SCENARIO_LANE"] = lane;
        startInfo.Environment["WORLDSIM_SCENARIO_MODE"] = "standard";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment["WORLDSIM_VISUAL_PROFILE"] = "Headless";
        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = seeds;
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configsJson;
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        if (additionalEnvironment is not null)
        {
            foreach (var pair in additionalEnvironment)
                startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = ScenarioRunnerProcess.Run(startInfo, artifactDir);
        return new ScenarioExecution(artifactDir, process);
    }

    private static void AssertSuccessful(ScenarioExecution execution)
        => Assert.True(
            execution.Process.ExitCode == 0,
            $"Expected exit code 0, got {execution.Process.ExitCode}.\nSTDOUT:\n{execution.Process.Stdout}\nSTDERR:\n{execution.Process.Stderr}");

    private string CreateArtifactDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"worldsim-step5c3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _temporaryPaths.Add(path);
        return path;
    }

    private string CreateTemporaryFile(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json");
        _temporaryPaths.Add(path);
        return path;
    }

    private static JsonDocument ReadJson(string path)
        => JsonDocument.Parse(File.ReadAllText(path));

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WorldSim.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate WorldSim.sln from test base directory.");
    }

    public void Dispose()
    {
        foreach (var path in _temporaryPaths.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                else if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Cleanup is best-effort and must not hide the assertion that failed.
            }
        }
    }

    private sealed record ScenarioExecution(string ArtifactDir, ScenarioRunnerProcessResult Process);
}
