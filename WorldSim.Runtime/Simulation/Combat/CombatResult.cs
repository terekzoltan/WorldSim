namespace WorldSim.Simulation.Combat;

public sealed record CombatResult(
    bool AttackerWon,
    float DamageDealt,
    float DamageReceived,
    string Outcome);
