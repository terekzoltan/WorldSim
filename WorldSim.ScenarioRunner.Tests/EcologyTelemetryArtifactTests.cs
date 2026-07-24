using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class EcologyTelemetryArtifactTests : IDisposable
{
    private readonly List<string> _temporaryPaths = new();

    [Fact]
    public void MainWorld_RunAndSummary_ExposeMatchingConstructorInitialEcology_AndExitZero()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "721",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode == 0, $"Expected successful initial ecology run, got exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        var ecology = firstRun.GetProperty("ecology");
        Assert.True(ecology.TryGetProperty("herbivores", out _));
        Assert.True(ecology.TryGetProperty("predators", out _));
        Assert.True(ecology.TryGetProperty("activeFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("depletedFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("herbivoreReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("predatorReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("emergencyRescues", out _));
        Assert.True(ecology.TryGetProperty("plantFoodProduced", out _));
        Assert.True(ecology.TryGetProperty("meatFoodProduced", out _));
        Assert.True(ecology.TryGetProperty("plantFoodConsumedByAnimals", out _));
        Assert.True(ecology.TryGetProperty("meatFromHunt", out _));
        Assert.True(ecology.TryGetProperty("supplyBridgeSkippedByNoBiomass", out _));
        Assert.True(ecology.TryGetProperty("emergencyRescuePolicy", out _));
        Assert.True(ecology.TryGetProperty("lastEmergencyRescueReason", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroHerbivores", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroPredators", out _));
        Assert.True(ecology.TryGetProperty("firstZeroHerbivoreTick", out _));
        Assert.True(ecology.TryGetProperty("firstZeroPredatorTick", out _));
        Assert.True(ecology.TryGetProperty("predatorDeaths", out _));
        Assert.True(ecology.TryGetProperty("predatorHumanHits", out _));

        var summaryInitialEcology = firstRun.GetProperty("initialEcology");
        AssertInitialEcologyShape(summaryInitialEcology);

        using var runArtifact = ReadSingleRunArtifact(artifactDir);
        var runInitialEcology = runArtifact.RootElement.GetProperty("initialEcology");
        AssertJsonSemanticallyEqual(summaryInitialEcology, runInitialEcology);

        using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("smr/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(0, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("assertionFailures").GetInt32());
        Assert.Equal(1, manifest.RootElement.GetProperty("totalRuns").GetInt32());
    }

    [Fact]
    public void Drilldown_TimelineContainsCompactEcologyFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "722",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "2"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode == 0, $"Expected successful timeline run, got exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var samples = timeline.RootElement.EnumerateArray().ToArray();
        var firstSample = samples.First();
        var ecology = firstSample.GetProperty("ecology");
        Assert.True(ecology.TryGetProperty("herbivores", out _));
        Assert.True(ecology.TryGetProperty("predators", out _));
        Assert.True(ecology.TryGetProperty("activeFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("depletedFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("herbivoreReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("predatorReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("emergencyRescues", out _));
        Assert.True(ecology.TryGetProperty("plantFoodProduced", out _));
        Assert.True(ecology.TryGetProperty("meatFoodProduced", out _));
        Assert.True(ecology.TryGetProperty("plantFoodConsumedByAnimals", out _));
        Assert.True(ecology.TryGetProperty("meatFromHunt", out _));
        Assert.True(ecology.TryGetProperty("supplyBridgeSkippedByNoBiomass", out _));
        Assert.True(ecology.TryGetProperty("emergencyRescuePolicy", out _));
        Assert.True(ecology.TryGetProperty("lastEmergencyRescueReason", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroHerbivores", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroPredators", out _));
        AssertNullableNumber(ecology, "firstPredatorHumanContactTick");
        AssertNullableNumber(ecology, "firstPredatorHuntTick");
        AssertNullableNumber(ecology, "firstHerbivoreGrazingTick");
        AssertNullableNumber(ecology, "firstPredatorDeathTick");
        AssertNullableNumber(ecology, "firstHerbivoreDeathTick");
        AssertNullableNumber(ecology, "firstPredatorBirthTick");
        AssertNullableNumber(ecology, "firstHerbivoreBirthTick");
        Assert.False(firstSample.TryGetProperty("initialEcology", out _));
        Assert.False(ecology.TryGetProperty("initialEcology", out _));

        var firstEventFields = new[]
        {
            "firstPredatorHumanContactTick", "firstPredatorHuntTick", "firstHerbivoreGrazingTick",
            "firstPredatorDeathTick", "firstHerbivoreDeathTick", "firstPredatorBirthTick",
            "firstHerbivoreBirthTick"
        };
        foreach (var sample in samples)
        {
            var sampleEcology = sample.GetProperty("ecology");
            foreach (var field in firstEventFields)
                AssertNullableNumber(sampleEcology, field);
            Assert.False(sample.TryGetProperty("initialEcology", out _));
            Assert.False(sampleEcology.TryGetProperty("initialEcology", out _));
        }

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var finalEcology = summary.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("ecology");
        var finalTimelineEcology = samples.Last().GetProperty("ecology");
        foreach (var field in firstEventFields)
            Assert.Equal(finalEcology.GetProperty(field).GetRawText(), finalTimelineEcology.GetProperty(field).GetRawText());
        Assert.Contains(firstEventFields, field => finalTimelineEcology.GetProperty(field).ValueKind == JsonValueKind.Number);
    }

    [Fact]
    public void InitialEcology_SameSeedAndConfig_IsByteEquivalentAcrossIndependentRuns()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "723",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

        var artifactA = CreateArtifactDir();
        var exitA = RunScenarioRunner(artifactA, env, out var stdoutA, out var stderrA);
        Assert.True(exitA == 0, $"Expected successful deterministic ecology run, got exit code {exitA}\nSTDOUT:\n{stdoutA}\nSTDERR:\n{stderrA}");

        var artifactB = CreateArtifactDir();
        var exitB = RunScenarioRunner(artifactB, env, out var stdoutB, out var stderrB);
        Assert.True(exitB == 0, $"Expected successful deterministic ecology run, got exit code {exitB}\nSTDOUT:\n{stdoutB}\nSTDERR:\n{stderrB}");

        using var summaryA = ReadJson(Path.Combine(artifactA, "summary.json"));
        using var summaryB = ReadJson(Path.Combine(artifactB, "summary.json"));
        var ecologyA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology").GetRawText();
        var ecologyB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology").GetRawText();
        var initialA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("initialEcology").GetRawText();
        var initialB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("initialEcology").GetRawText();

        using var runA = ReadSingleRunArtifact(artifactA);
        using var runB = ReadSingleRunArtifact(artifactB);
        var runInitialA = runA.RootElement.GetProperty("initialEcology").GetRawText();
        var runInitialB = runB.RootElement.GetProperty("initialEcology").GetRawText();

        Assert.Equal(ecologyA, ecologyB);
        Assert.Equal(initialA, initialB);
        Assert.Equal(runInitialA, runInitialB);
        AssertJsonSemanticallyEqual(
            summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("initialEcology"),
            runA.RootElement.GetProperty("initialEcology"));
        AssertJsonSemanticallyEqual(
            summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("initialEcology"),
            runB.RootElement.GetProperty("initialEcology"));
    }

    [Fact]
    public void Compare_CurrentBaselineWithInitialEcology_MatchesAndExitsZero()
    {
        var baseline = CreateBaseline("724");
        using var baselineSummary = ReadJson(baseline);
        Assert.Equal(JsonValueKind.Object, baselineSummary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("initialEcology").ValueKind);

        var compareArtifact = RunCompare("724", baseline);
        AssertCompareArtifact(compareArtifact);
    }

    [Fact]
    public void Compare_OldBaselineWithoutInitialEcology_MatchesAndExitsZero()
    {
        var baseline = CreateBaseline("7241");
        var oldBaseline = RemoveRunPropertiesFromSummary(baseline, "initialEcology");

        var compareArtifact = RunCompare("7241", oldBaseline);
        AssertCompareArtifact(compareArtifact);
    }

    [Fact]
    public void Compare_OldBaselineWithoutAnyEcologyBlocks_MatchesAndExitsZero()
    {
        var baseline = CreateBaseline("7242");
        var oldBaseline = RemoveRunPropertiesFromSummary(baseline, "initialEcology", "ecology", "ecologyBalance");

        var compareArtifact = RunCompare("7242", oldBaseline);
        AssertCompareArtifact(compareArtifact);
    }

    [Fact]
    public void Compare_BaselineWithNullableEmptyInitialDistanceSummary_ParsesAndExitsZero()
    {
        var baseline = CreateBaseline("7243");
        var nullableBaseline = SetEmptyDistanceSummary(baseline, "predatorToNearestPersonDistance");

        using (var patched = ReadJson(nullableBaseline))
        {
            var distance = patched.RootElement.GetProperty("runs").EnumerateArray().First()
                .GetProperty("initialEcology").GetProperty("predatorToNearestPersonDistance");
            Assert.Equal(0, distance.GetProperty("sampleCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, distance.GetProperty("minimum").ValueKind);
            Assert.Equal(JsonValueKind.Null, distance.GetProperty("maximum").ValueKind);
            Assert.Equal(JsonValueKind.Null, distance.GetProperty("average").ValueKind);
        }

        var compareArtifact = RunCompare("7243", nullableBaseline);
        AssertCompareArtifact(compareArtifact);
    }

    [Theory]
    [InlineData("organic_campaign_lifecycle")]
    [InlineData("organic_hostile_campaign_lifecycle")]
    [InlineData("manual_operator_campaign_lifecycle")]
    public void RuntimeBackedLifecycleModes_EmitExplicitNullInitialEcology_AndExitZero(string wave10Scenario)
    {
        var artifactDir = CreateArtifactDir();
        var env = CreateDefaultEnvironment("730");
        env["WORLDSIM_SCENARIO_CONFIGS_JSON"] = BuildConfigJson(
            "runtime-backed-null",
            wave10Scenario: wave10Scenario,
            enableCombatPrimitives: true);

        var exitCode = RunScenarioRunner(artifactDir, env, out var stdout, out var stderr);

        Assert.True(exitCode == 0, $"Expected successful runtime-backed run, got exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, run.GetProperty("initialEcology").ValueKind);
        Assert.Equal(JsonValueKind.Null, run.GetProperty("initialAnimalConfig").ValueKind);
        Assert.Equal(wave10Scenario, run.GetProperty("wave10").GetProperty("wave10Scenario").GetString());
        Assert.Equal("main_world_run", run.GetProperty("wave10").GetProperty("runtimeSource").GetString());

        using var runArtifact = ReadSingleRunArtifact(artifactDir);
        Assert.Equal(JsonValueKind.Null, runArtifact.RootElement.GetProperty("initialEcology").ValueKind);
        Assert.Equal(JsonValueKind.Null, runArtifact.RootElement.GetProperty("initialAnimalConfig").ValueKind);
        Assert.False(File.Exists(Path.Combine(artifactDir, "wave10-probes.json")));
    }

    [Theory]
    [InlineData("wave9", "army_supply_depletion")]
    [InlineData("wave10", "manual_operator_launch")]
    public void CompanionMainRuns_KeepDirectWorldConstructorInitialEcology(string family, string scenario)
    {
        var artifactDir = CreateArtifactDir();
        var env = CreateDefaultEnvironment("7301");
        env["WORLDSIM_SCENARIO_CONFIGS_JSON"] = family == "wave9"
            ? BuildConfigJson("wave9-companion", wave9Scenario: scenario)
            : BuildConfigJson("wave10-companion", wave10Scenario: scenario);

        var exitCode = RunScenarioRunner(artifactDir, env, out var stdout, out var stderr);

        Assert.True(exitCode == 0, $"Expected successful companion run, got {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Object, run.GetProperty("initialEcology").ValueKind);
        if (family == "wave9")
        {
            Assert.Equal(scenario, run.GetProperty("wave9").GetProperty("wave9Scenario").GetString());
            return;
        }

        using var probes = ReadJson(Path.Combine(artifactDir, "wave10-probes.json"));
        Assert.Equal(scenario, probes.RootElement.EnumerateArray().Single().GetProperty("wave10Scenario").GetString());
    }

    [Fact]
    public void InitialEcology_PostConstructionSupplyFixture_DoesNotChangeConstructorSnapshot()
    {
        const string seed = "731";
        var baselineArtifact = CreateArtifactDir();
        var baselineEnv = CreateDefaultEnvironment(seed);
        baselineEnv["WORLDSIM_SCENARIO_CONFIGS_JSON"] = BuildConfigJson("constructor-baseline");
        var baselineExit = RunScenarioRunner(baselineArtifact, baselineEnv, out var baselineStdout, out var baselineStderr);
        Assert.True(baselineExit == 0, $"Expected baseline exit 0, got {baselineExit}\nSTDOUT:\n{baselineStdout}\nSTDERR:\n{baselineStderr}");

        var supplyArtifact = CreateArtifactDir();
        var supplyEnv = CreateDefaultEnvironment(seed);
        supplyEnv["WORLDSIM_SCENARIO_CONFIGS_JSON"] = BuildConfigJson(
            "constructor-with-supply-fixture",
            supplyScenario: "storehouse_refill_consumption");
        var supplyExit = RunScenarioRunner(supplyArtifact, supplyEnv, out var supplyStdout, out var supplyStderr);
        Assert.True(supplyExit == 0, $"Expected supply-fixture exit 0, got {supplyExit}\nSTDOUT:\n{supplyStdout}\nSTDERR:\n{supplyStderr}");

        using var baselineSummary = ReadJson(Path.Combine(baselineArtifact, "summary.json"));
        using var supplySummary = ReadJson(Path.Combine(supplyArtifact, "summary.json"));
        var baselineInitial = baselineSummary.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("initialEcology");
        var supplyInitial = supplySummary.RootElement.GetProperty("runs").EnumerateArray().Single().GetProperty("initialEcology");
        Assert.Equal(baselineInitial.GetRawText(), supplyInitial.GetRawText());
    }

    [Fact]
    public void RunnerHelper_OverridesInheritedNonCoreLaneAndForeignConfig()
    {
        var inheritedValues = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_LANE"] = "refinery_fixture",
            ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = BuildConfigJson("foreign-parent-config", wave10Scenario: "organic_campaign_lifecycle"),
            ["WORLDSIM_SCENARIO_MODE"] = "all",
            ["WORLDSIM_SCENARIO_TICKS"] = "999",
            ["WORLDSIM_SCENARIO_DT"] = "9",
            ["WORLDSIM_SCENARIO_SEEDS"] = "999",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "htn",
            ["WORLDSIM_VISUAL_PROFILE"] = "Showcase"
        };
        var previousValues = inheritedValues.Keys.ToDictionary(
            key => key,
            key => Environment.GetEnvironmentVariable(key),
            StringComparer.Ordinal);
        try
        {
            foreach (var pair in inheritedValues)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);

            var artifactDir = CreateArtifactDir();
            var exitCode = RunScenarioRunner(artifactDir, new Dictionary<string, string>(), out var stdout, out var stderr);

            Assert.True(exitCode == 0, $"Expected helper-isolated core run, got {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
            var run = summary.RootElement.GetProperty("runs").EnumerateArray().Single();
            Assert.Equal("default", run.GetProperty("configName").GetString());
            Assert.Equal(101, run.GetProperty("seed").GetInt32());
            Assert.Equal("Simple", run.GetProperty("plannerMode").GetString());
            Assert.Equal(8, run.GetProperty("ticks").GetInt32());
            Assert.Equal(0.25f, run.GetProperty("dt").GetSingle());
            Assert.Equal("Headless", run.GetProperty("visualLane").GetString());
            Assert.Equal(JsonValueKind.Object, run.GetProperty("initialEcology").ValueKind);

            using var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
            Assert.Equal(1, manifest.RootElement.GetProperty("totalRuns").GetInt32());
            Assert.False(manifest.RootElement.GetProperty("compareEnabled").GetBoolean());
            Assert.False(manifest.RootElement.GetProperty("perfEnabled").GetBoolean());
            Assert.Equal("Headless", manifest.RootElement.GetProperty("effectiveVisualLane").GetString());
        }
        finally
        {
            foreach (var pair in previousValues)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [Fact]
    public void ConfigJson_EcologyBalanceValues_ArePersistedAsEffectiveValues()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"ecology-balance\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"AnimalReplenishmentChancePerSecond\":0.25,\"PredatorReplenishmentChance\":0.75,\"FoodRegrowthMinSeconds\":9.0,\"FoodRegrowthJitterSeconds\":4.0,\"EmergencyRescuePolicy\":\"enabled\"}]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "725",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), assertion =>
            assertion.GetProperty("invariantId").GetString() == "ECO-RESCUE-01"
            && assertion.GetProperty("passed").GetBoolean());
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var balance = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecologyBalance");
        Assert.Equal(0.25f, balance.GetProperty("animalReplenishmentChancePerSecond").GetSingle());
        Assert.Equal(0.75f, balance.GetProperty("predatorReplenishmentChance").GetSingle());
        Assert.Equal(9f, balance.GetProperty("foodRegrowthMinSeconds").GetSingle());
        Assert.Equal(4f, balance.GetProperty("foodRegrowthJitterSeconds").GetSingle());
        Assert.Equal("enabled", balance.GetProperty("emergencyRescuePolicy").GetString());
    }

    [Fact]
    public void ConfigJson_InvalidEcologyBalanceValues_AreClampedInArtifact()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"ecology-balance-clamp\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"AnimalReplenishmentChancePerSecond\":2.0,\"PredatorReplenishmentChance\":-1.0,\"FoodRegrowthMinSeconds\":-5.0,\"FoodRegrowthJitterSeconds\":99999.0}]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "726",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), assertion =>
            assertion.GetProperty("invariantId").GetString() == "ECO-RESCUE-01"
            && assertion.GetProperty("passed").GetBoolean());
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var balance = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecologyBalance");
        Assert.Equal(1f, balance.GetProperty("animalReplenishmentChancePerSecond").GetSingle());
        Assert.Equal(0f, balance.GetProperty("predatorReplenishmentChance").GetSingle());
        Assert.True(balance.GetProperty("foodRegrowthMinSeconds").GetSingle() > 0f);
        Assert.Equal(3600f, balance.GetProperty("foodRegrowthJitterSeconds").GetSingle());
        Assert.Equal("disabled", balance.GetProperty("emergencyRescuePolicy").GetString());
    }

    [Fact]
    public void AssertMode_FailsNormalLaneWhenEmergencyRescueOccurs()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"rescue-normal\",\"Width\":16,\"Height\":16,\"InitialPop\":12,\"Ticks\":1,\"Dt\":1.0,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"AnimalReplenishmentChancePerSecond\":1.0,\"PredatorReplenishmentChance\":0.0,\"EmergencyRescuePolicy\":\"enabled\"}]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "727",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.Equal(2, exitCode);
        using var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), assertion =>
            assertion.GetProperty("invariantId").GetString() == "ECO-RESCUE-01"
            && !assertion.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void AssertMode_AllowsExplicitRescueTestLane()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"rescue-test\",\"Width\":16,\"Height\":16,\"InitialPop\":12,\"Ticks\":1,\"Dt\":1.0,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"AnimalReplenishmentChancePerSecond\":1.0,\"PredatorReplenishmentChance\":0.0,\"EmergencyRescuePolicy\":\"enabled\",\"AllowEmergencyRescueInAcceptance\":true}]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "728",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        using var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), assertion =>
            assertion.GetProperty("invariantId").GetString() == "ECO-RESCUE-01"
            && assertion.GetProperty("passed").GetBoolean());
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var ecology = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology");
        Assert.True(ecology.GetProperty("emergencyRescues").GetInt32() > 0);
        Assert.Equal("enabled", ecology.GetProperty("emergencyRescuePolicy").GetString());
        Assert.Equal("herbivore_floor", ecology.GetProperty("lastEmergencyRescueReason").GetString());
    }

    [Fact]
    public void EcologySupplyBridgeFields_ArePresentAndDeterministic()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "729",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

        var artifactA = CreateArtifactDir();
        var exitA = RunScenarioRunner(artifactA, env, out var stdoutA, out var stderrA);
        Assert.True(exitA == 0, $"Expected successful deterministic supply bridge run, got exit code {exitA}\nSTDOUT:\n{stdoutA}\nSTDERR:\n{stderrA}");

        var artifactB = CreateArtifactDir();
        var exitB = RunScenarioRunner(artifactB, env, out var stdoutB, out var stderrB);
        Assert.True(exitB == 0, $"Expected successful deterministic supply bridge run, got exit code {exitB}\nSTDOUT:\n{stdoutB}\nSTDERR:\n{stderrB}");

        using var summaryA = ReadJson(Path.Combine(artifactA, "summary.json"));
        using var summaryB = ReadJson(Path.Combine(artifactB, "summary.json"));
        var ecologyA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology");
        var ecologyB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology");

        Assert.Equal(ecologyA.GetProperty("plantFoodProduced").GetInt32(), ecologyB.GetProperty("plantFoodProduced").GetInt32());
        Assert.Equal(ecologyA.GetProperty("meatFoodProduced").GetInt32(), ecologyB.GetProperty("meatFoodProduced").GetInt32());
        Assert.Equal(ecologyA.GetProperty("plantFoodConsumedByAnimals").GetInt32(), ecologyB.GetProperty("plantFoodConsumedByAnimals").GetInt32());
        Assert.Equal(ecologyA.GetProperty("meatFromHunt").GetInt32(), ecologyB.GetProperty("meatFromHunt").GetInt32());
        Assert.Equal(ecologyA.GetProperty("supplyBridgeSkippedByNoBiomass").GetInt32(), ecologyB.GetProperty("supplyBridgeSkippedByNoBiomass").GetInt32());
    }

    private string CreateBaseline(string seed)
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            CreateDefaultEnvironment(seed),
            out var stdout,
            out var stderr);
        Assert.True(exitCode == 0, $"Expected baseline exit 0, got {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        return Path.Combine(artifactDir, "summary.json");
    }

    private string RunCompare(string seed, string baselinePath)
    {
        var artifactDir = CreateArtifactDir();
        var env = CreateDefaultEnvironment(seed);
        env["WORLDSIM_SCENARIO_COMPARE"] = "true";
        env["WORLDSIM_SCENARIO_BASELINE_PATH"] = baselinePath;
        var exitCode = RunScenarioRunner(artifactDir, env, out var stdout, out var stderr);
        Assert.True(exitCode == 0, $"Expected compare exit 0, got {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        return artifactDir;
    }

    private static void AssertCompareArtifact(string artifactDir)
    {
        var comparePath = Path.Combine(artifactDir, "compare.json");
        Assert.True(File.Exists(comparePath));
        using var compare = ReadJson(comparePath);
        Assert.Equal(1, compare.RootElement.GetProperty("matchedRunCount").GetInt32());
        Assert.Empty(compare.RootElement.GetProperty("currentOnlyRunKeys").EnumerateArray());
        Assert.Empty(compare.RootElement.GetProperty("baselineOnlyRunKeys").EnumerateArray());
        Assert.Equal(0, compare.RootElement.GetProperty("totalFailureCount").GetInt32());
    }

    private string RemoveRunPropertiesFromSummary(string summaryPath, params string[] propertyNames)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
        {
            foreach (var propertyName in propertyNames)
                run.Remove(propertyName);
        }

        var patchedPath = CreateTemporaryFilePath("worldsim-baseline");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
    }

    private string SetEmptyDistanceSummary(string summaryPath, string distanceProperty)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
        {
            var initial = run["initialEcology"]?.AsObject()
                          ?? throw new InvalidOperationException("Summary missing initialEcology");
            initial[distanceProperty] = new JsonObject
            {
                ["sampleCount"] = 0,
                ["minimum"] = null,
                ["maximum"] = null,
                ["average"] = null
            };
        }

        var patchedPath = CreateTemporaryFilePath("worldsim-baseline-empty-distance");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
    }

    private static Dictionary<string, string> CreateDefaultEnvironment(string seed)
        => new()
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = seed,
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

    private static string BuildConfigJson(
        string name,
        string? supplyScenario = null,
        string? wave10Scenario = null,
        string? wave9Scenario = null,
        bool enableCombatPrimitives = false)
        => JsonSerializer.Serialize(new[]
        {
            new
            {
                Name = name,
                Width = 32,
                Height = 20,
                InitialPop = 24,
                Ticks = 8,
                Dt = 0.25,
                EnableCombatPrimitives = enableCombatPrimitives,
                EnableDiplomacy = true,
                StoneBuildingsEnabled = false,
                BirthRateMultiplier = 1.0,
                MovementSpeedMultiplier = 1.0,
                EnableSiege = true,
                EmergencyRescuePolicy = "disabled",
                SupplyScenario = supplyScenario,
                Wave9Scenario = wave9Scenario,
                Wave10Scenario = wave10Scenario
            }
        });

    private static void AssertInitialEcologyShape(JsonElement initialEcology)
    {
        Assert.Equal(JsonValueKind.Object, initialEcology.ValueKind);
        var requiredProperties = new[]
        {
            "initialAnimalPolicy", "initialAnimalPolicySource", "totalAnimals", "herbivores", "predators",
            "predatorHerbivoreRatio", "animalsOnWater", "animalsOnMovementBlockedTiles", "viableRegions",
            "viableRegionsWithoutHerbivores", "predatorsInPreyEmptyRegions", "herbivoresWithFoodInVision",
            "predatorsWithPreyInVision", "predatorsWithinHumanHarassRadius", "predatorsWithinEarlyHumanContactRadius",
            "foodVisionRadius", "preyVisionRadius", "humanHarassRadius", "earlyHumanContactRadius",
            "herbivoreToNearestFoodDistance", "predatorToNearestPreyDistance", "predatorToNearestPersonDistance",
            "regions"
        };
        foreach (var property in requiredProperties)
            Assert.True(initialEcology.TryGetProperty(property, out _), $"Missing initial ecology property '{property}'.");

        var herbivores = initialEcology.GetProperty("herbivores").GetInt32();
        var predators = initialEcology.GetProperty("predators").GetInt32();
        Assert.Equal(herbivores + predators, initialEcology.GetProperty("totalAnimals").GetInt32());

        var regions = initialEcology.GetProperty("regions").EnumerateArray().ToArray();
        Assert.Equal(herbivores, regions.Sum(region => region.GetProperty("herbivores").GetInt32()));
        Assert.Equal(predators, regions.Sum(region => region.GetProperty("predators").GetInt32()));
        Assert.Equal(
            regions.Select(region => region.GetProperty("regionId").GetInt32()).OrderBy(id => id),
            regions.Select(region => region.GetProperty("regionId").GetInt32()));

        AssertDistanceSummaryShape(initialEcology.GetProperty("herbivoreToNearestFoodDistance"));
        AssertDistanceSummaryShape(initialEcology.GetProperty("predatorToNearestPreyDistance"));
        AssertDistanceSummaryShape(initialEcology.GetProperty("predatorToNearestPersonDistance"));
    }

    private static void AssertDistanceSummaryShape(JsonElement distance)
    {
        var sampleCount = distance.GetProperty("sampleCount").GetInt32();
        var minimum = distance.GetProperty("minimum");
        var maximum = distance.GetProperty("maximum");
        var average = distance.GetProperty("average");
        if (sampleCount == 0)
        {
            Assert.Equal(JsonValueKind.Null, minimum.ValueKind);
            Assert.Equal(JsonValueKind.Null, maximum.ValueKind);
            Assert.Equal(JsonValueKind.Null, average.ValueKind);
            return;
        }

        var minimumValue = minimum.GetInt32();
        var maximumValue = maximum.GetInt32();
        var averageValue = average.GetDouble();
        Assert.InRange(averageValue, minimumValue, maximumValue);
    }

    private static void AssertNullableNumber(JsonElement owner, string propertyName)
    {
        var value = owner.GetProperty(propertyName);
        Assert.True(
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Number,
            $"Expected '{propertyName}' to be null or number, got {value.ValueKind}.");
    }

    private static void AssertJsonSemanticallyEqual(JsonElement expected, JsonElement actual)
    {
        var expectedNode = JsonNode.Parse(expected.GetRawText());
        var actualNode = JsonNode.Parse(actual.GetRawText());
        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode));
    }

    private static JsonDocument ReadSingleRunArtifact(string artifactDir)
    {
        var runFiles = Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json");
        Assert.Single(runFiles);
        return ReadJson(runFiles[0]);
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

        var isolatedVariables = new[]
        {
            "WORLDSIM_SCENARIO_MODE", "WORLDSIM_SCENARIO_ASSERT", "WORLDSIM_SCENARIO_ANOMALY_FAIL",
            "WORLDSIM_SCENARIO_COMPARE", "WORLDSIM_SCENARIO_PERF", "WORLDSIM_SCENARIO_PERF_FAIL",
            "WORLDSIM_SCENARIO_DELTA_FAIL", "WORLDSIM_SCENARIO_BASELINE_PATH", "WORLDSIM_SCENARIO_DRILLDOWN",
            "WORLDSIM_SCENARIO_DRILLDOWN_TOP", "WORLDSIM_SCENARIO_SAMPLE_EVERY",
            "WORLDSIM_SCENARIO_CONFIGS_JSON", "WORLDSIM_SCENARIO_LANE", "WORLDSIM_VISUAL_PROFILE"
        };
        foreach (var variable in isolatedVariables)
            startInfo.Environment.Remove(variable);

        startInfo.Environment["WORLDSIM_SCENARIO_LANE"] = "core";
        startInfo.Environment["WORLDSIM_SCENARIO_MODE"] = "standard";
        startInfo.Environment["WORLDSIM_SCENARIO_TICKS"] = "8";
        startInfo.Environment["WORLDSIM_SCENARIO_DT"] = "0.25";
        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = "101";
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment["WORLDSIM_VISUAL_PROFILE"] = "Headless";
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);
        stdout = result.Stdout;
        stderr = result.Stderr;
        return result.ExitCode;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-ecology-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _temporaryPaths.Add(dir);
        return dir;
    }

    private string CreateTemporaryFilePath(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.json");
        _temporaryPaths.Add(path);
        return path;
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
}
