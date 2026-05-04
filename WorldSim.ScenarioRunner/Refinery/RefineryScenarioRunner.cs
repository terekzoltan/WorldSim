using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;
using WorldSim.Simulation;
using WorldSim.Contracts.V2;

namespace WorldSim.ScenarioRunner.Refinery;

public static class RefineryScenarioRunner
{
    private static readonly HashSet<string> SupportedSolverPaths = new(StringComparer.Ordinal)
    {
        "unwired",
        "sidecar",
        "validated_core",
        "unavailable"
    };

    private static readonly HashSet<string> SupportedSolverStatuses = new(StringComparer.Ordinal)
    {
        "success",
        "non_success",
        "load_failure",
        "not_run"
    };

    private static readonly HashSet<string> SupportedSolverExtractions = new(StringComparer.Ordinal)
    {
        "success",
        "failed",
        "empty",
        "not_run"
    };

    private static readonly HashSet<string> SupportedOutputModes = new(StringComparer.Ordinal)
    {
        "both",
        "story_only",
        "nudge_only",
        "off",
        "unknown"
    };

    private static readonly HashSet<string> SupportedOutputSources = new(StringComparer.Ordinal)
    {
        "env",
        "profile",
        "operator",
        "response",
        "fallback",
        "unknown"
    };

    public static int Run(RefineryScenarioRunnerRequest request)
    {
        var runLog = new StringBuilder(request.InitialRunLog);
        void LogLine(string line)
        {
            Console.WriteLine(line);
            runLog.AppendLine(line);
        }
        void LogBufferOnly(string line)
        {
            runLog.AppendLine(line);
        }

        var options = RefineryScenarioOptions.FromEnvironment(request.RawLane, request.BaseDirectory)
            .ValidateAgainst(request);
        foreach (var warning in options.Warnings)
            LogBufferOnly(warning);
        if (options.PaidPreset is not null)
        {
            var preflightLine = $"Refinery paid preflight | preset={options.PaidPreset} estimatedCompletions={options.EstimatedCompletions?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} maxCompletions={options.MaxCompletions?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";
            Console.Error.WriteLine(preflightLine);
            runLog.AppendLine(preflightLine);
        }

        var runs = new List<RefineryRunResult>();
        var assertions = new List<RefineryAssertionResult>();
        var anomalies = new List<RefineryAnomaly>();
        var hasConfigError = options.HadConfigError || request.ConfigHadError;

        if (!hasConfigError && options.LaneType == "refinery" && !options.CostEstimateOnly)
        {
            foreach (var config in request.Configs.OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                foreach (var planner in request.Planners)
                {
                    foreach (var seed in request.Seeds.OrderBy(s => s))
                    {
                        var run = RunSingle(config, planner, seed, options);
                        runs.Add(run);
                        EvaluateRun(run, request.AssertEnabled, assertions, anomalies);
                    }
                }
            }
        }

        if (!hasConfigError && !options.CostEstimateOnly)
            AddPostRunGates(options, runs, anomalies);

        var failedAssertions = assertions.Count(a => !a.Passed && !a.Skipped && string.Equals(a.Severity, "error", StringComparison.Ordinal));
        var hardAnomaly = anomalies.Any(a => string.Equals(a.Severity, "error", StringComparison.Ordinal));
        var (exitCode, exitReason) = ResolveExitCode(hasConfigError, failedAssertions > 0, hardAnomaly);
        var envelope = BuildEnvelope(options, request, runs, assertions, anomalies, exitCode, exitReason);

        WriteOutput(envelope, request.OutputMode, LogLine, LogBufferOnly);
        if (!string.IsNullOrWhiteSpace(request.ArtifactDir))
            WriteArtifacts(envelope, request.ArtifactDir!, runLog.ToString());

        return exitCode;
    }

    private static RefineryRunResult RunSingle(
        RefineryScenarioConfig config,
        NpcPlannerMode planner,
        int seed,
        RefineryScenarioOptions options)
    {
        var aiOptions = new RuntimeAiOptions
        {
            PlannerMode = planner,
            PolicyMode = NpcPolicyMode.GlobalPlanner,
            DefaultFactionPlanner = planner,
            FactionPlannerTable = Enum.GetValues<Faction>().ToDictionary(faction => faction, _ => planner)
        };
        var runtime = new SimulationRuntime(config.Width, config.Height, config.InitialPop, FindTechPath(), aiOptions, seed);
        var patchRuntime = new RefineryPatchRuntime(options.ToRuntimeOptions());
        var scheduler = RefineryCheckpointScheduler.Create(options, config.Ticks);
        var checkpoints = new List<RefineryCheckpointRecord>();
        var scheduled = new Queue<int>(scheduler.ScheduledTicks);
        var triggerIndex = 0;

        for (var tick = 1; tick <= config.Ticks; tick++)
        {
            runtime.AdvanceTick(config.Dt);
            if (scheduled.Count == 0 || scheduled.Peek() != tick)
                continue;

            scheduled.Dequeue();
            triggerIndex++;
            checkpoints.Add(RunCheckpoint(patchRuntime, runtime, triggerIndex, tick, options));
        }

        return new RefineryRunResult(
            RunKey: BuildRunKey(config.Name, planner.ToString(), options.RefineryProfile ?? "unknown", seed),
            ConfigName: config.Name,
            PlannerMode: planner.ToString(),
            Seed: seed,
            LaneType: options.LaneType,
            RefineryProfile: options.RefineryProfile,
            Width: config.Width,
            Height: config.Height,
            InitialPop: config.InitialPop,
            Ticks: config.Ticks,
            Dt: config.Dt,
            Checkpoints: checkpoints);
    }

    private static RefineryCheckpointRecord RunCheckpoint(
        RefineryPatchRuntime patchRuntime,
        SimulationRuntime runtime,
        int triggerIndex,
        int triggerTick,
        RefineryScenarioOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        patchRuntime.Trigger(runtime, triggerTick);

        DirectorExecutionStatus status = DirectorExecutionStatus.NotTriggered;
        while (stopwatch.ElapsedMilliseconds <= options.WaitTimeoutMs)
        {
            patchRuntime.Pump();
            status = patchRuntime.LastDirectorExecutionStatus;
            if (status.Tick == triggerTick && IsTerminalOutcome(status.ApplyStatus))
                break;

            Thread.Sleep(10);
        }
        stopwatch.Stop();
        patchRuntime.Pump();
        status = patchRuntime.LastDirectorExecutionStatus;

        var terminalOutcome = status.Tick == triggerTick && IsTerminalOutcome(status.ApplyStatus)
            ? status.ApplyStatus
            : "request_failed";
        var stage = status.Tick == triggerTick ? status.Stage : "directorStage:unknown";
        var outputMode = status.Tick == triggerTick ? status.EffectiveOutputMode : "unknown";
        var outputSource = status.Tick == triggerTick ? status.EffectiveOutputModeSource : "unknown";
        var budgetUsed = status.Tick == triggerTick ? status.BudgetUsed : 0d;
        var budgetMarkerPresent = status.Tick == triggerTick && status.BudgetMarkerPresent;
        var rawStatusText = patchRuntime.LastStatus;
        var explainMarkers = status.Tick == triggerTick
            ? (status.ExplainMarkers?.ToList() ?? new List<string>())
            : new List<string> { "directorStage:unknown" };
        var parsedSolverMarkers = ParseSolverMarkers(explainMarkers);
        var parsedObservabilityMarkers = ParseObservabilityMarkers(explainMarkers);

        return new RefineryCheckpointRecord(
            TriggerIndex: triggerIndex,
            TriggerTick: triggerTick,
            TerminalOutcome: terminalOutcome,
            Stage: stage,
            ApplyStatus: terminalOutcome,
            OutputMode: outputMode,
            OutputSource: outputSource,
            BudgetUsed: budgetUsed,
            BudgetMarkerPresent: budgetMarkerPresent,
            ExplainMarkers: explainMarkers,
            DirectorSolverPath: parsedSolverMarkers.Path,
            DirectorSolverStatus: parsedSolverMarkers.Status,
            DirectorSolverGeneratorResult: parsedSolverMarkers.GeneratorResult,
            DirectorSolverExtraction: parsedSolverMarkers.Extraction,
            DirectorSolverValidatedCoverage: parsedSolverMarkers.ValidatedCoverage,
            DirectorSolverUnsupported: parsedSolverMarkers.Unsupported,
            DirectorSolverDiagnostic: parsedSolverMarkers.Diagnostics,
            SolverParseWarnings: parsedSolverMarkers.Warnings,
            LlmStage: parsedObservabilityMarkers.LlmStage,
            LlmCompletionCount: parsedObservabilityMarkers.LlmCompletionCount,
            LlmRetryRounds: parsedObservabilityMarkers.LlmRetryRounds,
            LlmCandidateSanitized: parsedObservabilityMarkers.LlmCandidateSanitized,
            LlmCandidateSanitizeTags: parsedObservabilityMarkers.LlmCandidateSanitizeTags,
            CausalChainOps: parsedObservabilityMarkers.CausalChainOps,
            ActionStatus: rawStatusText,
            SettleLatencyMs: stopwatch.ElapsedMilliseconds,
            WarningsCount: status.Tick == triggerTick ? status.WarningCount : (rawStatusText.Contains(", warn=", StringComparison.Ordinal) ? 1 : 0),
            CaptureMode: options.CaptureMode,
            ResponseHash: null,
            TelemetryRef: null,
            RequestFailureKind: terminalOutcome == "request_failed" ? TryExtractFailureKind(rawStatusText) : null,
            RawStatusText: rawStatusText);
    }

