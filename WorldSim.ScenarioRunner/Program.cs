using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.ScenarioRunner.Refinery;
using WorldSim.Runtime;
using WorldSim.Runtime.Diagnostics;
using WorldSim.Runtime.Profiles;
using WorldSim.Simulation;
using WorldSim.Simulation.Ecology;
using WorldSim.Simulation.Military;

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
var visualLaneResolution = LowCostProfileResolver.ResolveForScenarioRunner(Environment.GetEnvironmentVariable("WORLDSIM_VISUAL_PROFILE"));
foreach (var warning in parsedConfigs.Warnings)
    LogWarning(warning);
var configs = parsedConfigs.Configs;
var artifactDir = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_ARTIFACT_DIR");
const string InvalidWave9Scenario = "__invalid_wave9_scenario__";
const string InvalidWave10Scenario = "__invalid_wave10_scenario__";

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
        MovementSpeedMultiplier: 1f,
        EnablePredatorHumanAttacks: false));
}

var requestedScenarioLane = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_LANE");
if (!IsCoreScenarioLane(requestedScenarioLane))
{
    var refineryExitCode = RefineryScenarioRunner.Run(new RefineryScenarioRunnerRequest(
        RawLane: requestedScenarioLane,
        Configs: configs.Select(config => new RefineryScenarioConfig(config.Name, config.Width, config.Height, config.InitialPop, config.Ticks, config.Dt)).ToList(),
        Seeds: seeds,
        Planners: planners,
        ArtifactDir: artifactDir,
        OutputMode: outputMode.ToString(),
        AssertEnabled: assertEnabled,
        InitialRunLog: runLogBuffer.ToString(),
        BaseDirectory: Directory.GetCurrentDirectory(),
        ConfigHadError: parsedConfigs.HadError));
    Environment.ExitCode = refineryExitCode;
    return refineryExitCode;
}

