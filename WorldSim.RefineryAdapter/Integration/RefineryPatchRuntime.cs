using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WorldSim.Runtime;
using WorldSim.RefineryAdapter.Translation;
using WorldSimRefineryClient.Apply;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;
using WorldSimRefineryClient.Serialization;
using WorldSimRefineryClient.Service;

namespace WorldSim.RefineryAdapter.Integration;

public sealed class RefineryPatchRuntime
{
    private readonly RefineryRuntimeOptions _baselineOptions;
    private RefineryRuntimeOptions _activeOptions;
    private readonly PatchResponseParser _parser = new();
    private readonly PatchApplier _applier = new();
    private readonly PatchCommandTranslator _translator = new();
    private readonly RuntimePatchCommandExecutor _executor = new();
    private readonly SimulationPatchState _patchState = SimulationPatchState.CreateBaseline();
    private RefineryServiceClient? _serviceClient;
    private HttpClient? _httpClient;
    private string _serviceClientBaseUrl = string.Empty;

    private Task? _inFlight;
    private DateTime _circuitBreakerUntilUtc = DateTime.MinValue;
    private int _consecutiveFailures;
    private DateTime _lastTriggerUtc = DateTime.MinValue;
    private string _requestedDirectorOutputMode;
    private string _requestedDirectorOutputModeSource;
    private string _operatorProfileSource;

    public string LastStatus { get; private set; } = "Refinery status: not_triggered";
    public DirectorExecutionStatus LastDirectorExecutionStatus { get; private set; } = DirectorExecutionStatus.NotTriggered;
    public string OperatorProfileName => _activeOptions.OperatorProfileName;
    public string OperatorProfileSource => _operatorProfileSource;
    public string CurrentIntegrationMode => _activeOptions.Mode.ToString().ToLowerInvariant();
    public string RequestedDirectorOutputMode => _requestedDirectorOutputMode;
    public string RequestedDirectorOutputModeSource => _requestedDirectorOutputModeSource;

    public RefineryPatchRuntime(RefineryRuntimeOptions options)
    {
        _baselineOptions = options;
        _activeOptions = options;
        _requestedDirectorOutputMode = options.DirectorOutputMode;
        _requestedDirectorOutputModeSource = "env";
        _operatorProfileSource = "env";

        EnsureLiveClient();

        LastStatus = _activeOptions.Mode switch
        {
            RefineryIntegrationMode.Off => "Refinery lane=off",
            RefineryIntegrationMode.Fixture => "Refinery lane=fixture (F6 trigger)",
            RefineryIntegrationMode.Live => "Refinery lane=live (F6 trigger)",
            _ => LastStatus
        };
    }

    public void Trigger(SimulationRuntime runtime, long tick)
    {
        if (_activeOptions.Mode == RefineryIntegrationMode.Off)
        {
            LastStatus = "Refinery lane=off";
            return;
        }

        if (DateTime.UtcNow < _circuitBreakerUntilUtc)
        {
            LastStatus = $"Refinery circuit open until {_circuitBreakerUntilUtc:HH:mm:ss} UTC";
            return;
        }

        if (_inFlight is { IsCompleted: false })
        {
            LastStatus = "Refinery request already in progress";
            return;
        }

        if (_lastTriggerUtc != DateTime.MinValue)
        {
            var elapsedMs = (DateTime.UtcNow - _lastTriggerUtc).TotalMilliseconds;
            if (elapsedMs < _activeOptions.MinTriggerIntervalMs)
            {
                LastStatus = $"Refinery trigger throttled ({Math.Round(elapsedMs)}ms since last)";
                return;
            }
        }

        _lastTriggerUtc = DateTime.UtcNow;
        var requestOptions = CaptureRequestOptions();
        LastStatus = $"Refinery request started: goal={requestOptions.Options.Goal}, lane={requestOptions.Options.Mode.ToString().ToLowerInvariant()}, tick={tick}, requested={requestOptions.RequestedDirectorOutputMode}({requestOptions.RequestedDirectorOutputModeSource})";

        _inFlight = Task.Run(async () => await RunApplyAsync(runtime, tick, requestOptions));
    }

