using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class EcologyTelemetryArtifactTests
{
    [Fact]
    public void RunResults_ContainEcologyTelemetryBlock()
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

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs").EnumerateArray().First();
        var ecology = firstRun.GetProperty("ecology");
        Assert.True(ecology.TryGetProperty("herbivores", out _));
        Assert.True(ecology.TryGetProperty("predators", out _));
        Assert.True(ecology.TryGetProperty("activeFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("depletedFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("herbivoreReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("predatorReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroHerbivores", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroPredators", out _));
        Assert.True(ecology.TryGetProperty("firstZeroHerbivoreTick", out _));
        Assert.True(ecology.TryGetProperty("firstZeroPredatorTick", out _));
        Assert.True(ecology.TryGetProperty("predatorDeaths", out _));
        Assert.True(ecology.TryGetProperty("predatorHumanHits", out _));
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

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        using var index = ReadJson(Path.Combine(artifactDir, "drilldown", "index.json"));
        var runKey = index.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("runKey").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runKey));

        using var timeline = ReadJson(Path.Combine(artifactDir, "drilldown", runKey!, "timeline.json"));
        var firstSample = timeline.RootElement.EnumerateArray().First();
        var ecology = firstSample.GetProperty("ecology");
        Assert.True(ecology.TryGetProperty("herbivores", out _));
        Assert.True(ecology.TryGetProperty("predators", out _));
        Assert.True(ecology.TryGetProperty("activeFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("depletedFoodNodes", out _));
        Assert.True(ecology.TryGetProperty("herbivoreReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("predatorReplenishmentSpawns", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroHerbivores", out _));
        Assert.True(ecology.TryGetProperty("ticksWithZeroPredators", out _));
    }

    [Fact]
    public void EcologyTelemetry_IsDeterministicAcrossRepeatedRuns()
    {
        var env = new Dictionary<string, string>
        {
            ["WORLDSIM_SCENARIO_SEEDS"] = "723",
            ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
        };

        var artifactA = CreateArtifactDir();
        var exitA = RunScenarioRunner(artifactA, env, out var stdoutA, out var stderrA);
        Assert.True(exitA is 0 or 2 or 4, $"Unexpected exit code {exitA}\nSTDOUT:\n{stdoutA}\nSTDERR:\n{stderrA}");

        var artifactB = CreateArtifactDir();
        var exitB = RunScenarioRunner(artifactB, env, out var stdoutB, out var stderrB);
        Assert.True(exitB is 0 or 2 or 4, $"Unexpected exit code {exitB}\nSTDOUT:\n{stdoutB}\nSTDERR:\n{stderrB}");

        using var summaryA = ReadJson(Path.Combine(artifactA, "summary.json"));
        using var summaryB = ReadJson(Path.Combine(artifactB, "summary.json"));
        var ecologyA = summaryA.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology").GetRawText();
        var ecologyB = summaryB.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecology").GetRawText();

        Assert.Equal(ecologyA, ecologyB);
    }

    [Fact]
    public void Compare_OldBaselineWithoutEcologyBlock_StillParses()
    {
        var baselineArtifact = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "724",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.Equal(0, baselineExit);

        var oldBaselinePath = RemoveEcologyBlocksFromSummary(Path.Combine(baselineArtifact, "summary.json"));

        var compareArtifact = CreateArtifactDir();
        var compareExit = RunScenarioRunner(
            compareArtifact,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "724",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_COMPARE"] = "true",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = oldBaselinePath
            },
            out _,
            out _);

        Assert.Equal(0, compareExit);
        Assert.True(File.Exists(Path.Combine(compareArtifact, "compare.json")));
    }

    [Fact]
    public void ConfigJson_EcologyBalanceValues_ArePersistedAsEffectiveValues()
    {
        var artifactDir = CreateArtifactDir();
        var configJson = "[{\"Name\":\"ecology-balance\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"AnimalReplenishmentChancePerSecond\":0.25,\"PredatorReplenishmentChance\":0.75,\"FoodRegrowthMinSeconds\":9.0,\"FoodRegrowthJitterSeconds\":4.0}]";

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

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var balance = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecologyBalance");
        Assert.Equal(0.25f, balance.GetProperty("animalReplenishmentChancePerSecond").GetSingle());
        Assert.Equal(0.75f, balance.GetProperty("predatorReplenishmentChance").GetSingle());
        Assert.Equal(9f, balance.GetProperty("foodRegrowthMinSeconds").GetSingle());
        Assert.Equal(4f, balance.GetProperty("foodRegrowthJitterSeconds").GetSingle());
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

        Assert.Equal(0, exitCode);
        using var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var balance = summary.RootElement.GetProperty("runs").EnumerateArray().First().GetProperty("ecologyBalance");
        Assert.Equal(1f, balance.GetProperty("animalReplenishmentChancePerSecond").GetSingle());
        Assert.Equal(0f, balance.GetProperty("predatorReplenishmentChance").GetSingle());
        Assert.True(balance.GetProperty("foodRegrowthMinSeconds").GetSingle() > 0f);
        Assert.Equal(3600f, balance.GetProperty("foodRegrowthJitterSeconds").GetSingle());
    }

    private static string RemoveEcologyBlocksFromSummary(string summaryPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                   ?? throw new InvalidOperationException("Invalid summary json");
        var runs = node["runs"]?.AsArray() ?? throw new InvalidOperationException("Summary missing runs");
        foreach (var run in runs.OfType<JsonObject>())
        {
            run.Remove("ecology");
            run.Remove("ecologyBalance");
        }

        var patchedPath = Path.Combine(Path.GetTempPath(), $"worldsim-baseline-no-ecology-{Guid.NewGuid():N}.json");
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
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

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

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-ecology-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
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
