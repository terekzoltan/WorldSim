using WorldSim.SmrLab;
using WorldSim.SmrLab.Artifacts;
using WorldSim.SmrLab.ManualRuns;

// Offline post-walkthrough parity inspection. This path launches neither a host
// nor a simulation and does not accept input from a browser.
if (args.Length == 4 && args[0] == "--parity-lab" && args[2] == "--parity-cli")
{
    var checkout = LabStartup.ValidateWorkspace(Directory.GetCurrentDirectory(), typeof(LabHost).Assembly.Location);
    var allowed = Path.Combine(checkout, ".artifacts", "smr") + Path.DirectorySeparatorChar;
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    var labPath = Path.GetFullPath(args[1]);
    var cliPath = Path.GetFullPath(args[3]);
    if (!labPath.StartsWith(allowed, comparison) || !cliPath.StartsWith(allowed, comparison))
        throw new ArgumentException("Offline parity bundles must belong to this worktree's .artifacts/smr root.");
    var differences = ManualParity.Compare(labPath, cliPath);
    foreach (var difference in differences) Console.WriteLine(difference);
    Console.WriteLine(differences.Count == 0 ? "Semantic parity: MATCH" : $"Semantic parity: {differences.Count} differences");
    Environment.ExitCode = differences.Count == 0 ? 0 : 1;
    return;
}

var roots = new List<string>();
var port = 5217;
string? runRoot = null, runnerDll = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length) roots.Add(args[++i]);
    else if (args[i] == "--run-root" && i + 1 < args.Length) runRoot = args[++i];
    else if (args[i] == "--runner-dll" && i + 1 < args.Length) runnerDll = args[++i];
    else if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var parsed)) port = parsed;
    else throw new ArgumentException("Usage (from the Lab checkout root, after a Debug build): dotnet WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll --root ABSOLUTE_PATH [--root ABSOLUTE_PATH] [--run-root ABSOLUTE_PATH --runner-dll ABSOLUTE_PATH] [--port 5217]");
}

// Reject a foreign CWD before opening any artifact root. Roots are only the explicit --root arguments.
var workspace = LabStartup.ValidateWorkspace(Directory.GetCurrentDirectory(), typeof(LabHost).Assembly.Location);
if ((runRoot is null) != (runnerDll is null))
    throw new ArgumentException("Select both --run-root and --runner-dll to enable manual runs.");
ManualRunManager? manual = null;
if (runRoot is not null)
{
    ManualRunManager.ValidateReadRoots(runRoot, roots);
    manual = new ManualRunManager(workspace, runRoot, runnerDll!);
    roots.Add(manual.PublishedRoot);
}
var store = new ArtifactStore(workspace, roots);
manual?.AttachStore(store);
using (manual)
    await LabHost.Create(store, port, manual).RunAsync();