    public void Pump()
    {
        if (_inFlight == null || !_inFlight.IsCompleted)
        {
            return;
        }

        try
        {
            _inFlight.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LastStatus = "Refinery apply failed: " + ex.Message;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task RunApplyAsync(SimulationRuntime runtime, long tick, RuntimeRequestOptions requestOptions)
    {
        var isDirectorGoal = string.Equals(requestOptions.Options.Goal, DirectorGoals.SeasonDirectorCheckpoint, StringComparison.Ordinal);

        var beforeHash = CanonicalStateSerializer.Sha256(_patchState);
        var stagedPatchState = _patchState.Clone();
        var context = new DirectorExecutionContext(
            Mode: "unknown",
            Source: "unknown",
            Stage: DirectorExecutionStatus.NotTriggered.Stage,
            Tick: tick,
            IsDirectorGoal: isDirectorGoal,
            BudgetUsed: 0d,
            BudgetMarkerPresent: false,
            ExplainMarkers: Array.Empty<string>(),
            WarningCount: 0,
            ResponseReceived: false);

        try
        {
            PatchResponse rawResponse = requestOptions.Options.Mode switch
            {
                RefineryIntegrationMode.Fixture => LoadFixtureResponse(requestOptions.Options),
                RefineryIntegrationMode.Live => await LoadLiveResponseAsync(runtime, tick, requestOptions.Options),
                _ => throw new InvalidOperationException("Integration mode is OFF")
            };

            var selectedOutputMode = SelectOutputMode(rawResponse, requestOptions);
            var response = ApplyOutputMode(rawResponse, selectedOutputMode);
            var stageMarker = ReadStageMarker(response, isDirectorGoal);
            var hasBudgetMarker = TryReadBudgetUsed(response, out var budgetUsed);

            context = context with
            {
                Mode = selectedOutputMode.Mode,
                Source = selectedOutputMode.Source,
                Stage = stageMarker,
                BudgetUsed = hasBudgetMarker ? budgetUsed : 0d,
                BudgetMarkerPresent = hasBudgetMarker,
                ExplainMarkers = response.Explain,
                WarningCount = response.Warnings.Count,
                ResponseReceived = true
            };

            IReadOnlyList<RuntimePatchCommand> commands = Array.Empty<RuntimePatchCommand>();
            if (requestOptions.Options.ApplyToWorld)
            {
                commands = _translator.Translate(response);
                if (isDirectorGoal)
                    _executor.ValidateDirectorBatch(runtime, commands);
            }

            var result = _applier.Apply(stagedPatchState, response, new PatchApplyOptions(requestOptions.Options.StrictMode));

            if (requestOptions.Options.ApplyToWorld)
            {
                _executor.Execute(runtime, commands);
            }

            _patchState.CopyFrom(stagedPatchState);
            if (isDirectorGoal)
            {
                runtime.PrepareDirectorCheckpointBudget(requestOptions.Options.DirectorMaxBudget, tick);
                runtime.RecordDirectorCheckpointBudgetUsed(context.BudgetUsed, tick);
            }

            _consecutiveFailures = 0;
            _circuitBreakerUntilUtc = DateTime.MinValue;

            var afterHash = CanonicalStateSerializer.Sha256(stagedPatchState);
            LastDirectorExecutionStatus = BuildDirectorExecutionStatus(context, applyStatus: "applied");
            runtime.SetDirectorExecutionState(
                effectiveOutputMode: LastDirectorExecutionStatus.EffectiveOutputMode,
                effectiveOutputModeSource: LastDirectorExecutionStatus.EffectiveOutputModeSource,
                stage: LastDirectorExecutionStatus.Stage,
                tick: LastDirectorExecutionStatus.Tick,
                isDirectorGoal: LastDirectorExecutionStatus.IsDirectorGoal,
                applyStatus: LastDirectorExecutionStatus.ApplyStatus);

            var warningHead = response.Warnings.FirstOrDefault();
            var budgetLabel = isDirectorGoal
                ? (context.BudgetMarkerPresent ? context.BudgetUsed.ToString("0.###", CultureInfo.InvariantCulture) : "missing->0")
                : "n/a";
            LastStatus =
                $"Refinery applied: patchApplied={result.AppliedCount}, patchDeduped={result.DedupedCount}, patchNoOp={result.NoOpCount}, runtimeCommands={commands.Count}, " +
                $"techs={_patchState.TechIds.Count}, events={_patchState.EventIds.Count}, " +
                $"hash={beforeHash[..8]}->{afterHash[..8]}, stage={context.Stage}, mode={context.Mode}, source={context.Source}, budget={budgetLabel}" +
                (warningHead is null ? string.Empty : $", warn={warningHead}");
        }
        catch (Exception ex)
        {
            var applyStatus = context.ResponseReceived ? "apply_failed" : "request_failed";
            LastDirectorExecutionStatus = BuildDirectorExecutionStatus(context, applyStatus);

            runtime.SetDirectorExecutionState(
                effectiveOutputMode: LastDirectorExecutionStatus.EffectiveOutputMode,
                effectiveOutputModeSource: LastDirectorExecutionStatus.EffectiveOutputModeSource,
                stage: LastDirectorExecutionStatus.Stage,
                tick: LastDirectorExecutionStatus.Tick,
                isDirectorGoal: LastDirectorExecutionStatus.IsDirectorGoal,
                applyStatus: LastDirectorExecutionStatus.ApplyStatus,
                actionStatus: ex.Message);

            var budgetLabel = isDirectorGoal
                ? (context.BudgetMarkerPresent ? context.BudgetUsed.ToString("0.###", CultureInfo.InvariantCulture) : "n/a")
                : "n/a";
            LastStatus =
                $"Refinery apply failed: outcome={applyStatus}, stage={context.Stage}, mode={context.Mode}, source={context.Source}, budget={budgetLabel}, error={ex.Message}";
        }
    }

    public string CycleDirectorOutputMode()
    {
        if (_inFlight is { IsCompleted: false })
        {
            LastStatus = "Refinery requested mode change blocked: request already in progress";
            return _requestedDirectorOutputMode;
        }

        _requestedDirectorOutputMode = _requestedDirectorOutputMode switch
        {
            "auto" => "both",
            "both" => "story_only",
            "story_only" => "nudge_only",
            "nudge_only" => "off",
            _ => "auto"
        };
        _requestedDirectorOutputModeSource = "operator";
        LastStatus = $"Refinery requested mode updated: requested={_requestedDirectorOutputMode}, source={_requestedDirectorOutputModeSource}, profile={OperatorProfileName}, lane={CurrentIntegrationMode}";

        return _requestedDirectorOutputMode;
    }

    public string CycleOperatorPreset()
    {
        if (_inFlight is { IsCompleted: false })
        {
            LastStatus = "Refinery preset change blocked: request already in progress";
            return OperatorProfileName;
        }

        var nextPreset = RefineryRuntimeOptions.NextOperatorPresetName(OperatorProfileName);
        return ApplyOperatorPreset(nextPreset, "operator");
    }

    public string ApplyOperatorPreset(string presetName, string source = "operator")
    {
        var normalizedPreset = RefineryRuntimeOptions.NormalizeOperatorPresetName(presetName);
        if (normalizedPreset is null)
        {
            LastStatus = $"Refinery preset unchanged: unknown preset '{presetName}'";
            return OperatorProfileName;
        }

        if (_inFlight is { IsCompleted: false })
        {
            LastStatus = "Refinery preset change blocked: request already in progress";
            return OperatorProfileName;
        }

        _activeOptions = RefineryRuntimeOptions.ApplyOperatorPreset(_baselineOptions, normalizedPreset);
        _operatorProfileSource = source;
        _requestedDirectorOutputMode = _activeOptions.DirectorOutputMode;
        _requestedDirectorOutputModeSource = "profile";
        _lastTriggerUtc = DateTime.MinValue;
        _consecutiveFailures = 0;
        _circuitBreakerUntilUtc = DateTime.MinValue;
        EnsureLiveClient();

        LastStatus = $"Refinery preset applied: profile={OperatorProfileName}, lane={CurrentIntegrationMode}, requested={_requestedDirectorOutputMode}, source={_requestedDirectorOutputModeSource}";
        return OperatorProfileName;
    }

    private static DirectorExecutionStatus BuildDirectorExecutionStatus(DirectorExecutionContext context, string applyStatus)
    {
        return new DirectorExecutionStatus(
            EffectiveOutputMode: context.Mode,
            EffectiveOutputModeSource: context.Source,
            Stage: context.Stage,
            Tick: context.Tick,
            IsDirectorGoal: context.IsDirectorGoal,
            ApplyStatus: applyStatus,
            BudgetUsed: context.BudgetUsed,
            BudgetMarkerPresent: context.BudgetMarkerPresent,
            ExplainMarkers: context.ExplainMarkers,
            WarningCount: context.WarningCount
        );
    }

    private DirectorOutputModeSelection SelectOutputMode(PatchResponse response, RuntimeRequestOptions requestOptions)
    {
        if (!string.Equals(requestOptions.Options.Goal, DirectorGoals.SeasonDirectorCheckpoint, StringComparison.Ordinal))
        {
            return new DirectorOutputModeSelection("both", "non_director_goal");
        }

        if (!string.Equals(requestOptions.RequestedDirectorOutputMode, "auto", StringComparison.Ordinal))
        {
            return new DirectorOutputModeSelection(requestOptions.RequestedDirectorOutputMode, requestOptions.RequestedDirectorOutputModeSource);
        }

        var responseMode = TryReadResponseOutputMode(response);
        if (responseMode != null)
        {
            return new DirectorOutputModeSelection(responseMode, "response");
        }

        return new DirectorOutputModeSelection("both", "fallback");
    }

    private static PatchResponse ApplyOutputMode(PatchResponse response, DirectorOutputModeSelection selection)
    {
        if (string.Equals(selection.Mode, "both", StringComparison.Ordinal)
            && string.Equals(selection.Source, "response", StringComparison.Ordinal))
        {
            return response;
        }

        var filteredPatch = response.Patch
            .Where(op => KeepOpForOutputMode(op, selection.Mode))
            .ToList();

        var explain = new List<string>(response.Explain)
        {
            "adapterOutputMode:" + selection.Mode,
            "adapterOutputModeSource:" + selection.Source
        };

        var warnings = new List<string>(response.Warnings);
        if (!string.Equals(selection.Source, "response", StringComparison.Ordinal))
        {
            warnings.Add("Director output mode selected by adapter " + selection.Source + ".");
        }

        return new PatchResponse(
            response.SchemaVersion,
            response.RequestId,
            response.Seed,
            filteredPatch,
            explain,
            warnings
        );
    }

    private static bool KeepOpForOutputMode(PatchOp op, string mode)
    {
        if (mode == "story_only")
            return op is not SetColonyDirectiveOp;
        if (mode == "nudge_only")
            return op is not AddStoryBeatOp;
        if (mode == "off")
            return op is not AddStoryBeatOp && op is not SetColonyDirectiveOp;
        return true;
    }

    private static string? TryReadResponseOutputMode(PatchResponse response)
    {
        foreach (var explain in response.Explain)
        {
            const string prefix = "directorOutputMode:";
            if (!explain.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var mode = explain[prefix.Length..].Trim().ToLowerInvariant();
            if (mode is "both" or "story_only" or "nudge_only" or "off")
                return mode;
        }

        return null;
    }

    private PatchResponse LoadFixtureResponse(RefineryRuntimeOptions options)
    {
        if (!File.Exists(options.FixtureResponsePath))
        {
            throw new FileNotFoundException("Fixture response file not found", options.FixtureResponsePath);
        }

        var json = File.ReadAllText(options.FixtureResponsePath);
        return _parser.Parse(json, new PatchApplyOptions(options.StrictMode));
    }

    private async Task<PatchResponse> LoadLiveResponseAsync(SimulationRuntime runtime, long tick, RefineryRuntimeOptions options)
    {
        if (_serviceClient is null)
        {
            throw new InvalidOperationException("Live mode requires initialized service client.");
        }

        var request = new PatchRequest(
            "v1",
            Guid.NewGuid().ToString(),
            options.RequestSeed,
            tick,
            options.Goal,
            runtime.BuildRefinerySnapshot(),
            BuildRequestConstraints(options)
        );

        Exception? lastError = null;
        var attemptsPerformed = 0;
        var maxAttempts = Math.Max(1, options.LiveRetryCount + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            attemptsPerformed = attempt;
            try
            {
                using var cts = new CancellationTokenSource(options.LiveTimeoutMs);
                return await _serviceClient.GetPatchAsync(request, new PatchApplyOptions(options.StrictMode), cts.Token);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < maxAttempts)
            {
                lastError = ex;
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        _consecutiveFailures++;
        if (_consecutiveFailures >= 2)
        {
            _circuitBreakerUntilUtc = DateTime.UtcNow.AddSeconds(options.CircuitBreakerSeconds);
        }

        throw new InvalidOperationException(DescribeLiveRequestFailure(lastError, attemptsPerformed), lastError);
    }

    private static string DescribeLiveRequestFailure(Exception? exception, int attempts)
    {
        if (exception is null)
            return $"Live refinery request failed: kind=request_error, attempts={attempts}, detail=unknown";

        if (exception is OperationCanceledException)
        {
            return $"Live refinery request failed: kind=timeout, attempts={attempts}, detail={exception.Message}";
        }

        if (exception is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode.HasValue)
            {
                var code = (int)httpEx.StatusCode.Value;
                return $"Live refinery request failed: kind=http_{code}, attempts={attempts}, detail={httpEx.Message}";
            }

            if (ContainsSocketError(httpEx, SocketError.ConnectionRefused))
            {
                return $"Live refinery request failed: kind=connection_refused, attempts={attempts}, detail={httpEx.Message}";
            }

            return $"Live refinery request failed: kind=request_error, attempts={attempts}, detail={httpEx.Message}";
        }

        return $"Live refinery request failed: kind=request_error, attempts={attempts}, detail={exception.Message}";
    }

    private static string ReadStageMarker(PatchResponse response, bool isDirectorGoal)
    {
        if (isDirectorGoal)
        {
            return response.Explain.FirstOrDefault(item =>
                       item.StartsWith("directorStage:", StringComparison.Ordinal))
                   ?? "directorStage:unknown";
        }

        return response.Explain.FirstOrDefault(item =>
                   item.StartsWith("refineryStage:", StringComparison.Ordinal)
                   || item.StartsWith("directorStage:", StringComparison.Ordinal))
               ?? "refineryStage:unknown";
    }

    private static bool ContainsSocketError(Exception exception, SocketError target)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketEx && socketEx.SocketErrorCode == target)
                return true;
        }

        return false;
    }

    private static bool IsRetryable(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return true;
        }

        if (exception is HttpRequestException httpEx)
        {
            if (!httpEx.StatusCode.HasValue)
            {
                return true;
            }

            var code = (int)httpEx.StatusCode.Value;
            return code >= 500 || code == (int)HttpStatusCode.RequestTimeout;
        }

        return false;
    }

