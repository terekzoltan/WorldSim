using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class ComparisonTests
{
    [Fact]
    public void Compare_BaselineMissing_WhenEnabled_GracefulSkip()
    {
        var artifactDir = CreateArtifactDir();
        var missingBaseline = Path.Combine(Path.GetTempPath(), $"worldsim-missing-baseline-{Guid.NewGuid():N}.json");

        RunScenarioRunner(
            artifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "301",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = missingBaseline,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            });

        Assert.False(File.Exists(Path.Combine(artifactDir, "compare.json")));
    }

    [Fact]
    public void Compare_BaselineMissing_JsonStdoutRemainsParseable()
    {
        var artifactDir = CreateArtifactDir();
        var missingBaseline = Path.Combine(Path.GetTempPath(), $"worldsim-missing-baseline-{Guid.NewGuid():N}.json");

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
        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = "301";
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_COMPARE"] = "true";
        startInfo.Environment["WORLDSIM_SCENARIO_BASELINE_PATH"] = missingBaseline;
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        using var parsed = JsonDocument.Parse(stdout);
        Assert.Equal(JsonValueKind.Object, parsed.RootElement.ValueKind);
        Assert.Contains("compare baseline unavailable", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_RunDeltaFile_IsWritten()
    {
        var baselineArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            baselineArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "302",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            });

        var compareArtifactDir = CreateArtifactDir();
        var baselinePath = Path.Combine(baselineArtifactDir, "summary.json");
        RunScenarioRunner(
            compareArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "302",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = baselinePath
            });

        var compareJsonPath = Path.Combine(compareArtifactDir, "compare.json");
        Assert.True(File.Exists(compareJsonPath));
        var compareDoc = ReadJson(compareJsonPath);
        Assert.Equal(1, compareDoc.RootElement.GetProperty("matchedRunCount").GetInt32());
    }

    [Fact]
    public void Compare_DifferentVisualLanes_DoNotMatchBaselineRuns()
    {
        var baselineArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            baselineArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "399",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            });

        var compareArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            compareArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "399",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = Path.Combine(baselineArtifactDir, "summary.json"),
                ["WORLDSIM_VISUAL_PROFILE"] = "showcase"
            });

        var compareDoc = ReadJson(Path.Combine(compareArtifactDir, "compare.json"));
        Assert.Equal(0, compareDoc.RootElement.GetProperty("matchedRunCount").GetInt32());
        Assert.Equal(1, compareDoc.RootElement.GetProperty("currentOnlyRunKeys").GetArrayLength());
        Assert.Equal(1, compareDoc.RootElement.GetProperty("baselineOnlyRunKeys").GetArrayLength());
    }

    [Fact]
    public void Compare_PassToFailRegression_IsReported()
    {
        var baselineArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            baselineArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "303",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            });

        var patchedBaselinePath = PatchBaseline(Path.Combine(baselineArtifactDir, "summary.json"), runPatch: run =>
        {
            run["averageFoodPerPerson"] = 3.0;
        });

        var compareArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            compareArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "303",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = patchedBaselinePath
            });

        var compareDoc = ReadJson(Path.Combine(compareArtifactDir, "compare.json"));
        Assert.Contains(compareDoc.RootElement.GetProperty("passToFailRegressions").EnumerateArray(), item =>
            item.GetProperty("invariantId").GetString() == "SURV-04");
    }

    [Fact]
    public void Compare_DeltaFailEnabled_Returns4OnThresholdBreach()
    {
        var baselineArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            baselineArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "304",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            });

        var patchedBaselinePath = PatchBaseline(Path.Combine(baselineArtifactDir, "summary.json"), runPatch: run =>
        {
            run["people"] = 500;
            run["food"] = 1000;
            run["averageFoodPerPerson"] = 10.0;
            run["livingColonies"] = 8;
        });

        var compareArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            compareArtifactDir,
            expectedExitCode: 4,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "304",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = patchedBaselinePath,
                ["WORLDSIM_SCENARIO_DELTA_FAIL"] = "true"
            });
    }

    [Fact]
    public void Compare_ScalingInvariants_Evaluated()
    {
        var configsJson = "[{\"Name\":\"small\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0},{\"Name\":\"large\",\"Width\":64,\"Height\":40,\"InitialPop\":24,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        var baselineArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            baselineArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "305",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configsJson
            });

        var compareArtifactDir = CreateArtifactDir();
        RunScenarioRunner(
            compareArtifactDir,
            expectedExitCode: 0,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "305",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configsJson,
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = Path.Combine(baselineArtifactDir, "summary.json")
            });

        var compareDoc = ReadJson(Path.Combine(compareArtifactDir, "compare.json"));
        var scalingIds = compareDoc.RootElement.GetProperty("scalingChecks").EnumerateArray()
            .Select(item => item.GetProperty("invariantId").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("SCALE-01", scalingIds);
        Assert.Contains("SCALE-02", scalingIds);
    }

    private static string PatchBaseline(string baselineSummaryPath, Action<JsonObject> runPatch)
    {
        var node = JsonNode.Parse(File.ReadAllText(baselineSummaryPath))?.AsObject() ?? throw new InvalidOperationException("Invalid baseline summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Baseline summary missing runs array");
        if (runs.Count == 0)
            throw new InvalidOperationException("Baseline summary runs array empty");

        var firstRun = runs[0]?.AsObject() ?? throw new InvalidOperationException("Baseline run is not an object");
        runPatch(firstRun);

        var patchedPath = Path.Combine(Path.GetTempPath(), $"worldsim-baseline-patched-{Guid.NewGuid():N}.json");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-compare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
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
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_COMPARE");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_DELTA_FAIL");
        startInfo.Environment.Remove("WORLDSIM_SCENARIO_BASELINE_PATH");
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == expectedExitCode, $"ScenarioRunner exit mismatch. Expected={expectedExitCode} Actual={process.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
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