    private static void EvaluateRun(
        RefineryRunResult run,
        bool assertEnabled,
        List<RefineryAssertionResult> assertions,
        List<RefineryAnomaly> anomalies)
    {
        var acceptedCheckpoints = run.Checkpoints;
        var terminalCount = acceptedCheckpoints.Count(checkpoint => IsTerminalOutcome(checkpoint.TerminalOutcome));
        AddAssertion("RDIR-01", "refinery", "error", run.RunKey, assertEnabled, terminalCount == acceptedCheckpoints.Count, terminalCount.ToString(), acceptedCheckpoints.Count.ToString(), "Every accepted scheduled checkpoint reaches a terminal outcome", assertions);

        var stagesPresent = acceptedCheckpoints.All(checkpoint => !string.IsNullOrWhiteSpace(checkpoint.Stage));
        AddAssertion("RDIR-02", "refinery", "error", run.RunKey, assertEnabled, stagesPresent, stagesPresent.ToString(), "true", "Terminal director checkpoints expose a parseable stage", assertions);

        var applyFailed = acceptedCheckpoints.Count(checkpoint => string.Equals(checkpoint.TerminalOutcome, "apply_failed", StringComparison.Ordinal));
        AddAssertion("RDIR-03", "refinery", "error", run.RunKey, assertEnabled, applyFailed == 0, applyFailed.ToString(), "0", "No checkpoint reaches apply_failed", assertions);

        AddSkippedAssertion("RDIR-04", "refinery", run.RunKey, "budget marker requirement is Track D profile-dependent", "Budget marker consistency is advisory until Track D declares it required", assertions);

        var validModes = acceptedCheckpoints.All(checkpoint => SupportedOutputModes.Contains(checkpoint.OutputMode));
        AddAssertion("RDIR-05", "refinery", "error", run.RunKey, assertEnabled, validModes, string.Join(',', acceptedCheckpoints.Select(c => c.OutputMode).Distinct(StringComparer.Ordinal)), "both|story_only|nudge_only|off|unknown", "Output mode values stay within the supported set", assertions);

        var validSources = acceptedCheckpoints.All(checkpoint => SupportedOutputSources.Contains(checkpoint.OutputSource));
        AddAssertion("RDIR-06", "refinery", "error", run.RunKey, assertEnabled, validSources, string.Join(',', acceptedCheckpoints.Select(c => c.OutputSource).Distinct(StringComparer.Ordinal)), "env|profile|operator|response|fallback|unknown", "Output source values stay within the supported set", assertions);

        AddAssertion("RDIR-07", "refinery", "error", run.RunKey, assertEnabled, applyFailed == 0, applyFailed.ToString(), "0", "Bridge output applyability is represented by terminal outcome/apply status", assertions);

        var bookkeepingOk = acceptedCheckpoints.Select(c => c.TriggerIndex).SequenceEqual(Enumerable.Range(1, acceptedCheckpoints.Count))
            && acceptedCheckpoints.Select(c => c.TriggerTick).SequenceEqual(acceptedCheckpoints.Select(c => c.TriggerTick).OrderBy(tick => tick));
        AddAssertion("RDIR-08", "refinery", "error", run.RunKey, assertEnabled, bookkeepingOk, bookkeepingOk.ToString(), "true", "Checkpoint bookkeeping is internally consistent", assertions);

        var requestFailed = acceptedCheckpoints.Count(checkpoint => string.Equals(checkpoint.TerminalOutcome, "request_failed", StringComparison.Ordinal));
        if (requestFailed > 0)
        {
            anomalies.Add(new RefineryAnomaly("ANOM-RDIR-REQUEST-FAIL-HIGH", "refinery", "warning", run.RunKey, "One or more refinery checkpoints ended in request_failed", requestFailed.ToString(), "0"));
        }

        var fallbackCount = acceptedCheckpoints.Count(checkpoint => string.Equals(checkpoint.OutputSource, "fallback", StringComparison.Ordinal));
        if (fallbackCount > 0)
        {
            anomalies.Add(new RefineryAnomaly("ANOM-RDIR-FALLBACK-HIGH", "refinery", "warning", run.RunKey, "One or more refinery checkpoints used fallback output source", fallbackCount.ToString(), "0"));
        }

        var missingStage = acceptedCheckpoints.Count(checkpoint => string.IsNullOrWhiteSpace(checkpoint.Stage));
        if (missingStage > 0)
        {
            anomalies.Add(new RefineryAnomaly("ANOM-RDIR-STAGE-MISSING", "refinery", "warning", run.RunKey, "One or more refinery checkpoints missed stage data", missingStage.ToString(), "0"));
        }

        var missingBudgetMarkers = acceptedCheckpoints.Count(checkpoint => checkpoint.TerminalOutcome == "applied" && !checkpoint.BudgetMarkerPresent);
        if (missingBudgetMarkers > 0)
        {
            anomalies.Add(new RefineryAnomaly("ANOM-RDIR-BUDGET-MARKER-MISSING", "refinery", "warning", run.RunKey, "One or more applied refinery checkpoints missed budget marker data", missingBudgetMarkers.ToString(), "0"));
        }

        var solverParseWarnings = acceptedCheckpoints.SelectMany(checkpoint => checkpoint.SolverParseWarnings).ToList();
        if (solverParseWarnings.Count > 0)
        {
            anomalies.Add(new RefineryAnomaly(
                "ANOM-RDIR-SOLVER-MARKER-UNKNOWN",
                "refinery",
                "warning",
                run.RunKey,
                "One or more directorSolver enum markers had unknown values",
                string.Join(';', solverParseWarnings.Distinct(StringComparer.Ordinal)),
                "known contract values"));
        }
    }

