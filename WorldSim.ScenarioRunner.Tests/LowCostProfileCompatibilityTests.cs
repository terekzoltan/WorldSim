using System.Diagnostics;
using System.Text.Json;

namespace WorldSim.ScenarioRunner.Tests;

public sealed class LowCostProfileCompatibilityTests
{
    private const string CombatCompatibilityConfigJson = "[{\"Name\":\"lc1-c1-contact-compat\",\"Width\":96,\"Height\":56,\"InitialPop\":40,\"Ticks\":300,\"Dt\":0.25,\"EnableCombatPrimitives\":true,\"EnableDiplomacy\":true,\"EnableSiege\":true,\"StoneBuildingsEnabled\":false,\"BirthRateMultiplier\":1.0,\"MovementSpeedMultiplier\":1.0}]";

    [Theory]
    [InlineData("simple")]
    [InlineData("goap")]
    [InlineData("htn")]
    public void HeadlessAndDevLite_CrossLaneEvidence_RemainsDeterministic(string planner)
    {
        var (headlessRun, devLiteRun) = RunPair(seed: 901, planner: planner);

        Assert.Equal("Headless", headlessRun.GetProperty("visualLane").GetString());
        Assert.Equal("DevLite", devLiteRun.GetProperty("visualLane").GetString());

        Assert.Equal(
            headlessRun.GetProperty("ai").GetRawText(),
            devLiteRun.GetProperty("ai").GetRawText());

        Assert.Equal(
            headlessRun.GetProperty("contact").GetRawText(),
            devLiteRun.GetProperty("contact").GetRawText());

        Assert.Equal(
            headlessRun.GetProperty("ecology").GetRawText(),
            devLiteRun.GetProperty("ecology").GetRawText());

        Assert.Equal(
            headlessRun.GetProperty("aiNoPlanDecisions").GetInt32(),
            devLiteRun.GetProperty("aiNoPlanDecisions").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("aiReplanBackoffDecisions").GetInt32(),
            devLiteRun.GetProperty("aiReplanBackoffDecisions").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("aiResearchTechDecisions").GetInt32(),
            devLiteRun.GetProperty("aiResearchTechDecisions").GetInt32());

        AssertEqualCombatAdjacentRunMetrics(headlessRun, devLiteRun);

        AssertContactTelemetryIsNonTrivial(headlessRun.GetProperty("contact"));
        AssertContactTelemetryIsNonTrivial(devLiteRun.GetProperty("contact"));
    }

    private static (JsonElement HeadlessRun, JsonElement DevLiteRun) RunPair(int seed, string planner)
    {
        var headlessArtifact = CreateArtifactDir();
        var headlessExitCode = RunScenarioRunner(
            headlessArtifact,
            env: new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = seed.ToString(),
                ["WORLDSIM_SCENARIO_PLANNERS"] = planner,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = CombatCompatibilityConfigJson
            },
            out var headlessStdout,
            out var headlessStderr);
        Assert.True(
            headlessExitCode == 0,
            $"Expected exit code 0 for compatibility run. Exit={headlessExitCode}\nSTDOUT:\n{headlessStdout}\nSTDERR:\n{headlessStderr}");

        var devLiteArtifact = CreateArtifactDir();
        var devLiteExitCode = RunScenarioRunner(
            devLiteArtifact,
            env: new Dictionary<string, string>
            {
                ["WORLDSIM_SCENARIO_SEEDS"] = seed.ToString(),
                ["WORLDSIM_SCENARIO_PLANNERS"] = planner,
                ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
                ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = CombatCompatibilityConfigJson,
                ["WORLDSIM_VISUAL_PROFILE"] = "devlite"
            },
            out var devLiteStdout,
            out var devLiteStderr);
        Assert.True(
            devLiteExitCode == 0,
            $"Expected exit code 0 for compatibility run. Exit={devLiteExitCode}\nSTDOUT:\n{devLiteStdout}\nSTDERR:\n{devLiteStderr}");

        using var headlessSummary = ReadJson(Path.Combine(headlessArtifact, "summary.json"));
        using var devLiteSummary = ReadJson(Path.Combine(devLiteArtifact, "summary.json"));

        var headlessRun = headlessSummary.RootElement.GetProperty("runs").EnumerateArray().First();
        var devLiteRun = devLiteSummary.RootElement.GetProperty("runs").EnumerateArray().First();

        Assert.Equal("Headless", headlessRun.GetProperty("visualLane").GetString());
        Assert.Equal("DevLite", devLiteRun.GetProperty("visualLane").GetString());

        return (headlessRun.Clone(), devLiteRun.Clone());
    }

    private static void AssertContactTelemetryIsNonTrivial(JsonElement contact)
    {
        var hostileSensed = contact.GetProperty("hostileSensed").GetInt32();
        var adjacentContacts = contact.GetProperty("adjacentContacts").GetInt32();
        var factionDamageEvents = contact.GetProperty("factionCombatDamageEvents").GetInt32();
        var battlePairings = contact.GetProperty("battlePairings").GetInt32();

        Assert.True(hostileSensed > 0, "Expected hostile sensing activity in combat-enabled compatibility config.");
        Assert.True(
            adjacentContacts > 0 || factionDamageEvents > 0 || battlePairings > 0,
            "Expected non-trivial contact/combat activity in compatibility config.");
    }

    private static void AssertEqualCombatAdjacentRunMetrics(JsonElement headlessRun, JsonElement devLiteRun)
    {
        Assert.Equal(
            headlessRun.GetProperty("combatEngagements").GetInt32(),
            devLiteRun.GetProperty("combatEngagements").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("combatDeaths").GetInt32(),
            devLiteRun.GetProperty("combatDeaths").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("battleTicks").GetInt32(),
            devLiteRun.GetProperty("battleTicks").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("peakActiveBattles").GetInt32(),
            devLiteRun.GetProperty("peakActiveBattles").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("peakActiveCombatGroups").GetInt32(),
            devLiteRun.GetProperty("peakActiveCombatGroups").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("peakRoutingPeople").GetInt32(),
            devLiteRun.GetProperty("peakRoutingPeople").GetInt32());
        Assert.Equal(
            headlessRun.GetProperty("ticksWithActiveBattle").GetInt32(),
            devLiteRun.GetProperty("ticksWithActiveBattle").GetInt32());
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

        startInfo.Environment.Remove("WORLDSIM_SCENARIO_TICKS");
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
        var dir = Path.Combine(Path.GetTempPath(), $"worldsim-profile-compat-{Guid.NewGuid():N}");
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
