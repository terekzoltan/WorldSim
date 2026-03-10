using WorldSim.AI;

namespace WorldSim.Simulation;

public sealed record RuntimeAiDecision(
    int WorldTick,
    long Sequence,
    int ActorId,
    int ColonyId,
    int X,
    int Y,
    NpcCommand Command,
    Job Job,
    AiDecisionTrace Trace);
