using System.Diagnostics;
using System.Text;

namespace WorldSim.ScenarioRunner.Tests;

internal sealed record ScenarioRunnerProcessResult(int ExitCode, string Stdout, string Stderr);

internal static class ScenarioRunnerProcess
{
    public const string TimeoutMinutesEnvVar = "WORLDSIM_SCENARIO_TEST_TIMEOUT_MINUTES";
    private const int DefaultTimeoutMinutes = 30;

    public static ScenarioRunnerProcessResult Run(ProcessStartInfo startInfo, string? artifactDir = null)
    {
        NormalizeNestedDotnetRun(startInfo);
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ScenarioRunner process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var timeout = ResolveTimeout();

        if (!process.WaitForExit(ToWaitMilliseconds(timeout)))
        {
            KillProcessTree(process);
            var stdout = ReadCompletedOrPlaceholder(stdoutTask);
            var stderr = ReadCompletedOrPlaceholder(stderrTask);
            throw new TimeoutException(BuildTimeoutMessage(startInfo, timeout, artifactDir, stdout, stderr));
        }

        var completedStdout = stdoutTask.GetAwaiter().GetResult();
        var completedStderr = stderrTask.GetAwaiter().GetResult();
        return new ScenarioRunnerProcessResult(process.ExitCode, completedStdout, completedStderr);
    }

    private static void NormalizeNestedDotnetRun(ProcessStartInfo startInfo)
    {
        if (!string.Equals(Path.GetFileNameWithoutExtension(startInfo.FileName), "dotnet", StringComparison.OrdinalIgnoreCase))
            return;

        if (!startInfo.Arguments.StartsWith("run ", StringComparison.OrdinalIgnoreCase)
            || startInfo.Arguments.Contains("--no-build", StringComparison.OrdinalIgnoreCase))
            return;

        // The test project already builds the ScenarioRunner project reference. Avoid nested MSBuild
        // node reuse during artifact-smoke tests; direct/manual SMR CLI runs are unaffected.
        startInfo.Arguments = "run --no-build " + startInfo.Arguments[4..];
    }

    private static TimeSpan ResolveTimeout()
    {
        var raw = Environment.GetEnvironmentVariable(TimeoutMinutesEnvVar);
        if (!int.TryParse(raw, out var minutes) || minutes <= 0)
            minutes = DefaultTimeoutMinutes;

        return TimeSpan.FromMinutes(minutes);
    }

    private static int ToWaitMilliseconds(TimeSpan timeout)
    {
        if (timeout.TotalMilliseconds >= int.MaxValue)
            return int.MaxValue;

        return Math.Max(1, (int)timeout.TotalMilliseconds);
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(milliseconds: 10_000);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between timeout detection and cleanup.
        }
    }

    private static string ReadCompletedOrPlaceholder(Task<string> outputTask)
    {
        try
        {
            if (outputTask.Wait(millisecondsTimeout: 1_000))
                return outputTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Keep timeout failure reporting best-effort; the process cleanup is the important part.
        }

        return "<output unavailable after timeout cleanup>";
    }

    private static string BuildTimeoutMessage(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        string? artifactDir,
        string stdout,
        string stderr)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"ScenarioRunner test process timed out after {timeout.TotalMinutes:0.##} minutes.");
        builder.AppendLine($"Increase {TimeoutMinutesEnvVar} for intentionally long xUnit-wrapper ScenarioRunner runs.");
        builder.AppendLine("This timeout is test-only; direct ScenarioRunner/SMR CLI runs are not limited by it.");
        builder.AppendLine($"Command: {startInfo.FileName} {startInfo.Arguments}");
        builder.AppendLine($"WorkingDirectory: {startInfo.WorkingDirectory}");
        if (!string.IsNullOrWhiteSpace(artifactDir))
            builder.AppendLine($"ArtifactDir: {artifactDir}");
        builder.AppendLine("STDOUT:");
        builder.AppendLine(stdout);
        builder.AppendLine("STDERR:");
        builder.AppendLine(stderr);
        return builder.ToString();
    }
}
