using System.Diagnostics;
using System.Text.Json;
using WorldSim.OffDeficitEvidence;
using Xunit;

namespace WorldSim.OffDeficitEvidence.Tests;

public sealed class MeasurementTests
{
    static readonly Raw Initial = new(8, 2, 0, 0, 0, 0, 40, 5, 0, 0, "disabled");

    [Fact]
    public void Entry_IsNotRunUnlessExactTwoCaseSelectorIsPresent()
    {
        Assert.Equal("NOT_RUN", Measurement.ParseEntry([]));
        Assert.Throws<ArgumentException>(() => Measurement.ParseEntry(["--execute", "--cases=101/goap/off", "--tick-cap=888", "--out", "private"]));
        Assert.Throws<ArgumentException>(() => Measurement.ParseEntry(["--execute", "--cases=101/goap/off,202/simple/on", "--tick-cap=888", "--out", "private"]));
        Assert.Equal("private", Measurement.ParseEntry(["--execute", "--cases=" + Measurement.CaseSelection,
            "--tick-cap=888", "--out", "private"]));
        Assert.Equal("private", Measurement.ParseEntry(["--preflight", "--cases=" + Measurement.CaseSelection,
            "--tick-cap=888", "--out", "private"]));
    }

    [Fact]
    public void Sample_DistinguishesBirthLossAndNegativeResidual()
    {
        var zero = Measurement.SampleAt(0, Initial, Initial, null);
        var one = Measurement.SampleAt(1, Initial with { Herbivores = 7, Births = 1, Captures = 2,
            PlantFoodConsumed = 1 }, Initial, zero);
        Assert.Equal(0, zero.Residual);
        Assert.Equal(1, one.Deficit);
        Assert.Equal(0, one.Residual);
        Assert.Throws<InvalidDataException>(() => Measurement.SampleAt(2,
            Initial with { Herbivores = 7, Births = 1, Captures = 3 }, Initial, one));
    }

    [Fact]
    public void FirstZero_ReportsSeparateOnsetsAndIntervals()
    {
        var zero = Measurement.SampleAt(0, Initial, Initial, null);
        var one = Measurement.SampleAt(1, Initial with { Herbivores = 7, Captures = 1 }, Initial, zero);
        var two = Measurement.SampleAt(2, Initial with { Herbivores = 8, Captures = 1, Births = 1 }, Initial, one);
        var three = Measurement.SampleAt(3, Initial with { Herbivores = 0, Captures = 9, Births = 1 }, Initial, two);
        var summary = Measurement.Summarize(new Run("101/goap/off", [zero, one, two, three], "first_zero", 3, 4));
        Assert.Equal(1, summary.FirstOneTickDecline);
        Assert.Equal(1, summary.FirstBelowEight);
        Assert.Equal(3, summary.PersistentToStopOnset);
        Assert.Equal([(1, 1), (3, 3)], summary.Intervals.Select(i => (i.StartTick, i.EndTick)));
    }

