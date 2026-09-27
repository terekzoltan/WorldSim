using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorldSim.SmrLab.Artifacts;

// All filesystem access lives here. JSON-provided paths (including manifest.artifactDir) are never opened.
public sealed class ArtifactStore
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly string[] ComparedFields =
    [
        "configName", "plannerMode", "seed", "visualLane", "width", "height", "initialPop",
        "ticks", "dt", "enableCombatPrimitives", "enableDiplomacy", "enableSiege",
        "enablePredatorHumanAttacks", "stoneBuildingsEnabled", "birthRateMultiplier",
        "movementSpeedMultiplier", "people", "food", "livingColonies", "deathsStarvation",
        "deathsPredator", "ecology.herbivores", "ecology.predators", "ecology.activeFoodNodes"
    ];

    private readonly string[] _roots;
    public string WorkspaceRoot { get; }

    public ArtifactStore(string workspaceRoot, IEnumerable<string> roots)
    {
        var workspace = Path.GetFullPath(workspaceRoot);
        WorkspaceRoot = workspace;
        var selected = roots.ToArray();
        if (selected.Length is 0 or > LabLimits.MaxRoots || selected.Any(root => !Path.IsPathFullyQualified(root)))
            throw new ArgumentException($"Select between 1 and {LabLimits.MaxRoots} absolute artifact roots at startup.");
        _roots = selected.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();

        foreach (var root in _roots)
        {
            if (root.Equals(workspace, PathComparison))
                throw new ArgumentException("Select an artifact directory, not the entire checkout.");
            // The startup operator selects external roots explicitly. Browser IDs and artifact
            // metadata cannot add roots; every subsequent file access is contained in one selection.
            EnsureNoReparse(root);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Artifact root unavailable: {root}");
        }
    }

    public IReadOnlyList<LabRoot> Roots => _roots.Select((path, id) => new LabRoot(id, Path.GetFileName(path))).ToArray();

    public IReadOnlyList<LabBundle> ListBundles()
    {
        var result = new List<LabBundle>();
        var remaining = LabLimits.MaxReadBytes;
        for (var rootIndex = 0; rootIndex < _roots.Length; rootIndex++)
        {
            var root = _roots[rootIndex];
            var paths = File.Exists(Contained(root, root, "manifest.json")) ||
                        File.Exists(Contained(root, root, "payload.json"))
                ? new[] { root }
                : Directory.EnumerateDirectories(root).Take(LabLimits.MaxBundles + 1)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (paths.Length > LabLimits.MaxBundles)
                throw new InvalidDataException("Artifact root exceeds bundle listing limit.");
            for (var index = 0; index < paths.Length; index++)
            {
                var id = BundleId(rootIndex, root, paths[index]);
                try { result.Add(Load(paths[index], root, id, null, ref remaining)); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
                {
                    result.Add(new LabBundle(id, Path.GetFileName(root), "PARTIAL", "UNKNOWN", null,
                        "UNKNOWN", null, null, [], [], [SafeError(ex)], $"{Path.GetFileName(paths[index])}/manifest.json"));
                }
            }
        }
        return result;
    }

    public LabBundle? GetBundle(string id)
    {
        // Derive IDs from an enumerated root; do not interpret the supplied ID as a filename.
        return ListBundles().FirstOrDefault(bundle => bundle.Id == id);
    }

    public LabRun? GetRun(string bundleId, string runId)
    {
        var index = ListBundles().FirstOrDefault(bundle => bundle.Id == bundleId);
        if (index is null || !int.TryParse(runId, NumberStyles.None, CultureInfo.InvariantCulture, out var runIndex) ||
            runIndex < 0 || runIndex >= index.Runs.Count) return null;
        var (root, path) = FindBundle(bundleId);
        var remaining = LabLimits.MaxReadBytes;
        return Load(path, root, bundleId, runIndex, ref remaining).Runs.ElementAtOrDefault(runIndex);
    }

    public LabComparison? Compare(string leftBundle, string leftRun, string rightBundle, string rightRun)
    {
        var left = GetRun(leftBundle, leftRun);
        var right = GetRun(rightBundle, rightRun);
        if (left is null || right is null) return null;
        var leftView = GetBundle(leftBundle);
        var rightView = GetBundle(rightBundle);
        var fields = ComparedFields.Concat(["source", "provenance"]);
        var differences = fields.Select(field => new LabDifference(field,
                field == "source" ? left.Source : field == "provenance" ? leftView?.Provenance ?? "UNKNOWN" : left.Fields.GetValueOrDefault(field, "UNKNOWN"),
                field == "source" ? right.Source : field == "provenance" ? rightView?.Provenance ?? "UNKNOWN" : right.Fields.GetValueOrDefault(field, "UNKNOWN")))
            .Where(item => !string.Equals(item.Left, item.Right, StringComparison.Ordinal)).ToArray();
        var inference = left.Seed != right.Seed && left.Planner != right.Planner
            ? "Exploratory only: seed AND planner differ; this cannot establish a planner effect."
            : "Exploratory comparison; measured differences are not a Meta acceptance decision.";
        var leftBundleLimits = leftView?.Limitations ?? [];
        var rightBundleLimits = rightView?.Limitations ?? [];
        return new LabComparison(left, right, differences, inference,
            left.Limitations.Concat(right.Limitations).Concat(leftBundleLimits).Concat(rightBundleLimits)
                .Distinct().ToArray(), Outcome(left, leftView), Outcome(right, rightView));
    }

    private static LabOutcome Outcome(LabRun run, LabBundle? bundle)
    {
        var anomaliesKnown = bundle is not null &&
            !bundle.Limitations.Any(issue => issue.Contains("anomalies.json", StringComparison.Ordinal));
        return new LabOutcome(bundle?.Status ?? "UNKNOWN", run.AssertionFailures, run.AssertionSkips,
            anomaliesKnown ? run.Anomalies.Count : null, anomaliesKnown ? bundle!.Anomalies.Count : null,
            run.Fields.GetValueOrDefault("people", "UNKNOWN"), run.Fields.GetValueOrDefault("food", "UNKNOWN"),
            anomaliesKnown ? run.Anomalies : [], anomaliesKnown ? bundle!.Anomalies : []);
    }

    public string Report(LabComparison comparison)
    {
        static string Clean(string value) => WebUtility.HtmlEncode(value)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        var text = new StringBuilder("# SMR Lab observation (exploratory)\n\n");
        text.AppendLine("Measured outcome is not Meta acceptance. Missing data is UNKNOWN, not zero or PASS.\n");
        text.AppendLine($"- Left: {Clean(comparison.Left.Source)} (seed {Clean(comparison.Left.Seed)}, planner {Clean(comparison.Left.Planner)})");
        text.AppendLine($"- Right: {Clean(comparison.Right.Source)} (seed {Clean(comparison.Right.Seed)}, planner {Clean(comparison.Right.Planner)})\n");
        text.AppendLine("## Measured observations (not acceptance)\n");
        text.AppendLine("| Selection | Bundle status | Assertion failures | Assertion skips | Run anomalies | Bundle-level anomalies (unscoped) | People | Food |");
        text.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var (side, outcome) in new[] { ("Left", comparison.LeftOutcome), ("Right", comparison.RightOutcome) })
            text.AppendLine($"| {side} | {Clean(outcome.BundleStatus)} | {Clean(outcome.AssertionFailures?.ToString() ?? "UNKNOWN")} | " +
                $"{Clean(outcome.AssertionSkips?.ToString() ?? "UNKNOWN")} | {Clean(outcome.RunAnomalies?.ToString() ?? "UNKNOWN")} | " +
                $"{Clean(outcome.BundleAnomalies?.ToString() ?? "UNKNOWN")} | {Clean(outcome.People)} | {Clean(outcome.Food)} |");
        text.AppendLine("\nRun anomalies count only events attributed to that run; bundle-level anomalies have no run key.\n");
        foreach (var (side, outcome) in new[] { ("Left", comparison.LeftOutcome), ("Right", comparison.RightOutcome) })
        {
            foreach (var item in outcome.RunAnomalyItems.Take(3))
                text.AppendLine($"- {side} run anomaly: {Clean(item.Id)} ({Clean(item.Kind)}): {Clean(item.Message)}");
            foreach (var item in outcome.BundleAnomalyItems.Take(3))
                text.AppendLine($"- {side} unscoped bundle anomaly: {Clean(item.Id)} ({Clean(item.Kind)}): {Clean(item.Message)}");
            if (outcome.RunAnomalyItems.Count > 3 || outcome.BundleAnomalyItems.Count > 3)
                text.AppendLine($"- {side}: additional anomalies omitted from this short report; see selected artifacts.");
        }
        text.AppendLine("| Field | Left | Right |\n|---|---|---|");
        foreach (var item in comparison.Differences)
            text.AppendLine($"| {Clean(item.Field)} | {Clean(item.Left)} | {Clean(item.Right)} |");
        text.AppendLine($"\n## Remaining uncertainty\n{Clean(comparison.Inference)}");
        foreach (var limit in comparison.Limitations) text.AppendLine($"- {Clean(limit)}");
        return text.ToString();
    }

    private (string Root, string Path) FindBundle(string id)
    {
        for (var rootIndex = 0; rootIndex < _roots.Length; rootIndex++)
        {
            var root = _roots[rootIndex];
            var paths = File.Exists(Contained(root, root, "manifest.json")) || File.Exists(Contained(root, root, "payload.json"))
                ? new[] { root } : Directory.EnumerateDirectories(root).Take(LabLimits.MaxBundles + 1)
                    .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < paths.Length; i++)
                if (BundleId(rootIndex, root, paths[i]) == id) return (root, paths[i]);
        }
        throw new InvalidDataException("Unknown bundle identifier.");
    }

    private static string BundleId(int rootIndex, string root, string bundle) =>
        $"r{rootIndex}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, bundle))))[..12]}";

    private static LabBundle Load(string dir, string root, string id, int? selectedRun, ref long remaining)
    {
        var label = Path.GetFileName(dir);
        var issues = new List<string>();
        var source = $"{Path.GetFileName(root)}/{(dir == root ? "" : label + "/")}summary.json";
        using var manifest = TryJson(root, dir, "manifest.json", issues, ref remaining);
        if (manifest is null)
        {
            using var custom = TryJson(root, dir, "payload.json", issues, ref remaining);
            if (custom is not null && Text(custom.RootElement, "schemaVersion") == "e11h_smr_recruitment_mortality_v1")
                return new LabBundle(id, Path.GetFileName(root), "PARTIAL", "e11h_smr_recruitment_mortality_v1",
                    null, "UNKNOWN", null, null, [], [], ["E11-H real import unverified; conditional version dispatch only."],
                    source.Replace("summary.json", "payload.json", StringComparison.Ordinal));
            return new LabBundle(id, Path.GetFileName(root),
                File.Exists(Contained(root, dir, "summary.json")) ? "PARTIAL" : "UNKNOWN_FORMAT",
                "UNKNOWN", null, "UNKNOWN", null, null, [], [],
                issues.Count == 0 ? ["Unknown format: no supported manifest.json or versioned payload.json."] : issues,
                source);
        }
        var meta = manifest.RootElement;
        var format = Text(meta, "schemaVersion");
        if (format != "smr/v1")
            return new LabBundle(id, Path.GetFileName(root), "UNKNOWN_FORMAT", format, null, "UNKNOWN",
                null, null, [], [], [$"Unsupported schemaVersion: {format}"], source);

        using var summary = TryJson(root, dir, "summary.json", issues, ref remaining);
        if (summary is null || !Property(summary.RootElement, "runs", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            issues.Add("Summary runs are unavailable; bundle is PARTIAL.");
            return new LabBundle(id, Path.GetFileName(root), "PARTIAL", format, Text(meta, "runId"),
                ProvenanceOf(meta), Number(meta, "totalRuns"), Number(meta, "exitCode"), [], [], issues, source);
        }
        if (entries.GetArrayLength() > LabLimits.MaxRuns) throw new InvalidDataException("Run count exceeds limit.");
        var declared = Number(meta, "totalRuns");
        if (declared != entries.GetArrayLength()) issues.Add("Manifest totalRuns differs from summary run count.");

        var filesByIdentity = new Dictionary<string, (string Key, JsonElement Data)>(StringComparer.OrdinalIgnoreCase);
        var runsDir = Contained(root, dir, "runs");
        if (!Directory.Exists(runsDir)) issues.Add("runs/ is absent.");
        else
        {
            var files = Directory.EnumerateFiles(runsDir, "*.json").Take(LabLimits.MaxRuns + 1).ToArray();
            if (files.Length > LabLimits.MaxRuns) throw new InvalidDataException("Run file count exceeds limit.");
            if (files.Length != entries.GetArrayLength()) issues.Add("Per-run file count differs from summary run count.");
            foreach (var file in files)
            {
                using var runFile = TryJson(root, dir, Path.Combine("runs", Path.GetFileName(file)), issues, ref remaining);
                if (runFile is null) continue;
                var key = Path.GetFileNameWithoutExtension(file);
                if (!SafeKey(key)) { issues.Add("Unsafe per-run key."); continue; }
                if (runFile.RootElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add($"Malformed runs/{key}.json.");
                    continue;
                }
                if (!filesByIdentity.TryAdd(Identity(runFile.RootElement), (key, runFile.RootElement.Clone())))
                    issues.Add("Duplicate semantic run identity.");
            }
        }

        using var assertions = TryJson(root, dir, "assertions.json", issues, ref remaining);
        using var anomalies = TryJson(root, dir, "anomalies.json", issues, ref remaining);
        var assertionItems = Items(assertions, "invariantId", "assertion", issues);
        var anomalyItems = Items(anomalies, "id", "anomaly", issues);
        using var drilldown = TryJson(root, dir, Path.Combine("drilldown", "index.json"), issues, ref remaining, optional: true);
        if (Text(meta, "drilldownEnabled") == "true" && drilldown is null)
            issues.Add("Declared drilldown index is unavailable.");
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        if (drilldown is not null && Property(drilldown.RootElement, "runs", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            if (choices.GetArrayLength() > LabLimits.MaxRuns) throw new InvalidDataException("Drilldown index exceeds limit.");
            foreach (var choice in choices.EnumerateArray())
            {
                var key = Text(choice, "runKey");
                if (SafeKey(key))
                {
                    selectedKeys.Add(key);
                    // Validate retained source when indexing so a missing/corrupt selected timeline
                    // never leaves its bundle looking COMPLETE.
                    using var timeline = TryJson(root, dir, Path.Combine("drilldown", key, "timeline.json"), issues, ref remaining);
                    if (timeline is not null) _ = Samples(timeline, issues);
                    using var eventDocument = TryJson(root, dir, Path.Combine("drilldown", key, "events.json"), issues, ref remaining);
                    if (eventDocument is not null) _ = Items(eventDocument, "id", "event", issues);
                }
                else issues.Add("Unsafe drilldown key ignored.");
            }
        }
        foreach (var selectedKey in selectedKeys)
            if (!filesByIdentity.Values.Any(file => file.Key == selectedKey))
                issues.Add("Drilldown selection has no matching per-run source.");
        var interval = Number(meta, "drilldownSampleEvery");
        var views = new List<LabRun>();
        foreach (var entry in entries.EnumerateArray())
        {
            var index = views.Count;
            if (entry.ValueKind != JsonValueKind.Object) { issues.Add("Malformed run entry."); continue; }
            var file = filesByIdentity.GetValueOrDefault(Identity(entry));
            var key = file.Key;
            if (key is null) issues.Add("Run has no matching per-run source file.");
            var mismatch = key is null ? null : FirstDifference(entry, file.Data, "run");
            if (mismatch is not null)
                issues.Add($"Source mismatch at {mismatch}: summary.json#runs[{index}] differs from runs/{key}.json; displayed values are from summary.json.");
            var refs = key is null ? [] : assertionItems.Where(item => item.RunKey == key).ToArray();
            var warns = key is null ? [] : anomalyItems.Where(item => item.RunKey == key).ToArray();
            var selected = key is not null && selectedKeys.Contains(key);
            var timeline = selected ? "available" : "not retained";
            var samples = Array.Empty<LabSample>();
            var events = Array.Empty<LabItem>();
            var runLimits = new List<string> { "Source/build Git SHA UNKNOWN unless explicitly supplied by the artifact." };
            if (mismatch is not null)
                runLimits.Add($"Source mismatch at {mismatch}: summary.json#runs[{index}] differs from runs/{key}.json; displayed values are from summary.json.");
            if (key is null) runLimits.Add("Per-run source unavailable; displayed values are from summary.json.");
            if (!selected) runLimits.Add("Timeline not retained for this run.");
            if (selected && selectedRun == index)
            {
                using var sampleDoc = TryJson(root, dir, Path.Combine("drilldown", key!, "timeline.json"), runLimits, ref remaining);
                if (sampleDoc is not null) samples = Samples(sampleDoc, runLimits);
                else timeline = "not retained";
                using var eventDoc = TryJson(root, dir, Path.Combine("drilldown", key!, "events.json"), runLimits, ref remaining, optional: true);
                events = Items(eventDoc, "id", "event", runLimits).ToArray();
            }
            var fields = ComparedFields.ToDictionary(field => field,
                field => NestedText(entry, field), StringComparer.Ordinal);
            var unknownAssertion = assertionItems.Any(item => item.Kind == "UNKNOWN" && (item.RunKey is null || item.RunKey == key));
            if (assertions is null || unknownAssertion)
                runLimits.Add("Assertions UNKNOWN (file missing, invalid, or containing an unclassified result).");
            int? failures = assertions is null || key is null || unknownAssertion ? null : refs.Count(item => item.Kind == "FAIL");
            int? skips = assertions is null || key is null || unknownAssertion ? null : refs.Count(item => item.Kind == "SKIP");
            views.Add(new LabRun(index.ToString(CultureInfo.InvariantCulture),
                key is null || mismatch is not null ? $"{source}#runs[{index}]" :
                    $"{Path.GetFileName(root)}/{(dir == root ? "" : label + "/")}runs/{key}.json",
                Text(entry, "configName"), Text(entry, "plannerMode"), Text(entry, "seed"), fields,
                failures, skips, refs, warns, timeline, selected ? interval : null, samples, events, runLimits));
        }
        if (views.Count != entries.GetArrayLength()) issues.Add("Some summary runs were malformed.");
        var status = issues.Count == 0 ? "COMPLETE" : "PARTIAL";
        return new LabBundle(id, Path.GetFileName(root), status, format, Text(meta, "runId"),
            ProvenanceOf(meta), declared, Number(meta, "exitCode"), views,
            anomalyItems.Where(item => item.RunKey is null).ToArray(), issues, source);
    }

    private static string ProvenanceOf(JsonElement manifest)
    {
        var sha = Text(manifest, "gitSha");
        return sha == "UNKNOWN" ? Text(manifest, "sourceSha") : sha;
    }

    private static string Identity(JsonElement run) => string.Join("\u001f",
        Text(run, "configName"), Text(run, "plannerMode"), Text(run, "seed"), Text(run, "visualLane"));

    // Compare source JSON structurally: a filename and four identity fields do not establish
    // that the summary's displayed measurements came from the matching per-run file.
    private static string? FirstDifference(JsonElement summary, JsonElement file, string path)
    {
        if (summary.ValueKind != file.ValueKind) return path;
        if (summary.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in summary.EnumerateObject())
            {
                var name = field.Name.Length > 80 ? field.Name[..80] : field.Name;
                var child = $"{path}.{name}";
                if (!file.TryGetProperty(field.Name, out var counterpart)) return child;
                var difference = FirstDifference(field.Value, counterpart, child);
                if (difference is not null) return difference;
            }
            foreach (var field in file.EnumerateObject())
                if (!summary.TryGetProperty(field.Name, out _))
                    return $"{path}.{(field.Name.Length > 80 ? field.Name[..80] : field.Name)}";
            return null;
        }
        if (summary.ValueKind == JsonValueKind.Array)
        {
            if (summary.GetArrayLength() != file.GetArrayLength()) return path;
            var index = 0;
            var fileItems = file.EnumerateArray();
            foreach (var entry in summary.EnumerateArray())
            {
                _ = fileItems.MoveNext();
                var difference = FirstDifference(entry, fileItems.Current, $"{path}[{index}]");
                if (difference is not null) return difference;
                index++;
            }
            return null;
        }
        if (summary.ValueKind == JsonValueKind.Number && summary.TryGetDecimal(out var left) &&
            file.TryGetDecimal(out var right)) return left == right ? null : path;
        return summary.ToString() == file.ToString() ? null : path;
    }

    private static LabSample[] Samples(JsonDocument doc, List<string> issues)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) { issues.Add("Timeline is not an array."); return []; }
        if (root.GetArrayLength() > LabLimits.MaxSamples) throw new InvalidDataException("Timeline sample limit exceeded.");
        var result = new List<LabSample>();
        foreach (var element in root.EnumerateArray())
        {
            var tick = Number(element, "tick");
            if (tick is null) { issues.Add("Timeline sample missing tick."); continue; }
            result.Add(new LabSample(tick.Value, Numeric(element, "people"), Numeric(element, "food"),
                NestedNumeric(element, "ecology", "herbivores"), NestedNumeric(element, "ecology", "predators"),
                NestedNumeric(element, "ecology", "activeFoodNodes")));
        }
        return result.ToArray();
    }

    private static LabItem[] Items(JsonDocument? doc, string idField, string kind, List<string> issues)
    {
        if (doc is null) return [];
        if (doc.RootElement.ValueKind != JsonValueKind.Array) { issues.Add($"{kind} document is not an array."); return []; }
        if (doc.RootElement.GetArrayLength() > LabLimits.MaxEvents) throw new InvalidDataException($"{kind} count exceeds limit.");
        var result = new List<LabItem>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add($"Malformed {kind} entry.");
                continue;
            }
            var classification = kind == "assertion" ? ClassifyAssertion(item, issues) :
                kind == "event" ? $"{Text(item, "kind")}/{Text(item, "severity")}" :
                    Text(item, "severity") == "UNKNOWN" ? kind : Text(item, "severity");
            result.Add(new LabItem(Text(item, idField), classification,
                Text(item, "message"), Text(item, "runKey") is "UNKNOWN" ? null : Text(item, "runKey")));
        }
        return result.ToArray();
    }

    private static string ClassifyAssertion(JsonElement item, List<string> issues)
    {
        if (!Property(item, "passed", out var passed) || passed.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !Property(item, "skipped", out var skipped) || skipped.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add($"Malformed assertion {Text(item, "invariantId")}: passed/skipped state UNKNOWN.");
            return "UNKNOWN";
        }
        return skipped.ValueKind == JsonValueKind.True ? "SKIP" :
            passed.ValueKind == JsonValueKind.True ? "PASS" : "FAIL";
    }

    private static JsonDocument? TryJson(string root, string dir, string relative, List<string> issues,
        ref long remaining, bool optional = false)
    {
        try
        {
            var path = Contained(root, dir, relative);
            if (!File.Exists(path))
            {
                if (!optional) issues.Add($"Missing {relative}.");
                return null;
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > LabLimits.MaxFileBytes) throw new InvalidDataException($"{relative} exceeds file limit.");
            if (stream.Length > remaining) throw new InvalidDataException("Bundle read budget exceeded.");
            remaining -= stream.Length;
            return JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            issues.Add($"Unreadable {relative}: {SafeError(ex)}");
            return null;
        }
    }

    private static string SafeError(Exception exception) => exception switch
    {
        InvalidDataException => exception.Message,
        JsonException => "Malformed JSON.",
        _ => "File access unavailable."
    };

    private static string Contained(string root, string dir, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidDataException("Absolute artifact path rejected.");
        var path = Path.GetFullPath(Path.Combine(dir, relative));
        if (!IsWithin(root, path) || !IsWithin(dir, path)) throw new InvalidDataException("Artifact path escapes selected root.");
        EnsureNoReparse(path);
        return path;
    }

    private static bool IsWithin(string parent, string child) => child.Equals(parent, PathComparison) ||
        child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, PathComparison);

    private static void EnsureNoReparse(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Reparse-point artifact path rejected.");
            }
            catch (FileNotFoundException) { /* A future path segment does not yet exist. */ }
            catch (DirectoryNotFoundException) { /* A future path segment does not yet exist. */ }
        }
    }

    private static bool SafeKey(string value) => value.Length is > 0 and <= 160 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');

    private static bool Property(JsonElement obj, string key, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out value)) return true;
        value = default;
        return false;
    }

    private static string Text(JsonElement obj, string key)
    {
        if (!Property(obj, key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "UNKNOWN";
        var text = value.ValueKind switch
        {
            JsonValueKind.True => "true", JsonValueKind.False => "false", _ => value.ToString()
        };
        return text.Length > 512 ? text[..512] + "…" : text;
    }

    private static string NestedText(JsonElement obj, string key)
    {
        var parts = key.Split('.');
        return parts.Length == 2 && Property(obj, parts[0], out var nested)
            ? Text(nested, parts[1]) : Text(obj, key);
    }

    private static int? Number(JsonElement obj, string key) => Property(obj, key, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    private static double? Numeric(JsonElement obj, string key) => Property(obj, key, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;

    private static double? NestedNumeric(JsonElement obj, string parent, string child) =>
        Property(obj, parent, out var nested) ? Numeric(nested, child) : null;
}
