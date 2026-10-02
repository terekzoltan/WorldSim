using System.Text.Json;

namespace WorldSim.SmrLab.ManualRuns;

/// <summary>Offline semantic comparison only. Never starts ScenarioRunner or accepts browser paths.</summary>
public static class ManualParity
{
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "generatedAtUtc", "runId", "artifactDir", "perfAvgTickMs", "perfMaxTickMs",
        "perfP99TickMs", "perfPeakEntities", "perfTickMs"
    };

    public static IReadOnlyList<string> Compare(string labBundle, string controlBundle)
    {
        EnsureNoReparse(labBundle);
        EnsureNoReparse(controlBundle);
        var differences = new List<string>();
        foreach (var file in new[] { "manifest.json", "summary.json", "assertions.json", "anomalies.json" })
        {
            var left = Path.Combine(labBundle, file);
            var right = Path.Combine(controlBundle, file);
            if (!File.Exists(left) || !File.Exists(right))
            {
                differences.Add($"{file}: missing on {(File.Exists(left) ? "CLI" : "Lab")} side");
                continue;
            }
            EnsureNoReparse(left); EnsureNoReparse(right);
            using var a = JsonDocument.Parse(File.ReadAllText(left));
            using var b = JsonDocument.Parse(File.ReadAllText(right));
            CompareElements(a.RootElement, b.RootElement, file, differences);
        }
        foreach (var group in new[] { "runs", "drilldown" })
        {
            var leftRoot = Path.Combine(labBundle, group);
            var rightRoot = Path.Combine(controlBundle, group);
            if (!Directory.Exists(leftRoot) && !Directory.Exists(rightRoot)) continue;
            if (!Directory.Exists(leftRoot) || !Directory.Exists(rightRoot))
            { differences.Add($"{group}/: missing on one side"); continue; }
            EnsureNoReparse(leftRoot); EnsureNoReparse(rightRoot);
            var filenames = SafeJsonFiles(leftRoot)
                .Select(path => Path.GetRelativePath(leftRoot, path))
                .Concat(SafeJsonFiles(rightRoot)
                    .Select(path => Path.GetRelativePath(rightRoot, path)))
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
            foreach (var filename in filenames)
            {
                var left = Path.Combine(leftRoot, filename);
                var right = Path.Combine(rightRoot, filename);
                if (!File.Exists(left) || !File.Exists(right)) { differences.Add($"{group}/{filename}: missing on one side"); continue; }
                EnsureNoReparse(left); EnsureNoReparse(right);
                using var a = JsonDocument.Parse(File.ReadAllText(left));
                using var b = JsonDocument.Parse(File.ReadAllText(right));
                CompareElements(a.RootElement, b.RootElement, $"{group}/{filename}", differences);
            }
        }
        return differences;
    }

    private static void EnsureNoReparse(string path)
    {
        var current = path;
        while (current is not null)
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Offline parity does not follow reparse points.");
            current = Path.GetDirectoryName(current);
        }
    }

    private static IEnumerable<string> SafeJsonFiles(string root)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count > 0)
        {
            var directory = directories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Offline parity does not follow reparse points.");
                if ((attributes & FileAttributes.Directory) != 0) directories.Push(entry);
                else if (entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) yield return entry;
            }
        }
    }

    private static void CompareElements(JsonElement left, JsonElement right, string path, List<string> differences)
    {
        if (differences.Count >= 32) return;
        if (left.ValueKind != right.ValueKind) { differences.Add($"{path}: JSON type differs"); return; }
        if (left.ValueKind == JsonValueKind.Object)
        {
            var names = left.EnumerateObject().Select(p => p.Name).Concat(right.EnumerateObject().Select(p => p.Name))
                .Where(name => !Ignored.Contains(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (!left.TryGetProperty(name, out var a) || !right.TryGetProperty(name, out var b))
                    differences.Add($"{path}.{name}: absent on one side");
                else CompareElements(a, b, $"{path}.{name}", differences);
                if (differences.Count >= 32) return;
            }
        }
        else if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength())
                differences.Add($"{path}: count {left.GetArrayLength()} versus {right.GetArrayLength()}");
            else for (var i = 0; i < left.GetArrayLength(); i++)
                CompareElements(left[i], right[i], $"{path}[{i}]", differences);
        }
        else if (left.ValueKind == JsonValueKind.Number && left.TryGetDecimal(out var a) && right.TryGetDecimal(out var b))
        {
            if (a != b) differences.Add($"{path}: {a} versus {b}");
        }
        else if (left.ToString() != right.ToString())
            differences.Add($"{path}: {left} versus {right}");
    }
}
