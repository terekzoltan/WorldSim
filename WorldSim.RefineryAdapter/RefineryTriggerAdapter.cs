using WorldSim.Integration;
using WorldSim.Simulation;

namespace WorldSim.RefineryAdapter;

public sealed class RefineryTriggerAdapter
{
    private readonly RefineryPatchRuntime _runtime;

    public RefineryTriggerAdapter(string baseDirectory)
    {
        var options = RefineryRuntimeOptions.FromEnvironment(baseDirectory);
        _runtime = new RefineryPatchRuntime(options);
    }

    public string LastStatus => _runtime.LastStatus;

    public void Trigger(World world, long tick) => _runtime.Trigger(world, tick);

    public void Pump() => _runtime.Pump();
}
