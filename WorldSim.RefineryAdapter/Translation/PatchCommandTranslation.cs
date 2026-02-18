using WorldSim.Contracts.V1;
using WorldSim.Runtime;

namespace WorldSim.RefineryAdapter.Translation;

public abstract record RuntimePatchCommand;

public sealed record UnlockTechCommand(string TechId) : RuntimePatchCommand;

public sealed class PatchCommandTranslator
{
    public IReadOnlyList<RuntimePatchCommand> Translate(PatchResponse response)
    {
        var commands = new List<RuntimePatchCommand>(response.Patch.Count);

        foreach (var op in response.Patch)
        {
            switch (op)
            {
                case AddTechOp addTech:
                    commands.Add(new UnlockTechCommand(addTech.TechId));
                    break;
                default:
                    throw new NotSupportedException(
                        $"Adapter supports only addTech currently. Unsupported op: {op.GetType().Name}"
                    );
            }
        }

        return commands;
    }
}

public sealed class RuntimePatchCommandExecutor
{
    public void Execute(SimulationRuntime runtime, IReadOnlyList<RuntimePatchCommand> commands)
    {
        foreach (var command in commands)
        {
            switch (command)
            {
                case UnlockTechCommand unlockTech:
                    if (!runtime.IsKnownTech(unlockTech.TechId))
                    {
                        throw new InvalidOperationException(
                            "Cannot apply addTech: unknown techId '" + unlockTech.TechId +
                            "'. TODO: add Java->C# tech ID mapping layer for cross-project IDs."
                        );
                    }

                    runtime.UnlockTechForPrimaryColony(unlockTech.TechId);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported runtime patch command: {command.GetType().Name}"
                    );
            }
        }
    }
}
