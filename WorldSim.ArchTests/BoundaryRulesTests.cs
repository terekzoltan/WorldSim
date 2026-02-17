using System;
using System.IO;
using Xunit;

namespace WorldSim.ArchTests;

public class BoundaryRulesTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void RuntimeProject_DoesNotReferenceForbiddenProjects()
    {
        var runtimeCsproj = Read("WorldSim.Runtime/WorldSim.Runtime.csproj");

        Assert.DoesNotContain("WorldSim.RefineryClient", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.Graphics", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.App", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GraphicsProject_DoesNotUseSimulationNamespace()
    {
        var graphicsDir = Path.Combine(RepoRoot, "WorldSim.Graphics");
        var files = Directory.GetFiles(graphicsDir, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("using WorldSim.Simulation;", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AppGameHost_DoesNotUseDirectWorldOrTechTreeMutation()
    {
        var gameHost = Read("WorldSim.App/Game1.cs");

        Assert.DoesNotContain("_world.", gameHost, StringComparison.Ordinal);
        Assert.DoesNotContain("TechTree.", gameHost, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath));
}
