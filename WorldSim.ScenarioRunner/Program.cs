using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Simulation;

var outputMode = ParseOutputMode(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_OUTPUT"));
var runLogBuffer = new StringBuilder();
void LogLine(string line)
{
    Console.WriteLine(line);
    runLogBuffer.AppendLine(line);
}
void LogWarning(string line)
{
    if (outputMode == ScenarioOutputMode.Text)
        Console.WriteLine(line);
    else
        Console.Error.WriteLine(line);

    runLogBuffer.AppendLine(line);
}
void LogBufferOnly(string line)
{
    runLogBuffer.AppendLine(line);
}

var seeds = ParseCsvInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_SEEDS")) ?? new[] { 101, 202, 303 };
var planners = ParsePlannerModes(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PLANNERS"));
var mode = ParseScenarioMode(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_MODE"));
var assertEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_ASSERT"), false)
    || mode is ScenarioMode.Assert or ScenarioMode.All;
var anomalyFailEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_ANOMALY_FAIL"), false);
var compareEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_COMPARE"), false)
    || mode is ScenarioMode.Compare or ScenarioMode.All;
var deltaFailEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DELTA_FAIL"), false);
var perfEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PERF"), false)
    || mode is ScenarioMode.Perf or ScenarioMode.All;
var perfFailEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_PERF_FAIL"), false);
var drilldownEnabled = ParseBool(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DRILLDOWN"), false);
var drilldownTopN = ParseIntClamped(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DRILLDOWN_TOP"), fallback: 3, min: 1, max: 10);
var drilldownSampleEvery = ParseIntClamped(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_SAMPLE_EVERY"), fallback: 25, min: 1, max: 1000);
var baselinePath = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_BASELINE_PATH");
var rawConfigsJson = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_CONFIGS_JSON");
var parsedConfigs = ParseScenarioConfigs(rawConfigsJson);
foreach (var warning in parsedConfigs.Warnings)
    LogWarning(warning);
var configs = parsedConfigs.Configs;
var artifactDir = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_ARTIFACT_DIR");

if (configs.Count == 0 && string.IsNullOrWhiteSpace(rawConfigsJson))
{
    var fallbackTicks = ParseInt(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_TICKS"), 1200);
    var fallbackDt = ParseFloat(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_DT"), 0.25f);
    configs.Add(new ScenarioConfig(
        Name: "default",
        Width: 64,
        Height: 40,
        InitialPop: 24,
        Ticks: fallbackTicks,
        Dt: fallbackDt,
        EnableCombatPrimitives: false,
        EnableDiplomacy: false,
        EnableSiege: true,
        StoneBuildingsEnabled: false,
        BirthRateMultiplier: 1f,
        MovementSpeedMultiplier: 1f));
}

var runs = new List<ScenarioRunResult>(configs.Count * planners.Count * seeds.Length);
var runTimelines = new Dictionary<string, List<ScenarioTimelineSample>>(StringComparer.Ordinal);
foreach (var config in configs.OrderBy(c => c.Name, StringComparer.Ordinal))
{
    foreach (var planner in planners)
    {
        foreach (var seed in seeds.OrderBy(s => s))
        {
            var world = new World(
                width: config.Width,
                height: config.Height,
                initialPop: config.InitialPop,
                brainFactory: _ => new RuntimeNpcBrain(planner, $"ScenarioRunner:{planner}"),
                randomSeed: seed)
            {
                EnableCombatPrimitives = config.EnableCombatPrimitives,
                EnableDiplomacy = config.EnableDiplomacy,
                EnableSiege = config.EnableSiege,
                StoneBuildingsEnabled = config.StoneBuildingsEnabled,
                BirthRateMultiplier = config.BirthRateMultiplier,
                MovementSpeedMultiplier = config.MovementSpeedMultiplier
            };

            List<double>? tickTimesMs = perfEnabled ? new List<double>(config.Ticks) : null;
            long peakEntities = 0;
            var timelineSamples = drilldownEnabled ? new List<ScenarioTimelineSample>() : null;
            var peakActiveBattles = 0;
            var peakActiveCombatGroups = 0;
            var peakRoutingPeople = 0;
            var ticksWithActiveBattle = 0;
            var minCombatMoraleObserved = 100f;
            var sawLivingPerson = false;

            for (var i = 0; i < config.Ticks; i++)
            {
                var perfTickMs = 0d;
                if (perfEnabled)
                {
                    var stopwatch = Stopwatch.StartNew();
                    world.Update(config.Dt);
                    stopwatch.Stop();
                    perfTickMs = stopwatch.Elapsed.TotalMilliseconds;
                    tickTimesMs!.Add(perfTickMs);

                    var entityCount = world._people.Count(p => p.Health > 0f)
                        + world._animals.Count(a => a.IsAlive)
                        + world._colonies.Sum(c => c.HouseCount)
                        + world.DefensiveStructures.Count;
                    if (entityCount > peakEntities)
                        peakEntities = entityCount;
                }
                else
                {
                    world.Update(config.Dt);
                }

                if (timelineSamples is not null && ShouldCaptureTickSample(i, config.Ticks, drilldownSampleEvery))
                    timelineSamples.Add(BuildTimelineSample(world, i + 1, perfTickMs));

                peakActiveBattles = Math.Max(peakActiveBattles, world.ActiveBattleCount);
                peakActiveCombatGroups = Math.Max(peakActiveCombatGroups, world.ActiveCombatGroupCount);
                if (world.ActiveBattleCount > 0)
                    ticksWithActiveBattle++;

                var routingPeople = 0;
                foreach (var person in world._people)
                {
                    if (person.Health <= 0f)
                        continue;

                    sawLivingPerson = true;
                    routingPeople += person.IsRouting ? 1 : 0;
                    if (person.CombatMorale < minCombatMoraleObserved)
                        minCombatMoraleObserved = person.CombatMorale;
                }

                peakRoutingPeople = Math.Max(peakRoutingPeople, routingPeople);
            }

            var runResult = BuildRunResult(
                world,
                config,
                planner,
                seed,
                tickTimesMs,
                peakEntities,
                peakActiveBattles,
                peakActiveCombatGroups,
                peakRoutingPeople,
                ticksWithActiveBattle,
                sawLivingPerson ? minCombatMoraleObserved : 100f);
            runs.Add(runResult);
            if (timelineSamples is not null)
                runTimelines[BuildRunKey(runResult)] = timelineSamples;
        }
    }
}

var envelope = new ScenarioRunEnvelope(
    GeneratedAtUtc: DateTime.UtcNow,
    SeedCount: seeds.Length,
    PlannerCount: planners.Count,
    ConfigCount: configs.Count,
    Runs: runs.ToList());

var baselineLoad = compareEnabled
    ? ParseBaselineEnvelope(baselinePath)
    : new BaselineLoadResult(false, false, baselinePath, null, null);
if (compareEnabled && baselineLoad.ErrorMessage is not null)
{
    LogWarning($"Warning: compare baseline unavailable ({baselineLoad.ErrorMessage})");
}

if (drilldownEnabled && string.IsNullOrWhiteSpace(artifactDir))
    LogWarning("Warning: drilldown enabled but WORLDSIM_SCENARIO_ARTIFACT_DIR is not set; drilldown export skipped.");

var compareReport = compareEnabled && baselineLoad.Envelope is not null
    ? EvaluateBaselineComparison(envelope, baselineLoad.Envelope)
    : null;

var hasConfigError = parsedConfigs.HadError || baselineLoad.HadError;
var evaluation = EvaluateScenario(
    envelope,
    assertEnabled,
    anomalyFailEnabled,
    deltaFailEnabled,
    perfEnabled,
    perfFailEnabled,
    hasConfigError,
    compareEnabled,
    compareReport,
    LogBufferOnly);

WriteOutput(envelope, outputMode, seeds, planners, configs, LogLine);
WriteEvaluationOutput(evaluation, outputMode, LogLine, LogBufferOnly);

Environment.ExitCode = evaluation.ExitCode;
if (!string.IsNullOrWhiteSpace(artifactDir))
{
    WriteArtifactBundle(envelope, evaluation, artifactDir, runLogBuffer.ToString(), drilldownEnabled, drilldownTopN, drilldownSampleEvery, runTimelines);
}

return Environment.ExitCode;

static ScenarioRunResult BuildRunResult(
    World world,
    ScenarioConfig config,
    NpcPlannerMode planner,
    int seed,
    List<double>? tickTimesMs,
    long peakEntities,
    int peakActiveBattles,
    int peakActiveCombatGroups,
    int peakRoutingPeople,
    int ticksWithActiveBattle,
    float minCombatMoraleObserved)
{
    var livingColonies = world._colonies.Count(colony => world._people.Any(person => person.Home == colony && person.Health > 0f));
    var totalFood = world._colonies.Sum(colony => colony.Stock[Resource.Food]);
    var totalPeople = world._people.Count(person => person.Health > 0f);
    var avgFoodPerPerson = totalPeople > 0 ? totalFood / (float)totalPeople : 0f;

    var perfAvgTickMs = 0d;
    var perfMaxTickMs = 0d;
    var perfP99TickMs = 0d;
    var perfPeakEntities = 0L;
    var aiTelemetry = world.BuildScenarioAiTelemetrySnapshot();
    if (tickTimesMs is { Count: > 0 })
    {
        perfAvgTickMs = tickTimesMs.Average();
        perfMaxTickMs = tickTimesMs.Max();
        var sortedTickTimes = tickTimesMs.OrderBy(value => value).ToArray();
        var p99Index = (int)(0.99 * sortedTickTimes.Length);
        if (p99Index >= sortedTickTimes.Length)
            p99Index = sortedTickTimes.Length - 1;
        perfP99TickMs = sortedTickTimes[p99Index];
        perfPeakEntities = peakEntities;
    }

    return new ScenarioRunResult(
        ConfigName: config.Name,
        PlannerMode: planner.ToString(),
        Seed: seed,
        Width: config.Width,
        Height: config.Height,
        InitialPop: config.InitialPop,
        Ticks: config.Ticks,
        Dt: config.Dt,
        EnableCombatPrimitives: config.EnableCombatPrimitives,
        EnableDiplomacy: config.EnableDiplomacy,
        EnableSiege: config.EnableSiege,
        StoneBuildingsEnabled: config.StoneBuildingsEnabled,
        BirthRateMultiplier: config.BirthRateMultiplier,
        MovementSpeedMultiplier: config.MovementSpeedMultiplier,
        LivingColonies: livingColonies,
        People: totalPeople,
        Food: totalFood,
        AverageFoodPerPerson: avgFoodPerPerson,
        DeathsOldAge: world.TotalDeathsOldAge,
        DeathsStarvation: world.TotalDeathsStarvation,
        DeathsPredator: world.TotalDeathsPredator,
        DeathsOther: world.TotalDeathsOther,
        CombatDeaths: world.TotalCombatDeaths,
        CombatEngagements: world.TotalCombatEngagements,
        PredatorKillsByHumans: world.TotalPredatorKillsByHumans,
        BattleTicks: world.TotalBattleTicks,
        PeakActiveBattles: peakActiveBattles,
        PeakActiveCombatGroups: peakActiveCombatGroups,
        PeakRoutingPeople: peakRoutingPeople,
        TicksWithActiveBattle: ticksWithActiveBattle,
        MinCombatMoraleObserved: minCombatMoraleObserved,
        DeathsStarvationRecent60s: world.RecentDeathsStarvation60s,
        DeathsStarvationWithFood: world.TotalStarvationDeathsWithFood,
        OverlapResolveMoves: world.TotalOverlapResolveMoves,
        CrowdDissipationMoves: world.TotalCrowdDissipationMoves,
        BirthFallbackToOccupied: world.TotalBirthFallbackToOccupiedCount,
        BirthFallbackToParent: world.TotalBirthFallbackToParentCount,
        BuildSiteResets: world.TotalBuildSiteResetCount,
        NoProgressBackoffResource: world.TotalNoProgressBackoffResource,
        NoProgressBackoffBuild: world.TotalNoProgressBackoffBuild,
        NoProgressBackoffFlee: world.TotalNoProgressBackoffFlee,
        NoProgressBackoffCombat: world.TotalNoProgressBackoffCombat,
        AiNoPlanDecisions: world.TotalAiNoPlanDecisions,
        AiReplanBackoffDecisions: world.TotalAiReplanBackoffDecisions,
        AiResearchTechDecisions: world.TotalAiResearchTechDecisions,
        DenseNeighborhoodTicks: world.DenseNeighborhoodTicks,
        LastTickDenseActors: world.LastTickDenseActors,
        Ai: aiTelemetry,
        PerfAvgTickMs: perfAvgTickMs,
        PerfMaxTickMs: perfMaxTickMs,
        PerfP99TickMs: perfP99TickMs,
        PerfPeakEntities: perfPeakEntities);
}

static void WriteOutput(
    ScenarioRunEnvelope envelope,
    ScenarioOutputMode outputMode,
    IReadOnlyList<int> seeds,
    IReadOnlyList<NpcPlannerMode> planners,
    IReadOnlyList<ScenarioConfig> configs,
    Action<string> logLine)
{
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = outputMode == ScenarioOutputMode.Json
    };

    switch (outputMode)
    {
        case ScenarioOutputMode.Json:
            logLine(JsonSerializer.Serialize(envelope, jsonOptions));
            break;

        case ScenarioOutputMode.Jsonl:
            foreach (var run in envelope.Runs)
                logLine(JsonSerializer.Serialize(run, jsonOptions));
            break;

        default:
            logLine($"ScenarioRunner matrix | seeds=[{string.Join(",", seeds)}] planners=[{string.Join(",", planners)}] configs={configs.Count}");
            foreach (var run in envelope.Runs)
            {
                logLine(
                    $"config={run.ConfigName} planner={run.PlannerMode} seed={run.Seed} livingCols={run.LivingColonies} people={run.People} food={run.Food} avgFpp={run.AverageFoodPerPerson:0.00} " +
                    $"cluster(overlap/dissipate/denseTicks/lastDense)={run.OverlapResolveMoves}/{run.CrowdDissipationMoves}/{run.DenseNeighborhoodTicks}/{run.LastTickDenseActors} " +
                    $"birthFallback(occupied/parent)={run.BirthFallbackToOccupied}/{run.BirthFallbackToParent} " +
                    $"buildSiteResets={run.BuildSiteResets} " +
                    $"backoff(resource/build/flee/combat)={run.NoProgressBackoffResource}/{run.NoProgressBackoffBuild}/{run.NoProgressBackoffFlee}/{run.NoProgressBackoffCombat}");
            }
            break;
    }
}

static void WriteArtifactBundle(
    ScenarioRunEnvelope envelope,
    ScenarioEvaluation evaluation,
    string artifactDirRaw,
    string runLog,
    bool drilldownEnabled,
    int drilldownTopN,
    int drilldownSampleEvery,
    IReadOnlyDictionary<string, List<ScenarioTimelineSample>> runTimelines)
{
    var artifactDir = Path.GetFullPath(artifactDirRaw);
    var runsDir = Path.Combine(artifactDir, "runs");
    Directory.CreateDirectory(runsDir);

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    File.WriteAllText(Path.Combine(artifactDir, "summary.json"), JsonSerializer.Serialize(envelope, jsonOptions));
    File.WriteAllText(Path.Combine(artifactDir, "anomalies.json"), JsonSerializer.Serialize(evaluation.Anomalies, jsonOptions));
    File.WriteAllText(Path.Combine(artifactDir, "assertions.json"), JsonSerializer.Serialize(evaluation.Assertions, jsonOptions));
    if (evaluation.CompareReport is not null)
    {
        File.WriteAllText(Path.Combine(artifactDir, "compare.json"), JsonSerializer.Serialize(evaluation.CompareReport, jsonOptions));
    }
    if (evaluation.PerfSummary.PerfEnabled)
    {
        File.WriteAllText(Path.Combine(artifactDir, "perf.json"), JsonSerializer.Serialize(evaluation.PerfSummary.Runs, jsonOptions));
    }

    var drilldownSummary = new ScenarioDrilldownSummary(false, 0, drilldownTopN, drilldownSampleEvery);
    if (drilldownEnabled)
    {
        drilldownSummary = WriteDrilldownArtifacts(artifactDir, envelope, evaluation, runTimelines, drilldownTopN, drilldownSampleEvery, jsonOptions);
    }

    foreach (var run in envelope.Runs)
    {
        var fileName = $"{BuildRunKey(run)}.json";
        var runPath = Path.Combine(runsDir, fileName);
        File.WriteAllText(runPath, JsonSerializer.Serialize(run, jsonOptions));
    }

    var manifest = new ScenarioArtifactManifest(
        SchemaVersion: "smr/v1",
        GeneratedAtUtc: DateTime.UtcNow,
        RunId: Guid.NewGuid().ToString("D"),
        SeedCount: envelope.SeedCount,
        PlannerCount: envelope.PlannerCount,
        ConfigCount: envelope.ConfigCount,
        TotalRuns: envelope.Runs.Count,
        ArtifactDir: artifactDir,
        ExitCode: evaluation.ExitCode,
        ExitReason: evaluation.ExitReason,
        AssertionFailures: evaluation.Summary.FailedCount,
        AssertionSkipped: evaluation.Summary.SkippedCount,
        AnomalyCount: evaluation.Summary.AnomalyCount,
        CompareEnabled: evaluation.CompareEnabled,
        CompareMatchedRuns: evaluation.CompareReport?.MatchedRunCount ?? 0,
        CompareRegressions: evaluation.CompareReport?.PassToFailRegressions.Count ?? 0,
        CompareThresholdBreaches: evaluation.CompareReport?.ThresholdBreaches.Count ?? 0,
        PerfEnabled: evaluation.PerfSummary.PerfEnabled,
        PerfRunCount: evaluation.PerfSummary.PerfRunCount,
        PerfRedCount: evaluation.PerfSummary.PerfRedCount,
        PerfYellowCount: evaluation.PerfSummary.PerfYellowCount,
        DrilldownEnabled: drilldownSummary.Enabled,
        DrilldownSelectedRuns: drilldownSummary.SelectedRuns,
        DrilldownTopN: drilldownSummary.TopN,
        DrilldownSampleEvery: drilldownSummary.SampleEvery);

    File.WriteAllText(Path.Combine(artifactDir, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions));
    File.WriteAllText(Path.Combine(artifactDir, "run.log"), runLog);
}

static ScenarioDrilldownSummary WriteDrilldownArtifacts(
    string artifactDir,
    ScenarioRunEnvelope envelope,
    ScenarioEvaluation evaluation,
    IReadOnlyDictionary<string, List<ScenarioTimelineSample>> runTimelines,
    int topN,
    int sampleEvery,
    JsonSerializerOptions jsonOptions)
{
    var drilldownIndex = BuildDrilldownIndex(envelope, evaluation, topN);
    if (drilldownIndex.Runs.Count == 0)
        return new ScenarioDrilldownSummary(true, 0, topN, sampleEvery);

    var drilldownDir = Path.Combine(artifactDir, "drilldown");
    Directory.CreateDirectory(drilldownDir);

    for (var i = 0; i < drilldownIndex.Runs.Count; i++)
    {
        var selected = drilldownIndex.Runs[i];
        var runDir = Path.Combine(drilldownDir, selected.RunKey);
        Directory.CreateDirectory(runDir);

        var timeline = runTimelines.TryGetValue(selected.RunKey, out var samples)
            ? samples
            : new List<ScenarioTimelineSample>();
        File.WriteAllText(Path.Combine(runDir, "timeline.json"), JsonSerializer.Serialize(timeline, jsonOptions));

        var events = BuildDrilldownEvents(evaluation, selected.RunKey);
        File.WriteAllText(Path.Combine(runDir, "events.json"), JsonSerializer.Serialize(events, jsonOptions));

        var run = envelope.Runs.First(candidate => string.Equals(BuildRunKey(candidate), selected.RunKey, StringComparison.Ordinal));
        var replay = new ScenarioReplaySeed(
            RunKey: selected.RunKey,
            ConfigName: run.ConfigName,
            PlannerMode: run.PlannerMode,
            Seed: run.Seed,
            TickCount: run.Ticks,
            Dt: run.Dt,
            SampleEvery: sampleEvery,
            TimelineSampleCount: timeline.Count,
            FinalPeople: run.People,
            FinalFood: run.Food,
            FinalLivingColonies: run.LivingColonies);
        File.WriteAllText(Path.Combine(runDir, "replay.json"), JsonSerializer.Serialize(replay, jsonOptions));

        drilldownIndex.Runs[i] = selected with { TimelineSamples = timeline.Count, EventCount = events.Count };
    }

    File.WriteAllText(Path.Combine(drilldownDir, "index.json"), JsonSerializer.Serialize(drilldownIndex, jsonOptions));
    return new ScenarioDrilldownSummary(true, drilldownIndex.Runs.Count, topN, sampleEvery);
}

static ScenarioDrilldownIndex BuildDrilldownIndex(ScenarioRunEnvelope envelope, ScenarioEvaluation evaluation, int topN)
{
    var assertionsByRun = evaluation.Assertions
        .Where(assertion => !assertion.Skipped && !assertion.Passed)
        .GroupBy(assertion => assertion.RunKey, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    var anomaliesByRun = evaluation.Anomalies
        .Where(anomaly => !string.IsNullOrWhiteSpace(anomaly.RunKey))
        .GroupBy(anomaly => anomaly.RunKey!, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

    var regressionsByRun = evaluation.CompareReport?.PassToFailRegressions
        .GroupBy(regression => regression.RunKey, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
        ?? new Dictionary<string, int>(StringComparer.Ordinal);

    var thresholdByRun = evaluation.CompareReport?.ThresholdBreaches
        .GroupBy(threshold => threshold.RunKey, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
        ?? new Dictionary<string, int>(StringComparer.Ordinal);

    var scoredRuns = new List<ScenarioDrilldownSelection>();
    foreach (var run in envelope.Runs)
    {
        var runKey = BuildRunKey(run);
        var assertionFails = assertionsByRun.GetValueOrDefault(runKey);
        var regressions = regressionsByRun.GetValueOrDefault(runKey);
        var thresholdBreaches = thresholdByRun.GetValueOrDefault(runKey);
        var perfRed = anomaliesByRun.GetValueOrDefault(runKey)?.Count(a => string.Equals(a.Id, "ANOM-PERF-TICK-AVG", StringComparison.Ordinal) || string.Equals(a.Id, "ANOM-PERF-TICK-P99", StringComparison.Ordinal) || string.Equals(a.Id, "ANOM-PERF-PEAK-ENTITIES", StringComparison.Ordinal)) ?? 0;

        var score = assertionFails * 100d
            + regressions * 60d
            + thresholdBreaches * 40d
            + perfRed * 25d
            + Math.Max(0d, (1d - run.AverageFoodPerPerson) * 10d)
            + (run.LivingColonies == 0 ? 20d : 0d)
            + (run.DeathsStarvationWithFood > 2 ? 10d : 0d);

        var reasons = new List<string>();
        if (assertionFails > 0) reasons.Add($"assert_fail:{assertionFails}");
        if (regressions > 0) reasons.Add($"compare_regression:{regressions}");
        if (thresholdBreaches > 0) reasons.Add($"compare_threshold:{thresholdBreaches}");
        if (perfRed > 0) reasons.Add($"perf_red:{perfRed}");
        if (run.AverageFoodPerPerson < 1f) reasons.Add("low_food_per_person");
        if (run.LivingColonies == 0) reasons.Add("colony_extinction");
        if (run.DeathsStarvationWithFood > 2) reasons.Add("starvation_with_food");

        scoredRuns.Add(new ScenarioDrilldownSelection(
            RunKey: runKey,
            Score: Math.Round(score, 3),
            Reasons: reasons,
            TimelineSamples: 0,
            EventCount: 0));
    }

    var selected = scoredRuns
        .OrderByDescending(item => item.Score)
        .ThenBy(item => item.RunKey, StringComparer.Ordinal)
        .Take(topN)
        .ToList();

    return new ScenarioDrilldownIndex(
        GeneratedAtUtc: DateTime.UtcNow,
        TopN: topN,
        Runs: selected);
}

static List<ScenarioDrilldownEvent> BuildDrilldownEvents(ScenarioEvaluation evaluation, string runKey)
{
    var events = new List<ScenarioDrilldownEvent>();

    events.AddRange(evaluation.Assertions
        .Where(assertion => string.Equals(assertion.RunKey, runKey, StringComparison.Ordinal) && !assertion.Passed)
        .Select(assertion => new ScenarioDrilldownEvent(
            Kind: "assertion",
            Id: assertion.InvariantId,
            Severity: assertion.Severity,
            Message: assertion.Message,
            Value: assertion.Measured,
            Threshold: assertion.Threshold)));

    events.AddRange(evaluation.Anomalies
        .Where(anomaly => string.Equals(anomaly.RunKey, runKey, StringComparison.Ordinal))
        .Select(anomaly => new ScenarioDrilldownEvent(
            Kind: "anomaly",
            Id: anomaly.Id,
            Severity: anomaly.Severity,
            Message: anomaly.Message,
            Value: anomaly.Value,
            Threshold: anomaly.Threshold)));

    if (evaluation.CompareReport is not null)
    {
        events.AddRange(evaluation.CompareReport.PassToFailRegressions
            .Where(regression => string.Equals(regression.RunKey, runKey, StringComparison.Ordinal))
            .Select(regression => new ScenarioDrilldownEvent(
                Kind: "compare",
                Id: regression.InvariantId,
                Severity: "warning",
                Message: "pass_to_fail regression",
                Value: regression.Transition,
                Threshold: "no regression")));

        events.AddRange(evaluation.CompareReport.ThresholdBreaches
            .Where(threshold => string.Equals(threshold.RunKey, runKey, StringComparison.Ordinal))
            .Select(threshold => new ScenarioDrilldownEvent(
                Kind: "compare",
                Id: threshold.Id,
                Severity: "warning",
                Message: $"threshold breach: {threshold.Metric}",
                Value: threshold.Measured,
                Threshold: threshold.Threshold)));
    }

    return events
        .OrderBy(item => item.Kind, StringComparer.Ordinal)
        .ThenBy(item => item.Id, StringComparer.Ordinal)
        .ToList();
}

static ScenarioEvaluation EvaluateScenario(
    ScenarioRunEnvelope envelope,
    bool assertEnabled,
    bool anomalyFailEnabled,
    bool deltaFailEnabled,
    bool perfEnabled,
    bool perfFailEnabled,
    bool hasConfigError,
    bool compareEnabled,
    ScenarioCompareReport? compareReport,
    Action<string> logWarning)
{
    var assertions = EvaluateAssertions(envelope.Runs, assertEnabled);
    var anomalies = DetectAnomalies(envelope.Runs, assertions, assertEnabled);
    var perfDetection = DetectPerfAnomalies(envelope.Runs, perfEnabled, logWarning);
    anomalies.AddRange(perfDetection.Anomalies);
    if (compareReport is not null)
    {
        anomalies.AddRange(BuildCompareAnomalies(compareReport));
    }

    var failedAssertions = assertions.Count(a => !a.Passed && !a.Skipped && string.Equals(a.Severity, "error", StringComparison.Ordinal));
    var skippedAssertions = assertions.Count(a => a.Skipped);
    var compareFailures = compareReport?.TotalFailureCount ?? 0;
    var summary = new ScenarioEvaluationSummary(
        TotalCount: assertions.Count,
        PassedCount: assertions.Count(a => a.Passed),
        FailedCount: failedAssertions,
        SkippedCount: skippedAssertions,
        AnomalyCount: anomalies.Count,
        FailedIds: assertions.Where(a => !a.Passed && !a.Skipped).Select(a => a.InvariantId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        SkippedIds: assertions.Where(a => a.Skipped).Select(a => a.InvariantId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        CompareFailures: compareFailures,
        CompareEnabled: compareEnabled);

    var perfAnomalies = anomalies.Count(a => string.Equals(a.Category, "perf", StringComparison.Ordinal));
    var nonPerfAnomalies = anomalies.Count - perfAnomalies;
    var anomalyGateFailed = (anomalyFailEnabled && nonPerfAnomalies > 0)
        || (deltaFailEnabled && compareFailures > 0)
        || (perfFailEnabled && perfAnomalies > 0);
    var (exitCode, exitReason) = ResolveExitCode(hasConfigError, summary.FailedCount > 0, anomalyGateFailed);
    return new ScenarioEvaluation(
        assertions,
        anomalies,
        summary,
        exitCode,
        exitReason,
        assertEnabled,
        anomalyFailEnabled,
        compareEnabled,
        deltaFailEnabled,
        perfEnabled,
        perfFailEnabled,
        compareReport,
        perfDetection.Summary);
}

static List<ScenarioAssertionResult> EvaluateAssertions(IReadOnlyList<ScenarioRunResult> runs, bool assertEnabled)
{
    var results = new List<ScenarioAssertionResult>();
    foreach (var run in runs)
    {
        var runKey = BuildRunKey(run);
        var totalDeaths = run.DeathsOldAge + run.DeathsStarvation + run.DeathsPredator + run.DeathsOther;

        AddAssertion("SURV-01", "survival", "error", runKey, assertEnabled, run.LivingColonies >= 1, run.LivingColonies.ToString(), ">=1", "At least one colony survives", results);
        AddAssertion("SURV-02", "survival", "error", runKey, assertEnabled, run.People > 0, run.People.ToString(), ">0", "Population remains above zero", results);

        var starvationRatio = totalDeaths > 0 ? run.DeathsStarvation / (float)totalDeaths : 0f;
        AddAssertion("SURV-03", "survival", "error", runKey, assertEnabled, starvationRatio < 0.5f, starvationRatio.ToString("0.###"), "<0.5", "Starvation deaths stay below 50% of total deaths", results);

        AddAssertion("SURV-04", "survival", "error", runKey, assertEnabled, run.AverageFoodPerPerson >= 1f, run.AverageFoodPerPerson.ToString("0.###"), ">=1.0", "Average food per person above subsistence", results);
        AddAssertion("SURV-05", "survival", "error", runKey, assertEnabled, run.DeathsStarvationWithFood <= 2, run.DeathsStarvationWithFood.ToString(), "<=2", "Starvation-with-food anomaly remains rare", results);

        AddAssertion("ECON-01", "economy", "error", runKey, assertEnabled, run.Food > 0, run.Food.ToString(), ">0", "Total food remains positive", results);
        AddAssertion("ECON-02", "economy", "info", runKey, assertEnabled, true, "informational", "informational", "Degenerate colony check is informational only", results);

        if (!run.EnableCombatPrimitives)
        {
            AddSkippedAssertion("COMB-01", "combat", runKey, "combat_primitives_disabled", "Combat counters unavailable while combat primitives are disabled", results);
            AddSkippedAssertion("COMB-02", "combat", runKey, "combat_primitives_disabled", "Combat counters unavailable while combat primitives are disabled", results);
            AddSkippedAssertion("COMB-03", "combat", runKey, "combat_primitives_disabled", "Combat assertions skipped while combat primitives are disabled", results);
            continue;
        }

        if (!ShouldRequireCombatDeaths(run))
        {
            AddSkippedAssertion("COMB-01", "combat", runKey, "combat_not_sustained", "Combat death assertions are skipped for low-intensity combat runs", results);
            AddSkippedAssertion("COMB-02", "combat", runKey, "combat_not_sustained", "Combat kill/death counter assertions are skipped for low-intensity combat runs", results);
        }
        else
        {
            AddAssertion("COMB-01", "combat", "error", runKey, assertEnabled, run.CombatDeaths > 0, run.CombatDeaths.ToString(), ">0", "Combat deaths are observed in sustained combat runs", results);
            AddAssertion("COMB-02", "combat", "error", runKey, assertEnabled, (run.CombatDeaths + run.PredatorKillsByHumans) > 0, (run.CombatDeaths + run.PredatorKillsByHumans).ToString(), ">0", "Combat kills/deaths counters are active in sustained combat runs", results);
        }

        AddAssertion("COMB-03", "combat", "error", runKey, assertEnabled, run.CombatEngagements > 0, run.CombatEngagements.ToString(), ">0", "Combat engagements exist", results);
    }

    return results;
}

static bool ShouldRequireCombatDeaths(ScenarioRunResult run)
    => run.CombatEngagements >= 50 || run.BattleTicks >= 30;

static List<ScenarioAnomaly> DetectAnomalies(
    IReadOnlyList<ScenarioRunResult> runs,
    IReadOnlyList<ScenarioAssertionResult> assertions,
    bool assertEnabled)
{
    var anomalies = new List<ScenarioAnomaly>();

    foreach (var run in runs)
    {
        if (run.DeathsStarvationWithFood > 2)
        {
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-SURV-STARVATION-WITH-FOOD",
                Category: "survival",
                Severity: "warning",
                RunKey: BuildRunKey(run),
                Message: "Starvation-with-food exceeded warning threshold",
                Value: run.DeathsStarvationWithFood.ToString(),
                Threshold: "<=2"));
        }

        var totalBackoff = run.NoProgressBackoffResource + run.NoProgressBackoffBuild + run.NoProgressBackoffFlee + run.NoProgressBackoffCombat;
        if (run.Ticks > 0 && totalBackoff > run.Ticks / 2)
        {
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-CLUSTER-HIGH-BACKOFF",
                Category: "clustering",
                Severity: "warning",
                RunKey: BuildRunKey(run),
                Message: "No-progress backoff is unusually high",
                Value: totalBackoff.ToString(),
                Threshold: $"<={run.Ticks / 2}"));
        }

        if (run.Ticks > 0 && run.AiNoPlanDecisions > run.Ticks / 3)
        {
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-AI-NOPLAN-HIGH",
                Category: "ai",
                Severity: "warning",
                RunKey: BuildRunKey(run),
                Message: "AI no-plan decisions are unusually high",
                Value: run.AiNoPlanDecisions.ToString(),
                Threshold: $"<={run.Ticks / 3}"));
        }

        if (run.Ticks > 0 && run.AiReplanBackoffDecisions > run.Ticks / 3)
        {
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-AI-REPLAN-BACKOFF-HIGH",
                Category: "ai",
                Severity: "warning",
                RunKey: BuildRunKey(run),
                Message: "AI replan-backoff decisions are unusually high",
                Value: run.AiReplanBackoffDecisions.ToString(),
                Threshold: $"<={run.Ticks / 3}"));
        }

        if (!run.EnableCombatPrimitives)
            continue;

        // W5.1-B1: COMB-01/02 now evaluate directly from exported counters.
    }

    if (assertEnabled && assertions.All(a => a.Skipped))
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-ASSERT-ALL-SKIPPED",
            Category: "assert",
            Severity: "warning",
            RunKey: null,
            Message: "Assertion mode enabled but all assertions were skipped",
            Value: "all_skipped",
            Threshold: "some_assertions_evaluated"));
    }

    return anomalies;
}

static ScenarioPerfAnomalyDetection DetectPerfAnomalies(
    IReadOnlyList<ScenarioRunResult> runs,
    bool perfEnabled,
    Action<string> logWarning)
{
    if (!perfEnabled)
    {
        return new ScenarioPerfAnomalyDetection(
            new List<ScenarioAnomaly>(),
            new ScenarioPerfSummary(false, 0, 0, 0, new List<ScenarioPerfRunSummary>()));
    }

    var anomalies = new List<ScenarioAnomaly>();
    var runSummaries = new List<ScenarioPerfRunSummary>();
    var yellowCount = 0;
    var redCount = 0;

    foreach (var run in runs)
    {
        if (run.PerfAvgTickMs <= 0d && run.PerfMaxTickMs <= 0d && run.PerfP99TickMs <= 0d && run.PerfPeakEntities <= 0L)
            continue;

        var runKey = BuildRunKey(run);
        var avgStatus = ResolvePerfBudgetStatus(run.PerfAvgTickMs, yellowThreshold: 4d, redThreshold: 8d);
        var p99Status = ResolvePerfBudgetStatus(run.PerfP99TickMs, yellowThreshold: 8d, redThreshold: 12d);
        var peakStatus = ResolvePerfBudgetStatusForLong(run.PerfPeakEntities, yellowThreshold: 5000L, redThreshold: 10000L);

        runSummaries.Add(new ScenarioPerfRunSummary(
            RunKey: BuildPerfRunKey(run),
            AvgTickMs: run.PerfAvgTickMs,
            MaxTickMs: run.PerfMaxTickMs,
            P99TickMs: run.PerfP99TickMs,
            PeakEntities: run.PerfPeakEntities,
            Budget: new ScenarioPerfBudgetStatus(
                AvgTickStatus: avgStatus,
                P99TickStatus: p99Status,
                PeakEntitiesStatus: peakStatus)));

        if (string.Equals(avgStatus, "red", StringComparison.Ordinal))
        {
            redCount++;
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-PERF-TICK-AVG",
                Category: "perf",
                Severity: "warning",
                RunKey: runKey,
                Message: "Average tick time exceeded red-zone budget",
                Value: run.PerfAvgTickMs.ToString("0.###"),
                Threshold: ">8"));
        }
        else if (string.Equals(avgStatus, "yellow", StringComparison.Ordinal))
        {
            yellowCount++;
            logWarning($"Warning: perf yellow avg tick for {runKey} ({run.PerfAvgTickMs:0.###}ms > 4ms)");
        }

        if (string.Equals(p99Status, "red", StringComparison.Ordinal))
        {
            redCount++;
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-PERF-TICK-P99",
                Category: "perf",
                Severity: "warning",
                RunKey: runKey,
                Message: "P99 tick time exceeded red-zone budget",
                Value: run.PerfP99TickMs.ToString("0.###"),
                Threshold: ">12"));
        }
        else if (string.Equals(p99Status, "yellow", StringComparison.Ordinal))
        {
            yellowCount++;
            logWarning($"Warning: perf yellow p99 tick for {runKey} ({run.PerfP99TickMs:0.###}ms > 8ms)");
        }

        if (string.Equals(peakStatus, "red", StringComparison.Ordinal))
        {
            redCount++;
            anomalies.Add(new ScenarioAnomaly(
                Id: "ANOM-PERF-PEAK-ENTITIES",
                Category: "perf",
                Severity: "warning",
                RunKey: runKey,
                Message: "Peak entity count exceeded red-zone budget",
                Value: run.PerfPeakEntities.ToString(),
                Threshold: ">10000"));
        }
        else if (string.Equals(peakStatus, "yellow", StringComparison.Ordinal))
        {
            yellowCount++;
            logWarning($"Warning: perf yellow peak entities for {runKey} ({run.PerfPeakEntities} > 5000)");
        }
    }

    var summary = new ScenarioPerfSummary(
        PerfEnabled: true,
        PerfRunCount: runSummaries.Count,
        PerfRedCount: redCount,
        PerfYellowCount: yellowCount,
        Runs: runSummaries);
    return new ScenarioPerfAnomalyDetection(anomalies, summary);
}