    private static void AddPostRunGates(
        RefineryScenarioOptions options,
        List<RefineryRunResult> runs,
        List<RefineryAnomaly> anomalies)
    {
        var checkpoints = runs.SelectMany(run => run.Checkpoints).ToList();
        var observedCompletionCount = checkpoints.Any(checkpoint => checkpoint.LlmCompletionCount.HasValue)
            ? checkpoints.Where(checkpoint => checkpoint.LlmCompletionCount.HasValue).Sum(checkpoint => checkpoint.LlmCompletionCount!.Value)
            : (int?)null;

        if (options.RefineryProfile == "validator" && observedCompletionCount is > 0)
        {
            anomalies.Add(new RefineryAnomaly(
                "ANOM-RDIR-VALIDATOR-PAID-COMPLETION",
                "refinery",
                "error",
                null,
                "refinery_live_validator observed LLM completions and is not proven no-cost",
                observedCompletionCount.Value.ToString(CultureInfo.InvariantCulture),
                "0"));
        }

        if (options.RefineryProfile == "live_paid" && observedCompletionCount.HasValue)
        {
            var completionCap = options.EstimatedCompletions ?? options.MaxCompletions;
            if (completionCap.HasValue && observedCompletionCount.Value > completionCap.Value)
            {
                anomalies.Add(new RefineryAnomaly(
                    "ANOM-RDIR-PAID-COMPLETION-CAP-EXCEEDED",
                    "refinery",
                    "error",
                    null,
                    "refinery_live_paid observed completions exceeded the preset completion cap",
                    observedCompletionCount.Value.ToString(CultureInfo.InvariantCulture),
                    completionCap.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    private static RefineryRunEnvelope BuildEnvelope(
        RefineryScenarioOptions options,
        RefineryScenarioRunnerRequest request,
        List<RefineryRunResult> runs,
        List<RefineryAssertionResult> assertions,
        List<RefineryAnomaly> anomalies,
        int exitCode,
        string exitReason)
    {
        var checkpoints = runs.SelectMany(run => run.Checkpoints).ToList();
        var summary = new RefinerySummary(
            CheckpointCount: checkpoints.Count,
            StageHistogram: BuildHistogram(checkpoints.Select(c => c.Stage)),
            ApplyStatusHistogram: BuildHistogram(checkpoints.Select(c => c.ApplyStatus)),
            OutputModeHistogram: BuildHistogram(checkpoints.Select(c => c.OutputMode)),
            OutputSourceHistogram: BuildHistogram(checkpoints.Select(c => c.OutputSource)),
            TotalBudgetUsed: checkpoints.Sum(c => c.BudgetUsed),
            MaxBudgetUsed: checkpoints.Count == 0 ? 0d : checkpoints.Max(c => c.BudgetUsed),
            AverageSettleLatencyMs: checkpoints.Count == 0 ? 0d : checkpoints.Average(c => c.SettleLatencyMs),
            MaxSettleLatencyMs: checkpoints.Count == 0 ? 0 : checkpoints.Max(c => c.SettleLatencyMs),
            AppliedCount: checkpoints.Count(c => c.TerminalOutcome == "applied"),
            ApplyFailedCount: checkpoints.Count(c => c.TerminalOutcome == "apply_failed"),
            RequestFailedCount: checkpoints.Count(c => c.TerminalOutcome == "request_failed"),
            FallbackCount: checkpoints.Count(c => c.OutputSource == "fallback"),
            ValidatedCount: checkpoints.Count(c => HasValidatedCoverage(c)),
            DirectorSolverPathHistogram: BuildHistogram(checkpoints.Select(c => c.DirectorSolverPath)),
            DirectorSolverStatusHistogram: BuildHistogram(checkpoints.Select(c => c.DirectorSolverStatus)),
            DirectorSolverGeneratorResultHistogram: BuildHistogram(checkpoints.Select(c => c.DirectorSolverGeneratorResult)),
            DirectorSolverExtractionHistogram: BuildHistogram(checkpoints.Select(c => c.DirectorSolverExtraction)),
            DirectorSolverValidatedCoverageCheckpointCounts: BuildCheckpointValueHistogram(checkpoints.Select(c => c.DirectorSolverValidatedCoverage)),
            DirectorSolverUnsupportedCheckpointCounts: BuildCheckpointValueHistogram(checkpoints.Select(c => c.DirectorSolverUnsupported)),
            DirectorSolverDiagnosticCounts: BuildValueOccurrencesHistogram(checkpoints.Select(c => c.DirectorSolverDiagnostic)),
            LlmStageHistogram: BuildHistogram(checkpoints.Select(c => c.LlmStage)),
            ObservedCompletionCount: checkpoints.Any(c => c.LlmCompletionCount.HasValue) ? checkpoints.Where(c => c.LlmCompletionCount.HasValue).Sum(c => c.LlmCompletionCount!.Value) : null,
            ObservedRetryRounds: checkpoints.Any(c => c.LlmRetryRounds.HasValue) ? checkpoints.Where(c => c.LlmRetryRounds.HasValue).Sum(c => c.LlmRetryRounds!.Value) : null,
            LlmCandidateSanitizedHistogram: BuildHistogram(checkpoints.Select(c => c.LlmCandidateSanitized)),
            LlmCandidateSanitizeTagCounts: BuildValueOccurrencesHistogram(checkpoints.Select(c => c.LlmCandidateSanitizeTags)),
            CausalChainOpsTotal: checkpoints.Any(c => c.CausalChainOps.HasValue) ? checkpoints.Where(c => c.CausalChainOps.HasValue).Sum(c => c.CausalChainOps!.Value) : null);

        return new RefineryRunEnvelope(
            SchemaVersion: "smr/refinery/v1",
            GeneratedAtUtc: DateTime.UtcNow,
            LaneType: options.LaneType,
            RefineryEnabled: options.LaneType == "refinery",
            RefineryProfile: options.RefineryProfile,
            RefineryGoal: options.RefineryGoal,
            CheckpointPolicy: options.TriggerPolicy,
            MaxTriggers: options.MaxTriggers,
            WaitMode: options.WaitMode,
            CaptureMode: options.CaptureMode,
            RequestTimeoutMs: options.RequestTimeoutMs,
            RetryCount: options.RetryCount,
            DirectorMaxBudget: options.DirectorMaxBudget,
            PaidPreset: options.PaidPreset,
            EstimatedCompletions: options.EstimatedCompletions,
            MaxCompletions: options.MaxCompletions,
            ExpectedJavaDirectorMaxRetries: options.ExpectedJavaDirectorMaxRetries,
            PaidConfirmPresent: options.PaidConfirmPresent,
            RehearsalArtifact: options.NormalizedRehearsalManifestPath,
            CostEstimateOnly: options.CostEstimateOnly,
            SeedCount: request.Seeds.Count,
            PlannerCount: request.Planners.Count,
            ConfigCount: request.Configs.Count,
            TotalRuns: runs.Count,
            ExitCode: exitCode,
            ExitReason: exitReason,
            Assertions: assertions,
            Anomalies: anomalies,
            Summary: summary,
            Runs: runs);
    }

    private static void WriteOutput(RefineryRunEnvelope envelope, string outputMode, Action<string> logLine, Action<string> logBufferOnly)
    {
        var jsonOptions = CreateJsonOptions(writeIndented: string.Equals(outputMode, "Json", StringComparison.Ordinal));
        if (string.Equals(outputMode, "Json", StringComparison.Ordinal))
        {
            logLine(JsonSerializer.Serialize(envelope, jsonOptions));
            return;
        }

        if (string.Equals(outputMode, "Jsonl", StringComparison.Ordinal))
        {
            logLine(JsonSerializer.Serialize(envelope, jsonOptions));
            return;
        }

        var line = $"Refinery ScenarioRunner lane | profile={envelope.RefineryProfile ?? "none"} runs={envelope.TotalRuns} checkpoints={envelope.Summary.CheckpointCount} exit={envelope.ExitCode}:{envelope.ExitReason}";
        logLine(line);
    }

    private static void WriteArtifacts(RefineryRunEnvelope envelope, string artifactDirRaw, string runLog)
    {
        var artifactDir = Path.GetFullPath(artifactDirRaw);
        Directory.CreateDirectory(artifactDir);
        var runsDir = Path.Combine(artifactDir, "runs");
        var refineryDir = Path.Combine(artifactDir, "refinery");
        Directory.CreateDirectory(runsDir);
        Directory.CreateDirectory(refineryDir);

        var jsonOptions = CreateJsonOptions(writeIndented: true);
        File.WriteAllText(Path.Combine(artifactDir, "summary.json"), JsonSerializer.Serialize(envelope, jsonOptions));
        File.WriteAllText(Path.Combine(artifactDir, "assertions.json"), JsonSerializer.Serialize(envelope.Assertions, jsonOptions));
        File.WriteAllText(Path.Combine(artifactDir, "anomalies.json"), JsonSerializer.Serialize(envelope.Anomalies, jsonOptions));
        File.WriteAllText(Path.Combine(artifactDir, "run.log"), runLog);

        foreach (var run in envelope.Runs)
        {
            File.WriteAllText(Path.Combine(runsDir, run.RunKey + ".json"), JsonSerializer.Serialize(run, jsonOptions));
            var runDir = Path.Combine(refineryDir, run.RunKey);
            var checkpointsDir = Path.Combine(runDir, "checkpoints");
            Directory.CreateDirectory(checkpointsDir);
            File.WriteAllText(Path.Combine(runDir, "checkpoints.json"), JsonSerializer.Serialize(run.Checkpoints, jsonOptions));
            for (var i = 0; i < run.Checkpoints.Count; i++)
            {
                File.WriteAllText(Path.Combine(checkpointsDir, $"{i + 1:000}.json"), JsonSerializer.Serialize(run.Checkpoints[i], jsonOptions));
            }
        }

        var index = new RefineryArtifactIndex(
            SchemaVersion: envelope.SchemaVersion,
            LaneType: envelope.LaneType,
            RefineryProfile: envelope.RefineryProfile,
            Runs: envelope.Runs.Select(run => new RefineryArtifactRunIndex(run.RunKey, run.ConfigName, run.PlannerMode, run.Seed, run.Checkpoints.Count)).ToList());
        File.WriteAllText(Path.Combine(refineryDir, "index.json"), JsonSerializer.Serialize(index, jsonOptions));
        File.WriteAllText(Path.Combine(refineryDir, "summary.json"), JsonSerializer.Serialize(envelope.Summary, jsonOptions));
        File.WriteAllText(Path.Combine(refineryDir, "scorecard.json"), JsonSerializer.Serialize(BuildScorecard(envelope), jsonOptions));
        if (envelope.CostEstimateOnly)
        {
            File.WriteAllText(Path.Combine(refineryDir, "preflight.json"), JsonSerializer.Serialize(new
            {
                envelope.PaidPreset,
                envelope.EstimatedCompletions,
                envelope.MaxCompletions,
                envelope.ExpectedJavaDirectorMaxRetries,
                envelope.PaidConfirmPresent,
                envelope.RehearsalArtifact,
                envelope.CostEstimateOnly,
                javaExecutionSkipped = true
            }, jsonOptions));
        }

        var manifest = new RefineryArtifactManifest(
            SchemaVersion: "smr/v1",
            GeneratedAtUtc: DateTime.UtcNow,
            RunId: Guid.NewGuid().ToString("D"),
            ArtifactDir: artifactDir,
            ExitCode: envelope.ExitCode,
            ExitReason: envelope.ExitReason,
            TotalRuns: envelope.TotalRuns,
            LaneType: envelope.LaneType,
            RefineryEnabled: envelope.RefineryEnabled,
            RefineryProfile: envelope.RefineryProfile,
            RefineryGoal: envelope.RefineryGoal,
            CheckpointPolicy: envelope.CheckpointPolicy,
            CheckpointCount: envelope.Summary.CheckpointCount,
            MaxTriggers: envelope.MaxTriggers,
            WaitMode: envelope.WaitMode,
            CaptureMode: envelope.CaptureMode,
            RequestTimeoutMs: envelope.RequestTimeoutMs,
            RetryCount: envelope.RetryCount,
            DirectorMaxBudget: envelope.DirectorMaxBudget,
            PaidPreset: envelope.PaidPreset,
            EstimatedCompletions: envelope.EstimatedCompletions,
            MaxCompletions: envelope.MaxCompletions,
            ExpectedJavaDirectorMaxRetries: envelope.ExpectedJavaDirectorMaxRetries,
            ObservedCompletionCount: envelope.Summary.ObservedCompletionCount,
            PaidConfirmPresent: envelope.PaidConfirmPresent,
            RehearsalArtifact: envelope.RehearsalArtifact,
            CostEstimateOnly: envelope.CostEstimateOnly,
            RefineryAppliedCount: envelope.Summary.AppliedCount,
            RefineryApplyFailedCount: envelope.Summary.ApplyFailedCount,
            RefineryRequestFailedCount: envelope.Summary.RequestFailedCount,
            RefineryFallbackCount: envelope.Summary.FallbackCount,
            RefineryValidatedCount: envelope.Summary.ValidatedCount);
        File.WriteAllText(Path.Combine(artifactDir, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions));
    }

    private static object BuildScorecard(RefineryRunEnvelope envelope)
    {
        var hardAnomalyCount = envelope.Anomalies.Count(anomaly => string.Equals(anomaly.Severity, "error", StringComparison.Ordinal));
        return new
        {
            balanceStability = new
            {
                status = "deferred_to_smr_analyst",
                note = "Track B records refinery lane signals only; SMR Analyst owns balance verdict."
            },
            directorCreativity = new
            {
                status = "marker_and_operator_review_only",
                hashCaptureStatus = envelope.CaptureMode == "hash" ? "raw_response_hash_not_available_in_b1" : "not_requested",
                note = "W8.6-B1 does not persist raw paid payloads; creativity review uses safe markers, distributions, and operator notes."
            },
            failureHardening = new
            {
                status = hardAnomalyCount == 0 ? "no_hard_refinery_anomaly" : "hard_refinery_anomaly",
                requestFailed = envelope.Summary.RequestFailedCount,
                applyFailed = envelope.Summary.ApplyFailedCount,
                hardAnomalyCount
            },
            formalRefineryQuality = new
            {
                status = "track_d_semantics_consumed",
                observedCompletionCount = envelope.Summary.ObservedCompletionCount,
                observedRetryRounds = envelope.Summary.ObservedRetryRounds,
                solverCoverage = envelope.Summary.DirectorSolverValidatedCoverageCheckpointCounts,
                unsupported = envelope.Summary.DirectorSolverUnsupportedCheckpointCounts
            },
            finalVerdictOwner = "SMR Analyst"
        };
    }

    private static Dictionary<string, int> BuildHistogram(IEnumerable<string?> values)
        => values.GroupBy(value => string.IsNullOrWhiteSpace(value) ? "unknown" : value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static Dictionary<string, int> BuildCheckpointValueHistogram(IEnumerable<IReadOnlyList<string>> values)
        => values
            .Select(items => items.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal))
            .SelectMany(items => items)
            .GroupBy(item => item, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static Dictionary<string, int> BuildValueOccurrencesHistogram(IEnumerable<IReadOnlyList<string>> values)
        => values
            .SelectMany(items => items.Where(item => !string.IsNullOrWhiteSpace(item)))
            .GroupBy(item => item, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static bool HasValidatedCoverage(RefineryCheckpointRecord checkpoint)
        => checkpoint.DirectorSolverValidatedCoverage.Any(item => string.Equals(item, "story_core", StringComparison.Ordinal)
            || string.Equals(item, "directive_core", StringComparison.Ordinal));

    private static SolverMarkerParseResult ParseSolverMarkers(IReadOnlyList<string> explainMarkers)
    {
        string? path = null;
        string? status = null;
        string? generatorResult = null;
        string? extraction = null;
        var coverage = new List<string>();
        var unsupported = new List<string>();
        var diagnostics = new List<string>();
        var warnings = new List<string>();

        foreach (var marker in explainMarkers)
        {
            ConsumeSingleValueMarker(marker, "directorSolverPath:", SupportedSolverPaths, ref path, warnings);
            ConsumeSingleValueMarker(marker, "directorSolverStatus:", SupportedSolverStatuses, ref status, warnings);
            ConsumeGeneratorResultMarker(marker, ref generatorResult);
            ConsumeSingleValueMarker(marker, "directorSolverExtraction:", SupportedSolverExtractions, ref extraction, warnings);
            ConsumeRepeatedMarker(marker, "directorSolverValidatedCoverage:", coverage);
            ConsumeRepeatedMarker(marker, "directorSolverUnsupported:", unsupported);
            ConsumeRepeatedMarker(marker, "directorSolverDiagnostic:", diagnostics);
        }

        return new SolverMarkerParseResult(
            Path: path,
            Status: status,
            GeneratorResult: generatorResult,
            Extraction: extraction,
            ValidatedCoverage: coverage.Count == 0 ? Array.Empty<string>() : coverage.Distinct(StringComparer.Ordinal).ToArray(),
            Unsupported: unsupported.Count == 0 ? Array.Empty<string>() : unsupported.Distinct(StringComparer.Ordinal).ToArray(),
            Diagnostics: diagnostics.Count == 0 ? Array.Empty<string>() : diagnostics.ToArray(),
            Warnings: warnings.Count == 0 ? Array.Empty<string>() : warnings.ToArray());
    }

    private static ObservabilityMarkerParseResult ParseObservabilityMarkers(IReadOnlyList<string> explainMarkers)
    {
        string? llmStage = null;
        int? llmCompletionCount = null;
        int? llmRetryRounds = null;
        string? llmCandidateSanitized = null;
        var sanitizeTags = new List<string>();
        int? causalChainOps = null;

        foreach (var marker in explainMarkers)
        {
            ConsumeStringMarker(marker, "llmStage:", ref llmStage);
            ConsumeIntMarker(marker, "llmCompletionCount:", ref llmCompletionCount);
            ConsumeIntMarker(marker, "llmRetryRounds:", ref llmRetryRounds);
            ConsumeStringMarker(marker, "llmCandidateSanitized:", ref llmCandidateSanitized);
            ConsumeRepeatedMarker(marker, "llmCandidateSanitizeTags:", sanitizeTags);
            ConsumeIntMarker(marker, "causalChainOps:", ref causalChainOps);
        }

        return new ObservabilityMarkerParseResult(
            LlmStage: llmStage,
            LlmCompletionCount: llmCompletionCount,
            LlmRetryRounds: llmRetryRounds,
            LlmCandidateSanitized: llmCandidateSanitized,
            LlmCandidateSanitizeTags: sanitizeTags.Count == 0 ? Array.Empty<string>() : sanitizeTags.ToArray(),
            CausalChainOps: causalChainOps);
    }

    private static void ConsumeStringMarker(string marker, string prefix, ref string? target)
    {
        if (marker.StartsWith(prefix, StringComparison.Ordinal))
            target = marker[prefix.Length..].Trim();
    }

    private static void ConsumeIntMarker(string marker, string prefix, ref int? target)
    {
        if (!marker.StartsWith(prefix, StringComparison.Ordinal))
            return;

        if (int.TryParse(marker[prefix.Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            target = parsed;
    }

    private static void ConsumeSingleValueMarker(string marker, string prefix, HashSet<string> knownValues, ref string? target, List<string> warnings)
    {
        if (!marker.StartsWith(prefix, StringComparison.Ordinal))
            return;

        var value = marker[prefix.Length..].Trim();
        target = value;
        if (!knownValues.Contains(value))
            warnings.Add(prefix[..^1] + "=" + value);
    }

    private static void ConsumeGeneratorResultMarker(string marker, ref string? target)
    {
        const string prefix = "directorSolverGeneratorResult:";
        if (!marker.StartsWith(prefix, StringComparison.Ordinal))
            return;

        target = marker[prefix.Length..].Trim();
    }

    private static void ConsumeRepeatedMarker(string marker, string prefix, List<string> target)
    {
        if (!marker.StartsWith(prefix, StringComparison.Ordinal))
            return;

        target.Add(marker[prefix.Length..].Trim());
    }

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented
        };

    private static void AddAssertion(
        string invariantId,
        string category,
        string severity,
        string runKey,
        bool assertEnabled,
        bool condition,
        string measured,
        string threshold,
        string message,
        List<RefineryAssertionResult> results)
    {
        results.Add(new RefineryAssertionResult(invariantId, category, severity, runKey, !assertEnabled || condition, false, null, message, measured, threshold));
    }

    private static void AddSkippedAssertion(
        string invariantId,
        string category,
        string runKey,
        string skipReason,
        string message,
        List<RefineryAssertionResult> results)
    {
        results.Add(new RefineryAssertionResult(invariantId, category, "warning", runKey, true, true, skipReason, message, null, null));
    }

    private static (int ExitCode, string ExitReason) ResolveExitCode(bool hasConfigError, bool hasAssertionFailures, bool hasHardAnomaly)
    {
        if (hasConfigError)
            return (3, "config_error");
        if (hasAssertionFailures)
            return (2, "assert_fail");
        if (hasHardAnomaly)
            return (4, "anomaly_gate_fail");
        return (0, "ok");
    }

    private static bool IsTerminalOutcome(string? status)
        => status is "applied" or "apply_failed" or "request_failed";

    private static string? TryExtractFailureKind(string rawStatusText)
    {
        const string prefix = "kind=";
        var index = rawStatusText.IndexOf(prefix, StringComparison.Ordinal);
        if (index < 0)
            return null;
        var start = index + prefix.Length;
        var end = rawStatusText.IndexOf(',', start);
        return end < 0 ? rawStatusText[start..].Trim() : rawStatusText[start..end].Trim();
    }

    private static string FindTechPath()
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

    private static string BuildRunKey(string configName, string planner, string profile, int seed)
        => $"{BuildStableKeyPart(configName)}_{BuildStableKeyPart(planner)}_{BuildStableKeyPart(profile)}_{seed}";

    private static string BuildStableKeyPart(string value)
        => $"{ToFileSafeToken(value)}_{ComputeStableHash(value)}";

    private static string ComputeStableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 4)).ToLowerInvariant();
    }

    private static string ToFileSafeToken(string value)
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
            }
            else if (ch == '-')
            {
                builder.Append('-');
                previousUnderscore = false;
            }
            else if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }
        var token = builder.ToString().Trim('_');
        return token.Length > 0 ? token : "run";
    }
}

public sealed record RefineryScenarioRunnerRequest(
    string? RawLane,
    IReadOnlyList<RefineryScenarioConfig> Configs,
    IReadOnlyList<int> Seeds,
    IReadOnlyList<NpcPlannerMode> Planners,
    string? ArtifactDir,
    string OutputMode,
    bool AssertEnabled,
    string InitialRunLog,
    string BaseDirectory,
    bool ConfigHadError);

public sealed record RefineryScenarioConfig(string Name, int Width, int Height, int InitialPop, int Ticks, float Dt);

internal sealed record SolverMarkerParseResult(
    string? Path,
    string? Status,
    string? GeneratorResult,
    string? Extraction,
    IReadOnlyList<string> ValidatedCoverage,
    IReadOnlyList<string> Unsupported,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Warnings);

internal sealed record ObservabilityMarkerParseResult(
    string? LlmStage,
    int? LlmCompletionCount,
    int? LlmRetryRounds,
    string? LlmCandidateSanitized,
    IReadOnlyList<string> LlmCandidateSanitizeTags,
    int? CausalChainOps);

internal sealed record RefineryScenarioOptions(
    string LaneType,
    string? RefineryProfile,
    string RefineryGoal,
    string TriggerPolicy,
    int TriggerEvery,
    IReadOnlyList<int> TriggerTicks,
    int MaxTriggers,
    string WaitMode,
    int WaitTimeoutMs,
    string CaptureMode,
    int RequestTimeoutMs,
    int RetryCount,
    double DirectorMaxBudget,
    string FixtureResponsePath,
    string BaseDirectory,
    string? PaidPreset,
    bool PaidConfirmPresent,
    string? NormalizedRehearsalManifestPath,
    int? MaxCompletions,
    int? EstimatedCompletions,
    int? ExpectedJavaDirectorMaxRetries,
    bool CostEstimateOnly,
    bool HadConfigError,
    IReadOnlyList<string> Warnings)
{
    public static RefineryScenarioOptions FromEnvironment(string? rawLane, string baseDirectory)
    {
        var warnings = new List<string>();
        var normalizedLane = string.IsNullOrWhiteSpace(rawLane) ? "core" : rawLane.Trim().ToLowerInvariant();
        if (normalizedLane is "core")
        {
            return Create("core", null, baseDirectory, warnings, hadConfigError: false);
        }
        if (normalizedLane == "refinery_live_validator")
        {
            return Create("refinery", "validator", baseDirectory, warnings, hadConfigError: false);
        }
        if (normalizedLane == "refinery_live_paid")
        {
            return Create("refinery", "live_paid", baseDirectory, warnings, hadConfigError: false);
        }
        if (normalizedLane is not ("refinery_fixture" or "refinery_live_mock"))
        {
            warnings.Add($"Warning: unknown WORLDSIM_SCENARIO_LANE='{rawLane}'. Exiting with config_error.");
            return Create("refinery", null, baseDirectory, warnings, hadConfigError: true);
        }

        var profile = normalizedLane == "refinery_fixture" ? "fixture" : "live_mock";
        var options = Create("refinery", profile, baseDirectory, warnings, hadConfigError: false);
        if (string.Equals(options.TriggerPolicy, "season_boundary", StringComparison.Ordinal))
        {
            warnings.Add("Warning: WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY=season_boundary is unsupported in TR2-D. Exiting with config_error.");
            return options with { HadConfigError = true, Warnings = warnings };
        }
        return options with { Warnings = warnings };
    }

    private static RefineryScenarioOptions Create(string laneType, string? profile, string baseDirectory, List<string> warnings, bool hadConfigError)
    {
        var isPaid = profile == "live_paid";
        var triggerPolicy = ReadToken("WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY", isPaid ? "tick_list" : "every_n_ticks");
        if (triggerPolicy is not ("every_n_ticks" or "tick_list" or "season_boundary"))
        {
            warnings.Add($"Warning: unsupported WORLDSIM_SCENARIO_REFINERY_TRIGGER_POLICY='{triggerPolicy}'. Exiting with config_error.");
            hadConfigError = true;
        }

        var captureMode = ReadToken("WORLDSIM_SCENARIO_REFINERY_CAPTURE", isPaid ? "hash" : "redacted");
        if (captureMode is not ("none" or "hash" or "redacted"))
        {
            warnings.Add(isPaid && captureMode == "full"
                ? "Warning: WORLDSIM_SCENARIO_REFINERY_CAPTURE=full is not allowed for paid Wave 8.6 presets. Exiting with config_error."
                : $"Warning: unsupported WORLDSIM_SCENARIO_REFINERY_CAPTURE='{captureMode}'. Exiting with config_error.");
            hadConfigError = true;
        }

        var waitMode = ReadToken("WORLDSIM_SCENARIO_REFINERY_WAIT_MODE", "block_until_settled");
        if (waitMode != "block_until_settled")
        {
            warnings.Add($"Warning: unsupported WORLDSIM_SCENARIO_REFINERY_WAIT_MODE='{waitMode}'. Exiting with config_error.");
            hadConfigError = true;
        }

        return new RefineryScenarioOptions(
            LaneType: laneType,
            RefineryProfile: profile,
            RefineryGoal: DirectorGoals.SeasonDirectorCheckpoint,
            TriggerPolicy: triggerPolicy,
            TriggerEvery: ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_TRIGGER_EVERY", 4, 1, 100_000),
            TriggerTicks: ReadTickList("WORLDSIM_SCENARIO_REFINERY_TRIGGER_TICKS"),
            MaxTriggers: ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS", 1, 1, isPaid ? 3 : 10),
            WaitMode: waitMode,
            WaitTimeoutMs: ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS", 4_000, 1, 120_000),
            CaptureMode: captureMode,
            RequestTimeoutMs: ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS", 1_200, 1, 120_000),
            RetryCount: ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT", 0, 0, 3),
            DirectorMaxBudget: ReadDouble("WORLDSIM_SCENARIO_REFINERY_DIRECTOR_MAX_BUDGET", 5d),
            FixtureResponsePath: ReadFixtureResponsePath(baseDirectory),
            BaseDirectory: baseDirectory,
            PaidPreset: isPaid ? ReadToken("WORLDSIM_SCENARIO_REFINERY_PAID_PRESET", string.Empty) : null,
            PaidConfirmPresent: string.Equals(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM"), "I_UNDERSTAND_OPENROUTER_COSTS", StringComparison.Ordinal),
            NormalizedRehearsalManifestPath: isPaid ? NormalizeRehearsalManifestPath(Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT")) : null,
            MaxCompletions: isPaid ? ReadIntClamped("WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS", 8, 1, 8) : null,
            EstimatedCompletions: null,
            ExpectedJavaDirectorMaxRetries: null,
            CostEstimateOnly: isPaid && ReadBool("WORLDSIM_SCENARIO_REFINERY_COST_ESTIMATE_ONLY"),
            HadConfigError: hadConfigError,
            Warnings: warnings);
    }

    public RefineryScenarioOptions ValidateAgainst(RefineryScenarioRunnerRequest request)
    {
        if (RefineryProfile != "live_paid")
            return this;

        var warnings = Warnings.ToList();
        var hadConfigError = HadConfigError;
        var preset = string.IsNullOrWhiteSpace(PaidPreset) ? null : PaidPreset;
        var expectedCheckpoints = 0;
        var expectedJavaRetries = 0;
        if (preset is null)
        {
            warnings.Add("Warning: refinery_live_paid requires WORLDSIM_SCENARIO_REFINERY_PAID_PRESET. Exiting with config_error.");
            hadConfigError = true;
        }
        else if (preset == "paid_micro_total2")
        {
            expectedCheckpoints = 1;
            expectedJavaRetries = 0;
        }
        else if (preset == "paid_probe_2x2x2")
        {
            expectedCheckpoints = 2;
            expectedJavaRetries = 1;
        }
        else if (preset == "custom")
        {
            warnings.Add("Warning: custom paid preset is deferred beyond W8.6-B1. Exiting with config_error.");
            hadConfigError = true;
        }
        else
        {
            warnings.Add($"Warning: unsupported WORLDSIM_SCENARIO_REFINERY_PAID_PRESET='{preset}'. Exiting with config_error.");
            hadConfigError = true;
        }

        if (!PaidConfirmPresent)
        {
            warnings.Add("Warning: refinery_live_paid requires WORLDSIM_SCENARIO_REFINERY_PAID_CONFIRM=I_UNDERSTAND_OPENROUTER_COSTS. Exiting with config_error.");
            hadConfigError = true;
        }

        if (!IsEnvPresent("WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS") || !IsEnvPresent("WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS"))
        {
            warnings.Add("Warning: refinery_live_paid requires explicit request and wait timeout env vars. Exiting with config_error.");
            hadConfigError = true;
        }

        ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_REQUEST_TIMEOUT_MS", 1, 120_000, required: true, warnings, ref hadConfigError);
        ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_WAIT_TIMEOUT_MS", 1, 120_000, required: true, warnings, ref hadConfigError);
        ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_MAX_TRIGGERS", 1, 3, required: false, warnings, ref hadConfigError);
        ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_MAX_COMPLETIONS", 1, 8, required: false, warnings, ref hadConfigError);

        if (RetryCount != 0)
        {
            warnings.Add("Warning: Wave 8.6 paid presets require WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT=0. Exiting with config_error.");
            hadConfigError = true;
        }
        var declaredRetryCount = ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_RETRY_COUNT", 0, 0, required: true, warnings, ref hadConfigError);
        var declaredJavaRetries = ValidatePaidIntEnv("WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES", 0, 2, required: true, warnings, ref hadConfigError);
        if (declaredJavaRetries.HasValue && expectedCheckpoints > 0 && declaredJavaRetries.Value != expectedJavaRetries)
        {
            warnings.Add($"Warning: {preset} requires WORLDSIM_SCENARIO_REFINERY_EXPECTED_JAVA_MAX_RETRIES={expectedJavaRetries}; got {declaredJavaRetries.Value}. Exiting with config_error.");
            hadConfigError = true;
        }

        var rehearsalStatus = ValidateRehearsalArtifact(NormalizedRehearsalManifestPath);
        if (!rehearsalStatus.Accepted)
        {
            warnings.Add($"Warning: refinery_live_paid requires a GREEN rehearsal artifact ({rehearsalStatus.Message}). Exiting with config_error.");
            hadConfigError = true;
        }

        if (expectedCheckpoints > 0)
        {
            ValidateExactPaidShape(request, expectedCheckpoints, warnings, ref hadConfigError);
        }

        var estimatedCompletions = expectedCheckpoints > 0
            ? request.Seeds.Count * request.Planners.Count * request.Configs.Count * expectedCheckpoints * ((declaredRetryCount ?? RetryCount) + 1) * ((declaredJavaRetries ?? expectedJavaRetries) + 1)
            : 0;
        if (MaxCompletions.HasValue && estimatedCompletions > MaxCompletions.Value)
        {
            warnings.Add($"Warning: paid estimated completions {estimatedCompletions} exceed cap {MaxCompletions.Value}. Exiting with config_error.");
            hadConfigError = true;
        }

        return this with
        {
            EstimatedCompletions = estimatedCompletions,
            ExpectedJavaDirectorMaxRetries = expectedCheckpoints > 0 ? expectedJavaRetries : null,
            HadConfigError = hadConfigError,
            Warnings = warnings
        };
    }

    private void ValidateExactPaidShape(RefineryScenarioRunnerRequest request, int expectedCheckpoints, List<string> warnings, ref bool hadConfigError)
    {
        if (request.Seeds.Count != 2)
        {
            warnings.Add($"Warning: {PaidPreset} requires exactly 2 seeds; got {request.Seeds.Count}. Exiting with config_error.");
            hadConfigError = true;
        }

        if (request.Planners.Count != 1)
        {
            warnings.Add($"Warning: {PaidPreset} requires exactly 1 planner; got {request.Planners.Count}. Exiting with config_error.");
            hadConfigError = true;
        }

        if (request.Configs.Count != 1)
        {
            warnings.Add($"Warning: {PaidPreset} requires exactly 1 config; got {request.Configs.Count}. Exiting with config_error.");
            hadConfigError = true;
        }

        foreach (var config in request.Configs)
        {
            var scheduledCount = RefineryCheckpointScheduler.Create(this, config.Ticks).ScheduledTicks.Count;
            if (scheduledCount != expectedCheckpoints)
            {
                warnings.Add($"Warning: {PaidPreset} requires exactly {expectedCheckpoints} checkpoint(s) per run; got {scheduledCount} for config '{config.Name}'. Exiting with config_error.");
                hadConfigError = true;
            }
        }
    }

    private static (bool Accepted, string Message) ValidateRehearsalArtifact(string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            return (false, "missing WORLDSIM_SCENARIO_REFINERY_REHEARSAL_ARTIFACT");
        if (!File.Exists(manifestPath))
            return (false, "manifest not found");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var accepted = GetString(root, "laneType") == "refinery"
                && GetBool(root, "refineryEnabled") == true
                && GetString(root, "refineryProfile") == "validator"
                && GetInt(root, "exitCode") == 0
                && GetInt(root, "checkpointCount") is > 0
                && GetInt(root, "refineryRequestFailedCount") == 0
                && GetInt(root, "refineryApplyFailedCount") == 0;
            return accepted ? (true, "green") : (false, "manifest is not GREEN validator rehearsal");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return (false, "manifest unreadable");
        }
    }

    private static string? NormalizeRehearsalManifestPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var path = Path.GetFullPath(raw);
        return Directory.Exists(path) ? Path.Combine(path, "manifest.json") : path;
    }

    private static string? GetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? GetBool(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static int? GetInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : null;

    private static int? ValidatePaidIntEnv(string key, int min, int max, bool required, List<string> warnings, ref bool hadConfigError)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (required)
            {
                warnings.Add($"Warning: refinery_live_paid requires explicit {key}. Exiting with config_error.");
                hadConfigError = true;
            }
            return null;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            warnings.Add($"Warning: refinery_live_paid requires numeric {key}; got '{raw}'. Exiting with config_error.");
            hadConfigError = true;
            return null;
        }

        if (parsed < min || parsed > max)
        {
            warnings.Add($"Warning: refinery_live_paid {key} must be between {min} and {max}; got {parsed}. Exiting with config_error.");
            hadConfigError = true;
        }

        return parsed;
    }

    public RefineryRuntimeOptions ToRuntimeOptions()
    {
        var mode = RefineryProfile == "fixture" ? RefineryIntegrationMode.Fixture : RefineryIntegrationMode.Live;
        return new RefineryRuntimeOptions(
            Mode: mode,
            Goal: RefineryGoal,
            DirectorOutputMode: "auto",
            FixtureResponsePath: FixtureResponsePath,
            ServiceBaseUrl: Environment.GetEnvironmentVariable("REFINERY_BASE_URL") ?? "http://localhost:8091",
            StrictMode: true,
            RequestSeed: 123L,
            LiveTimeoutMs: RequestTimeoutMs,
            LiveRetryCount: RetryCount,
            CircuitBreakerSeconds: 1,
            ApplyToWorld: true,
            MinTriggerIntervalMs: 0,
            DirectorMaxBudget: DirectorMaxBudget,
            OperatorProfileName: RefineryProfile switch
            {
                "fixture" => RefineryRuntimeOptions.ProfileFixtureSmoke,
                "live_mock" => RefineryRuntimeOptions.ProfileLiveMock,
                _ => RefineryRuntimeOptions.ProfileLiveDirector
            });
    }

    private static string ReadToken(string key, string fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim().ToLowerInvariant().Replace('-', '_');
    }

    private static bool IsEnvPresent(string key)
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key));