var runs = new List<ScenarioRunResult>(configs.Count * planners.Count * seeds.Length);
var wave10ProbeEvidence = new List<ScenarioWave10ProbeEvidence>();
var runTimelines = new Dictionary<string, List<ScenarioTimelineSample>>(StringComparer.Ordinal);
foreach (var config in configs.OrderBy(c => c.Name, StringComparer.Ordinal))
{
    foreach (var planner in planners)
    {
        foreach (var seed in seeds.OrderBy(s => s))
        {
            var mainRunConfig = ResolveMainRunExecutionConfig(config, assertEnabled);
            if (IsWave10LifecycleScenario(mainRunConfig.Wave10Scenario))
            {
                var lifecycleRunResult = RunRuntimeBackedWave10LifecycleScenario(
                    mainRunConfig,
                    planner,
                    seed,
                    visualLaneResolution.Effective,
                    perfEnabled,
                    drilldownEnabled,
                    drilldownSampleEvery,
                    out var lifecycleTimelineSamples);
                runs.Add(lifecycleRunResult);
                if (lifecycleTimelineSamples is not null)
                    runTimelines[BuildRunKey(lifecycleRunResult)] = lifecycleTimelineSamples;
                continue;
            }

            var world = new World(
                width: mainRunConfig.Width,
                height: mainRunConfig.Height,
                initialPop: mainRunConfig.InitialPop,
                brainFactory: _ => new RuntimeNpcBrain(planner, $"ScenarioRunner:{planner}"),
                randomSeed: seed)
            {
                EnableCombatPrimitives = mainRunConfig.EnableCombatPrimitives,
                EnableDiplomacy = mainRunConfig.EnableDiplomacy,
                EnableSiege = mainRunConfig.EnableSiege,
                EnablePredatorHumanAttacks = mainRunConfig.EnablePredatorHumanAttacks,
                StoneBuildingsEnabled = mainRunConfig.StoneBuildingsEnabled,
                BirthRateMultiplier = mainRunConfig.BirthRateMultiplier,
                MovementSpeedMultiplier = mainRunConfig.MovementSpeedMultiplier
            };
            var initialEcology = world.BuildScenarioInitialEcologyTelemetrySnapshot();
            ApplyEcologyBalanceConfig(world, mainRunConfig);
            ApplySupplyScenarioConfig(world, mainRunConfig);

            List<double>? tickTimesMs = perfEnabled ? new List<double>(mainRunConfig.Ticks) : null;
            long peakEntities = 0;
            var timelineSamples = drilldownEnabled ? new List<ScenarioTimelineSample>() : null;
            var peakActiveBattles = 0;
            var peakActiveCombatGroups = 0;
            var peakRoutingPeople = 0;
            var ticksWithActiveBattle = 0;
            var minCombatMoraleObserved = 100f;
            var sawLivingPerson = false;

            for (var i = 0; i < mainRunConfig.Ticks; i++)
            {
                var perfTickMs = 0d;
                if (perfEnabled)
                {
                    var stopwatch = Stopwatch.StartNew();
                    world.Update(mainRunConfig.Dt);
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
                    world.Update(mainRunConfig.Dt);
                }

                if (timelineSamples is not null && ShouldCaptureTickSample(i, mainRunConfig.Ticks, drilldownSampleEvery))
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

            var wave9Telemetry = BuildWave9ScenarioTelemetry(config, planner, seed);
            var wave10Probe = BuildWave10ScenarioTelemetry(config, planner, seed);
            if (wave10Probe.ProofType != ScenarioWave10Evidence.ProofTypeNotConfigured)
                wave10ProbeEvidence.Add(BuildWave10ProbeEvidence(config, planner, seed, wave10Probe));

            var runResult = BuildRunResult(
                world,
                initialEcology,
                mainRunConfig,
                planner,
                seed,
                visualLaneResolution.Effective,
                wave9Telemetry,
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

static ScenarioConfig ResolveMainRunExecutionConfig(ScenarioConfig config, bool assertEnabled)
{
    if (IsWave10LifecycleScenario(config.Wave10Scenario))
    {
        return config with
        {
            MovementSpeedMultiplier = config.MovementSpeedMultiplier > 0f ? config.MovementSpeedMultiplier : 1f
        };
    }

    if (!assertEnabled || string.IsNullOrWhiteSpace(config.Wave10Scenario))
        return config;

    // Wave10 feature proof comes from side probes. Under assert mode, keep the companion
    // main-world run on a stable health-check profile instead of the tiny 8-tick proof setup.
    return config with
    {
        Width = Math.Max(config.Width, 64),
        Height = Math.Max(config.Height, 40),
        InitialPop = Math.Max(config.InitialPop, 24),
        Ticks = Math.Max(config.Ticks, 300),
        EnableCombatPrimitives = false,
        EnableDiplomacy = false,
        EnableSiege = true,
        BirthRateMultiplier = config.BirthRateMultiplier > 0f ? config.BirthRateMultiplier : 1f,
        MovementSpeedMultiplier = config.MovementSpeedMultiplier > 0f ? config.MovementSpeedMultiplier : 1f
    };
}

var envelope = new ScenarioRunEnvelope(
    GeneratedAtUtc: DateTime.UtcNow,
    SeedCount: seeds.Length,
    PlannerCount: planners.Count,
    ConfigCount: configs.Count,
    Runs: runs.ToList(),
    Wave10ProbeEvidence: BuildWave10ProbeEvidenceSummary(wave10ProbeEvidence));

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
    WriteArtifactBundle(
        envelope,
        evaluation,
        visualLaneResolution,
        artifactDir,
        runLogBuffer.ToString(),
        drilldownEnabled,
        drilldownTopN,
        drilldownSampleEvery,
        runTimelines,
        wave10ProbeEvidence);
}

return Environment.ExitCode;

static ScenarioRunResult BuildRunResult(
    World world,
    ScenarioInitialEcologyTelemetrySnapshot initialEcology,
    ScenarioConfig config,
    NpcPlannerMode planner,
    int seed,
    LowCostProfileLane visualLane,
    ScenarioWave9TelemetrySnapshot wave9Telemetry,
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
    var contactTelemetry = world.BuildScenarioContactTelemetrySnapshot();
    var aiTelemetry = world.BuildScenarioAiTelemetrySnapshot();
    var ecologyTelemetry = world.BuildScenarioEcologyTelemetrySnapshot();
    var ecologyBalance = world.BuildScenarioEcologyBalanceSnapshot();
    var supplyTelemetry = world.BuildScenarioSupplyTelemetrySnapshot();
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
        VisualLane: visualLane.ToString(),
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
        Contact: contactTelemetry,
        Ai: aiTelemetry,
        Wave9: wave9Telemetry,
        Wave10: ScenarioWave10TelemetrySnapshot.Empty,
        PerfAvgTickMs: perfAvgTickMs,
        PerfMaxTickMs: perfMaxTickMs,
        PerfP99TickMs: perfP99TickMs,
        PerfPeakEntities: perfPeakEntities,
        Ecology: ecologyTelemetry,
        EcologyBalance: ecologyBalance,
        Supply: supplyTelemetry,
        EnablePredatorHumanAttacks: world.EnablePredatorHumanAttacks,
        AllowEmergencyRescueInAcceptance: config.AllowEmergencyRescueInAcceptance,
        InitialEcology: initialEcology);
}

static ScenarioRunResult RunRuntimeBackedWave10LifecycleScenario(
    ScenarioConfig config,
    NpcPlannerMode planner,
    int seed,
    LowCostProfileLane visualLane,
    bool perfEnabled,
    bool drilldownEnabled,
    int drilldownSampleEvery,
    out List<ScenarioTimelineSample>? timelineSamples)
{
    var runtime = CreateScenarioRuntime(config, planner, seed);
    ApplyWave10LifecyclePreconditions(runtime, config.Wave10Scenario);

    timelineSamples = drilldownEnabled ? new List<ScenarioTimelineSample>() : null;
    List<double>? tickTimesMs = perfEnabled ? new List<double>(config.Ticks) : null;
    long peakEntities = 0;
    var peakActiveBattles = 0;
    var peakActiveCombatGroups = 0;
    var peakRoutingPeople = 0;
    var ticksWithActiveBattle = 0;
    var minCombatMoraleObserved = 100f;
    var sawLivingPerson = false;
    long? manualLaunchAttemptTick = null;
    var manualLaunchSucceeded = false;
    var manualLaunchStatus = "not_attempted";
    var manualLaunchTick = ResolveManualLaunchTick(config);

    for (var i = 0; i < config.Ticks; i++)
    {
        var tickNumber = i + 1L;
        if (config.Wave10Scenario == "manual_operator_campaign_lifecycle" && !manualLaunchAttemptTick.HasValue && tickNumber >= manualLaunchTick)
        {
            var creation = runtime.TryCreateManualCampaign(ManualCampaignLaunchCommand.DefaultOperatorSmoke);
            manualLaunchAttemptTick = tickNumber;
            manualLaunchSucceeded = creation.Success;
            manualLaunchStatus = creation.Status.ToString();
        }

        var perfTickMs = 0d;
        if (perfEnabled)
        {
            var stopwatch = Stopwatch.StartNew();
            runtime.AdvanceTick(config.Dt);
            stopwatch.Stop();
            perfTickMs = stopwatch.Elapsed.TotalMilliseconds;
            tickTimesMs!.Add(perfTickMs);
        }
        else
        {
            runtime.AdvanceTick(config.Dt);
        }

        var tickTelemetry = runtime.BuildScenarioRunTelemetrySnapshot();
        peakEntities = Math.Max(peakEntities, tickTelemetry.EntityCount);
        peakActiveBattles = Math.Max(peakActiveBattles, tickTelemetry.ActiveBattles);
        peakActiveCombatGroups = Math.Max(peakActiveCombatGroups, tickTelemetry.ActiveCombatGroups);
        if (tickTelemetry.ActiveBattles > 0)
            ticksWithActiveBattle++;
        peakRoutingPeople = Math.Max(peakRoutingPeople, tickTelemetry.RoutingPeople);
        if (tickTelemetry.People > 0)
        {
            sawLivingPerson = true;
            minCombatMoraleObserved = Math.Min(minCombatMoraleObserved, tickTelemetry.MinCombatMorale);
        }

        if (timelineSamples is not null && ShouldCaptureTickSample(i, config.Ticks, drilldownSampleEvery))
        {
            var wave10Timeline = BuildLifecycleWave10Telemetry(
                    runtime,
                    config,
                    manualLaunchAttemptTick,
                    manualLaunchSucceeded,
                    manualLaunchStatus)
                .ToTimelineSnapshot();
            timelineSamples.Add(BuildRuntimeTimelineSample(tickTelemetry, wave10Timeline, (int)tickNumber, perfTickMs));
        }
    }

    var finalTelemetry = runtime.BuildScenarioRunTelemetrySnapshot();
    var wave10Telemetry = BuildLifecycleWave10Telemetry(
        runtime,
        config,
        manualLaunchAttemptTick,
        manualLaunchSucceeded,
        manualLaunchStatus);

    return BuildRuntimeBackedRunResult(
        finalTelemetry,
        config,
        planner,
        seed,
        visualLane,
        wave10Telemetry,
        tickTimesMs,
        peakEntities,
        peakActiveBattles,
        peakActiveCombatGroups,
        peakRoutingPeople,
        ticksWithActiveBattle,
        sawLivingPerson ? minCombatMoraleObserved : 100f);
}

static ScenarioRunResult BuildRuntimeBackedRunResult(
    ScenarioRunTelemetrySnapshot telemetry,
    ScenarioConfig config,
    NpcPlannerMode planner,
    int seed,
    LowCostProfileLane visualLane,
    ScenarioWave10TelemetrySnapshot wave10Telemetry,
    List<double>? tickTimesMs,
    long peakEntities,
    int peakActiveBattles,
    int peakActiveCombatGroups,
    int peakRoutingPeople,
    int ticksWithActiveBattle,
    float minCombatMoraleObserved)
{
    var perfAvgTickMs = 0d;
    var perfMaxTickMs = 0d;
    var perfP99TickMs = 0d;
    var perfPeakEntities = 0L;
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
        VisualLane: visualLane.ToString(),
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
        LivingColonies: telemetry.LivingColonies,
        People: telemetry.People,
        Food: telemetry.Food,
        AverageFoodPerPerson: telemetry.AverageFoodPerPerson,
        DeathsOldAge: telemetry.DeathsOldAge,
        DeathsStarvation: telemetry.DeathsStarvation,
        DeathsPredator: telemetry.DeathsPredator,
        DeathsOther: telemetry.DeathsOther,
        CombatDeaths: telemetry.CombatDeaths,
        CombatEngagements: telemetry.CombatEngagements,
        PredatorKillsByHumans: telemetry.PredatorKillsByHumans,
        BattleTicks: telemetry.BattleTicks,
        PeakActiveBattles: peakActiveBattles,
        PeakActiveCombatGroups: peakActiveCombatGroups,
        PeakRoutingPeople: peakRoutingPeople,
        TicksWithActiveBattle: ticksWithActiveBattle,
        MinCombatMoraleObserved: minCombatMoraleObserved,
        DeathsStarvationRecent60s: telemetry.DeathsStarvationRecent60s,
        DeathsStarvationWithFood: telemetry.DeathsStarvationWithFood,
        OverlapResolveMoves: telemetry.OverlapResolveMoves,
        CrowdDissipationMoves: telemetry.CrowdDissipationMoves,
        BirthFallbackToOccupied: telemetry.BirthFallbackToOccupied,
        BirthFallbackToParent: telemetry.BirthFallbackToParent,
        BuildSiteResets: telemetry.BuildSiteResets,
        NoProgressBackoffResource: telemetry.NoProgressBackoffResource,
        NoProgressBackoffBuild: telemetry.NoProgressBackoffBuild,
        NoProgressBackoffFlee: telemetry.NoProgressBackoffFlee,
        NoProgressBackoffCombat: telemetry.NoProgressBackoffCombat,
        AiNoPlanDecisions: telemetry.AiNoPlanDecisions,
        AiReplanBackoffDecisions: telemetry.AiReplanBackoffDecisions,
        AiResearchTechDecisions: telemetry.AiResearchTechDecisions,
        DenseNeighborhoodTicks: telemetry.DenseNeighborhoodTicks,
        LastTickDenseActors: telemetry.LastTickDenseActors,
        Contact: telemetry.Contact,
        Ai: telemetry.Ai,
        Wave9: ScenarioWave9TelemetrySnapshot.Empty,
        Wave10: wave10Telemetry,
        PerfAvgTickMs: perfAvgTickMs,
        PerfMaxTickMs: perfMaxTickMs,
        PerfP99TickMs: perfP99TickMs,
        PerfPeakEntities: perfPeakEntities,
        Ecology: telemetry.Ecology,
        EcologyBalance: telemetry.EcologyBalance,
        Supply: telemetry.Supply,
        EnablePredatorHumanAttacks: telemetry.EnablePredatorHumanAttacks);
}

static ScenarioTimelineSample BuildRuntimeTimelineSample(
    ScenarioRunTelemetrySnapshot telemetry,
    ScenarioWave10TimelineSnapshot wave10Telemetry,
    int tick,
    double perfTickMs)
    => new(
        Tick: tick,
        People: telemetry.People,
        Food: telemetry.Food,
        LivingColonies: telemetry.LivingColonies,
        CombatDeaths: telemetry.CombatDeaths,
        CombatEngagements: telemetry.CombatEngagements,
        BattleTicks: telemetry.BattleTicks,
        ActiveBattles: telemetry.ActiveBattles,
        ActiveCombatGroups: telemetry.ActiveCombatGroups,
        RoutingPeople: telemetry.RoutingPeople,
        MinCombatMorale: telemetry.MinCombatMorale,
        NoProgressBackoffResource: telemetry.NoProgressBackoffResource,
        NoProgressBackoffBuild: telemetry.NoProgressBackoffBuild,
        NoProgressBackoffFlee: telemetry.NoProgressBackoffFlee,
        NoProgressBackoffCombat: telemetry.NoProgressBackoffCombat,
        AiNoPlanDecisions: telemetry.AiNoPlanDecisions,
        AiReplanBackoffDecisions: telemetry.AiReplanBackoffDecisions,
        AiResearchTechDecisions: telemetry.AiResearchTechDecisions,
        Contact: telemetry.Contact.ToTimelineSnapshot(),
        Ai: telemetry.Ai.ToTimelineSnapshot(),
        Wave9: ScenarioWave9TimelineSnapshot.Empty,
        Wave10: wave10Telemetry,
        Ecology: telemetry.Ecology.ToTimelineSnapshot(),
        Supply: telemetry.Supply.ToTimelineSnapshot(),
        PerfTickMs: perfTickMs);

static ScenarioWave10TelemetrySnapshot BuildLifecycleWave10Telemetry(
    SimulationRuntime runtime,
    ScenarioConfig config,
    long? manualLaunchAttemptTick,
    bool manualLaunchSucceeded,
    string manualLaunchStatus)
{
    var proofType = config.Wave10Scenario == "manual_operator_campaign_lifecycle"
        ? ScenarioWave10Evidence.ProofTypeManualOperator
        : ScenarioWave10Evidence.ProofTypeOrganic;
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        proofType,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        BuildLifecycleNonClaims(config.Wave10Scenario),
        ScenarioWave10Evidence.RuntimeSourceMainWorldRun,
        manualLaunchAttemptTick,
        manualLaunchSucceeded,
        manualLaunchStatus);

    if (config.Wave10Scenario == "manual_operator_campaign_lifecycle" && !manualLaunchSucceeded)
    {
        return telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonLaneNotConfigured,
            NonClaims = RouteToStep10C(
                "manual operator lifecycle attempted the runtime command but campaign creation did not succeed",
                "route: Step10C-B runtime manual/operator launch follow-up")
        };
    }

    if (config.Wave10Scenario == "organic_hostile_campaign_lifecycle" && telemetry.CampaignLaunches == 0)
    {
        return telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonOrganicLaunchNotReproduced,
            NonClaims = RouteToStep10C(
                "hostile organic lifecycle ran with runtime-owned war preconditions but no organic campaign launch occurred",
                "route: Step10C-B/C classify runtime precondition vs strategist/advisory suppression")
        };
    }

    return telemetry;
}

