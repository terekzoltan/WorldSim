using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;

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

    public DirectorExecutionStatus LastDirectorExecutionStatus => _runtime.LastDirectorExecutionStatus;

    public void Trigger(SimulationRuntime runtime, long tick) => _runtime.Trigger(runtime, tick);

    public void Pump() => _runtime.Pump();
}
