using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
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
    private readonly RefineryRuntimeOptions _options;
    private readonly PatchResponseParser _parser = new();
    private readonly PatchApplier _applier = new();
    private readonly PatchCommandTranslator _translator = new();
    private readonly RuntimePatchCommandExecutor _executor = new();
    private readonly SimulationPatchState _patchState = SimulationPatchState.CreateBaseline();
    private readonly RefineryServiceClient? _serviceClient;
    private readonly HttpClient? _httpClient;

    private Task? _inFlight;
    private DateTime _circuitBreakerUntilUtc = DateTime.MinValue;
    private int _consecutiveFailures;
    private DateTime _lastTriggerUtc = DateTime.MinValue;

    public string LastStatus { get; private set; } = "Refinery integration: not triggered";
    public DirectorExecutionStatus LastDirectorExecutionStatus { get; private set; } = DirectorExecutionStatus.NotTriggered;

    public RefineryPatchRuntime(RefineryRuntimeOptions options)
    {
        _options = options;

        if (_options.Mode == RefineryIntegrationMode.Live)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(_options.ServiceBaseUrl) };
            _serviceClient = new RefineryServiceClient(_httpClient);
        }

        LastStatus = _options.Mode switch
        {
            RefineryIntegrationMode.Off => "Refinery integration OFF",
            RefineryIntegrationMode.Fixture => "Refinery integration FIXTURE (F6 trigger)",
            RefineryIntegrationMode.Live => "Refinery integration LIVE (F6 trigger)",
            _ => LastStatus
        };
    }

    public void Trigger(SimulationRuntime runtime, long tick)
    {
        if (_options.Mode == RefineryIntegrationMode.Off)
        {
            LastStatus = "Refinery integration OFF";
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
            if (elapsedMs < _options.MinTriggerIntervalMs)
            {
                LastStatus = $"Refinery trigger throttled ({Math.Round(elapsedMs)}ms since last)";
                return;
            }
        }

        _lastTriggerUtc = DateTime.UtcNow;

        _inFlight = Task.Run(async () => await RunApplyAsync(runtime, tick));
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

    private async Task RunApplyAsync(SimulationRuntime runtime, long tick)
    {
        var beforeHash = CanonicalStateSerializer.Sha256(_patchState);

        PatchResponse rawResponse = _options.Mode switch
        {
            RefineryIntegrationMode.Fixture => LoadFixtureResponse(),
            RefineryIntegrationMode.Live => await LoadLiveResponseAsync(runtime, tick),
            _ => throw new InvalidOperationException("Integration mode is OFF")
        };

        var selectedOutputMode = SelectOutputMode(rawResponse);
        var response = ApplyOutputMode(rawResponse, selectedOutputMode);

        var result = _applier.Apply(_patchState, response, new PatchApplyOptions(_options.StrictMode));

        if (_options.ApplyToWorld)
        {
            var commands = _translator.Translate(response);
            _executor.Execute(runtime, commands);
        }

        _consecutiveFailures = 0;
        _circuitBreakerUntilUtc = DateTime.MinValue;

        var afterHash = CanonicalStateSerializer.Sha256(_patchState);
        var stageMarker = response.Explain.FirstOrDefault(item =>
                               item.StartsWith("refineryStage:", StringComparison.Ordinal)
                               || item.StartsWith("directorStage:", StringComparison.Ordinal))
                          ?? "refineryStage:unknown";

        LastDirectorExecutionStatus = new DirectorExecutionStatus(
            EffectiveOutputMode: selectedOutputMode.Mode,
            EffectiveOutputModeSource: selectedOutputMode.Source,
            Stage: stageMarker,
            Tick: tick,
            IsDirectorGoal: string.Equals(_options.Goal, DirectorGoals.SeasonDirectorCheckpoint, StringComparison.Ordinal)
        );

        runtime.SetDirectorExecutionState(
            effectiveOutputMode: LastDirectorExecutionStatus.EffectiveOutputMode,
            effectiveOutputModeSource: LastDirectorExecutionStatus.EffectiveOutputModeSource,
            stage: LastDirectorExecutionStatus.Stage,
            tick: LastDirectorExecutionStatus.Tick,
            isDirectorGoal: LastDirectorExecutionStatus.IsDirectorGoal);

        var warningHead = response.Warnings.FirstOrDefault();
        LastStatus =
            $"Refinery applied: applied={result.AppliedCount}, deduped={result.DedupedCount}, noop={result.NoOpCount}, " +
            $"techs={_patchState.TechIds.Count}, events={_patchState.EventIds.Count}, " +
            $"hash={beforeHash[..8]}->{afterHash[..8]}, stage={stageMarker}, mode={selectedOutputMode.Mode}, source={selectedOutputMode.Source}" +
            (warningHead is null ? string.Empty : $", warn={warningHead}");
    }

    private DirectorOutputModeSelection SelectOutputMode(PatchResponse response)
    {
        if (!string.Equals(_options.Goal, DirectorGoals.SeasonDirectorCheckpoint, StringComparison.Ordinal))
        {
            return new DirectorOutputModeSelection("both", "non_director_goal");
        }

        if (!string.Equals(_options.DirectorOutputMode, "auto", StringComparison.Ordinal))
        {
            return new DirectorOutputModeSelection(_options.DirectorOutputMode, "env");
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

    private PatchResponse LoadFixtureResponse()
    {
        if (!File.Exists(_options.FixtureResponsePath))
        {
            throw new FileNotFoundException("Fixture response file not found", _options.FixtureResponsePath);
        }

        var json = File.ReadAllText(_options.FixtureResponsePath);
        return _parser.Parse(json, new PatchApplyOptions(_options.StrictMode));
    }

    private async Task<PatchResponse> LoadLiveResponseAsync(SimulationRuntime runtime, long tick)
    {
        if (_serviceClient is null)
        {
            throw new InvalidOperationException("Live mode requires initialized service client.");
        }

        var request = new PatchRequest(
            "v1",
            Guid.NewGuid().ToString(),
            _options.RequestSeed,
            tick,
            _options.Goal,
            runtime.BuildRefinerySnapshot(),
            null
        );

        Exception? lastError = null;
        var maxAttempts = Math.Max(1, _options.LiveRetryCount + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(_options.LiveTimeoutMs);
                return await _serviceClient.GetPatchAsync(request, new PatchApplyOptions(_options.StrictMode), cts.Token);
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
            _circuitBreakerUntilUtc = DateTime.UtcNow.AddSeconds(_options.CircuitBreakerSeconds);
        }

        throw new InvalidOperationException("Live refinery request failed", lastError);
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

    private sealed record DirectorOutputModeSelection(string Mode, string Source);

}