static string[] BuildLifecycleNonClaims(string? wave10Scenario)
    => wave10Scenario switch
    {
        "manual_operator_campaign_lifecycle" => new[] { "manual operator lifecycle is runtime command proof, not App hotkey/UI proof" },
        "organic_hostile_campaign_lifecycle" => new[] { "hostile lifecycle uses runtime war preconditions but does not directly create campaigns" },
        "organic_campaign_lifecycle" => new[] { "pure organic no-launch runs may still be valid rarity evidence" },
        _ => Array.Empty<string>()
    };

static int ResolveManualLaunchTick(ScenarioConfig config)
{
    var requested = config.Wave10ManualLaunchTick ?? 100;
    return Math.Clamp(requested, 1, Math.Max(1, config.Ticks));
}

static SimulationRuntime CreateScenarioRuntime(ScenarioConfig config, NpcPlannerMode planner, int seed)
    => WithTemporaryEnvironment(
        new Dictionary<string, string?>
        {
            ["WORLDSIM_ENABLE_DIPLOMACY"] = config.EnableDiplomacy.ToString(),
            ["WORLDSIM_ENABLE_COMBAT_PRIMITIVES"] = config.EnableCombatPrimitives.ToString(),
            ["WORLDSIM_ENABLE_SIEGE"] = config.EnableSiege.ToString(),
            ["WORLDSIM_ENABLE_PREDATOR_ATTACKS"] = config.EnablePredatorHumanAttacks.ToString()
        },
        () =>
        {
            var runtime = new SimulationRuntime(
                width: config.Width,
                height: config.Height,
                initialPopulation: config.InitialPop,
                technologyFilePath: FindTechPath(),
                aiOptions: new RuntimeAiOptions { PlannerMode = planner },
                randomSeed: seed);
            runtime.ConfigureScenarioRunnerWorldOptions(
                config.EnableCombatPrimitives,
                config.EnableDiplomacy,
                config.EnableSiege,
                config.EnablePredatorHumanAttacks,
                config.StoneBuildingsEnabled,
                config.BirthRateMultiplier,
                config.MovementSpeedMultiplier,
                config.AnimalReplenishmentChancePerSecond,
                config.PredatorReplenishmentChance,
                config.FoodRegrowthMinSeconds,
                config.FoodRegrowthJitterSeconds,
                ParseEmergencyRescuePolicy(config.EmergencyRescuePolicy));
            return runtime;
        });

static void ApplyWave10LifecyclePreconditions(SimulationRuntime runtime, string? wave10Scenario)
{
    if (wave10Scenario == "organic_hostile_campaign_lifecycle")
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "wave10 step10b2 hostile lifecycle");
}

static void ApplyEcologyBalanceConfig(World world, ScenarioConfig config)
{
    world.EmergencyRescuePolicy = ParseEmergencyRescuePolicy(config.EmergencyRescuePolicy);

    if (config.AnimalReplenishmentChancePerSecond.HasValue)
        world.AnimalReplenishmentChancePerSecond = config.AnimalReplenishmentChancePerSecond.Value;

    if (config.PredatorReplenishmentChance.HasValue)
        world.PredatorReplenishmentChance = config.PredatorReplenishmentChance.Value;

    if (config.FoodRegrowthMinSeconds.HasValue)
        world.FoodRegrowthMinSeconds = config.FoodRegrowthMinSeconds.Value;

    if (config.FoodRegrowthJitterSeconds.HasValue)
        world.FoodRegrowthJitterSeconds = config.FoodRegrowthJitterSeconds.Value;
}

static ScenarioWave9TelemetrySnapshot BuildWave9ScenarioTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    if (string.IsNullOrWhiteSpace(config.Wave9Scenario))
        return ScenarioWave9TelemetrySnapshot.Empty;

    return config.Wave9Scenario switch
    {
        "army_supply_depletion" => BuildArmySupplyDepletionTelemetry(config.Wave9Scenario, seed),
        "carrier_resupply" => BuildCarrierResupplyTelemetry(config.Wave9Scenario, seed),
        "campaign_foraging" => BuildCampaignForagingTelemetry(config.Wave9Scenario, seed),
        "campaign_assembly_march_encounter" => BuildCampaignAssemblyMarchEncounterTelemetry(config, planner, seed),
        _ => ScenarioWave9TelemetrySnapshot.Empty with { Wave9Scenario = config.Wave9Scenario }
    };
}