    private static bool ReadBool(string key)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadIntClamped(string key, int fallback, int min, int max)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        var parsed = int.TryParse(raw, out var value) ? value : fallback;
        return Math.Clamp(parsed, min, max);
    }

    private static double ReadDouble(string key, double fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || double.IsNaN(parsed) || double.IsInfinity(parsed))
            return fallback;
        return Math.Max(0d, parsed);
    }

    private static IReadOnlyList<int> ReadTickList(string key)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<int>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var tick) ? tick : -1)
            .Where(tick => tick > 0)
            .Distinct()
            .OrderBy(tick => tick)
            .ToArray();
    }

    private static string ReadFixtureResponsePath(string baseDirectory)
    {
        var overridePath = Environment.GetEnvironmentVariable("WORLDSIM_SCENARIO_REFINERY_FIXTURE_RESPONSE");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        return Path.Combine(FindRepoRoot(baseDirectory), "refinery-service-java", "examples", "responses", "patch-season-director-v1.expected.json");
    }

    private static string FindRepoRoot(string baseDirectory)
    {
        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "refinery-service-java")))
                return current.FullName;
            current = current.Parent;
        }
        return baseDirectory;
    }
}

internal sealed record RefineryCheckpointScheduler(IReadOnlyList<int> ScheduledTicks)
{
    public static RefineryCheckpointScheduler Create(RefineryScenarioOptions options, int tickCount)
    {
        var ticks = options.TriggerPolicy == "tick_list"
            ? options.TriggerTicks.Where(tick => tick <= tickCount)
            : Enumerable.Range(1, tickCount).Where(tick => tick % options.TriggerEvery == 0);
        return new RefineryCheckpointScheduler(ticks.Take(options.MaxTriggers).ToArray());
    }
}