static string ResolvePerfBudgetStatus(double value, double yellowThreshold, double redThreshold)
{
    if (value > redThreshold)
        return "red";
    if (value > yellowThreshold)
        return "yellow";
    return "green";
}

static string ResolvePerfBudgetStatusForLong(long value, long yellowThreshold, long redThreshold)
{
    if (value > redThreshold)
        return "red";
    if (value > yellowThreshold)
        return "yellow";
    return "green";
}

static BaselineLoadResult ParseBaselineEnvelope(string? baselinePath)
{
    if (string.IsNullOrWhiteSpace(baselinePath))
    {
        return new BaselineLoadResult(true, false, null, null, "WORLDSIM_SCENARIO_BASELINE_PATH is not set; compare is skipped");
    }

    try
    {
        var fullPath = Path.GetFullPath(baselinePath);
        if (!File.Exists(fullPath))
        {
            return new BaselineLoadResult(true, false, fullPath, null, "baseline file does not exist; compare is skipped");
        }

        var json = File.ReadAllText(fullPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var envelope = JsonSerializer.Deserialize<ScenarioRunEnvelope>(json, options);
        if (envelope is null)
        {
            return new BaselineLoadResult(true, true, fullPath, null, "baseline file is empty or invalid");
        }

        return new BaselineLoadResult(true, false, fullPath, envelope, null);
    }
    catch (Exception ex)
    {
        return new BaselineLoadResult(true, true, baselinePath, null, $"baseline parse failed: {ex.Message}");
    }
}

static ScenarioCompareReport EvaluateBaselineComparison(ScenarioRunEnvelope current, ScenarioRunEnvelope baseline)
{
    var currentRunsByKey = current.Runs.ToDictionary(BuildRunKey, StringComparer.Ordinal);
    var baselineRunsByKey = baseline.Runs.ToDictionary(BuildRunKey, StringComparer.Ordinal);

    var matchedKeys = currentRunsByKey.Keys.Intersect(baselineRunsByKey.Keys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    var currentOnlyKeys = currentRunsByKey.Keys.Except(baselineRunsByKey.Keys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
    var baselineOnlyKeys = baselineRunsByKey.Keys.Except(currentRunsByKey.Keys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();

    var deltas = new List<ScenarioRunDelta>(matchedKeys.Length);
    var thresholdBreaches = new List<ScenarioThresholdBreach>();

    foreach (var key in matchedKeys)
    {
        var currentRun = currentRunsByKey[key];
        var baselineRun = baselineRunsByKey[key];
        var runDeltas = new List<ScenarioMetricDelta>
        {
            BuildMetricDelta("people", baselineRun.People, currentRun.People),
            BuildMetricDelta("food", baselineRun.Food, currentRun.Food),
            BuildMetricDelta("avgFoodPerPerson", baselineRun.AverageFoodPerPerson, currentRun.AverageFoodPerPerson),
            BuildMetricDelta("livingColonies", baselineRun.LivingColonies, currentRun.LivingColonies),
            BuildMetricDelta("combatEngagements", baselineRun.CombatEngagements, currentRun.CombatEngagements)
        };
        deltas.Add(new ScenarioRunDelta(key, runDeltas));

        if (baselineRun.People > 0 && currentRun.People < baselineRun.People * 0.8f)
            thresholdBreaches.Add(new ScenarioThresholdBreach("THR-PEOPLE-DROP", key, "people", currentRun.People.ToString(), $">={baselineRun.People * 0.8f:0.###}"));

        if (baselineRun.Food > 0 && currentRun.Food < baselineRun.Food * 0.7f)
            thresholdBreaches.Add(new ScenarioThresholdBreach("THR-FOOD-DROP", key, "food", currentRun.Food.ToString(), $">={baselineRun.Food * 0.7f:0.###}"));

        if (baselineRun.AverageFoodPerPerson > 0f && currentRun.AverageFoodPerPerson < baselineRun.AverageFoodPerPerson * 0.8f)
            thresholdBreaches.Add(new ScenarioThresholdBreach("THR-AVG_FPP-DROP", key, "avgFoodPerPerson", currentRun.AverageFoodPerPerson.ToString("0.###"), $">={baselineRun.AverageFoodPerPerson * 0.8f:0.###}"));

        if (currentRun.LivingColonies < baselineRun.LivingColonies)
            thresholdBreaches.Add(new ScenarioThresholdBreach("THR-COLONY-DROP", key, "livingColonies", currentRun.LivingColonies.ToString(), $">={baselineRun.LivingColonies}"));
    }

    var strictCurrentAssertions = EvaluateAssertions(current.Runs, assertEnabled: true);
    var strictBaselineAssertions = EvaluateAssertions(baseline.Runs, assertEnabled: true);
    var currentAssertionMap = strictCurrentAssertions
        .Where(a => !a.Skipped)
        .ToDictionary(a => $"{a.RunKey}:{a.InvariantId}", StringComparer.Ordinal);
    var passToFailRegressions = strictBaselineAssertions
        .Where(a => !a.Skipped && a.Passed)
        .Where(a =>
        {
            if (!currentAssertionMap.TryGetValue($"{a.RunKey}:{a.InvariantId}", out var currentAssertion))
                return false;
            return !currentAssertion.Passed;
        })
        .Select(a => new ScenarioRegression(a.InvariantId, a.RunKey, "pass_to_fail"))
        .OrderBy(r => r.InvariantId, StringComparer.Ordinal)
        .ThenBy(r => r.RunKey, StringComparer.Ordinal)
        .ToList();

    var scalingChecks = EvaluateScalingChecks(current.Runs);

    return new ScenarioCompareReport(
        BaselineGeneratedAtUtc: baseline.GeneratedAtUtc,
        MatchedRunCount: matchedKeys.Length,
        CurrentOnlyRunKeys: currentOnlyKeys,
        BaselineOnlyRunKeys: baselineOnlyKeys,
        RunDeltas: deltas,
        PassToFailRegressions: passToFailRegressions,
        ThresholdBreaches: thresholdBreaches,
        ScalingChecks: scalingChecks,
        TotalFailureCount: passToFailRegressions.Count + thresholdBreaches.Count + scalingChecks.Count(s => !s.Passed));
}

static List<ScenarioScalingCheck> EvaluateScalingChecks(IReadOnlyList<ScenarioRunResult> runs)
{
    var checks = new List<ScenarioScalingCheck>();
    var byConfig = runs
        .GroupBy(run => run.ConfigName, StringComparer.Ordinal)
        .Select(group => new
        {
            Name = group.Key,
            AvgArea = group.Average(run => run.Width * run.Height),
            AvgColonies = group.Average(run => run.LivingColonies)
        })
        .OrderBy(item => item.AvgArea)
        .ToList();

    if (byConfig.Count < 2)
    {
        checks.Add(new ScenarioScalingCheck("SCALE-01", true, "insufficient configs for map-size scaling check"));
    }
    else
    {
        var small = byConfig.First();
        var large = byConfig.Last();
        var passed = large.AvgColonies >= small.AvgColonies;
        checks.Add(new ScenarioScalingCheck("SCALE-01", passed, $"{large.Name}:{large.AvgColonies:0.###} >= {small.Name}:{small.AvgColonies:0.###}"));
    }

    var failingScale02 = runs
        .Where(run => run.People < run.InitialPop * 0.3f)
        .Select(BuildRunKey)
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToArray();
    checks.Add(new ScenarioScalingCheck("SCALE-02", failingScale02.Length == 0, failingScale02.Length == 0 ? "all runs pass" : string.Join(",", failingScale02)));

    return checks;
}

static ScenarioMetricDelta BuildMetricDelta(string metricName, float baselineValue, float currentValue)
{
    var delta = currentValue - baselineValue;
    float? deltaPercent = null;
    if (Math.Abs(baselineValue) > float.Epsilon)
        deltaPercent = (delta / baselineValue) * 100f;

    return new ScenarioMetricDelta(metricName, baselineValue, currentValue, delta, deltaPercent);
}

static List<ScenarioAnomaly> BuildCompareAnomalies(ScenarioCompareReport report)
{
    var anomalies = new List<ScenarioAnomaly>();
    foreach (var regression in report.PassToFailRegressions)
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-COMPARE-PASS-TO-FAIL",
            Category: "compare",
            Severity: "warning",
            RunKey: regression.RunKey,
            Message: $"Invariant regressed: {regression.InvariantId}",
            Value: "pass_to_fail",
            Threshold: "no regression"));
    }

    foreach (var breach in report.ThresholdBreaches)
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-COMPARE-THRESHOLD-BREACH",
            Category: "compare",
            Severity: "warning",
            RunKey: breach.RunKey,
            Message: $"Threshold breached for {breach.Metric}",
            Value: breach.Measured,
            Threshold: breach.Threshold));
    }

    if (report.CurrentOnlyRunKeys.Length > 0)
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-COMPARE-CURRENT-ONLY",
            Category: "compare",
            Severity: "warning",
            RunKey: null,
            Message: "Current run contains keys missing from baseline",
            Value: string.Join(",", report.CurrentOnlyRunKeys),
            Threshold: "all current keys in baseline"));
    }

    if (report.BaselineOnlyRunKeys.Length > 0)
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-COMPARE-BASELINE-ONLY",
            Category: "compare",
            Severity: "warning",
            RunKey: null,
            Message: "Baseline contains keys missing from current run",
            Value: string.Join(",", report.BaselineOnlyRunKeys),
            Threshold: "all baseline keys in current"));
    }

    foreach (var scaling in report.ScalingChecks.Where(check => !check.Passed))
    {
        anomalies.Add(new ScenarioAnomaly(
            Id: "ANOM-COMPARE-SCALING-FAIL",
            Category: "compare",
            Severity: "warning",
            RunKey: null,
            Message: $"Scaling check failed: {scaling.InvariantId}",
            Value: scaling.Details,
            Threshold: "scaling check pass"));
    }

    return anomalies;
}