    [Fact]
    public void PrivateOutput_IsExclusiveAndVerifiedAgainstExactBytes()
    {
        var zero = Measurement.SampleAt(0, Initial, Initial, null);
        var one = Measurement.SampleAt(1, Initial with { Herbivores = 0, Captures = 8 }, Initial, zero);
        var first = new Run("101/goap/off", [zero, one], "first_zero", 1, 5);
        var second = first with { Key = "202/simple/off" };
        var source = new SourceIdentity("checkout", "tree", "base", "baseTree", "runtime", "ai", "tech", "test");
        var config = new Configuration(64, 40, 24, .25f, "global_planner", true, false, true,
            false, 1f, 1f, "disabled", false, false, 888);
        var payload = new Payload(Measurement.Schema, source, config, [first, second],
            [Measurement.Summarize(first), Measurement.Summarize(second)]);
        var parent = Path.Combine(Path.GetTempPath(), "opencode", "worldsim-off-deficit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var destination = Path.Combine(parent, "output");
        try
        {
            var manifest = SourceAndStore.WriteVerified(destination, payload);
            var bytes = File.ReadAllBytes(Path.Combine(destination, "off-deficit-v1.json"));
            Assert.Equal(bytes.Length, manifest.ByteCount);
            Assert.Equal(Measurement.Hash(bytes), manifest.Sha256);
            Assert.Throws<InvalidDataException>(() => SourceAndStore.WriteVerified(destination, payload));
            SourceAndStore.CheckIdentity(payload, manifest);
            Assert.Throws<InvalidDataException>(() => SourceAndStore.CheckIdentity(
                payload with { Runs = [second, first] }, manifest));
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task WorkerMonitor_AbortsOnReadFaultOrUnexpectedResultWithoutDetachedTaskFault()
    {
        var terminations = 0;
        void Terminate() => terminations++;

        await WorkerAuthorization.MonitorParentAsync(
            () => throw new InvalidOperationException("read initiation failed"), () => false, Terminate);
        Assert.Equal(1, terminations);

        await WorkerAuthorization.MonitorParentAsync(
            () => Task.FromException<string?>(new IOException("asynchronous read failed")), () => false, Terminate);
        Assert.Equal(2, terminations);

        await WorkerAuthorization.MonitorParentAsync(
            () => Task.FromResult<string?>(null), () => false, Terminate);
        Assert.Equal(3, terminations);

        await WorkerAuthorization.MonitorParentAsync(
            () => Task.FromResult<string?>("unexpected message"), () => false, Terminate);
        Assert.Equal(4, terminations);

        await WorkerAuthorization.MonitorParentAsync(
            () => Task.FromException<string?>(new ObjectDisposedException("completed session")),
            () => true, Terminate);
        await WorkerAuthorization.MonitorParentAsync(
            () => Task.FromResult<string?>(null), () => true, Terminate);
        Assert.Equal(4, terminations);
    }

    static Payload SyntheticPayload()
    {
        var zero = Measurement.SampleAt(0, Initial, Initial, null);
        var one = Measurement.SampleAt(1, Initial with { Herbivores = 0, Captures = 8 }, Initial, zero);
        var first = new Run("101/goap/off", [zero, one], "first_zero", 1, 5);
        var second = first with { Key = "202/simple/off" };
        return new Payload(Measurement.Schema,
            new SourceIdentity("checkout", "tree", "base", "baseTree", "runtime", "ai", "tech", "test"),
            new Configuration(64, 40, 24, .25f, "global_planner", true, false, true,
                false, 1f, 1f, "disabled", false, false, 888),
            [first, second], [Measurement.Summarize(first), Measurement.Summarize(second)]);
    }

    [Fact]
    public void SummaryReadback_RejectsWrongScalarsAndIntervalValues()
    {
        var payload = SyntheticPayload();
        var manifest = new Manifest(payload.SchemaVersion, payload.Source, payload.Configuration,
            ["101/goap/off", "202/simple/off"], [2, 2], [1, 1], ["first_zero", "first_zero"], 1, "synthetic");
        SourceAndStore.CheckIdentity(payload, manifest);
        foreach (var bad in new[]
        {
            payload.Summaries[0] with { FirstBelowEight = 0 },
            payload.Summaries[0] with { RightCensored = true },
            payload.Summaries[0] with { Intervals = [payload.Summaries[0].Intervals[0] with { Captures = 7 }] }
        })
        {
            Assert.Throws<InvalidDataException>(() => SourceAndStore.CheckIdentity(
                payload with { Summaries = [bad, payload.Summaries[1]] }, manifest));
        }
    }

    [Fact]
    public void PublishedArtifact_ReadOnly_StillMatchesExactBytesAndRecomputedSummaries()
    {
        var root = SourceAndStore.FindRoot();
        var path = Path.Combine(root, Measurement.PrivateDirectory);
        var rawPath = Path.Combine(path, "off-deficit-v1.json");
        var manifestPath = Path.Combine(path, "manifest.json");
        var rawTime = File.GetLastWriteTimeUtc(rawPath);
        var manifestTime = File.GetLastWriteTimeUtc(manifestPath);
        using var raw = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var manifestFile = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var memory = new MemoryStream();
        raw.CopyTo(memory);
        var bytes = memory.ToArray();
        Assert.Equal(367457, bytes.Length);
        Assert.Equal("3509a9c254768e3dba824f617fc635d7a69de84f39f9b5b83d237127fc054109",
            Measurement.Hash(bytes));
        var payload = JsonSerializer.Deserialize<Payload>(bytes, Measurement.Json)!;
        var manifest = JsonSerializer.Deserialize<Manifest>(manifestFile, Measurement.Json)!;
        Assert.Equal(bytes.Length, manifest.ByteCount);
        Assert.Equal(Measurement.Hash(bytes), manifest.Sha256);
        SourceAndStore.CheckIdentity(payload, manifest);
        Assert.Equal(rawTime, File.GetLastWriteTimeUtc(rawPath));
        Assert.Equal(manifestTime, File.GetLastWriteTimeUtc(manifestPath));
    }

    [Fact]
    public void Attempts_ReserveBeforeLaunchAndNeverReuseInterruptedOrFailedCase()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opencode", "e11off-attempt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var payload = SyntheticPayload();
            var destination = Path.Combine(folder, "new-measurement");
            var attempt = SourceAndStore.ReserveAttempt(destination, payload.Source, payload.Configuration);
            Assert.True(File.Exists(destination + ".attempt"));
            Assert.False(Directory.Exists(destination));
            attempt.AboutToLaunch(0, "101/goap/off");
            Assert.True(File.Exists(destination + ".case-0-about-to-launch"));
            Assert.Throws<InvalidDataException>(() => attempt.AboutToLaunch(1, "202/simple/off"));
            attempt.Fail(0, new TimeoutException());
            Assert.True(File.Exists(destination + ".case-0-failed"));
            Assert.Throws<IOException>(() => SourceAndStore.ReserveAttempt(destination, payload.Source, payload.Configuration));
            Assert.Throws<IOException>(() => attempt.AboutToLaunch(0, "101/goap/off"));

            var secondDestination = Path.Combine(folder, "interrupted");
            using (new FileStream(secondDestination + ".attempt", FileMode.CreateNew, FileAccess.Write)) { }
            Assert.Throws<IOException>(() => SourceAndStore.ReserveAttempt(secondDestination, payload.Source, payload.Configuration));

            var laterDestination = Path.Combine(folder, "second-fails");
            var later = SourceAndStore.ReserveAttempt(laterDestination, payload.Source, payload.Configuration);
            later.AboutToLaunch(0, "101/goap/off");
            later.Complete(0, payload.Runs[0]);
            later.AboutToLaunch(1, "202/simple/off");
            later.Fail(1, new IOException());
            Assert.Throws<IOException>(() => SourceAndStore.ReserveAttempt(laterDestination, payload.Source, payload.Configuration));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task Attempts_ConcurrentClaimAndUnsafeParentsRefuseWrites()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opencode", "e11off-claim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var payload = SyntheticPayload();
            var destination = Path.Combine(folder, "contested");
            var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                try { SourceAndStore.ReserveAttempt(destination, payload.Source, payload.Configuration); return true; }
                catch (IOException) { return false; }
            })));
            Assert.Equal(1, attempts.Count(success => success));
            Assert.False(Directory.Exists(destination));

            var missing = Path.Combine(folder, "missing", "output");
            Assert.Throws<InvalidDataException>(() => SourceAndStore.ReserveAttempt(missing, payload.Source, payload.Configuration));
            var fileParent = Path.Combine(folder, "file-parent");
            File.WriteAllText(fileParent, "not a directory");
            Assert.Throws<InvalidDataException>(() => SourceAndStore.ReserveAttempt(
                Path.Combine(fileParent, "output"), payload.Source, payload.Configuration));
            Directory.CreateDirectory(Path.Combine(folder, "published"));
            Assert.Throws<InvalidDataException>(() => SourceAndStore.ReserveAttempt(
                Path.Combine(folder, "published"), payload.Source, payload.Configuration));
            Assert.False(File.Exists(Path.Combine(folder, "published.attempt")));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task AttemptCoordinator_StopsAfterFirstFailureAndDoesNotRetrySpentClaim()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opencode", "e11off-sequence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var payload = SyntheticPayload();
            var destination = Path.Combine(folder, "attempt");
            var calls = 0;
            Task<Run> Failed(string key, string attempt, TimeSpan time)
            {
                calls++;
                Assert.Equal("101/goap/off", key);
                Assert.NotEmpty(attempt);
                Assert.True(time > TimeSpan.Zero && time <= TimeSpan.FromSeconds(120));
                Assert.True(File.Exists(destination + ".attempt"));
                Assert.True(File.Exists(destination + ".case-0-about-to-launch"));
                return Task.FromException<Run>(new TimeoutException("synthetic worker timeout"));
            }
            await Assert.ThrowsAsync<TimeoutException>(() => WorldSim.OffDeficitEvidence.Program.CollectAuthorizedAsync(
                destination, payload.Source, payload.Configuration, Failed));
            Assert.Equal(1, calls);
            Assert.True(File.Exists(destination + ".case-0-failed"));
            Assert.False(File.Exists(destination + ".case-1-about-to-launch"));
            await Assert.ThrowsAsync<IOException>(() => WorldSim.OffDeficitEvidence.Program.CollectAuthorizedAsync(
                destination, payload.Source, payload.Configuration, Failed));
            Assert.Equal(1, calls);

            var secondDestination = Path.Combine(folder, "second-fails");
            var secondCalls = 0;
            await Assert.ThrowsAsync<InvalidDataException>(() => WorldSim.OffDeficitEvidence.Program.CollectAuthorizedAsync(
                secondDestination, payload.Source, payload.Configuration, (key, _, _) =>
                {
                    secondCalls++;
                    return key == "101/goap/off" ? Task.FromResult(payload.Runs[0])
                        : Task.FromException<Run>(new InvalidDataException("synthetic second case failed"));
                }));
            Assert.Equal(2, secondCalls);
            Assert.True(File.Exists(secondDestination + ".case-0-complete"));
            Assert.True(File.Exists(secondDestination + ".case-1-failed"));
            await Assert.ThrowsAsync<IOException>(() => WorldSim.OffDeficitEvidence.Program.CollectAuthorizedAsync(
                secondDestination, payload.Source, payload.Configuration,
                (_, _, _) => Task.FromResult(payload.Runs[0])));
            Assert.False(Directory.Exists(secondDestination));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void Attempt_RefusesRedirectedWindowsParent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "opencode", "e11off-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "real");
        var junction = Path.Combine(folder, "redirected");
        Directory.CreateDirectory(target);
        try
        {
            var start = new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{junction}\" \"{target}\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using var mklink = Process.Start(start)!;
            Assert.True(mklink.WaitForExit(10_000));
            Assert.Equal(0, mklink.ExitCode);
            var payload = SyntheticPayload();
            Assert.Throws<InvalidDataException>(() => SourceAndStore.ReserveAttempt(
                Path.Combine(junction, "output"), payload.Source, payload.Configuration));
            Assert.False(File.Exists(Path.Combine(target, "output.attempt")));
        }
        finally
        {
            if (Directory.Exists(junction)) Directory.Delete(junction);
            Directory.Delete(folder, recursive: true);
        }
    }

    static ProcessStartInfo Child(string key, string mode)
    {
        var dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..",
            "SyntheticChild", "bin", "Release", "net8.0", "WorldSim.OffDeficitEvidence.SyntheticChild.dll"));
        Assert.True(File.Exists(dll), $"Missing synthetic child: {dll}");
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetDirectoryName(dll)! };
        start.ArgumentList.Add(dll);
        start.ArgumentList.Add(key);
        start.ArgumentList.Add(mode);
        return start;
    }

    [Fact]
    public async Task Worker_RequiresLiveBoundParentBeforeAnySimulationEntry()
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = SourceAndStore.FindRoot(),
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(typeof(WorldSim.OffDeficitEvidence.Program).Assembly.Location);
        start.ArgumentList.Add("--worker=101/goap/off");
        using var direct = Process.Start(start)!;
        await direct.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(1, direct.ExitCode);
        Assert.Contains("Live parent authorization is required", await direct.StandardError.ReadToEndAsync());

        var selected = Child("101/goap/off", "success");
        var allowed = await WorkerAuthorization.RunSupervisedAsync(selected,
            "synthetic-one", "101/goap/off", TimeSpan.FromSeconds(15));
        Assert.Equal(0, allowed.ExitCode);
        Assert.Contains("SYNTHETIC_OK", allowed.Output);
        var replay = Child("101/goap/off", "success");
        replay.Environment[WorkerAuthorization.PipeEnvironment] = selected.Environment[WorkerAuthorization.PipeEnvironment];
        replay.Environment[WorkerAuthorization.AttemptEnvironment] = selected.Environment[WorkerAuthorization.AttemptEnvironment];
        replay.RedirectStandardOutput = true;
        replay.RedirectStandardError = true;
        using (var stale = Process.Start(replay)!)
        {
            await stale.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual(0, stale.ExitCode);
            Assert.DoesNotContain("SYNTHETIC_OK", await stale.StandardOutput.ReadToEndAsync());
        }
        await Assert.ThrowsAsync<InvalidDataException>(async () => await WorkerAuthorization.RunSupervisedAsync(
            Child("202/simple/off", "success"), "synthetic-wrong-key", "101/goap/off", TimeSpan.FromSeconds(15)));
    }

    [Fact]
    public async Task Worker_ForgeryDisconnectionAndTimeoutFailClosed()
    {
        var wrongAttempt = await Record.ExceptionAsync(() => WorkerAuthorization.RunSupervisedAsync(
            Child("101/goap/off", "wrong-attempt"), "synthetic-actual-attempt", "101/goap/off",
            TimeSpan.FromSeconds(15)));
        Assert.True(wrongAttempt is InvalidDataException or IOException,
            $"Wrong-attempt handshake did not fail closed: {wrongAttempt}");
        var wrongParent = await Record.ExceptionAsync(() => WorkerAuthorization.RunSupervisedAsync(
            Child("101/goap/off", "wrong-parent"), "synthetic-wrong-parent", "101/goap/off",
            TimeSpan.FromSeconds(15)));
        Assert.True(wrongParent is InvalidDataException or IOException,
            $"Wrong-parent handshake did not fail closed: {wrongParent}");
        await Assert.ThrowsAsync<InvalidDataException>(() => WorkerAuthorization.RunSupervisedAsync(
            Child("101/goap/off", "success"), "synthetic-wrong-client", "101/goap/off",
            TimeSpan.FromSeconds(15), expectedClientPidForTest: int.MaxValue));
        var disconnected = await WorkerAuthorization.RunSupervisedAsync(Child("101/goap/off", "wait"),
            "synthetic-disconnect", "101/goap/off", TimeSpan.FromSeconds(15), disconnectForTest: true);
        Assert.NotEqual(0, disconnected.ExitCode);
        await Assert.ThrowsAsync<TimeoutException>(() => WorkerAuthorization.RunSupervisedAsync(
            Child("101/goap/off", "wait"), "synthetic-timeout", "101/goap/off",
            TimeSpan.FromMilliseconds(900)));
        await Assert.ThrowsAsync<TimeoutException>(() => WorkerAuthorization.RunSupervisedAsync(
            Child("101/goap/off", "no-connect"), "synthetic-no-handshake", "101/goap/off",
            TimeSpan.FromMilliseconds(900)));
    }
}