internal sealed record RefineryRunEnvelope(
    string SchemaVersion,
    DateTime GeneratedAtUtc,
    string LaneType,
    bool RefineryEnabled,
    string? RefineryProfile,
    string RefineryGoal,
    string CheckpointPolicy,
    int MaxTriggers,
    string WaitMode,
    string CaptureMode,
    int RequestTimeoutMs,
    int RetryCount,
    double DirectorMaxBudget,
    string? PaidPreset,
    int? EstimatedCompletions,
    int? MaxCompletions,
    int? ExpectedJavaDirectorMaxRetries,
    bool PaidConfirmPresent,
    string? RehearsalArtifact,
    bool CostEstimateOnly,
    int SeedCount,
    int PlannerCount,
    int ConfigCount,
    int TotalRuns,
    int ExitCode,
    string ExitReason,
    List<RefineryAssertionResult> Assertions,
    List<RefineryAnomaly> Anomalies,
    RefinerySummary Summary,
    List<RefineryRunResult> Runs);

internal sealed record RefineryRunResult(
    string RunKey,
    string ConfigName,
    string PlannerMode,
    int Seed,
    string LaneType,
    string? RefineryProfile,
    int Width,
    int Height,
    int InitialPop,
    int Ticks,
    float Dt,
    List<RefineryCheckpointRecord> Checkpoints);

