using WorldSim.SmrLab.Artifacts;

namespace WorldSim.SmrLab.Tests;

public sealed class LabStartupTests
{
    private static string CurrentBuildAssembly()
    {
        var target = new DirectoryInfo(AppContext.BaseDirectory);
        return Path.Combine(ArtifactStoreTests.Workspace(), "WorldSim.SmrLab", "bin",
            target.Parent!.Name, target.Name, "WorldSim.SmrLab.dll");
    }

    [Fact]
    public void CurrentCheckout_InfersProjectBuildLocationAndOpensOnlyExplicitFixtures()
    {
        var workspace = ArtifactStoreTests.Workspace();
        Assert.Equal(workspace, LabStartup.ValidateWorkspace(workspace, CurrentBuildAssembly()));
        var store = new ArtifactStore(workspace,
        [
            "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-001",
            "C:/EGYETEM/FUNSTUFF/WorldSim/.artifacts/smr/all-around-smoke-wave10-001"
        ]);
        Assert.Equal(new[] { 27, 9 }, store.ListBundles().Select(bundle => bundle.Runs.Count));
    }

    [Fact]
    public void MovedCheckout_InfersDifferentRootFromSameProjectBuildLayout()
    {
        using var fixture = new ArtifactStoreTests.SyntheticFixture();
        var moved = fixture.CreateDirectory("relocated-checkout");
        Assert.NotEqual(ArtifactStoreTests.Workspace(), moved);
        File.WriteAllText(Path.Combine(moved, "WorldSim.sln"), "synthetic layout marker");
        var project = Path.Combine(moved, "WorldSim.SmrLab");
        System.IO.Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "WorldSim.SmrLab.csproj"), "synthetic layout marker");
        var build = Path.Combine(project, "bin", "Debug", "net8.0");
        System.IO.Directory.CreateDirectory(build);
        var assembly = Path.Combine(build, "WorldSim.SmrLab.dll");
        File.WriteAllText(assembly, "synthetic layout marker");

        Assert.Equal(moved, LabStartup.ValidateWorkspace(moved, assembly));
        Assert.Throws<InvalidOperationException>(() =>
            LabStartup.ValidateWorkspace(ArtifactStoreTests.Workspace(), assembly));
        Assert.Throws<InvalidOperationException>(() =>
            LabStartup.ValidateWorkspace("C:/EGYETEM/FUNSTUFF/WorldSim", assembly));
        var wrongProject = fixture.CreateDirectory("another-project");
        Assert.Throws<InvalidOperationException>(() => LabStartup.ValidateWorkspace(wrongProject, assembly));
        var lookalikeBuild = Path.Combine(moved, "OtherProject", "bin", "Debug", "net8.0");
        System.IO.Directory.CreateDirectory(lookalikeBuild);
        var lookalikeAssembly = Path.Combine(lookalikeBuild, "WorldSim.SmrLab.dll");
        File.WriteAllText(lookalikeAssembly, "not the Lab project build");
        Assert.Throws<InvalidOperationException>(() => LabStartup.ValidateWorkspace(moved, lookalikeAssembly));
    }

    [Theory]
    [InlineData("C:/EGYETEM/FUNSTUFF/WorldSim")]
    [InlineData("C:/Users/ASUS/AppData/Local/Temp/opencode")]
    public void HomeOrForeignCwd_IsRejectedAgainstActualBuild(string path)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            LabStartup.ValidateWorkspace(path, CurrentBuildAssembly()));
        Assert.Contains("own inferred checkout", error.Message);
    }
}
