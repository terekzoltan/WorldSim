using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class AssertionEngineTests
{
    [Fact]
    public void ExitCode_AssertionFailure_Returns2()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"combat\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":10,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 2,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "101",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(2, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("assert_fail", manifest.RootElement.GetProperty("exitReason").GetString());

        var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), a =>
            a.GetProperty("severity").GetString() == "error" &&
            a.GetProperty("passed").GetBoolean() == false);
    }

    [Fact]
    public void Assertions_CombatCountersEvaluatedWhenEnabled()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"combat\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":10,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 2,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "102",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            });

        var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        Assert.Contains(assertions.RootElement.EnumerateArray(), a =>
            a.GetProperty("invariantId").GetString() == "COMB-01");
        Assert.Contains(assertions.RootElement.EnumerateArray(), a =>
            a.GetProperty("invariantId").GetString() == "COMB-02");
        Assert.Contains(assertions.RootElement.EnumerateArray(), a =>
            a.GetProperty("invariantId").GetString() == "COMB-03" &&
            !a.GetProperty("skipped").GetBoolean());
    }

    [Fact]
    public void Assertions_LowIntensityCombat_SkipsDeathCounterChecks()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"medium-default\",\"Width\":128,\"Height\":72,\"InitialPop\":48,\"Ticks\":20,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":true,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "505",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "goap",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_TICKS"] = "20",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            });

        var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        var comb01 = FindAssertion(assertions, "COMB-01");
        var comb02 = FindAssertion(assertions, "COMB-02");
        var comb03 = FindAssertion(assertions, "COMB-03");

        Assert.True(comb01.GetProperty("skipped").GetBoolean());
        Assert.Equal("combat_not_sustained", comb01.GetProperty("skipReason").GetString());
        Assert.True(comb02.GetProperty("skipped").GetBoolean());
        Assert.Equal("combat_not_sustained", comb02.GetProperty("skipReason").GetString());
        Assert.False(comb03.GetProperty("skipped").GetBoolean());
        Assert.True(comb03.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void Assertions_SustainedCombat_RequiresDeathCounters()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"medium-default\",\"Width\":128,\"Height\":72,\"InitialPop\":48,\"Ticks\":1200,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":true,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "202",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "goap",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_TICKS"] = "1200",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            });

        var assertions = ReadJson(Path.Combine(artifactDir, "assertions.json"));
        var comb01 = FindAssertion(assertions, "COMB-01");
        var comb02 = FindAssertion(assertions, "COMB-02");

        Assert.False(comb01.GetProperty("skipped").GetBoolean());
        Assert.True(comb01.GetProperty("passed").GetBoolean());
        Assert.False(comb02.GetProperty("skipped").GetBoolean());
        Assert.True(comb02.GetProperty("passed").GetBoolean());
    }

    [Fact]
    public void Anomalies_DoNotFailByDefault()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"combat\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":10,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "103",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "false",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson
            });

        var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.DoesNotContain(anomalies.RootElement.EnumerateArray(), a =>
            a.GetProperty("id").GetString() == "ANOM-COMB-COUNTERS-MISSING");

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(0, manifest.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public void ExitCode_ConfigError_Returns3()
    {
        var artifactDir = CreateArtifactDir();

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 3,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "104",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = "{not valid json"
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(3, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("config_error", manifest.RootElement.GetProperty("exitReason").GetString());
    }

    [Fact]
    public void ExitCode_SemanticallyInvalidConfig_Returns3WithoutDefaultFallback()
    {
        var artifactDir = CreateArtifactDir();

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 3,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "105",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = "[{\"Name\":\"bad\",\"Width\":0,\"Height\":40,\"InitialPop\":24,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]"
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(3, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("totalRuns").GetInt32());

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        Assert.Equal(0, summary.RootElement.GetProperty("runs").GetArrayLength());
    }

    [Fact]
    public void ExitCode_InvalidConfigJson_JsonStdoutRemainsParseable()
    {
        var artifactDir = CreateArtifactDir();
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
        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = "106";
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment["WORLDSIM_SCENARIO_CONFIGS_JSON"] = "{not valid json";
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);

        Assert.Equal(3, result.ExitCode);
        var stdout = result.Stdout;
        var stderr = result.Stderr;
        using var parsed = JsonDocument.Parse(stdout);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
        Assert.Contains("invalid WORLDSIM_SCENARIO_CONFIGS_JSON", stderr, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-assertions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static JsonElement FindAssertion(JsonDocument assertions, string invariantId)
    {
        return assertions.RootElement.EnumerateArray().Single(a =>
            a.GetProperty("invariantId").GetString() == invariantId);
    }

    private static void RunScenarioRunner(string artifactDir, int expectedExitCode, IReadOnlyDictionary<string, string> env)
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
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");
        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);

        Assert.True(result.ExitCode == expectedExitCode, $"ScenarioRunner exit mismatch. Expected={expectedExitCode} Actual={result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");
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