static (int ExitCode, string ExitReason) ResolveExitCode(bool hasConfigError, bool hasAssertionFailures, bool anomalyGateFailed)
{
    if (hasConfigError)
        return (3, "config_error");
    if (hasAssertionFailures)
        return (2, "assert_fail");
    if (anomalyGateFailed)
        return (4, "anomaly_gate_fail");
    return (0, "ok");
}

static void WriteEvaluationOutput(
    ScenarioEvaluation evaluation,
    ScenarioOutputMode outputMode,
    Action<string> logLine,
    Action<string> logBufferOnly)
{
    var line = $"SMR evaluation | assertions={evaluation.Summary.TotalCount} pass={evaluation.Summary.PassedCount} fail={evaluation.Summary.FailedCount} skip={evaluation.Summary.SkippedCount} anomalies={evaluation.Summary.AnomalyCount} exit={evaluation.ExitCode}:{evaluation.ExitReason}";

    if (outputMode == ScenarioOutputMode.Text)
    {
        logLine(line);
        if (evaluation.Summary.FailedIds.Length > 0)
            logLine($"SMR failed invariants: {string.Join(",", evaluation.Summary.FailedIds)}");
        if (evaluation.Summary.SkippedIds.Length > 0)
            logLine($"SMR skipped invariants: {string.Join(",", evaluation.Summary.SkippedIds)}");
        if (evaluation.CompareReport is not null)
            logLine($"SMR compare | matched={evaluation.CompareReport.MatchedRunCount} regressions={evaluation.CompareReport.PassToFailRegressions.Count} thresholdBreaches={evaluation.CompareReport.ThresholdBreaches.Count} scalingFails={evaluation.CompareReport.ScalingChecks.Count(c => !c.Passed)}");
        if (evaluation.PerfSummary.PerfEnabled)
            logLine($"SMR perf | runs={evaluation.PerfSummary.PerfRunCount} red={evaluation.PerfSummary.PerfRedCount} yellow={evaluation.PerfSummary.PerfYellowCount}");
        return;
    }

    logBufferOnly(line);
    if (evaluation.Summary.FailedIds.Length > 0)
        logBufferOnly($"SMR failed invariants: {string.Join(",", evaluation.Summary.FailedIds)}");
    if (evaluation.Summary.SkippedIds.Length > 0)
        logBufferOnly($"SMR skipped invariants: {string.Join(",", evaluation.Summary.SkippedIds)}");

    if (evaluation.CompareReport is not null)
        logBufferOnly($"SMR compare | matched={evaluation.CompareReport.MatchedRunCount} regressions={evaluation.CompareReport.PassToFailRegressions.Count} thresholdBreaches={evaluation.CompareReport.ThresholdBreaches.Count} scalingFails={evaluation.CompareReport.ScalingChecks.Count(c => !c.Passed)}");
    if (evaluation.PerfSummary.PerfEnabled)
        logBufferOnly($"SMR perf | runs={evaluation.PerfSummary.PerfRunCount} red={evaluation.PerfSummary.PerfRedCount} yellow={evaluation.PerfSummary.PerfYellowCount}");
}

