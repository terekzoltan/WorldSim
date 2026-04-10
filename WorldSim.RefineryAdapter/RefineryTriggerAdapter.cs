using WorldSim.RefineryAdapter.Integration;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter;

public sealed class RefineryTriggerAdapter
{
    private readonly RefineryPatchRuntime _runtime;

    public RefineryTriggerAdapter(string baseDirectory)
    {
        var options = RefineryRuntimeOptions.FromEnvironment(baseDirectory, applyOperatorPreset: false);
        _runtime = new RefineryPatchRuntime(options);

        var startupPreset = RefineryRuntimeOptions.ReadOperatorPresetFromEnvironment();
        if (startupPreset is not null)
            _runtime.ApplyOperatorPreset(startupPreset, source: "env");
    }

    public string LastStatus => _runtime.LastStatus;

    public DirectorExecutionStatus LastDirectorExecutionStatus => _runtime.LastDirectorExecutionStatus;

    public string CurrentOperatorProfileName => _runtime.OperatorProfileName;

    public string CurrentOperatorProfileSource => _runtime.OperatorProfileSource;

    public string CurrentIntegrationMode => _runtime.CurrentIntegrationMode;

    public string RequestedDirectorOutputMode => _runtime.RequestedDirectorOutputMode;

    public string RequestedDirectorOutputModeSource => _runtime.RequestedDirectorOutputModeSource;

    public string CycleDirectorOutputMode() => _runtime.CycleDirectorOutputMode();

    public string CycleOperatorPreset() => _runtime.CycleOperatorPreset();

    public void Trigger(SimulationRuntime runtime, long tick) => _runtime.Trigger(runtime, tick);

    public void Pump() => _runtime.Pump();
}