internal sealed record RefineryCheckpointRecord(
    int TriggerIndex,
    int TriggerTick,
    string TerminalOutcome,
    string Stage,
    string ApplyStatus,
    string OutputMode,
    string OutputSource,
    double BudgetUsed,
    bool BudgetMarkerPresent,
    IReadOnlyList<string> ExplainMarkers,
    string? DirectorSolverPath,
    string? DirectorSolverStatus,
    string? DirectorSolverGeneratorResult,
    string? DirectorSolverExtraction,
    IReadOnlyList<string> DirectorSolverValidatedCoverage,
    IReadOnlyList<string> DirectorSolverUnsupported,
    IReadOnlyList<string> DirectorSolverDiagnostic,
    IReadOnlyList<string> SolverParseWarnings,
    string? LlmStage,
    int? LlmCompletionCount,
    int? LlmRetryRounds,
    string? LlmCandidateSanitized,
    IReadOnlyList<string> LlmCandidateSanitizeTags,
    int? CausalChainOps,
    string ActionStatus,
    long SettleLatencyMs,
    int WarningsCount,
    string CaptureMode,
    string? ResponseHash,
    string? TelemetryRef,
    string? RequestFailureKind,
    string RawStatusText);

internal sealed record RefinerySummary(
    int CheckpointCount,
    Dictionary<string, int> StageHistogram,
    Dictionary<string, int> ApplyStatusHistogram,
    Dictionary<string, int> OutputModeHistogram,
    Dictionary<string, int> OutputSourceHistogram,
    double TotalBudgetUsed,
    double MaxBudgetUsed,
    double AverageSettleLatencyMs,
    long MaxSettleLatencyMs,
    int AppliedCount,
    int ApplyFailedCount,
    int RequestFailedCount,
    int FallbackCount,
    int ValidatedCount,
    Dictionary<string, int> DirectorSolverPathHistogram,
    Dictionary<string, int> DirectorSolverStatusHistogram,
    Dictionary<string, int> DirectorSolverGeneratorResultHistogram,
    Dictionary<string, int> DirectorSolverExtractionHistogram,
    Dictionary<string, int> DirectorSolverValidatedCoverageCheckpointCounts,
    Dictionary<string, int> DirectorSolverUnsupportedCheckpointCounts,
    Dictionary<string, int> DirectorSolverDiagnosticCounts,
    Dictionary<string, int> LlmStageHistogram,
    int? ObservedCompletionCount,
    int? ObservedRetryRounds,
    Dictionary<string, int> LlmCandidateSanitizedHistogram,
    Dictionary<string, int> LlmCandidateSanitizeTagCounts,
    int? CausalChainOpsTotal);

