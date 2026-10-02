using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorldSim.SmrLab.ManualRuns;

public sealed record ManualRunRequest(Guid IntentId, string Profile, string Preset, int Seed, string Planner, int Ticks, string Mode);

public sealed record ManualRunInvocation(string Workspace, string RunnerDll, string JobId,
    IReadOnlyDictionary<string, string> Environment);

public sealed record ManualProcessResult(string Status, int? ExitCode, string Diagnostic);

public interface IManualRunner
{
    Task<ManualProcessResult> RunAsync(ManualRunInvocation invocation,
        Action<int, DateTimeOffset> started, CancellationToken cancellationToken);
}

public static class ManualRunContract
{
    public const int TimeoutSeconds = 120;
    public static readonly string[] Presets = ["lab_small", "lab_default"];
    public static readonly string[] Planners = ["Simple", "Goap", "Htn"];
    public static readonly string[] Modes = ["standard", "assert"];

    public static string? Validate(ManualRunRequest? request)
    {
        if (request is null || request.IntentId == Guid.Empty || request.Profile != "core" ||
            !Presets.Contains(request.Preset, StringComparer.Ordinal) ||
            !Planners.Contains(request.Planner, StringComparer.Ordinal) ||
            !Modes.Contains(request.Mode, StringComparer.Ordinal) ||
            request.Seed is < 1 or > 1_000_000 || request.Ticks is < 10 or > 120)
            return "Select one supported core preset, seed, planner, mode and 10..120 ticks with a nonempty intent ID.";
        return null;
    }

    public static Dictionary<string, string> EnvironmentFor(ManualRunRequest request, string output)
    {
        if (Validate(request) is { } problem) throw new ArgumentException(problem, nameof(request));
        var small = request.Preset == "lab_small";
        // Runner uses default JsonSerializer options on input: Pascal-case constructor names,
        // not the camel-case naming policy it uses for artifact output.
        var config = new[] { new
        {
            Name = request.Preset, Width = small ? 32 : 64, Height = small ? 20 : 40,
            InitialPop = small ? 12 : 24, Ticks = request.Ticks, Dt = 0.25f,
            EnableCombatPrimitives = false, EnableDiplomacy = false,
            StoneBuildingsEnabled = false, BirthRateMultiplier = 1f,
            MovementSpeedMultiplier = 1f, EnableSiege = true,
            EnablePredatorHumanAttacks = false
        } };
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WORLDSIM_SCENARIO_LANE"] = "core",
            ["WORLDSIM_SCENARIO_SEEDS"] = request.Seed.ToString(CultureInfo.InvariantCulture),
            ["WORLDSIM_SCENARIO_PLANNERS"] = request.Planner,
            ["WORLDSIM_SCENARIO_CONFIGS_JSON"] = JsonSerializer.Serialize(config),
            ["WORLDSIM_SCENARIO_MODE"] = request.Mode,
            ["WORLDSIM_SCENARIO_ASSERT"] = request.Mode == "assert" ? "true" : "false",
            ["WORLDSIM_SCENARIO_COMPARE"] = "false",
            ["WORLDSIM_SCENARIO_PERF"] = "false",
            ["WORLDSIM_SCENARIO_ANOMALY_FAIL"] = "false",
            ["WORLDSIM_SCENARIO_DELTA_FAIL"] = "false",
            ["WORLDSIM_SCENARIO_PERF_FAIL"] = "false",
            ["WORLDSIM_SCENARIO_OUTPUT"] = "json",
            ["WORLDSIM_VISUAL_PROFILE"] = "Headless",
            ["WORLDSIM_SCENARIO_DRILLDOWN"] = "true",
            ["WORLDSIM_SCENARIO_DRILLDOWN_TOP"] = "1",
            ["WORLDSIM_SCENARIO_SAMPLE_EVERY"] = "10",
            ["WORLDSIM_SCENARIO_ARTIFACT_DIR"] = output
        };
    }
}

public sealed class DotnetManualRunner : IManualRunner
{
    private const int MaxDiagnosticChars = 4096;

    public async Task<ManualProcessResult> RunAsync(ManualRunInvocation invocation,
        Action<int, DateTimeOffset> started, CancellationToken cancellationToken)
    {
        // Resolve the host once; never use the browser, shell or an inherited WorldSim variable.
        var host = Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root
            ? Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")
            : Environment.ProcessPath is { } path && Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? path : null;
        if (host is null || !Path.IsPathFullyQualified(host) || !File.Exists(host))
            return new ManualProcessResult("LAUNCH_ERROR", null, "Unable to resolve an installed dotnet host; no process started.");

        var info = new ProcessStartInfo(host)
        {
            WorkingDirectory = invocation.Workspace,
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(invocation.RunnerDll);
        info.Environment.Clear();
        // Windows needs SystemRoot to load system DLLs. DOTNET_ROOT and PATH are not needed
        // when invoking the resolved absolute host and DLL, but may locate native .NET dependencies.
        foreach (var key in new[] { "SystemRoot", "WINDIR", "DOTNET_ROOT", "DOTNET_ROOT_X64", "PATH", "HOME", "USERPROFILE", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(key) is { Length: > 0 } value) info.Environment[key] = value;
        foreach (var (key, value) in invocation.Environment) info.Environment[key] = value;

        using var process = new Process { StartInfo = info };
        var hasStarted = false;
        try
        {
            if (!process.Start()) return new ManualProcessResult("LAUNCH_ERROR", null, "dotnet did not start.");
            hasStarted = true;
            // A failure persisting the PID cannot leave a known owned child running.
            try { started(process.Id, new DateTimeOffset(process.StartTime.ToUniversalTime())); }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            var stdout = Drain(process.StandardOutput);
            var stderr = Drain(process.StandardError);
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                await Task.WhenAll(stdout, stderr);
                return new ManualProcessResult("TIMEOUT", process.ExitCode, "Owned child exceeded the 120-second limit.");
            }
            await Task.WhenAll(stdout, stderr);
            return new ManualProcessResult("EXITED", process.ExitCode, $"stdout: {stdout.Result}; stderr: {stderr.Result}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new ManualProcessResult(hasStarted ? "UNKNOWN" : "LAUNCH_ERROR", null,
                $"Process could not be safely completed: {ex.GetType().Name}");
        }
    }

    private static async Task<string> Drain(StreamReader reader)
    {
        var retained = new System.Text.StringBuilder();
        var buffer = new char[1024];
        int length;
        while ((length = await reader.ReadAsync(buffer)) != 0)
        {
            var remaining = MaxDiagnosticChars - retained.Length;
            if (remaining > 0) retained.Append(buffer, 0, Math.Min(length, remaining));
        }
        return retained.ToString();
    }
}
