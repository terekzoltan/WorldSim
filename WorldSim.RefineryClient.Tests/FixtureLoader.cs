using System.Text;

namespace WorldSim.RefineryClient.Tests;

internal static class FixtureLoader
{
    public static string Read(string relativePath)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
