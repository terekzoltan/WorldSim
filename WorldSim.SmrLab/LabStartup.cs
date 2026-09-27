namespace WorldSim.SmrLab;

// Derive authority from the actually launched Lab project build, never the process CWD.
public static class LabStartup
{
    public static string ValidateWorkspace(string launchDirectory, string assemblyLocation)
    {
        var assembly = Path.GetFullPath(assemblyLocation);
        if (Path.GetFileName(assembly) != "WorldSim.SmrLab.dll" || !File.Exists(assembly))
            throw new InvalidOperationException("SMR Lab build location is unavailable.");

        // Supported local layout: checkout/WorldSim.SmrLab/bin/{configuration}/{target}/WorldSim.SmrLab.dll.
        var target = new FileInfo(assembly).Directory;
        var bin = target?.Parent?.Parent;
        var project = bin?.Parent;
        var checkout = project?.Parent;
        if (target is null || bin?.Name != "bin" || project?.Name != "WorldSim.SmrLab" || checkout is null ||
            !File.Exists(Path.Combine(project.FullName, "WorldSim.SmrLab.csproj")) ||
            !File.Exists(Path.Combine(checkout.FullName, "WorldSim.sln")))
            throw new InvalidOperationException("SMR Lab project/build checkout layout is unavailable.");

        var expected = Path.GetFullPath(checkout.FullName);
        var selected = Path.GetFullPath(launchDirectory);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.TrimEndingDirectorySeparator(selected).Equals(Path.TrimEndingDirectorySeparator(expected), comparison))
            throw new InvalidOperationException("SMR Lab must launch from its own inferred checkout.");
        return expected;
    }
}