static ScenarioWave9TelemetrySnapshot BuildArmySupplyDepletionTelemetry(string scenario, int seed)
{
    var world = new World(width: 16, height: 16, initialPop: 4, randomSeed: seed);
    var members = world._people.Take(2).ToArray();
    var state = new ArmySupplyState();
    var options = new ArmySupplyOptions(
        FoodConsumedPerPersonPerSecond: 1f,
        OutOfSupplyMoraleLossPerSecond: 100f,
        OutOfSupplyStaminaLossPerSecond: 100f,
        RouteAfterOutOfSupplyTicks: 1,
        RouteMoraleThreshold: 99f,
        RouteStaminaThreshold: 99f,
        RoutingTicks: 3);

    var result = ArmySupplyModel.Tick(members, state, dt: 1f, options);
    return (ScenarioWave9TelemetrySnapshot.Empty with
    {
        Wave9Scenario = scenario,
        ActiveArmies = 1,
        TotalArmyMembers = result.ActiveMemberCount,
        SupplySourceMode = "carried_inventory",
        OutOfSupplyTicks = result.IsOutOfSupply ? 1 : 0,
        LowSupplyTicks = result.IsLowSupply ? 1 : 0,
        SupplyAttritionEvents = result.AttritionEventCount,
        SupplyRoutingEvents = result.RoutedMemberCount,
        CampaignPhase = "none"
    }).AsDeterministicProbe();
}

static ScenarioWave9TelemetrySnapshot BuildCarrierResupplyTelemetry(string scenario, int seed)
{
    var world = new World(width: 16, height: 16, initialPop: 4, randomSeed: seed);
    var members = world._people.Take(2).ToArray();
    foreach (var member in members)
        _ = member.Inventory.TryAdd(ItemType.Food, 2);

    var supplyState = new ArmySupplyState();
    var carrierState = new ArmySupplyCarrierState();
    var assigned = ArmySupplyCarrierModel.AssignCarrier(members[0], carrierState);
    var carriedResult = ArmySupplyCarrierModel.TickCarriedInventory(
        members,
        supplyState,
        carrierState,
        tick: 1,
        dt: 1f,
        new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));
    var rationPool = new ArmyRationPoolState(rationPoolFood: 5);
    var rationResult = ArmySupplyCarrierModel.TickRationPool(
        members,
        supplyState,
        carrierState,
        rationPool,
        tick: 2,
        dt: 1f,
        new ArmySupplyOptions(FoodConsumedPerPersonPerSecond: 1f));

    var carriedConsumed = carriedResult.CarriedInventoryResult?.FoodConsumed ?? 0;
    var rationConsumed = rationResult.RationPoolResult?.FoodConsumed ?? 0;
    var supplyApplications = (carriedConsumed > 0 ? 1 : 0) + (rationConsumed > 0 ? 1 : 0);
    return (ScenarioWave9TelemetrySnapshot.Empty with
    {
        Wave9Scenario = scenario,
        ActiveArmies = 1,
        TotalArmyMembers = members.Length,
        TotalRationPoolFood = rationPool.RationPoolFood,
        SupplySourceMode = "ration_pool",
        MemberInventoryConsumed = carriedConsumed,
        RationPoolConsumed = rationConsumed,
        CarriedInventorySupplyTicks = carriedResult.Status == ArmySupplyCarrierTickStatus.Processed ? 1 : 0,
        RationPoolSupplyTicks = rationResult.Status == ArmySupplyCarrierTickStatus.Processed ? 1 : 0,
        CarrierAssignments = assigned.IsAssigned ? 1 : 0,
        CarrierDeliveries = supplyApplications,
        CarrierSupplyApplications = supplyApplications,
        ResupplyDelivered = carriedConsumed + rationConsumed,
        CampaignPhase = "none"
    }).AsDeterministicProbe();
}

static ScenarioWave9TelemetrySnapshot BuildCampaignForagingTelemetry(string scenario, int seed)
{
    var world = new World(width: 16, height: 16, initialPop: 2, randomSeed: seed);
    var forager = world._people.OrderBy(person => person.Id).First();
    var source = FindNearestForageSource(world, forager.Pos);
    world.GetTile(source.x, source.y).ReplaceNode(new ResourceNode(Resource.Food, 6));
    forager.Pos = source;

    var pool = new ArmyRationPoolState();
    var state = new ArmyForagingState();
    var options = new ArmyForagingOptions(MaxFoodPerAttempt: 2, MaxFoodPerConsumer: 2);
    var first = ArmyForagingModel.TryForageToRationPool(world, forager, pool, state, source.x, source.y, "army:wave9", options);
    var second = ArmyForagingModel.TryForageToRationPool(world, forager, pool, state, source.x, source.y, "army:wave9", options);

    return (ScenarioWave9TelemetrySnapshot.Empty with
    {
        Wave9Scenario = scenario,
        ActiveArmies = 1,
        TotalArmyMembers = 1,
        TotalRationPoolFood = pool.RationPoolFood,
        SupplySourceMode = "ration_pool",
        CampaignForageAttempts = state.Attempts,
        CampaignForageSuccesses = state.Successes,
        CampaignForageFoodGained = state.FoodGained,
        CampaignForageCapReached = second.FailureReason == ArmyForageFailureReason.ConsumerCapReached ? 1 : 0,
        RationPoolConsumed = 0,
        CampaignPhase = first.Status == ArmyForageStatus.Succeeded ? "none" : "unknown"
    }).AsDeterministicProbe();
}

static (int x, int y) FindNearestForageSource(World world, (int x, int y) origin)
{
    for (var radius = 0; radius <= Math.Max(world.Width, world.Height); radius++)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) > radius)
                    continue;

                var x = origin.x + dx;
                var y = origin.y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height)
                    continue;
                if (world.GetTile(x, y).Ground == Ground.Water)
                    continue;
                return (x, y);
            }
        }
    }

    throw new InvalidOperationException("Could not find a land tile for Wave 9 foraging scenario.");
}

static ScenarioWave9TelemetrySnapshot BuildCampaignAssemblyMarchEncounterTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = WithTemporaryEnvironment(
        new Dictionary<string, string?>
        {
            ["WORLDSIM_ENABLE_DIPLOMACY"] = "true",
            ["WORLDSIM_ENABLE_COMBAT_PRIMITIVES"] = "true"
        },
        () => new SimulationRuntime(
            width: Math.Max(config.Width, 32),
            height: Math.Max(config.Height, 32),
            initialPopulation: Math.Max(config.InitialPop, 80),
            technologyFilePath: FindTechPath(),
            aiOptions: new RuntimeAiOptions { PlannerMode = planner },
            randomSeed: seed));

    _ = runtime.PrepareWave9CampaignScenario(Faction.Obsidari, candidateCount: 64, carriedFoodPerCandidate: 3);
    var creation = runtime.TryCreateCampaign(Faction.Obsidari, Faction.Aetheri, requestedMemberCount: 1);
    if (!creation.Success)
        return (ScenarioWave9TelemetrySnapshot.Empty with { Wave9Scenario = config.Wave9Scenario ?? "campaign_assembly_march_encounter" }).AsDeterministicProbe();
    runtime.DisableWave9CampaignScenarioCombatInvalidation();

    var maxTicks = Math.Max(config.Ticks, 600);
    for (var tick = 0; tick < maxTicks; tick++)
    {
        runtime.AdvanceTick(config.Dt);
        var campaign = runtime.Campaigns.FirstOrDefault();
        if (campaign?.Phase == CampaignPhase.Encounter)
            break;
    }

    _ = runtime.GetSnapshot();
    return runtime.BuildScenarioWave9TelemetrySnapshot(config.Wave9Scenario);
}

static ScenarioWave10TelemetrySnapshot BuildWave10ScenarioTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    if (string.IsNullOrWhiteSpace(config.Wave10Scenario))
        return ScenarioWave10TelemetrySnapshot.Empty;

    return config.Wave10Scenario switch
    {
        "manual_operator_launch" => BuildManualOperatorLaunchTelemetry(config, planner, seed),
        "organic_campaign_launch" => BuildOrganicCampaignLaunchTelemetry(config, planner, seed),
        "siege_unit_breach" => BuildSiegeUnitBreachTelemetry(config, planner, seed),
        "multi_front_bounded" => BuildMultiFrontBoundedTelemetry(config, planner, seed),
        "campaign_siege_resolution" => BuildCampaignSiegeResolutionTelemetry(config, planner, seed),
        "supply_line_convoy" => BuildSupplyLineConvoyTelemetry(config, planner, seed),
        "forward_base_long_campaign" => BuildForwardBaseLongCampaignTelemetry(config, planner, seed),
        "scout_intel_campaign_choice" => BuildScoutIntelCampaignChoiceTelemetry(config, planner, seed),
        _ => ScenarioWave10TelemetrySnapshot.Empty with { Wave10Scenario = config.Wave10Scenario }
    };
}

