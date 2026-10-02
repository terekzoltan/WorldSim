using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldSim.SmrLab.Artifacts;

namespace WorldSim.SmrLab.ManualRuns;

public sealed record ManualJob(
    string Id, ManualRunRequest Request, string ProcessStatus, int? ExitCode,
    string ArtifactStatus, int? AssertionFailures, int? AssertionSkipped, int? AnomalyCount,
    int? ProcessId, DateTimeOffset? ProcessStartedAt, DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt, string? BundleId, string Diagnostic, string RunnerHash,
    IReadOnlyDictionary<string, string> EffectiveEnvironment, string CheckoutHead = "UNKNOWN",
    string? PublicationStatus = null);

public sealed record ManualStartResult(int Status, ManualJob? Job, string? Error);

/// <summary>One host instance, one active child; retained journal is the restart authority.</summary>
public sealed class ManualRunManager : IDisposable
{
    private readonly object _sync = new();
    private readonly string _workspace, _root, _staging, _published, _journals, _runnerDll;
    private ArtifactStore? _store;
    private readonly IManualRunner _runner;
    private readonly Func<Task>? _beforePublication;
    private readonly FileStream _lockFile;
    private readonly Dictionary<string, ManualJob> _jobs = new(StringComparer.Ordinal);
    private bool _ambiguous;

    public string PublishedRoot => _published;
    public string? Blocker { get; private set; }
    public string RunnerHash { get; }
    public string CheckoutHead { get; }