    private JsonObject? BuildRequestConstraints(RefineryRuntimeOptions options)
    {
        if (!string.Equals(options.Goal, DirectorGoals.SeasonDirectorCheckpoint, StringComparison.Ordinal))
            return null;

        var constraints = new JsonObject
        {
            ["maxBudget"] = options.DirectorMaxBudget
        };

        if (!string.Equals(options.DirectorOutputMode, "auto", StringComparison.Ordinal))
            constraints["outputMode"] = options.DirectorOutputMode;

        return constraints;
    }

    private RuntimeRequestOptions CaptureRequestOptions()
    {
        return new RuntimeRequestOptions(
            _activeOptions with { DirectorOutputMode = _requestedDirectorOutputMode },
            _requestedDirectorOutputMode,
            _requestedDirectorOutputModeSource);
    }

    private static bool TryReadBudgetUsed(PatchResponse response, out double budgetUsed)
    {
        foreach (var explain in response.Explain)
        {
            const string prefix = "budgetUsed:";
            if (!explain.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var raw = explain[prefix.Length..].Trim();
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                continue;
            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
                continue;

            budgetUsed = Math.Max(0d, parsed);
            return true;
        }

        budgetUsed = 0d;
        return false;
    }

    private void EnsureLiveClient()
    {
        if (_activeOptions.Mode != RefineryIntegrationMode.Live)
            return;

        if (_serviceClient is not null
            && string.Equals(_serviceClientBaseUrl, _activeOptions.ServiceBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _httpClient?.Dispose();
        _httpClient = new HttpClient { BaseAddress = new Uri(_activeOptions.ServiceBaseUrl) };
        _serviceClient = new RefineryServiceClient(_httpClient);
        _serviceClientBaseUrl = _activeOptions.ServiceBaseUrl;
    }

    private sealed record DirectorOutputModeSelection(string Mode, string Source);
    private sealed record RuntimeRequestOptions(
        RefineryRuntimeOptions Options,
        string RequestedDirectorOutputMode,
        string RequestedDirectorOutputModeSource);
    private sealed record DirectorExecutionContext(
        string Mode,
        string Source,
        string Stage,
        long Tick,
        bool IsDirectorGoal,
        double BudgetUsed,
        bool BudgetMarkerPresent,
        IReadOnlyList<string> ExplainMarkers,
        int WarningCount,
        bool ResponseReceived);

}