static ScenarioWave10ProbeEvidence BuildWave10ProbeEvidence(ScenarioConfig config, NpcPlannerMode planner, int seed, ScenarioWave10TelemetrySnapshot telemetry)
    => new(
        ProbeKey: BuildWave10ProbeKey(config.Name, planner, seed, telemetry.Wave10Scenario),
        ConfigName: config.Name,
        Planner: planner.ToString(),
        Seed: seed,
        Wave10Scenario: telemetry.Wave10Scenario,
        RuntimeSource: ScenarioWave10Evidence.RuntimeSourceSimulationRuntimeProbe,
        Ticks: Math.Max(config.Ticks, GetWave10ProbeMinimumTicks(telemetry.Wave10Scenario)),
        Dt: config.Dt,
        Width: Math.Max(config.Width, 40),
        Height: Math.Max(config.Height, 40),
        InitialPopulation: Math.Max(config.InitialPop, 80),
        Telemetry: telemetry);

static ScenarioWave10ProbeEvidenceSummary BuildWave10ProbeEvidenceSummary(IReadOnlyList<ScenarioWave10ProbeEvidence> probes)
{
    var lanes = probes
        .Select(probe => probe.Wave10Scenario)
        .Where(name => !string.IsNullOrWhiteSpace(name) && name != "none")
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();
    var proofTypes = probes
        .Select(probe => probe.Telemetry.ProofType)
        .Where(proofType => !string.IsNullOrWhiteSpace(proofType))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(proofType => proofType, StringComparer.Ordinal)
        .ToArray();
    var unavailable = probes
        .Where(probe => probe.Telemetry.EvidenceStatus == ScenarioWave10Evidence.EvidenceStatusProofUnavailable)
        .Select(probe => probe.Wave10Scenario)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    return new ScenarioWave10ProbeEvidenceSummary(
        Enabled: probes.Count > 0,
        ProbeCount: probes.Count,
        LaneNames: lanes,
        ProofTypes: proofTypes,
        UnavailableLaneNames: unavailable,
        ArtifactFile: probes.Count > 0 ? "wave10-probes.json" : null);
}

static ScenarioWave10ManifestSummary BuildWave10ManifestSummary(ScenarioRunEnvelope envelope, IReadOnlyList<ScenarioWave10ProbeEvidence> probes)
{
    var mainRunWave10 = envelope.Runs
        .Select(run => run.Wave10)
        .Where(wave10 => wave10.ProofType != ScenarioWave10Evidence.ProofTypeNotConfigured)
        .ToArray();

    var lanes = probes
        .Select(probe => probe.Wave10Scenario)
        .Concat(mainRunWave10.Select(wave10 => wave10.Wave10Scenario))
        .Where(name => !string.IsNullOrWhiteSpace(name) && name != "none")
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();
    var proofTypes = probes
        .Select(probe => probe.Telemetry.ProofType)
        .Concat(mainRunWave10.Select(wave10 => wave10.ProofType))
        .Where(proofType => !string.IsNullOrWhiteSpace(proofType))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(proofType => proofType, StringComparer.Ordinal)
        .ToArray();

    return new ScenarioWave10ManifestSummary(
        Enabled: probes.Count > 0 || mainRunWave10.Length > 0,
        RunCount: probes.Count + mainRunWave10.Length,
        LaneNames: lanes,
        ProofTypes: proofTypes);
}

static string BuildWave10ProbeKey(string configName, NpcPlannerMode planner, int seed, string scenario)
    => $"{ToFileSafeToken(configName)}__{ToFileSafeToken(planner.ToString())}__seed{seed}__{ToFileSafeToken(scenario)}";

static int GetWave10ProbeMinimumTicks(string scenario)
    => scenario switch
    {
        "manual_operator_launch" => 80,
        "organic_campaign_launch" => 720,
        "siege_unit_breach" => 900,
        "multi_front_bounded" => 120,
        "campaign_siege_resolution" => 900,
        "supply_line_convoy" => 900,
        "forward_base_long_campaign" => 900,
        "scout_intel_campaign_choice" => 20,
        _ => 0
    };

static CampaignCreationResult CreatePreparedProbeCampaign(
    SimulationRuntime runtime,
    Faction owner,
    Faction target,
    int requestedMemberCount = 1,
    int candidateCount = 12,
    int carriedFoodPerCandidate = 0)
{
    _ = runtime.PrepareWave9CampaignScenario(owner, candidateCount, carriedFoodPerCandidate);
    return runtime.TryCreateCampaign(owner, target, requestedMemberCount);
}

static string[] RouteToStep10C(string nonClaim, string route)
    => new[] { nonClaim, route };

static bool HasAnyConvoyOutcome(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.ConvoysDelivered > 0
       || telemetry.ConvoysFailed > 0
       || telemetry.ConvoyThrottleBlocks > 0
       || telemetry.ConvoyCapBlocks > 0
       || telemetry.ConvoyHomeDefenseBlocks > 0
       || telemetry.ConvoyRouteBudgetExhausted > 0;

static bool HasCampaignSiegeResolutionSignal(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.SiegePressureTicks > 0
       || telemetry.ResolvedCampaigns > 0
       || telemetry.CampaignBreaches > 0
       || telemetry.AttackerVictories > 0
       || telemetry.DefenderHeld > 0;

static bool HasSiegeUnitActionSignal(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.SiegeUnitActionTicks > 0
       || telemetry.SiegePressureTicks > 0
       || telemetry.CampaignBreaches > 0;

static bool HasFreshScoutIntelSignal(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.FreshScoutIntel > 0;

static bool HasActiveMultiFrontProof(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.MaxActiveCampaignsForAnyFaction > 1
       || telemetry.MaxUnresolvedPairsForAnyFactionPair > 1;

static bool HasMultiFrontBoundProof(ScenarioWave10TelemetrySnapshot telemetry)
    => telemetry.CampaignLaunchBlockedByCap > 0
       || telemetry.CampaignLaunchBlockedByPairCap > 0
       || telemetry.CampaignLaunchBlockedByHomeDefense > 0
       || telemetry.CampaignLaunchRouteBudgetExhausted > 0;

static ScenarioWave10TelemetrySnapshot BuildManualOperatorLaunchTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var creation = runtime.TryCreateManualCampaign(ManualCampaignLaunchCommand.DefaultOperatorSmoke);
    AdvanceRuntime(runtime, config, minimumTicks: 80);

    return runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeManualOperator,
        creation.Success ? ScenarioWave10Evidence.EvidenceStatusPositive : ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        creation.Success ? ScenarioWave10Evidence.ReasonNone : ScenarioWave10Evidence.ReasonLaneNotConfigured,
        creation.Success
            ? new[] { "manual operator launch is not organic campaign proof" }
            : new[] { "manual operator launch did not create a campaign in bounded prep" });
}

static ScenarioWave10TelemetrySnapshot BuildOrganicCampaignLaunchTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario);
    if (!prepared)
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "wave10 organic evidence lane");
    AdvanceRuntime(runtime, config, minimumTicks: 720);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeOrganic,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone);

    return telemetry.CampaignLaunches > 0
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonOrganicLaunchNotReproduced,
            NonClaims = RouteToStep10C(
                prepared
                    ? "organic setup preconditions were prepared but organic strategist/runtime launch was not reproduced; manual/operator proof is not organic proof"
                    : "organic campaign setup preconditions were not reproduced through the bounded runtime evidence setup; manual/operator proof is not organic proof",
                prepared
                    ? "route: Step10C-C organic strategist/runtime evidence follow-up"
                    : "route: Step10C-B runtime organic setup evidence follow-up")
        };
}

static ScenarioWave10TelemetrySnapshot BuildSiegeUnitBreachTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    if (!runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario))
    {
        return runtime.BuildScenarioWave10TelemetrySnapshot(
            config.Wave10Scenario,
            ScenarioWave10Evidence.ProofTypeDeterministicProbe,
            ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
            ScenarioWave10Evidence.ReasonSiegeUnitNotReproduced,
            RouteToStep10C(
                "siege_craft setup was not reproduced through existing public tech unlock without gameplay policy/resource changes",
                "route: Step10C-B runtime siege-unit evidence setup follow-up"));
    }

    AdvanceRuntime(runtime, config, minimumTicks: 900);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeDeterministicProbe,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        new[] { "deterministic probe is not organic siege-unit proof" });

    return telemetry.SiegeUnitsSpawned > 0 && HasSiegeUnitActionSignal(telemetry)
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonSiegeUnitNotReproduced,
            NonClaims = RouteToStep10C(
                "siege-unit spawn/action/breach evidence was not reproduced with existing production-safe probe setup",
                "route: Step10C-B runtime siege-unit evidence follow-up; Step10C-A only if runtime proof exists but visual/manual consume remains missing")
        };
}

