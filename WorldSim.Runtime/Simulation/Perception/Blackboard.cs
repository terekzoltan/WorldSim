using System.Collections.Generic;

namespace WorldSim.Simulation;

public class Blackboard
{
    public List<FactualEvent> FactualEvents { get; } = new();

    public void Add(FactualEvent factualEvent) => FactualEvents.Add(factualEvent);
    public void Clear() => FactualEvents.Clear();
}