static string BuildRunKey(ScenarioRunResult run)
    => $"{BuildStableKeyPart(run.ConfigName)}_{BuildStableKeyPart(run.PlannerMode)}_{run.Seed}";

static string BuildPerfRunKey(ScenarioRunResult run)
    => $"{run.ConfigName}/{run.PlannerMode}/{run.Seed}";

static string BuildStableKeyPart(string value)
    => $"{ToFileSafeToken(value)}_{ComputeStableHash(value)}";

static bool ShouldCaptureTickSample(int tickIndex, int tickCount, int sampleEvery)
{
    if (tickIndex == 0)
        return true;
    if (tickIndex == tickCount - 1)
        return true;
    return (tickIndex + 1) % sampleEvery == 0;
}

static ScenarioTimelineSample BuildTimelineSample(World world, int tick, double perfTickMs)
{
    var livingColonies = world._colonies.Count(colony => world._people.Any(person => person.Home == colony && person.Health > 0f));
    var totalFood = world._colonies.Sum(colony => colony.Stock[Resource.Food]);
    var totalPeople = world._people.Count(person => person.Health > 0f);
    var routingPeople = world._people.Count(person => person.Health > 0f && person.IsRouting);
    var minCombatMorale = world._people
        .Where(person => person.Health > 0f)
        .Select(person => person.CombatMorale)
        .DefaultIfEmpty(100f)
        .Min();
    var aiTelemetry = world.BuildScenarioAiTelemetrySnapshot().ToTimelineSnapshot();

    return new ScenarioTimelineSample(
        Tick: tick,
        People: totalPeople,
        Food: totalFood,
        LivingColonies: livingColonies,
        CombatDeaths: world.TotalCombatDeaths,
        CombatEngagements: world.TotalCombatEngagements,
        BattleTicks: world.TotalBattleTicks,
        ActiveBattles: world.ActiveBattleCount,
        ActiveCombatGroups: world.ActiveCombatGroupCount,
        RoutingPeople: routingPeople,
        MinCombatMorale: minCombatMorale,
        NoProgressBackoffResource: world.TotalNoProgressBackoffResource,
        NoProgressBackoffBuild: world.TotalNoProgressBackoffBuild,
        NoProgressBackoffFlee: world.TotalNoProgressBackoffFlee,
        NoProgressBackoffCombat: world.TotalNoProgressBackoffCombat,
        AiNoPlanDecisions: world.TotalAiNoPlanDecisions,
        AiReplanBackoffDecisions: world.TotalAiReplanBackoffDecisions,
        AiResearchTechDecisions: world.TotalAiResearchTechDecisions,
        Ai: aiTelemetry,
        PerfTickMs: perfTickMs);
}