static ScenarioWave10TelemetrySnapshot BuildMultiFrontBoundedTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario);
    if (!prepared)
    {
        _ = CreatePreparedProbeCampaign(runtime, Faction.Obsidari, Faction.Aetheri, carriedFoodPerCandidate: 2);
        _ = CreatePreparedProbeCampaign(runtime, Faction.Obsidari, Faction.Sylvars, carriedFoodPerCandidate: 2);
        _ = CreatePreparedProbeCampaign(runtime, Faction.Sylvars, Faction.Chirita, carriedFoodPerCandidate: 2);
    }
    AdvanceRuntime(runtime, config, minimumTicks: 120);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeDeterministicProbe,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        new[] { "deterministic active multi-front probe is not organic launch proof" });

    return HasActiveMultiFrontProof(telemetry) || HasMultiFrontBoundProof(telemetry)
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonMultiFrontBoundNotReproduced,
            NonClaims = RouteToStep10C(
                "multi-front active campaign or cap/bound proof was not reproduced without gameplay policy changes",
                "route: Step10C-B runtime multi-front evidence follow-up")
        };
}

static ScenarioWave10TelemetrySnapshot BuildCampaignSiegeResolutionTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario);
    if (!prepared)
    {
        var creation = CreatePreparedProbeCampaign(runtime, Faction.Obsidari, Faction.Aetheri, carriedFoodPerCandidate: 2);
        if (!creation.Success)
            _ = runtime.TryCreateManualCampaign(ManualCampaignLaunchCommand.DefaultOperatorSmoke);
    }
    AdvanceRuntime(runtime, config, minimumTicks: 900);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeDeterministicProbe,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        new[] { "deterministic campaign setup is not organic campaign proof" });

    return telemetry.CampaignSiegesEntered > 0 && HasCampaignSiegeResolutionSignal(telemetry)
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonCampaignSiegeNotReproduced,
            NonClaims = RouteToStep10C(
                prepared
                    ? "campaign siege setup was prepared but siege entry plus pressure/resolution proof was not reproduced without gameplay policy changes"
                    : "campaign siege entry plus pressure/resolution proof was not reproduced with existing production-safe probe setup",
                "route: Step10C-B runtime campaign siege/resolution evidence follow-up")
        };
}

static ScenarioWave10TelemetrySnapshot BuildSupplyLineConvoyTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario);
    if (!prepared)
        _ = runtime.TryCreateManualCampaign(ManualCampaignLaunchCommand.DefaultOperatorSmoke);
    AdvanceRuntime(runtime, config, minimumTicks: 900);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeDeterministicProbe,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        new[] { "deterministic supply-line probe is not organic campaign proof" });

    return telemetry.ConvoysSpawned > 0 && HasAnyConvoyOutcome(telemetry)
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonSupplyLineConvoyNotReproduced,
            NonClaims = RouteToStep10C(
                prepared
                    ? "supply-line convoy setup was prepared but spawn plus outcome proof was not reproduced without gameplay policy changes"
                    : "supply-line convoy spawn plus outcome proof was not reproduced with existing production-safe probe setup",
                "route: Step10C-B runtime supply-line evidence follow-up")
        };
}

static ScenarioWave10TelemetrySnapshot BuildForwardBaseLongCampaignTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(config.Wave10Scenario);
    if (!prepared)
        _ = runtime.TryCreateManualCampaign(ManualCampaignLaunchCommand.DefaultOperatorSmoke);
    AdvanceRuntime(runtime, config, minimumTicks: 900);
    var telemetry = runtime.BuildScenarioWave10TelemetrySnapshot(
        config.Wave10Scenario,
        ScenarioWave10Evidence.ProofTypeDeterministicProbe,
        ScenarioWave10Evidence.EvidenceStatusPositive,
        ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
        ScenarioWave10Evidence.ReasonNone,
        new[] { "deterministic forward-base probe is not organic campaign proof" });

    return telemetry.ForwardBasesEstablished > 0 && (telemetry.ForwardBaseRestTicks > 0 || telemetry.ForwardBasesExpired > 0 || telemetry.ForwardBasesAbandoned > 0)
        ? telemetry
        : telemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonForwardBaseLifecycleNotReproduced,
            NonClaims = RouteToStep10C(
                prepared
                    ? "forward-base setup was prepared but established plus rest/expiry/abandon lifecycle proof was not reproduced without gameplay policy changes"
                    : "forward-base established plus rest/expiry/abandon lifecycle proof was not reproduced with existing production-safe probe setup",
                "route: Step10C-B runtime forward-base lifecycle evidence follow-up")
        };
}

static ScenarioWave10TelemetrySnapshot BuildScoutIntelCampaignChoiceTelemetry(ScenarioConfig config, NpcPlannerMode planner, int seed)
{
    var scenario = config.Wave10Scenario ?? "scout_intel_campaign_choice";
    var runtime = CreateWave10Runtime(config, planner, seed);
    var prepared = runtime.TryPrepareWave10EvidenceScenario(scenario);
    if (!prepared)
        runtime.DeclareWar(Faction.Obsidari, Faction.Aetheri, "wave10 scout-intel evidence lane");

    var captureTicks = Math.Max(config.Ticks, GetWave10ProbeMinimumTicks(scenario));
    ScenarioWave10TelemetrySnapshot? positiveTelemetry = null;
    ScenarioWave10TelemetrySnapshot? freshScoutTelemetry = null;
    ScenarioWave10TelemetrySnapshot latestTelemetry = BuildPositiveScoutTelemetry();

    ScenarioWave10TelemetrySnapshot BuildPositiveScoutTelemetry()
        => runtime.BuildScenarioWave10TelemetrySnapshot(
            scenario,
            ScenarioWave10Evidence.ProofTypeDeterministicProbe,
            ScenarioWave10Evidence.EvidenceStatusPositive,
            ScenarioWave10Evidence.TimelineSemanticsNotTickSampled,
            ScenarioWave10Evidence.ReasonNone,
            new[] { "deterministic scout-intel probe is not organic campaign proof" });

    void CaptureTelemetrySample()
    {
        var telemetry = BuildPositiveScoutTelemetry();

        if (telemetry.ScoutIntelObserved > 0 && telemetry.CampaignTargetsWithScoutIntel > 0)
            positiveTelemetry ??= telemetry;
        if (telemetry.ScoutIntelObserved > 0 && HasFreshScoutIntelSignal(telemetry))
            freshScoutTelemetry = telemetry;

        latestTelemetry = telemetry;
    }

    CaptureTelemetrySample();
    for (var tick = 0; tick < captureTicks; tick++)
    {
        runtime.AdvanceTick(config.Dt);
        CaptureTelemetrySample();
    }

    if (positiveTelemetry != null)
        return positiveTelemetry;

    if (freshScoutTelemetry != null)
    {
        return freshScoutTelemetry with
        {
            EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
            ReasonCode = ScenarioWave10Evidence.ReasonScoutIntelChoiceNotReproduced,
            NonClaims = RouteToStep10C(
                "scout intel was observed and remained fresh, but campaign target-with-intel proof was not reproduced under fresh-intel conditions",
                "route: P7-C(C) / Step10C-C scout-intel campaign-choice consume follow-up")
        };
    }

    return latestTelemetry with
    {
        EvidenceStatus = ScenarioWave10Evidence.EvidenceStatusProofUnavailable,
        ReasonCode = ScenarioWave10Evidence.ReasonScoutIntelChoiceNotReproduced,
        NonClaims = latestTelemetry.ScoutIntelObserved > 0
            ? RouteToStep10C(
                "scout intel was observed, but the probe window did not retain fresh scout intel long enough to prove campaign target-with-intel consume",
                "route: Step10C-B runtime scout-intel timing/freshness follow-up")
            : RouteToStep10C(
                prepared
                    ? "scout-intel setup was prepared but observe plus campaign-choice proof was not reproduced without gameplay policy changes"
                    : "scout-intel observe plus campaign-choice proof was not reproduced with existing production-safe probe setup",
                "route: Step10C-B runtime scout-intel evidence setup follow-up")
    };
}

