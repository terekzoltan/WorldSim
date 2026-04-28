using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class SupplyTelemetryArtifactTests
{
    [Fact]
    public void RunResults_ContainSupplyTelemetryBlock()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "801",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        AssertSupplyBlockShape(run.GetProperty("supply"));

        var perRunPath = Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json").Single();
        using var perRun = ReadJson(perRunPath);
        AssertSupplyBlockShape(perRun.RootElement.GetProperty("supply"));
    }

    [Fact]
    public void Drilldown_TimelineContainsCompactSupplyFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "802",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "2"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var supply = timeline.RootElement.EnumerateArray().First().GetProperty("supply");
        Assert.True(supply.TryGetProperty("inventoryFoodConsumed", out _));
        Assert.True(supply.TryGetProperty("carriersWithFood", out _));
        Assert.True(supply.TryGetProperty("totalCarriedFood", out _));
        Assert.True(supply.TryGetProperty("avgInventoryUsedSlots", out _));
        Assert.True(supply.TryGetProperty("avgInventoryCapacitySlots", out _));
    }

    [Fact]
    public void Compare_OldBaselineWithoutSupplyBlock_StillParses()
    {
        var baselineArtifact = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "803",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.Equal(0, baselineExit);

        var oldBaselinePath = RemoveSupplyBlocksFromSummary(Path.Combine(baselineArtifact, "summary.json"));

        var compareArtifact = CreateArtifactDir();
        var compareExit = RunScenarioRunner(
            compareArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "803",
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
    public void SupplyScenario_StorehouseRefillConsumption_ProducesNonZeroSupplyEvidence()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"supply-storehouse-refill-consumption\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":0.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"SupplyScenario\":\"storehouse_refill_consumption\"}]";

        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "804",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson,
                ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
                ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
                ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "1"
            },
            out var stdout,
            out var stderr);

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var supply = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("supply");
        Assert.True(supply.GetProperty("inventoryFoodConsumed").GetInt32() > 0, $"No inventory consumption\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(
            supply.GetProperty("carriersWithFood").GetInt32() > 0 || supply.GetProperty("totalCarriedFood").GetInt32() > 0,
            $"No carried-food evidence\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(supply.GetProperty("coloniesWithBackpacks").GetInt32() > 0);
        Assert.True(supply.GetProperty("coloniesWithRationing").GetInt32() > 0);
    }

    private static void AssertSupplyBlockShape(JsonElement supply)
    {
        Assert.True(supply.TryGetProperty("inventoryFoodConsumed", out _));
        Assert.True(supply.TryGetProperty("carriersWithFood", out _));
        Assert.True(supply.TryGetProperty("totalCarriedFood", out _));
        Assert.True(supply.TryGetProperty("avgInventoryUsedSlots", out _));
        Assert.True(supply.TryGetProperty("avgInventoryCapacitySlots", out _));
        Assert.True(supply.TryGetProperty("coloniesWithBackpacks", out _));
        Assert.True(supply.TryGetProperty("coloniesWithRationing", out _));
    }

    private static string RemoveSupplyBlocksFromSummary(string summaryPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
            run.Remove("supply");

        var patchedPath = Path.Combine(Path.GetTempPath(), $"worldsim-baseline-no-supply-{Guid.NewGuid():N}.json");
        File.WriteAllText(patchedPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return patchedPath;
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

        foreach (var pair in env)
            startInfo.Environment[pair.Key] = pair.Value;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        stdout = process.StandardOutput.ReadToEnd();
        stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-artifacts-{Guid.NewGuid():N}");
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