static void AddAssertion(
    string invariantId,
    string category,
    string severity,
    string runKey,
    bool assertEnabled,
    bool condition,
    string measured,
    string threshold,
    string message,
    List<ScenarioAssertionResult> results)
{
    var passed = !assertEnabled || condition;
    results.Add(new ScenarioAssertionResult(
        InvariantId: invariantId,
        Category: category,
        Severity: severity,
        RunKey: runKey,
        Passed: passed,
        Skipped: false,
        SkipReason: null,
        Message: message,
        Measured: measured,
        Threshold: threshold));
}

static void AddSkippedAssertion(
    string invariantId,
    string category,
    string runKey,
    string skipReason,
    string message,
    List<ScenarioAssertionResult> results)
{
    results.Add(new ScenarioAssertionResult(
        InvariantId: invariantId,
        Category: category,
        Severity: "warning",
        RunKey: runKey,
        Passed: true,
        Skipped: true,
        SkipReason: skipReason,
        Message: message,
        Measured: null,
        Threshold: null));
}

static string ToFileSafeToken(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return "run";

    var builder = new StringBuilder(value.Length);
    var previousUnderscore = false;
    foreach (var ch in value)
    {
        if (char.IsLetterOrDigit(ch))
        {
            builder.Append(char.ToLowerInvariant(ch));
            previousUnderscore = false;
            continue;
        }

        if (ch == '-')
        {
            builder.Append('-');
            previousUnderscore = false;
            continue;
        }

        if (previousUnderscore)
            continue;

        builder.Append('_');
        previousUnderscore = true;
    }

    var token = builder.ToString().Trim('_');
    return token.Length > 0 ? token : "run";
}