    public ManualRunManager(string workspace, string runRoot, string runnerDll, IManualRunner? runner = null,
        Func<Task>? beforePublication = null)
    {
        _workspace = Path.GetFullPath(workspace);
        _root = Path.GetFullPath(runRoot);
        _runnerDll = Path.GetFullPath(runnerDll);
        ValidateLocation(_workspace, _root, _runnerDll);
        _staging = Path.Combine(_root, ".staging");
        _published = Path.Combine(_root, "published");
        _journals = Path.Combine(_root, ".jobs");
        _runner = runner ?? new DotnetManualRunner();
        _beforePublication = beforePublication;
        RunnerHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_runnerDll)));
        CheckoutHead = TryReadHead(_workspace);
        InitializeRoot();
        _lockFile = new FileStream(Path.Combine(_root, ".host.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        Reconcile();
    }

    public void AttachStore(ArtifactStore store)
    {
        if (_store is not null || !store.Roots.Any(root => root.Name == "published"))
            throw new InvalidOperationException("Attach the public bundle store exactly once.");
        _store = store;
        lock (_sync)
        {
            if (_ambiguous) return;
            string? recoveringId = null;
            try
            {
                // A terminal journal alone never proves publication. Reconcile old and
                // pending journals against actual retained directories before Start opens.
                foreach (var job in _jobs.Values.ToArray())
                {
                    if (job.FinishedAt is null || job.ProcessStatus is "UNKNOWN" or "STARTING" or "RUNNING") continue;
                    recoveringId = job.Id;
                    var stage = Path.Combine(_staging, job.Id);
                    var published = Path.Combine(_published, job.Id);
                    if (Directory.Exists(stage) && Directory.Exists(published))
                        throw new InvalidDataException("Both staging and published evidence exist for one job.");
                    if (Directory.Exists(published))
                    {
                        var found = _store.ListBundles().SingleOrDefault(b => b.Source == $"published/{job.Id}/summary.json");
                        if (found is null || found.Format != "smr/v1" || found.Runs.Count != 1 ||
                            job.BundleId is not null && job.BundleId != found.Id ||
                            job.PublicationStatus == "NO_BUNDLE")
                            throw new InvalidDataException("Published evidence cannot be verified against its journal.");
                        if (!Inspect(job, job.ExitCode, published).Readable)
                            throw new InvalidDataException("Published run does not match its terminal journal.");
                        // Legacy published journals remain byte-for-byte intact.
                        _jobs[job.Id] = job with { BundleId = found.Id, PublicationStatus = "PUBLISHED" };
                        continue;
                    }
                    if (job.BundleId is not null || !Directory.Exists(stage) || job.PublicationStatus == "PUBLISHED")
                        throw new InvalidDataException("Terminal publication has no retained source or destination.");
                    if (job.PublicationStatus == "NO_BUNDLE") continue;
                    var inspection = Inspect(job, job.ExitCode);
                    if (!inspection.Readable ||
                        Directory.EnumerateDirectories(_published).Take(LabLimits.MaxBundles + 1).Count() >= LabLimits.MaxBundles)
                    {
                        Update(job.Id, current => current with { PublicationStatus = "NO_BUNDLE",
                            ArtifactStatus = inspection.Status, AssertionFailures = inspection.Failures,
                            AssertionSkipped = inspection.Skipped, AnomalyCount = inspection.Anomalies,
                            Diagnostic = current.Diagnostic + " Publication settled without a readable bundle; evidence retained privately." });
                        continue;
                    }
                    Update(job.Id, current => current with { PublicationStatus = "PENDING" });
                    Directory.Move(stage, published);
                    Publish(job.Id);
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                _ambiguous = true;
                Blocker = $"Terminal publication recovery failed closed: {ex.GetType().Name}";
                if (recoveringId is not null) TrySavePublicationUncertain(recoveringId, Blocker);
            }
        }
    }

    private static string TryReadHead(string workspace)
    {
        try
        {
            using var git = new Process { StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workspace, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            } };
            git.StartInfo.ArgumentList.Add("rev-parse"); git.StartInfo.ArgumentList.Add("HEAD");
            if (!git.Start()) return "UNKNOWN";
            var sha = git.StandardOutput.ReadLine();
            if (!git.WaitForExit(3000)) { git.Kill(); return "UNKNOWN"; }
            return git.ExitCode == 0 && sha is { Length: 40 } && sha.All(Uri.IsHexDigit) ? sha : "UNKNOWN";
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { return "UNKNOWN"; }
    }

    public static void ValidateLocation(string workspace, string runRoot, string runnerDll)
    {
        if (!Path.IsPathFullyQualified(runRoot) || !Path.IsPathFullyQualified(runnerDll))
            throw new ArgumentException("Choose fully qualified run-root and runner DLL paths.");
        var parent = Path.Combine(Path.GetFullPath(workspace), ".artifacts", "smr");
        if (!Inside(parent, runRoot) || !Path.GetFullPath(runnerDll).Equals(
                Path.Combine(workspace, "WorldSim.ScenarioRunner", "bin", "Debug", "net8.0", "WorldSim.ScenarioRunner.dll"),
                PathComparison) || !File.Exists(runnerDll))
            throw new ArgumentException("Choose a Lab-owned .artifacts/smr run root and this checkout's built Debug ScenarioRunner DLL.");
        EnsureNoReparse(workspace, Path.GetFullPath(runRoot));
        EnsureNoReparse(workspace, Path.GetFullPath(runnerDll));
    }

    public static void ValidateReadRoots(string runRoot, IEnumerable<string> roots)
    {
        foreach (var root in roots)
            if (Inside(root, runRoot) || Inside(runRoot, root) ||
                Path.GetFullPath(root).Equals(Path.GetFullPath(runRoot), PathComparison))
                throw new ArgumentException("An explicit read root must not expose the private manual-run root.");
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool Inside(string parent, string child) => Path.GetFullPath(child).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, PathComparison);

    private static void EnsureNoReparse(string workspace, string path)
    {
        var current = path;
        while (Inside(workspace, current) || current.Equals(workspace, PathComparison))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Reparse points are not supported for manual-run paths.");
            current = Path.GetDirectoryName(current)!;
        }
    }

    private void InitializeRoot()
    {
        var existed = Directory.Exists(_root);
        if (existed && (File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Manual root is a reparse point.");
        Directory.CreateDirectory(_root);
        var marker = Path.Combine(_root, ".lab-b-owner");
        if (!File.Exists(marker))
        {
            if (existed && Directory.EnumerateFileSystemEntries(_root).Any())
                throw new InvalidDataException("Nonempty run root has no Lab B ownership marker; starts are disabled.");
            using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var bytes = Encoding.UTF8.GetBytes("SMR-LAB-B/v1");
            stream.Write(bytes); stream.Flush(flushToDisk: true);
        }
        else if (File.ReadAllText(marker) != "SMR-LAB-B/v1")
            throw new InvalidDataException("Manual root marker is invalid.");
        foreach (var dir in new[] { _staging, _published, _journals })
        {
            EnsureNoReparse(_workspace, dir);
            Directory.CreateDirectory(dir);
        }
    }

    private void Reconcile()
    {
        try
        {
            if (Directory.EnumerateFiles(_journals).Any(path => !path.EndsWith(".json", StringComparison.Ordinal)))
                throw new InvalidDataException("Unfinished journal write.");
            foreach (var path in Directory.EnumerateFiles(_journals, "*.json"))
            {
                EnsureNoReparse(_workspace, path);
                var job = JsonSerializer.Deserialize<ManualJob>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("Empty job journal.");
                if (job.Request is null || job.EffectiveEnvironment is null ||
                    !Guid.TryParseExact(job.Id, "N", out _) ||
                    !Path.GetFileName(path).Equals(job.Id + ".json", StringComparison.Ordinal) ||
                    job.Request.IntentId.ToString("N") != job.Id ||
                    job.ProcessStatus is not ("EXITED" or "LAUNCH_ERROR" or "TIMEOUT" or "STARTING" or "RUNNING" or "UNKNOWN") ||
                    job.PublicationStatus is not (null or "PENDING" or "PUBLISHED" or "NO_BUNDLE" or "UNCERTAIN") ||
                    job.ProcessStatus == "EXITED" && (job.FinishedAt is null || job.ExitCode is null) ||
                    job.ProcessStatus is "LAUNCH_ERROR" or "TIMEOUT" && job.FinishedAt is null ||
                    !_jobs.TryAdd(job.Id, job))
                    throw new InvalidDataException("Journal identity mismatch or duplicate.");
                if (job.ProcessStatus is "STARTING" or "RUNNING" or "UNKNOWN" || job.PublicationStatus == "UNCERTAIN")
                {
                    _ambiguous = true;
                    Blocker = job.PublicationStatus == "UNCERTAIN" ? $"Job {job.Id} has uncertain publication. No new starts."
                        : IsKnownChildAlive(job)
                        ? $"Job {job.Id} has a matching live child. No new starts."
                        : $"Job {job.Id} has no proven terminal exit. No automatic replay or new starts.";
                    if (job.ProcessStatus is "STARTING" or "RUNNING")
                    {
                        var interrupted = job with { ProcessStatus = "UNKNOWN", Diagnostic = Blocker };
                        Save(interrupted);
                        _jobs[job.Id] = interrupted;
                    }
                }
            }
            foreach (var dir in Directory.EnumerateDirectories(_staging))
            {
                EnsureNoReparse(_workspace, dir);
                if (!_jobs.ContainsKey(Path.GetFileName(dir)))
                    throw new InvalidDataException("Unregistered staging directory; a spawn-to-journal crash is possible.");
            }
            foreach (var dir in Directory.EnumerateDirectories(_published))
            {
                EnsureNoReparse(_workspace, dir);
                if (!_jobs.TryGetValue(Path.GetFileName(dir), out var job) || job.FinishedAt is null ||
                    job.ProcessStatus is "STARTING" or "RUNNING" or "UNKNOWN")
                    throw new InvalidDataException("Published directory has no reconciled terminal journal.");
            }
            foreach (var job in _jobs.Values)
                if (job.BundleId is not null && !Directory.Exists(Path.Combine(_published, job.Id)))
                    throw new InvalidDataException("Published job directory is missing.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _ambiguous = true;
            Blocker = $"Manual journal/restart reconciliation failed closed: {ex.Message}";
        }
    }

    private static bool IsKnownChildAlive(ManualJob job)
    {
        if (job.ProcessId is not { } id || job.ProcessStartedAt is not { } started) return false;
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited && Math.Abs((process.StartTime.ToUniversalTime() - started.UtcDateTime).TotalSeconds) < 2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }

    public IReadOnlyList<ManualJob> List() { lock (_sync) return _jobs.Values.OrderByDescending(j => j.CreatedAt).ToArray(); }
    public ManualJob? Get(string id)
    {
        if (!Guid.TryParse(id, out var parsed)) return null;
        lock (_sync) return _jobs.GetValueOrDefault(parsed.ToString("N"));
    }

    public ManualStartResult Start(ManualRunRequest request)
    {
        if (ManualRunContract.Validate(request) is { } error) return new ManualStartResult(400, null, error);
        if (_store is null) return new ManualStartResult(409, null, "Bundle store not attached.");
        var id = request.IntentId.ToString("N");
        lock (_sync)
        {
            if (_jobs.TryGetValue(id, out var prior))
                return prior.Request == request ? new ManualStartResult(200, prior, null)
                    : new ManualStartResult(409, null, "Intent key already belongs to different settings.");
            if (_ambiguous) return new ManualStartResult(409, null, Blocker ?? "Restart requires reconciliation.");
            if (_jobs.Values.Any(job => job.ProcessStatus is "STARTING" or "RUNNING" || job.PublicationStatus == "PENDING"))
                return new ManualStartResult(409, null, "One manual job is active or its publication is pending.");
            if (Directory.EnumerateDirectories(_published).Take(LabLimits.MaxBundles + 1).Count() >= LabLimits.MaxBundles)
                return new ManualStartResult(409, null, "Published root has reached A's 32-bundle limit; no evidence was deleted.");

            var stage = Path.Combine(_staging, id);
            try
            {
                if (Directory.Exists(stage) || File.Exists(Path.Combine(_journals, id + ".json")))
                    throw new InvalidDataException("Job identity already has unregistered evidence.");
                Directory.CreateDirectory(stage);
                var effective = ManualRunContract.EnvironmentFor(request, stage);
                var job = new ManualJob(id, request, "STARTING", null, "UNKNOWN", null, null, null,
                    null, null, DateTimeOffset.UtcNow, null, null, "Journal reserved before spawn.",
                    RunnerHash, effective, CheckoutHead);
                Save(job); // flushed, atomically published and read back before any process starts
                _jobs.Add(id, job);
                _ = Task.Run(() => ExecuteAsync(id));
                return new ManualStartResult(202, job, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                _ambiguous = true;
                Blocker = $"Reservation failed closed; inspect private job path {id}: {ex.GetType().Name}";
                return new ManualStartResult(409, null, Blocker);
            }
        }
    }

    private async Task ExecuteAsync(string id)
    {
        ManualJob job;
        lock (_sync) job = _jobs[id];
        ManualProcessResult result;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(ManualRunContract.TimeoutSeconds));
            result = await _runner.RunAsync(new ManualRunInvocation(_workspace, _runnerDll, id, job.EffectiveEnvironment),
                (pid, started) => Update(id, current => current with
                {
                    ProcessId = pid, ProcessStartedAt = started, ProcessStatus = "RUNNING", Diagnostic = "Owned child started."
                }), deadline.Token);
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _ambiguous = true;
                Blocker = $"Job {id} could not be safely reconciled ({ex.GetType().Name}).";
                TrySaveUnknown(id, Blocker);
            }
            return;
        }

        try
        {
            var (status, failures, skipped, anomalies, detail, readable) = Inspect(job, result.ExitCode);
            Update(id, current => current with
            {
                ProcessStatus = result.Status, ExitCode = result.ExitCode, FinishedAt = DateTimeOffset.UtcNow,
                PublicationStatus = result.Status == "UNKNOWN" ? "UNCERTAIN" : "PENDING",
                ArtifactStatus = status, AssertionFailures = failures, AssertionSkipped = skipped,
                AnomalyCount = anomalies, Diagnostic = $"{detail} {result.Diagnostic}"[..Math.Min(1024, detail.Length + result.Diagnostic.Length + 1)]
            });
            if (result.Status == "UNKNOWN")
            {
                lock (_sync) { _ambiguous = true; Blocker = $"Job {id} has no proven terminal process result."; }
                return;
            }
            if (_beforePublication is not null) await _beforePublication();
            if (!readable)
            {
                Update(id, current => current with { PublicationStatus = "NO_BUNDLE" });
                return;
            }
            lock (_sync)
            {
                if (Directory.EnumerateDirectories(_published).Take(LabLimits.MaxBundles + 1).Count() >= LabLimits.MaxBundles)
                {
                    Update(id, current => current with { PublicationStatus = "NO_BUNDLE",
                        Diagnostic = current.Diagnostic + " Publication capacity reached; retained privately." });
                    return;
                }
                var destination = Path.Combine(_published, id);
                Directory.Move(Path.Combine(_staging, id), destination);
                Publish(id);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            lock (_sync)
            {
                _ambiguous = true;
                Blocker = $"Job {id} publication/reconciliation is ambiguous ({ex.GetType().Name}).";
                if (_jobs[id].FinishedAt is null) TrySaveUnknown(id, Blocker);
                else TrySavePublicationUncertain(id, Blocker);
            }
        }
    }

    private void Publish(string id)
    {
        var found = _store!.ListBundles().SingleOrDefault(b => b.Source == $"published/{id}/summary.json");
        if (found is null || found.Format != "smr/v1" || found.Runs.Count != 1)
            throw new InvalidDataException("Published bundle could not be verified.");
        Update(id, current => current with { BundleId = found.Id, PublicationStatus = "PUBLISHED" });
    }

    private void TrySavePublicationUncertain(string id, string diagnostic)
    {
        try { Update(id, current => current with { PublicationStatus = "UNCERTAIN", BundleId = null, Diagnostic = diagnostic }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _jobs[id] = _jobs[id] with { PublicationStatus = "UNCERTAIN", BundleId = null, Diagnostic = diagnostic };
        }
    }

    private (string Status, int? Failures, int? Skipped, int? Anomalies, string Detail, bool Readable)
        Inspect(ManualJob job, int? exitCode, string? artifactDirectory = null)
    {
        var stage = artifactDirectory ?? Path.Combine(_staging, job.Id);
        var manifestPath = Path.Combine(stage, "manifest.json");
        if (!File.Exists(manifestPath)) return ("UNKNOWN", null, null, null, "No manifest; evidence retained privately.", false);
        if (!File.Exists(Path.Combine(stage, "run.log")))
            return ("PARTIAL", null, null, null, "No terminal run.log; evidence retained privately.", false);
        try
        {
            EnsureNoReparse(_workspace, manifestPath);
            EnsureNoReparse(_workspace, Path.Combine(stage, "run.log"));
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var meta = manifest.RootElement;
            if (meta.GetProperty("schemaVersion").GetString() != "smr/v1")
                return ("UNKNOWN_FORMAT", null, null, null, "Unsupported artifact format.", false);
            var bundle = new ArtifactStore(_workspace, [stage]).ListBundles().Single();
            if (bundle.DeclaredRuns != 1 || bundle.Runs.Count != 1 ||
                meta.GetProperty("seedCount").GetInt32() != 1 ||
                meta.GetProperty("plannerCount").GetInt32() != 1 ||
                meta.GetProperty("configCount").GetInt32() != 1 ||
                meta.GetProperty("totalRuns").GetInt32() != 1)
                return ("PARTIAL", null, null, null, "Bundle is not exactly one complete run; retained privately.", false);
            var run = bundle.Runs[0];
            var small = job.Request.Preset == "lab_small";
            var fields = run.Fields;
            bool Same(string field, string value) => fields.GetValueOrDefault(field) == value;
            if (run.Config != job.Request.Preset || run.Planner != job.Request.Planner ||
                run.Seed != job.Request.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                !Same("ticks", job.Request.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)) ||
                !Same("dt", "0.25") || !Same("width", small ? "32" : "64") ||
                !Same("height", small ? "20" : "40") || !Same("initialPop", small ? "12" : "24") ||
                !Same("visualLane", "Headless") || !Same("enableCombatPrimitives", "false") ||
                !Same("enableDiplomacy", "false") || !Same("enableSiege", "true") ||
                !Same("stoneBuildingsEnabled", "false") || !Same("enablePredatorHumanAttacks", "false") ||
                !Same("birthRateMultiplier", "1") || !Same("movementSpeedMultiplier", "1"))
                return ("PARTIAL", null, null, null, "Effective run settings differ from request; retained privately.", false);
            var exitMatch = meta.GetProperty("exitCode").GetInt32() == exitCode;
            var status = bundle.Status == "COMPLETE" && exitMatch ? "COMPLETE" : "PARTIAL";
            return (status, bundle.Runs[0].AssertionFailures, bundle.Runs[0].AssertionSkips,
                meta.TryGetProperty("anomalyCount", out var count) ? count.GetInt32() : null,
                exitMatch ? "Terminal bundle reconciled." : "Manifest/process exit mismatch; retained as PARTIAL.", true);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or KeyNotFoundException
            or InvalidOperationException or FormatException)
        {
            return ("PARTIAL", null, null, null, $"Unreadable terminal artifact: {ex.GetType().Name}.", false);
        }
    }

    private void Update(string id, Func<ManualJob, ManualJob> change)
    {
        lock (_sync)
        {
            var updated = change(_jobs[id]);
            Save(updated);
            _jobs[id] = updated;
        }
    }

    private void TrySaveUnknown(string id, string diagnostic)
    {
        try { Update(id, current => current with { ProcessStatus = "UNKNOWN", PublicationStatus = "UNCERTAIN", Diagnostic = diagnostic }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* fail closed in memory */ }
    }

    private void Save(ManualJob job)
    {
        var path = Path.Combine(_journals, job.Id + ".json");
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, job);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
        var persisted = JsonSerializer.Deserialize<ManualJob>(File.ReadAllText(path));
        if (persisted?.Id != job.Id || persisted.ProcessStatus != job.ProcessStatus ||
            persisted.PublicationStatus != job.PublicationStatus || persisted.BundleId != job.BundleId ||
            persisted.Request != job.Request || persisted.EffectiveEnvironment?.Count != job.EffectiveEnvironment.Count ||
            job.EffectiveEnvironment.Any(entry => !persisted.EffectiveEnvironment.TryGetValue(entry.Key, out var value) || value != entry.Value))
            throw new InvalidDataException("Job journal verification failed.");
    }

    public void Dispose() => _lockFile.Dispose();
}
