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

        Assert.Contains("WorldSim.AI", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.RefineryClient", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.Graphics", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.App", runtimeCsproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProject_DoesNotReferenceRefineryClientDirectly()
    {
        var appCsproj = Read("WorldSim.App/WorldSim.App.csproj");

        Assert.DoesNotContain("WorldSim.RefineryClient", appCsproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdapterProject_ReferencesOnlyExpectedCoreProjects()
    {
        var adapterCsproj = Read("WorldSim.RefineryAdapter/WorldSim.RefineryAdapter.csproj");

        Assert.Contains("WorldSim.Contracts", adapterCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorldSim.Runtime", adapterCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WorldSim.RefineryClient", adapterCsproj, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("WorldSim.App", adapterCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.Graphics", adapterCsproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefineryClientProject_ReferencesContracts()
    {
        var refineryClientCsproj = Read("WorldSim.RefineryClient/WorldSim.RefineryClient.csproj");

        Assert.Contains("WorldSim.Contracts", refineryClientCsproj, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AiProject_DoesNotReferenceForbiddenProjects()
    {
        var aiCsproj = Read("WorldSim.AI/WorldSim.AI.csproj");

        Assert.DoesNotContain("WorldSim.Runtime", aiCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.Graphics", aiCsproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorldSim.App", aiCsproj, StringComparison.OrdinalIgnoreCase);
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