static string ComputeStableHash(string value)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes.AsSpan(0, 4)).ToLowerInvariant();
}

static ScenarioOutputMode ParseOutputMode(string? raw)
{
    if (string.Equals(raw, "json", StringComparison.OrdinalIgnoreCase))
        return ScenarioOutputMode.Json;
    if (string.Equals(raw, "text", StringComparison.OrdinalIgnoreCase))
        return ScenarioOutputMode.Text;
    return ScenarioOutputMode.Jsonl;
}

static List<NpcPlannerMode> ParsePlannerModes(string? raw)
{
    var defaults = new List<NpcPlannerMode>
    {
        NpcPlannerMode.Simple,
        NpcPlannerMode.Goap,
        NpcPlannerMode.Htn
    };

    if (string.IsNullOrWhiteSpace(raw))
        return defaults;

    var result = new List<NpcPlannerMode>();
    foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Enum.TryParse<NpcPlannerMode>(token, ignoreCase: true, out var parsed))
            continue;
        if (result.Contains(parsed))
            continue;
        result.Add(parsed);
    }

    return result.Count > 0 ? result : defaults;
}

static ScenarioConfigParseResult ParseScenarioConfigs(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return new ScenarioConfigParseResult(new List<ScenarioConfig>(), false, Array.Empty<string>());

    try
    {
        var parsed = JsonSerializer.Deserialize<List<ScenarioConfig>>(raw);
        if (parsed == null)
            return new ScenarioConfigParseResult(
                new List<ScenarioConfig>(),
                true,
                new[] { "Warning: WORLDSIM_SCENARIO_CONFIGS_JSON did not produce a config array. Exiting with config_error." });

        var configs = new List<ScenarioConfig>(parsed.Count);
        var warnings = new List<string>();
        for (var index = 0; index < parsed.Count; index++)
        {
            var config = parsed[index];
            if (config.Width <= 0 || config.Height <= 0 || config.InitialPop <= 0 || config.Ticks <= 0 || config.Dt <= 0f)
            {
                warnings.Add($"Warning: invalid scenario config at index {index} (width/height/initialPop/ticks/dt must be > 0). Ignoring entry and marking run as config_error.");
                continue;
            }

            configs.Add(config with
            {
                Name = string.IsNullOrWhiteSpace(config.Name) ? $"config_{index + 1}" : config.Name
            });
        }

        if (configs.Count == 0)
        {
            warnings.Add("Warning: no valid scenario configs were provided. Exiting with config_error.");
            return new ScenarioConfigParseResult(configs, true, warnings);
        }

        return new ScenarioConfigParseResult(configs, warnings.Count > 0, warnings);
    }
    catch (Exception ex)
    {
        return new ScenarioConfigParseResult(
            new List<ScenarioConfig>(),
            true,
            new[] { $"Warning: invalid WORLDSIM_SCENARIO_CONFIGS_JSON ({ex.Message}). Exiting with config_error." });
    }
}

