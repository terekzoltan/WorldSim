using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed record RuntimeAiDecision(
    long Sequence,
    int ColonyId,
    int X,
    int Y,
    NpcCommand Command,
    Job Job,
    AiDecisionTrace Trace);
