using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class ArtifactBundleTests
{
    [Fact]
    public void ArtifactBundle_ManifestHasCorrectRunCount()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(artifactDir, "11,12", "simple", output: "json");

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("smr/v1", manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(2, manifest.RootElement.GetProperty("totalRuns").GetInt32());
        Assert.Equal(0, manifest.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("ok", manifest.RootElement.GetProperty("exitReason").GetString());
        Assert.Equal("Headless", manifest.RootElement.GetProperty("effectiveVisualLane").GetString());
        Assert.Equal("default", manifest.RootElement.GetProperty("visualLaneSource").GetString());

        var runId = manifest.RootElement.GetProperty("runId").GetString();
        Assert.True(Guid.TryParse(runId, out _));
    }

    [Fact]
    public void ArtifactBundle_PerRunFilesExist()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(artifactDir, "101,303", "simple", output: "json");

        var runsDir = Path.Combine(artifactDir, "runs");
        Assert.True(Directory.Exists(runsDir));

        var files = Directory.GetFiles(runsDir, "*.json")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, files.Length);
        Assert.All(files, file =>
        {
            Assert.StartsWith("default_", file, StringComparison.Ordinal);
            Assert.EndsWith(".json", file, StringComparison.Ordinal);
        });
        Assert.Contains(files, file => file!.EndsWith("_101.json", StringComparison.Ordinal));
        Assert.Contains(files, file => file!.EndsWith("_303.json", StringComparison.Ordinal));

        foreach (var file in files)
        {
            var runDoc = ReadJson(Path.Combine(runsDir, file!));
            Assert.Equal("default", runDoc.RootElement.GetProperty("configName").GetString());
            Assert.Equal("Headless", runDoc.RootElement.GetProperty("visualLane").GetString());
            Assert.True(runDoc.RootElement.TryGetProperty("aiNoPlanDecisions", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("aiReplanBackoffDecisions", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("aiResearchTechDecisions", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("contact", out var contactTelemetry));
            Assert.True(contactTelemetry.TryGetProperty("hostileSensed", out _));
            Assert.True(contactTelemetry.TryGetProperty("routingBeforeDamage", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("ai", out var aiTelemetry));
            Assert.True(aiTelemetry.TryGetProperty("goalCounts", out _));
            Assert.True(aiTelemetry.TryGetProperty("targetKindCounts", out _));
            Assert.True(aiTelemetry.TryGetProperty("latestDecision", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("ecology", out var ecologyTelemetry));
            Assert.True(ecologyTelemetry.TryGetProperty("herbivores", out _));
            Assert.True(ecologyTelemetry.TryGetProperty("predators", out _));
            Assert.True(ecologyTelemetry.TryGetProperty("activeFoodNodes", out _));
            Assert.True(ecologyTelemetry.TryGetProperty("depletedFoodNodes", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("ecologyBalance", out var ecologyBalance));
            Assert.True(ecologyBalance.TryGetProperty("animalReplenishmentChancePerSecond", out _));
            Assert.True(ecologyBalance.TryGetProperty("predatorReplenishmentChance", out _));
            Assert.True(ecologyBalance.TryGetProperty("foodRegrowthMinSeconds", out _));
            Assert.True(ecologyBalance.TryGetProperty("foodRegrowthJitterSeconds", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("enablePredatorHumanAttacks", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("combatDeaths", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("predatorKillsByHumans", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("battleTicks", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("peakActiveBattles", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("peakActiveCombatGroups", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("peakRoutingPeople", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("ticksWithActiveBattle", out _));
            Assert.True(runDoc.RootElement.TryGetProperty("minCombatMoraleObserved", out _));
        }
    }

    [Fact]
    public void ArtifactBundle_SummaryMatchesRunCount()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(artifactDir, "7,8", "simple", output: "json");

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var runsLength = summary.RootElement.GetProperty("runs").GetArrayLength();
        Assert.Equal(2, runsLength);

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(manifest.RootElement.GetProperty("totalRuns").GetInt32(), runsLength);
    }

    [Fact]
    public void ArtifactBundle_AnomaliesIsEmptyArray()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(artifactDir, "42", "simple", output: "json");

        var anomalies = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        Assert.Equal(JsonValueKind.Array, anomalies.RootElement.ValueKind);
        Assert.Equal(0, anomalies.RootElement.GetArrayLength());

        var runLogPath = Path.Combine(artifactDir, "run.log");
        Assert.True(File.Exists(runLogPath));
        Assert.NotEmpty(File.ReadAllText(runLogPath));
    }

    [Fact]
    public void ArtifactBundle_NotWrittenWhenDirNotSet()
    {
        var repoRoot = FindRepoRoot();
        var manifestPath = Path.Combine(repoRoot, "manifest.json");
        var summaryPath = Path.Combine(repoRoot, "summary.json");
        var anomaliesPath = Path.Combine(repoRoot, "anomalies.json");
        var runLogPath = Path.Combine(repoRoot, "run.log");

        File.Delete(manifestPath);
        File.Delete(summaryPath);
        File.Delete(anomaliesPath);
        File.Delete(runLogPath);

        RunScenarioRunner(artifactDir: null, "13", "simple", output: "json");

        Assert.False(File.Exists(manifestPath));
        Assert.False(File.Exists(summaryPath));
        Assert.False(File.Exists(anomaliesPath));
        Assert.False(File.Exists(runLogPath));
    }

    [Fact]
    public void ArtifactBundle_VisualLaneOverride_IsPersisted()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(
            artifactDir,
            "77",
            "simple",
            output: "json",
            extraEnv: new Dictionary<string, string>
            {
                ["WORLDSIM_VISUAL_PROFILE"] = "showcase"
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("Showcase", manifest.RootElement.GetProperty("effectiveVisualLane").GetString());
        Assert.Equal("env", manifest.RootElement.GetProperty("visualLaneSource").GetString());

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs")[0];
        Assert.Equal("Showcase", firstRun.GetProperty("visualLane").GetString());
    }

    [Fact]
    public void ArtifactBundle_InvalidVisualProfile_FallsBackToHeadlessWithFallbackSource()
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(
            artifactDir,
            "79",
            "simple",
            output: "json",
            extraEnv: new Dictionary<string, string>
            {
                ["WORLDSIM_VISUAL_PROFILE"] = "invalid-lane"
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal("Headless", manifest.RootElement.GetProperty("effectiveVisualLane").GetString());
        Assert.Equal("fallback_invalid", manifest.RootElement.GetProperty("visualLaneSource").GetString());

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs")[0];
        Assert.Equal("Headless", firstRun.GetProperty("visualLane").GetString());
    }

    [Theory]
    [InlineData("dev_lite", "DevLite")]
    [InlineData("low", "DevLite")]
    [InlineData("medium", "Showcase")]
    public void ArtifactBundle_VisualProfileAliases_ArePersistedWithEnvSource(string aliasValue, string expectedLane)
    {
        var artifactDir = CreateArtifactDir();
        RunScenarioRunner(
            artifactDir,
            "80",
            "simple",
            output: "json",
            extraEnv: new Dictionary<string, string>
            {
                ["WORLDSIM_VISUAL_PROFILE"] = aliasValue
            });

        var manifest = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.Equal(expectedLane, manifest.RootElement.GetProperty("effectiveVisualLane").GetString());
        Assert.Equal("env", manifest.RootElement.GetProperty("visualLaneSource").GetString());

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summary.RootElement.GetProperty("runs")[0];
        Assert.Equal(expectedLane, firstRun.GetProperty("visualLane").GetString());
    }

    [Fact]
    public void ArtifactBundle_DistinctConfigNames_DoNotCollideAfterSanitization()
    {
        var artifactDir = CreateArtifactDir();
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "WorldSim.ScenarioRunner", "WorldSim.ScenarioRunner.csproj");
        var configJson = "[{\"Name\":\"A B\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0},{\"Name\":\"A_B\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

        var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = "901";
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        startInfo.Environment["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson;
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);

        Assert.True(result.ExitCode == 0, $"ScenarioRunner failed. Exit={result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");

        var files = Directory.GetFiles(Path.Combine(artifactDir, "runs"), "*.json")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, files.Length);
        Assert.NotEqual(files[0], files[1]);
    }

    [Fact]
    public void ArtifactBundle_ConfigJsonPredatorHumanToggle_IsPersistedPerRun()
    {
        var artifactDir = CreateArtifactDir();
        var repoRoot = FindRepoRoot();
        var projectPath = Path.Combine(repoRoot, "WorldSim.ScenarioRunner", "WorldSim.ScenarioRunner.csproj");
        var configJson = "[{\"Name\":\"predator-toggle\",\"Width\":32,\"Height\":20,\"InitialPop\":12,\"Ticks\":8,\"Dt\":0.25,\"EnableCombatPrimitives\":false,\"EnableDiplomacy\":false,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0,\"EnableSiege\":true,\"EnablePredatorHumanAttacks\":true}]";

        var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = "902";
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = "simple";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = "json";
        startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        startInfo.Environment["WORLDSIM_SCENARIO_CONFIGS_JSON"] = configJson;
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);

        Assert.True(result.ExitCode == 0, $"ScenarioRunner failed. Exit={result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");

        var summary = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var run = summary.RootElement.GetProperty("runs")[0];
        Assert.True(run.GetProperty("enablePredatorHumanAttacks").GetBoolean());
    }

    private static string CreateArtifactDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-artifacts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static JsonDocument ReadJson(string path)
    {
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private static void RunScenarioRunner(
        string? artifactDir,
        string seeds,
        string planners,
        string output,
        IReadOnlyDictionary<string, string>? extraEnv = null)
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

        startInfo.Environment["WORLDSIM_SCENARIO_SEEDS"] = seeds;
        startInfo.Environment["WORLDSIM_SCENARIO_PLANNERS"] = planners;
        startInfo.Environment["WORLDSIM_SCENARIO_TICKS"] = "8";
        startInfo.Environment["WORLDSIM_SCENARIO_OUTPUT"] = output;
        startInfo.Environment.Remove("WORLDSIM_VISUAL_PROFILE");

        if (artifactDir is not null)
            startInfo.Environment["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = artifactDir;
        else
            startInfo.Environment.Remove("WORLDSIM_SCENARIO_ARTIFACT_DIR");

        if (extraEnv is not null)
        {
            foreach (var (key, value) in extraEnv)
                startInfo.Environment[key] = value;
        }

        var result = ScenarioRunnerProcess.Run(startInfo, artifactDir);

        Assert.True(result.ExitCode == 0, $"ScenarioRunner failed. Exit={result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");
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