internal sealed record RefineryAssertionResult(
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

internal sealed record RefineryAnomaly(
    string Id,
    string Category,
    string Severity,
    string? RunKey,
    string Message,
    string? Value,
    string? Threshold);

internal sealed record RefineryArtifactIndex(string SchemaVersion, string LaneType, string? RefineryProfile, List<RefineryArtifactRunIndex> Runs);

internal sealed record RefineryArtifactRunIndex(string RunKey, string ConfigName, string PlannerMode, int Seed, int CheckpointCount);

internal sealed record RefineryArtifactManifest(
    string SchemaVersion,
    DateTime GeneratedAtUtc,
    string RunId,
    string ArtifactDir,
    int ExitCode,
    string ExitReason,
    int TotalRuns,
    string LaneType,
    bool RefineryEnabled,
    string? RefineryProfile,
    string RefineryGoal,
    string CheckpointPolicy,
    int CheckpointCount,
    int MaxTriggers,
    string WaitMode,
    string CaptureMode,
    int RequestTimeoutMs,
    int RetryCount,
    double DirectorMaxBudget,
    string? PaidPreset,
    int? EstimatedCompletions,
    int? MaxCompletions,
    int? ExpectedJavaDirectorMaxRetries,
    int? ObservedCompletionCount,
    bool PaidConfirmPresent,
    string? RehearsalArtifact,
    bool CostEstimateOnly,
    int RefineryAppliedCount,
    int RefineryApplyFailedCount,
    int RefineryRequestFailedCount,
    int RefineryFallbackCount,
    int RefineryValidatedCount);
