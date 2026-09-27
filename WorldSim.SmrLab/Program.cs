using WorldSim.SmrLab;
using WorldSim.SmrLab.Artifacts;

var roots = new List<string>();
var port = 5217;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--root" && i + 1 < args.Length) roots.Add(args[++i]);
    else if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var parsed)) port = parsed;
    else throw new ArgumentException("Usage (from the Lab checkout root, after a Debug build): dotnet WorldSim.SmrLab/bin/Debug/net8.0/WorldSim.SmrLab.dll --root ABSOLUTE_PATH [--root ABSOLUTE_PATH] [--port 5217]");
}

// Reject a foreign CWD before opening any artifact root. Roots are only the explicit --root arguments.
var workspace = LabStartup.ValidateWorkspace(Directory.GetCurrentDirectory(), typeof(LabHost).Assembly.Location);
var store = new ArtifactStore(workspace, roots);
await LabHost.Create(store, port).RunAsync();