static SimulationRuntime CreateWave10Runtime(ScenarioConfig config, NpcPlannerMode planner, int seed)
    => WithTemporaryEnvironment(
        new Dictionary<string, string?>
        {
            ["WORLDSIM_ENABLE_DIPLOMACY"] = "true",
            ["WORLDSIM_ENABLE_COMBAT_PRIMITIVES"] = "true",
            ["WORLDSIM_ENABLE_SIEGE"] = "true"
        },
        () => new SimulationRuntime(
            width: Math.Max(config.Width, 40),
            height: Math.Max(config.Height, 40),
            initialPopulation: Math.Max(config.InitialPop, 80),
            technologyFilePath: FindTechPath(),
            aiOptions: new RuntimeAiOptions { PlannerMode = planner },
            randomSeed: seed));

static void AdvanceRuntime(SimulationRuntime runtime, ScenarioConfig config, int minimumTicks)
{
    var maxTicks = Math.Max(config.Ticks, minimumTicks);
    for (var tick = 0; tick < maxTicks; tick++)
        runtime.AdvanceTick(config.Dt);
}

static T WithTemporaryEnvironment<T>(IReadOnlyDictionary<string, string?> values, Func<T> action)
{
    var previous = values.ToDictionary(pair => pair.Key, pair => Environment.GetEnvironmentVariable(pair.Key), StringComparer.Ordinal);
    try
    {
        foreach (var pair in values)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        return action();
    }
    finally
    {
        foreach (var pair in previous)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }
}

static void ApplySupplyScenarioConfig(World world, ScenarioConfig config)
{
    if (string.IsNullOrWhiteSpace(config.SupplyScenario))
        return;

    if (!string.Equals(config.SupplyScenario, "storehouse_refill_consumption", StringComparison.OrdinalIgnoreCase))
        return;

    var colony = world._colonies.FirstOrDefault();
    if (colony == null)
        return;

    var actor = world._people
        .Where(person => person.Health > 0f && ReferenceEquals(person.Home, colony))
        .OrderBy(person => person.Id)
        .FirstOrDefault();
    if (actor == null)
        return;

    TechTree.Load(FindTechPath());
    var previousAllowFreeTechUnlocks = world.AllowFreeTechUnlocks;
    try
    {
        world.AllowFreeTechUnlocks = true;
        _ = TechTree.TryUnlock("backpacks", world, colony);
        _ = TechTree.TryUnlock("rationing", world, colony);
    }
    finally
    {
        world.AllowFreeTechUnlocks = previousAllowFreeTechUnlocks;
    }

    var (storehouse, access, shouldAddStorehouse) = EnsureOwnedStorehouseWithAccess(world, colony);
    if (shouldAddStorehouse)
        world.AddSpecializedBuilding(colony, storehouse, SpecializedBuildingKind.Storehouse);
    actor.Pos = access;
    actor.Profession = Profession.Generalist;
    actor.Needs["Hunger"] = 96f;
    colony.Stock[Resource.Food] = Math.Max(colony.Stock[Resource.Food], actor.Inventory.CapacitySlots + 3);

    _ = actor.TryRefillInventoryFromStorehouse(world);
}

static ((int x, int y) Storehouse, (int x, int y) Access, bool ShouldAddStorehouse) EnsureOwnedStorehouseWithAccess(World world, Colony colony)
{
    if (world.TryFindNearestOwnedStorehouseAccessTile(colony, colony.Origin, out var existingAccess))
    {
        var existingStorehouse = world.SpecializedBuildings
            .Where(building => building.Kind == SpecializedBuildingKind.Storehouse && ReferenceEquals(building.Owner, colony))
            .OrderBy(building => Math.Abs(building.Pos.x - existingAccess.x) + Math.Abs(building.Pos.y - existingAccess.y))
            .ThenBy(building => building.Pos.x)
            .ThenBy(building => building.Pos.y)
            .First();
        return (existingStorehouse.Pos, existingAccess, ShouldAddStorehouse: false);
    }

    for (var radius = 1; radius <= Math.Max(world.Width, world.Height); radius++)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Abs(dx) + Math.Abs(dy) != radius)
                    continue;

                var storehouse = (x: colony.Origin.x + dx, y: colony.Origin.y + dy);
                if (!world.CanPlaceStructureAt(storehouse.x, storehouse.y))
                    continue;

                var access = CardinalNeighbors(storehouse)
                    .Where(tile => !world.IsMovementBlocked(tile.x, tile.y, colony.Id))
                    .OrderBy(tile => Math.Abs(tile.x - colony.Origin.x) + Math.Abs(tile.y - colony.Origin.y))
                    .ThenBy(tile => tile.x)
                    .ThenBy(tile => tile.y)
                    .Select(tile => ((int x, int y)?)tile)
                    .FirstOrDefault();

                if (access.HasValue)
                    return (storehouse, access.Value, ShouldAddStorehouse: true);
            }
        }
    }

    throw new InvalidOperationException($"Could not prepare supply scenario storehouse access for config colony {colony.Id}.");
}

static IEnumerable<(int x, int y)> CardinalNeighbors((int x, int y) pos)
{
    yield return (pos.x + 1, pos.y);
    yield return (pos.x - 1, pos.y);
    yield return (pos.x, pos.y + 1);
    yield return (pos.x, pos.y - 1);
}

static string FindTechPath()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current != null)
    {
        var path = Path.Combine(current.FullName, "Tech", "technologies.json");
        if (File.Exists(path))
            return path;

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate repository root containing Tech/technologies.json");
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
                    $"config={run.ConfigName} planner={run.PlannerMode} seed={run.Seed} lane={run.VisualLane} livingCols={run.LivingColonies} people={run.People} food={run.Food} avgFpp={run.AverageFoodPerPerson:0.00} " +
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
    LowCostProfileResolution visualLaneResolution,
    string artifactDirRaw,
    string runLog,
    bool drilldownEnabled,
    int drilldownTopN,
    int drilldownSampleEvery,
    IReadOnlyDictionary<string, List<ScenarioTimelineSample>> runTimelines,
    IReadOnlyList<ScenarioWave10ProbeEvidence> wave10ProbeEvidence)
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

    if (wave10ProbeEvidence.Count > 0)
        File.WriteAllText(Path.Combine(artifactDir, "wave10-probes.json"), JsonSerializer.Serialize(wave10ProbeEvidence, jsonOptions));
    var wave10Summary = BuildWave10ProbeEvidenceSummary(wave10ProbeEvidence);
    var wave10ManifestSummary = BuildWave10ManifestSummary(envelope, wave10ProbeEvidence);

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
        EffectiveVisualLane: visualLaneResolution.Effective.ToString(),
        VisualLaneSource: visualLaneResolution.Source,
        DrilldownEnabled: drilldownSummary.Enabled,
        DrilldownSelectedRuns: drilldownSummary.SelectedRuns,
        DrilldownTopN: drilldownSummary.TopN,
        DrilldownSampleEvery: drilldownSummary.SampleEvery,
        Wave10Enabled: wave10ManifestSummary.Enabled,
        Wave10RunCount: wave10ManifestSummary.RunCount,
        Wave10LaneNames: wave10ManifestSummary.LaneNames,
        Wave10ProofTypes: wave10ManifestSummary.ProofTypes);

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

        var emergencyRescues = run.Ecology?.EmergencyRescues ?? 0;
        AddAssertion("ECO-RESCUE-01", "ecology", "error", runKey, assertEnabled, run.AllowEmergencyRescueInAcceptance || emergencyRescues == 0, emergencyRescues.ToString(), "0 unless allowed", "Normal acceptance lanes do not depend on emergency rescue", results);

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

static EmergencyRescuePolicy ParseEmergencyRescuePolicy(string? value)
    => EmergencyRescuePolicyFormatter.ParsePolicyOrDisabled(value);

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
    => $"{BuildStableKeyPart(run.ConfigName)}_{BuildStableKeyPart(run.PlannerMode)}_{BuildStableKeyPart(run.VisualLane)}_{run.Seed}";

