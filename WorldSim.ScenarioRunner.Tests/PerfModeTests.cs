using System.Diagnostics;
using System.Text.Json;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class PerfModeTests
{
    [Fact]
    public void PerfMode_PerfJsonArtifact_IsWritten()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "401",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_PERF"] = "true"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        var perfPath = Path.Combine(artifactDir, "perf.json");
        Assert.True(File.Exists(perfPath));

        var perfDoc = ReadJson(perfPath);
        Assert.True(perfDoc.RootElement.GetArrayLength() > 0);
        var first = perfDoc.RootElement[0];
        var budget = first.GetProperty("budget");
        Assert.Contains(budget.GetProperty("avgTickStatus").GetString(), new[] { "green", "yellow", "red" });
        Assert.Contains(budget.GetProperty("p99TickStatus").GetString(), new[] { "green", "yellow", "red" });
        Assert.Contains(budget.GetProperty("peakEntitiesStatus").GetString(), new[] { "green", "yellow", "red" });

        var manifestDoc = ReadJson(Path.Combine(artifactDir, "manifest.json"));
        Assert.True(manifestDoc.RootElement.GetProperty("perfEnabled").GetBoolean());
        Assert.True(manifestDoc.RootElement.TryGetProperty("perfRunCount", out _));
        Assert.True(manifestDoc.RootElement.TryGetProperty("perfRedCount", out _));
        Assert.True(manifestDoc.RootElement.TryGetProperty("perfYellowCount", out _));
    }

    [Fact]
    public void PerfMode_RunResults_HaveNonZeroPerfFields()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "402",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_PERF"] = "true"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        var summaryDoc = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summaryDoc.RootElement.GetProperty("runs")[0];

        var avg = firstRun.GetProperty("perfAvgTickMs").GetDouble();
        var max = firstRun.GetProperty("perfMaxTickMs").GetDouble();
        var p99 = firstRun.GetProperty("perfP99TickMs").GetDouble();
        var peak = firstRun.GetProperty("perfPeakEntities").GetInt64();

        Assert.True(avg > 0d);
        Assert.True(max >= avg);
        Assert.True(p99 <= max);
        Assert.True(peak > 0L);
    }

    [Fact]
    public void PerfMode_Disabled_PerfFieldsAreZero()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "403",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        var summaryDoc = ReadJson(Path.Combine(artifactDir, "summary.json"));
        var firstRun = summaryDoc.RootElement.GetProperty("runs")[0];

        Assert.Equal(0d, firstRun.GetProperty("perfAvgTickMs").GetDouble());
        Assert.Equal(0d, firstRun.GetProperty("perfMaxTickMs").GetDouble());
        Assert.Equal(0d, firstRun.GetProperty("perfP99TickMs").GetDouble());
        Assert.Equal(0L, firstRun.GetProperty("perfPeakEntities").GetInt64());
    }

    [Fact]
    public void PerfMode_PerfFail_Returns4OnRedZone()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "404",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_PERF"] = "true",
                ["WORLDSIM_SCENARIO_PERF_FAIL"] = "true"
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode == 0 || exitCode == 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        var anomaliesDoc = ReadJson(Path.Combine(artifactDir, "anomalies.json"));
        var perfAnomalies = anomaliesDoc.RootElement.EnumerateArray()
            .Where(item => item.GetProperty("id").GetString()?.StartsWith("ANOM-PERF-", StringComparison.Ordinal) == true)
            .ToArray();

        foreach (var anomaly in perfAnomalies)
            Assert.Equal("perf", anomaly.GetProperty("category").GetString());
    }

    [Fact]
    public void ModeAll_ProducesCompatibleArtifacts()
    {
        var baselineArtifactDir = CreateArtifactDir();
        var baselineExit = RunScenarioRunner(
            baselineArtifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "405",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json"
            },
            out _,
            out _);
        Assert.True(baselineExit is 0 or 2);

        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "405",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_MODE"] = "all",
                ["WORLDSIM_SCENARIO_BASELINE_PATH"] = Path.Combine(baselineArtifactDir, "summary.json")
            },
            out var stdout,
            out var stderr);

        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        Assert.True(File.Exists(Path.Combine(artifactDir, "assertions.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "compare.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "perf.json")));
    }

    [Fact]
    public void ModeAll_WithoutBaseline_GracefulSkip()
    {
        var artifactDir = CreateArtifactDir();
        var exitCode = RunScenarioRunner(
            artifactDir,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "406",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_MODE"] = "all"
            },
            out var stdout,
            out var stderr);

        Assert.NotEqual(3, exitCode);
        Assert.False(File.Exists(Path.Combine(artifactDir, "compare.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "assertions.json")));
        Assert.True(File.Exists(Path.Combine(artifactDir, "perf.json")));
        Assert.True(exitCode is 0 or 2 or 4, $"Unexpected exit code {exitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    [Fact]
    public void ModeAssert_EquivalentToAssertFlag()
    {
        var artifactDirMode = CreateArtifactDir();
        var exitMode = RunScenarioRunner(
            artifactDirMode,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "407",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_MODE"] = "assert"
            },
            out _,
            out _);

        var artifactDirFlag = CreateArtifactDir();
        var exitFlag = RunScenarioRunner(
            artifactDirFlag,
            new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = "407",
                ["WORLDSIM_SCENARIO_PLANNERS"] = "simple",
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_ASSERT"] = "true"
            },
            out _,
            out _);

        Assert.Equal(exitFlag, exitMode);
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
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-scenario-perf-{Guid.NewGuid():N}");
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