static bool ParseBool(string? value, bool fallback)
{
    if (string.IsNullOrWhiteSpace(value))
        return fallback;
    if (bool.TryParse(value, out var parsed))
        return parsed;

    return value.Trim() switch
    {
        "1" => true,
        "0" => false,
        _ => fallback
    };
}

static ScenarioMode ParseScenarioMode(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return ScenarioMode.None;

    return value.Trim().ToLowerInvariant() switch
    {
        "standard" => ScenarioMode.Standard,
        "assert" => ScenarioMode.Assert,
        "compare" => ScenarioMode.Compare,
        "perf" => ScenarioMode.Perf,
        "all" => ScenarioMode.All,
        _ => ScenarioMode.None
    };
}

static int ParseInt(string? value, int fallback)
    => int.TryParse(value, out var parsed) ? parsed : fallback;

static int ParseIntClamped(string? value, int fallback, int min, int max)
{
    var parsed = ParseInt(value, fallback);
    return Math.Clamp(parsed, min, max);
}

static float ParseFloat(string? value, float fallback)
    => float.TryParse(value, out var parsed) ? parsed : fallback;

static int[]? ParseCsvInt(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var parsed = new List<int>(items.Length);
    foreach (var item in items)
    {
        if (!int.TryParse(item, out var num))
            continue;
        parsed.Add(num);
    }

    return parsed.Count > 0 ? parsed.ToArray() : null;
}