static string BuildPerfRunKey(ScenarioRunResult run)
    => $"{run.ConfigName}/{run.PlannerMode}/{run.VisualLane}/{run.Seed}";

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
    var contactTelemetry = world.BuildScenarioContactTelemetrySnapshot().ToTimelineSnapshot();
    var aiTelemetry = world.BuildScenarioAiTelemetrySnapshot().ToTimelineSnapshot();
    var ecologyTelemetry = world.BuildScenarioEcologyTelemetrySnapshot().ToTimelineSnapshot();
    var supplyTelemetry = world.BuildScenarioSupplyTelemetrySnapshot().ToTimelineSnapshot();
    var wave9Telemetry = ScenarioWave9TimelineSnapshot.Empty;
    var wave10Telemetry = ScenarioWave10TimelineSnapshot.Empty;

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
        Contact: contactTelemetry,
        Ai: aiTelemetry,
        Wave9: wave9Telemetry,
        Wave10: wave10Telemetry,
        Ecology: ecologyTelemetry,
        Supply: supplyTelemetry,
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

            if (!IsKnownSupplyScenario(config.SupplyScenario))
            {
                warnings.Add($"Warning: invalid scenario config at index {index} (SupplyScenario must be null or 'storehouse_refill_consumption'). Ignoring entry and marking run as config_error.");
                continue;
            }

            var normalizedWave9Scenario = NormalizeWave9Scenario(config.Wave9Scenario);
            if (normalizedWave9Scenario == InvalidWave9Scenario)
            {
                warnings.Add($"Warning: invalid scenario config at index {index} (Wave9Scenario must be null or one of: army_supply_depletion, carrier_resupply, campaign_foraging, campaign_assembly_march_encounter, or documented aliases). Ignoring entry and marking run as config_error.");
                continue;
            }

            var normalizedWave10Scenario = NormalizeWave10Scenario(config.Wave10Scenario);
            if (normalizedWave10Scenario == InvalidWave10Scenario)
            {
                warnings.Add($"Warning: invalid scenario config at index {index} (Wave10Scenario must be null or one of: manual_operator_launch, organic_campaign_launch, organic_campaign_lifecycle, organic_hostile_campaign_lifecycle, manual_operator_campaign_lifecycle, siege_unit_breach, multi_front_bounded, campaign_siege_resolution, supply_line_convoy, forward_base_long_campaign, scout_intel_campaign_choice, or documented aliases). Ignoring entry and marking run as config_error.");
                continue;
            }

            configs.Add(config with
            {
                Name = string.IsNullOrWhiteSpace(config.Name) ? $"config_{index + 1}" : config.Name,
                Wave9Scenario = normalizedWave9Scenario,
                Wave10Scenario = normalizedWave10Scenario
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

static bool IsKnownSupplyScenario(string? supplyScenario)
    => string.IsNullOrWhiteSpace(supplyScenario)
       || string.Equals(supplyScenario, "storehouse_refill_consumption", StringComparison.OrdinalIgnoreCase);

static string? NormalizeWave9Scenario(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return null;

    return raw.Trim().ToLowerInvariant() switch
    {
        "army_supply_depletion" or "army-supply-depletion" => "army_supply_depletion",
        "carrier_resupply" or "carrier-resupply" => "carrier_resupply",
        "campaign_foraging" or "foraging-extension" or "campaign-foraging" => "campaign_foraging",
        "campaign_assembly_march_encounter" or "campaign-assembly-march-encounter" => "campaign_assembly_march_encounter",
        _ => InvalidWave9Scenario
    };
}

static string? NormalizeWave10Scenario(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return null;

    return raw.Trim().ToLowerInvariant() switch
    {
        "manual_operator_launch" or "manual-operator-launch" => "manual_operator_launch",
        "organic_campaign_launch" or "organic-campaign-launch" => "organic_campaign_launch",
        "organic_campaign_lifecycle" or "organic-campaign-lifecycle" => "organic_campaign_lifecycle",
        "organic_hostile_campaign_lifecycle" or "organic-hostile-campaign-lifecycle" => "organic_hostile_campaign_lifecycle",
        "manual_operator_campaign_lifecycle" or "manual-operator-campaign-lifecycle" => "manual_operator_campaign_lifecycle",
        "siege_unit_breach" or "siege-unit-breach" => "siege_unit_breach",
        "multi_front_bounded" or "multi-front-bounded" => "multi_front_bounded",
        "campaign_siege_resolution" or "campaign-siege-resolution" => "campaign_siege_resolution",
        "supply_line_convoy" or "supply-line-convoy" => "supply_line_convoy",
        "forward_base_long_campaign" or "forward-base-long-campaign" => "forward_base_long_campaign",
        "scout_intel_campaign_choice" or "scout-intel-campaign-choice" => "scout_intel_campaign_choice",
        _ => InvalidWave10Scenario
    };
}

static bool IsWave10LifecycleScenario(string? raw)
    => raw is "organic_campaign_lifecycle"
        or "organic_hostile_campaign_lifecycle"
        or "manual_operator_campaign_lifecycle";

static bool IsCoreScenarioLane(string? rawLane)
    => string.IsNullOrWhiteSpace(rawLane)
       || string.Equals(rawLane.Trim(), "core", StringComparison.OrdinalIgnoreCase);

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
    bool EnableSiege = true,
    bool EnablePredatorHumanAttacks = false,
    float? AnimalReplenishmentChancePerSecond = null,
    float? PredatorReplenishmentChance = null,
    float? FoodRegrowthMinSeconds = null,
    float? FoodRegrowthJitterSeconds = null,
    string? EmergencyRescuePolicy = null,
    bool AllowEmergencyRescueInAcceptance = false,
    string? SupplyScenario = null,
    string? Wave9Scenario = null,
    string? Wave10Scenario = null,
    int? Wave10ManualLaunchTick = null);

sealed record ScenarioRunResult(
    string ConfigName,
    string PlannerMode,
    int Seed,
    string VisualLane,
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
    ScenarioContactTelemetrySnapshot Contact,
    ScenarioAiTelemetrySnapshot Ai,
    ScenarioWave9TelemetrySnapshot? Wave9,
    ScenarioWave10TelemetrySnapshot Wave10,
    double PerfAvgTickMs,
    double PerfMaxTickMs,
    double PerfP99TickMs,
    long PerfPeakEntities,
    ScenarioEcologyTelemetrySnapshot? Ecology = null,
    ScenarioEcologyBalanceSnapshot? EcologyBalance = null,
    ScenarioSupplyTelemetrySnapshot? Supply = null,
    bool EnablePredatorHumanAttacks = false,
    bool AllowEmergencyRescueInAcceptance = false,
    ScenarioInitialEcologyTelemetrySnapshot? InitialEcology = null);

sealed record ScenarioRunEnvelope(
    DateTime GeneratedAtUtc,
    int SeedCount,
    int PlannerCount,
    int ConfigCount,
    List<ScenarioRunResult> Runs,
    ScenarioWave10ProbeEvidenceSummary? Wave10ProbeEvidence = null);

sealed record ScenarioWave10ProbeEvidenceSummary(
    bool Enabled,
    int ProbeCount,
    string[] LaneNames,
    string[] ProofTypes,
    string[] UnavailableLaneNames,
    string? ArtifactFile);

sealed record ScenarioWave10ManifestSummary(
    bool Enabled,
    int RunCount,
    string[] LaneNames,
    string[] ProofTypes);

sealed record ScenarioWave10ProbeEvidence(
    string ProbeKey,
    string ConfigName,
    string Planner,
    int Seed,
    string Wave10Scenario,
    string RuntimeSource,
    int Ticks,
    float Dt,
    int Width,
    int Height,
    int InitialPopulation,
    ScenarioWave10TelemetrySnapshot Telemetry);

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
    string EffectiveVisualLane,
    string VisualLaneSource,
    bool DrilldownEnabled,
    int DrilldownSelectedRuns,
    int DrilldownTopN,
    int DrilldownSampleEvery,
    bool Wave10Enabled,
    int Wave10RunCount,
    string[] Wave10LaneNames,
    string[] Wave10ProofTypes);

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
    ScenarioContactTimelineSnapshot Contact,
    ScenarioAiTimelineSnapshot Ai,
    ScenarioWave9TimelineSnapshot? Wave9,
    ScenarioWave10TimelineSnapshot Wave10,
    ScenarioEcologyTimelineSnapshot? Ecology,
    ScenarioSupplyTimelineSnapshot? Supply,
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
