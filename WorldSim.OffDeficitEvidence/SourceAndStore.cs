using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WorldSim.OffDeficitEvidence;

public static class SourceAndStore
{
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateDirectoryExclusive(string path, IntPtr securityAttributes);

    const string Base = "db8d18a799f12fcf39dd593e7a42f6811663c21e";
    const string BaseTree = "fc62cdd7885f6f6c3997486ba27213aee5771c03";
    const string Diagnostic = "04b645b685286b8de41a92ab65eac3b25cec1236";
    const string RuntimeTree = "54e2788ba61f2594c81d1314508916fe0c91b2af";
    const string AiTree = "08a3d9cd219574f1bc3925a8467f94b4d6274060";
    const string TechBlob = "237c90380d803259cad0997302f489c8107cfa1f";
    const string DiagnosticTestBlob = "dcc163272d77c950cca860b21c20b8022055caec";

    public static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Tech", "technologies.json"))
                && (Directory.Exists(Path.Combine(current.FullName, ".git"))
                    || File.Exists(Path.Combine(current.FullName, ".git"))))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidDataException("Repository root not found.");
    }

    public static SourceIdentity Verify(string root)
    {
        string Git(params string[] args)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = root,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidDataException("Cannot start Git identity check.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(10_000) || process.ExitCode != 0)
                throw new InvalidDataException("Git identity check failed: " + stderr);
            return stdout.Trim();
        }
        void Require(string actual, string expected, string name)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException($"{name} drift: expected {expected}, got {actual}.");
        }
        var checkoutCommit = Git("rev-parse", "HEAD");
        var checkoutTree = Git("show", "-s", "--format=%T", "HEAD");
        Require(Git("show", "-s", "--format=%T", Base), BaseTree, "accepted base tree");
        Require(Git("rev-parse", $"{Base}:WorldSim.Runtime"), RuntimeTree, "base Runtime tree");
        Require(Git("rev-parse", $"{Diagnostic}:WorldSim.Runtime"), RuntimeTree, "diagnostic Runtime tree");
        Require(Git("rev-parse", "HEAD:WorldSim.Runtime"), RuntimeTree, "checkout Runtime tree");
        Require(Git("rev-parse", $"{Base}:WorldSim.AI"), AiTree, "base AI tree");
        Require(Git("rev-parse", $"{Diagnostic}:WorldSim.AI"), AiTree, "diagnostic AI tree");
        Require(Git("rev-parse", "HEAD:WorldSim.AI"), AiTree, "checkout AI tree");
        Require(Git("rev-parse", $"{Diagnostic}:Tech/technologies.json"), TechBlob, "diagnostic Tech blob");
        Require(Git("rev-parse", "HEAD:Tech/technologies.json"), TechBlob, "checkout Tech blob");
        Require(Git("hash-object", "Tech/technologies.json"), TechBlob, "working Tech blob");
        var testPath = "WorldSim.Runtime.Tests/Wave11SmrRecruitmentMortalityEvidenceTests.cs";
        Require(Git("rev-parse", $"{Diagnostic}:{testPath}"), DiagnosticTestBlob, "diagnostic test blob");
        Require(Git("rev-parse", $"HEAD:{testPath}"), DiagnosticTestBlob, "checkout diagnostic test blob");
        Require(Git("hash-object", testPath), DiagnosticTestBlob, "working diagnostic test blob");
        var relevantStatus = Git("status", "--porcelain", "--untracked-files=all", "--",
            "WorldSim.Runtime", "WorldSim.AI", "Tech/technologies.json", testPath);
        Require(relevantStatus, "", "relevant working files");
        return new SourceIdentity(checkoutCommit, checkoutTree, Base, BaseTree,
            RuntimeTree, AiTree, TechBlob, DiagnosticTestBlob);
    }

    public static string ExactDestination(string root, string requested)
    {
        var expected = Path.GetFullPath(Path.Combine(root, Measurement.PrivateDirectory));
        var actual = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(root, requested));
        if (!string.Equals(actual, expected, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Output is not the approved private directory.");
        EnsureUnusedDestination(expected);
        return expected;
    }

    public static void EnsureUnusedDestination(string destination)
    {
        // Never traverse or modify an already published measurement, including a link to one.
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidDataException("Exclusive destination already exists.");
        EnsureSafeParents(destination);
        if (new FileInfo(destination).LinkTarget is not null || new DirectoryInfo(destination).LinkTarget is not null)
            throw new InvalidDataException("Exclusive destination is a redirected link.");
        try
        {
            _ = File.GetAttributes(destination);
            throw new InvalidDataException("Exclusive destination already exists.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    static void EnsureSafeParents(string path)
    {
        for (var parent = Directory.GetParent(Path.GetFullPath(path)); parent != null; parent = parent.Parent)
        {
            if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Private artifact parent missing or redirected.");
        }
    }

    public static AttemptClaim ReserveAttempt(string destination, SourceIdentity source, Configuration configuration)
    {
        EnsureUnusedDestination(destination);
        var attempt = new AttemptClaim(destination, Guid.NewGuid().ToString("N"));
        attempt.WriteMarker("attempt", new { attempt.Id, Destination = destination, Source = source,
            Configuration = configuration, Keys = Measurement.CaseSelection });
        return attempt;
    }

    public static Manifest WriteVerified(string destination, Payload payload)
    {
        EnsureUnusedDestination(destination);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Exclusive Windows publication is required.");
        if (!CreateDirectoryExclusive(destination, IntPtr.Zero))
            throw new IOException($"Exclusive destination creation failed (Win32 {Marshal.GetLastWin32Error()}).");
        EnsureSafeOutput(destination);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Measurement.Json);
        var manifest = new Manifest(payload.SchemaVersion, payload.Source, payload.Configuration,
            payload.Runs.Select(r => r.Key).ToArray(), payload.Runs.Select(r => r.Samples.Length).ToArray(),
            payload.Runs.Select(r => r.StopTick).ToArray(), payload.Runs.Select(r => r.StopReason).ToArray(),
            bytes.Length, Measurement.Hash(bytes));
        var rawPath = Path.Combine(destination, "off-deficit-v1.json");
        EnsureSafeOutput(destination);
        using (var file = new FileStream(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        }
        var reread = File.ReadAllBytes(rawPath);
        if (reread.Length != manifest.ByteCount || Measurement.Hash(reread) != manifest.Sha256)
            throw new InvalidDataException("Raw byte count/hash read-back mismatch; evidence invalid.");
        var restored = JsonSerializer.Deserialize<Payload>(reread, Measurement.Json)
            ?? throw new InvalidDataException("Raw JSON could not be read back.");
        CheckIdentity(restored, manifest);
        var manifestPath = Path.Combine(destination, "manifest.json");
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, Measurement.Json);
        EnsureSafeOutput(destination);
        using (var file = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(manifestBytes);
            file.Flush(flushToDisk: true);
        }
        var rereadManifestBytes = File.ReadAllBytes(manifestPath);
        if (rereadManifestBytes.Length != manifestBytes.Length
            || Measurement.Hash(rereadManifestBytes) != Measurement.Hash(manifestBytes))
            throw new InvalidDataException("Manifest byte count/hash read-back mismatch; evidence invalid.");
        var rereadManifest = JsonSerializer.Deserialize<Manifest>(rereadManifestBytes, Measurement.Json)
            ?? throw new InvalidDataException("Manifest could not be read back.");
        CheckIdentity(restored, rereadManifest);
        if (rereadManifest.Sha256 != Measurement.Hash(reread)
            || rereadManifest.ByteCount != reread.Length)
            throw new InvalidDataException("Manifest does not match raw bytes; evidence invalid.");
        return rereadManifest;
    }

    static void EnsureSafeOutput(string destination)
    {
        EnsureSafeParents(destination);
        if (!Directory.Exists(destination) || (new DirectoryInfo(destination).Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Private destination disappeared or was redirected.");
    }

    public static void CheckIdentity(Payload payload, Manifest manifest)
    {
        if (payload.SchemaVersion != Measurement.Schema || manifest.SchemaVersion != payload.SchemaVersion
            || payload.Source != manifest.Source || payload.Configuration != manifest.Configuration
            || payload.Configuration.TickCap != Measurement.TickCap || payload.Runs.Length != 2
            || payload.Runs[0].Key != "101/goap/off" || payload.Runs[1].Key != "202/simple/off"
            || payload.Summaries.Length != 2 || payload.Summaries[0].Key != payload.Runs[0].Key
            || payload.Summaries[1].Key != payload.Runs[1].Key
            || !manifest.RunKeys.SequenceEqual(payload.Runs.Select(run => run.Key))
            || !manifest.SampleCounts.SequenceEqual(payload.Runs.Select(run => run.Samples.Length))
            || !manifest.StopTicks.SequenceEqual(payload.Runs.Select(run => run.StopTick))
            || !manifest.StopReasons.SequenceEqual(payload.Runs.Select(run => run.StopReason)))
            throw new InvalidDataException("Payload/manifest identity mismatch.");
        for (var index = 0; index < payload.Runs.Length; index++)
        {
            var expected = Measurement.Summarize(Measurement.ValidateRun(payload.Runs[index]));
            var actual = payload.Summaries[index];
            if (actual is null || actual.Intervals is null || expected.Key != actual.Key
                || expected.FirstOneTickDecline != actual.FirstOneTickDecline
                || expected.FirstBelowEight != actual.FirstBelowEight
                || expected.PersistentToStopOnset != actual.PersistentToStopOnset
                || expected.RightCensored != actual.RightCensored || expected.StopTick != actual.StopTick
                || expected.HistoricalFirstZeroComparison != actual.HistoricalFirstZeroComparison
                || !expected.Intervals.SequenceEqual(actual.Intervals))
                throw new InvalidDataException("Summary differs from validated samples.");
        }
    }
}

public sealed class AttemptClaim(string destination, string id)
{
    public string Id { get; } = id;

    // A marker that exists but is empty, torn, or lacks a status is still a spent attempt.
    internal void WriteMarker(string suffix, object data)
    {
        SourceAndStore.EnsureUnusedDestination(destination);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, Measurement.Json);
        using var file = new FileStream(destination + "." + suffix, FileMode.CreateNew,
            FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(flushToDisk: true);
    }

    public void AboutToLaunch(int index, string key)
    {
        if (index is < 0 or > 1 || key != (index == 0 ? "101/goap/off" : "202/simple/off"))
            throw new InvalidDataException("Invalid selected case.");
        if (index == 1 && !File.Exists(destination + ".case-0-complete"))
            throw new InvalidDataException("First case has not completed.");
        WriteMarker($"case-{index}-about-to-launch", new { Id, Key = key });
    }

    public void Complete(int index, Run run)
    {
        if (index is < 0 or > 1 || !File.Exists(destination + $".case-{index}-about-to-launch"))
            throw new InvalidDataException("Case was not durably marked before launch.");
        if (run.Key != (index == 0 ? "101/goap/off" : "202/simple/off"))
            throw new InvalidDataException("Worker returned the wrong selected case.");
        Measurement.ValidateRun(run);
        WriteMarker($"case-{index}-complete", new { Id, run.Key, run.StopTick, run.StopReason });
    }

    public void Fail(int index, Exception error)
    {
        try { WriteMarker($"case-{index}-failed", new { Id, Error = error.GetType().Name }); }
        catch (Exception writeError) when (writeError is IOException or InvalidDataException or UnauthorizedAccessException)
        { } // The original claim and about-to-launch marker remain spent.
    }
}