enum ScenarioOutputMode
{
    Text,
    Jsonl,
    Json
}

enum ScenarioMode
{
    None,
    Standard,
    Assert,
    Compare,
    Perf,
    All
}

sealed record ScenarioConfig(
    string Name,
    int Width,
    int Height,
    int InitialPop,
    int Ticks,
    float Dt,
    bool EnableCombatPrimitives,
    bool EnableDiplomacy,
    bool StoneBuildingsEnabled,
    float BirthRateMultiplier,
    float MovementSpeedMultiplier,
    bool EnableSiege = true);

sealed record ScenarioRunResult(
    string ConfigName,
    string PlannerMode,
    int Seed,
    int Width,
    int Height,
    int InitialPop,
    int Ticks,
    float Dt,
    bool EnableCombatPrimitives,
    bool EnableDiplomacy,
    bool EnableSiege,
    bool StoneBuildingsEnabled,
    float BirthRateMultiplier,
    float MovementSpeedMultiplier,
    int LivingColonies,
    int People,
    int Food,
    float AverageFoodPerPerson,
    int DeathsOldAge,
    int DeathsStarvation,
    int DeathsPredator,
    int DeathsOther,
    int CombatDeaths,
    int CombatEngagements,
    int PredatorKillsByHumans,
    int BattleTicks,
    int PeakActiveBattles,
    int PeakActiveCombatGroups,
    int PeakRoutingPeople,
    int TicksWithActiveBattle,
    float MinCombatMoraleObserved,
    int DeathsStarvationRecent60s,
    int DeathsStarvationWithFood,
    int OverlapResolveMoves,
    int CrowdDissipationMoves,
    int BirthFallbackToOccupied,
    int BirthFallbackToParent,
    int BuildSiteResets,
    int NoProgressBackoffResource,
    int NoProgressBackoffBuild,
    int NoProgressBackoffFlee,
    int NoProgressBackoffCombat,
    int AiNoPlanDecisions,
    int AiReplanBackoffDecisions,
    int AiResearchTechDecisions,
    int DenseNeighborhoodTicks,
    int LastTickDenseActors,
    ScenarioAiTelemetrySnapshot Ai,
    double PerfAvgTickMs,
    double PerfMaxTickMs,
    double PerfP99TickMs,
    long PerfPeakEntities);

sealed record ScenarioRunEnvelope(
    DateTime GeneratedAtUtc,
    int SeedCount,
    int PlannerCount,
    int ConfigCount,
    List<ScenarioRunResult> Runs);

sealed record ScenarioArtifactManifest(
    string SchemaVersion,
    DateTime GeneratedAtUtc,
    string RunId,
    int SeedCount,
    int PlannerCount,
    int ConfigCount,
    int TotalRuns,
    string ArtifactDir,
    int ExitCode,
    string ExitReason,
    int AssertionFailures,
    int AssertionSkipped,
    int AnomalyCount,
    bool CompareEnabled,
    int CompareMatchedRuns,
    int CompareRegressions,
    int CompareThresholdBreaches,
    bool PerfEnabled,
    int PerfRunCount,
    int PerfRedCount,
    int PerfYellowCount,
    bool DrilldownEnabled,
    int DrilldownSelectedRuns,
    int DrilldownTopN,
    int DrilldownSampleEvery);

sealed record ScenarioConfigParseResult(List<ScenarioConfig> Configs, bool HadError, IReadOnlyList<string> Warnings);

sealed record ScenarioAssertionResult(
    string InvariantId,
    string Category,
    string Severity,
    string RunKey,
    bool Passed,
    bool Skipped,
    string? SkipReason,
    string Message,
    string? Measured,
    string? Threshold);

sealed record ScenarioAnomaly(
    string Id,
    string Category,
    string Severity,
    string? RunKey,
    string Message,
    string? Value,
    string? Threshold);

sealed record ScenarioEvaluationSummary(
    int TotalCount,
    int PassedCount,
    int FailedCount,
    int SkippedCount,
    int AnomalyCount,
    string[] FailedIds,
    string[] SkippedIds,
    int CompareFailures,
    bool CompareEnabled);

sealed record ScenarioEvaluation(
    List<ScenarioAssertionResult> Assertions,
    List<ScenarioAnomaly> Anomalies,
    ScenarioEvaluationSummary Summary,
    int ExitCode,
    string ExitReason,
    bool AssertEnabled,
    bool AnomalyFailEnabled,
    bool CompareEnabled,
    bool DeltaFailEnabled,
    bool PerfEnabled,
    bool PerfFailEnabled,
    ScenarioCompareReport? CompareReport,
    ScenarioPerfSummary PerfSummary);

sealed record BaselineLoadResult(
    bool CompareEnabled,
    bool HadError,
    string? BaselinePath,
    ScenarioRunEnvelope? Envelope,
    string? ErrorMessage);

sealed record ScenarioCompareReport(
    DateTime BaselineGeneratedAtUtc,
    int MatchedRunCount,
    string[] CurrentOnlyRunKeys,
    string[] BaselineOnlyRunKeys,
    List<ScenarioRunDelta> RunDeltas,
    List<ScenarioRegression> PassToFailRegressions,
    List<ScenarioThresholdBreach> ThresholdBreaches,
    List<ScenarioScalingCheck> ScalingChecks,
    int TotalFailureCount);

sealed record ScenarioRunDelta(
    string RunKey,
    List<ScenarioMetricDelta> Metrics);

sealed record ScenarioMetricDelta(
    string Metric,
    float Baseline,
    float Current,
    float Delta,
    float? DeltaPercent);

sealed record ScenarioRegression(
    string InvariantId,
    string RunKey,
    string Transition);

sealed record ScenarioThresholdBreach(
    string Id,
    string RunKey,
    string Metric,
    string Measured,
    string Threshold);

sealed record ScenarioScalingCheck(
    string InvariantId,
    bool Passed,
    string Details);

sealed record ScenarioPerfSummary(
    bool PerfEnabled,
    int PerfRunCount,
    int PerfRedCount,
    int PerfYellowCount,
    List<ScenarioPerfRunSummary> Runs);

sealed record ScenarioPerfRunSummary(
    string RunKey,
    double AvgTickMs,
    double MaxTickMs,
    double P99TickMs,
    long PeakEntities,
    ScenarioPerfBudgetStatus Budget);

sealed record ScenarioPerfBudgetStatus(
    string AvgTickStatus,
    string P99TickStatus,
    string PeakEntitiesStatus);

sealed record ScenarioPerfAnomalyDetection(
    List<ScenarioAnomaly> Anomalies,
    ScenarioPerfSummary Summary);

sealed record ScenarioTimelineSample(
    int Tick,
    int People,
    int Food,
    int LivingColonies,
    int CombatDeaths,
    int CombatEngagements,
    int BattleTicks,
    int ActiveBattles,
    int ActiveCombatGroups,
    int RoutingPeople,
    float MinCombatMorale,
    int NoProgressBackoffResource,
    int NoProgressBackoffBuild,
    int NoProgressBackoffFlee,
    int NoProgressBackoffCombat,
    int AiNoPlanDecisions,
    int AiReplanBackoffDecisions,
    int AiResearchTechDecisions,
    ScenarioAiTimelineSnapshot Ai,
    double PerfTickMs);

sealed record ScenarioDrilldownSummary(
    bool Enabled,
    int SelectedRuns,
    int TopN,
    int SampleEvery);

sealed record ScenarioDrilldownIndex(
    DateTime GeneratedAtUtc,
    int TopN,
    List<ScenarioDrilldownSelection> Runs);

sealed record ScenarioDrilldownSelection(
    string RunKey,
    double Score,
    List<string> Reasons,
    int TimelineSamples,
    int EventCount);

sealed record ScenarioDrilldownEvent(
    string Kind,
    string Id,
    string Severity,
    string Message,
    string? Value,
    string? Threshold);

sealed record ScenarioReplaySeed(
    string RunKey,
    string ConfigName,
    string PlannerMode,
    int Seed,
    int TickCount,
    float Dt,
    int SampleEvery,
    int TimelineSampleCount,
    int FinalPeople,
    int FinalFood,
    int FinalLivingColonies);
