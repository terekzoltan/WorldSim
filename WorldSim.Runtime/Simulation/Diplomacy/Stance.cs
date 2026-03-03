namespace WorldSim.Simulation.Diplomacy;

public enum Stance
{
    Neutral,
    Hostile,
    War
}

public readonly record struct FactionStanceState(Faction Left, Faction Right, Stance Stance);
