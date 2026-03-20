using WorldSim.Simulation.Defense;

namespace WorldSim.Simulation.Combat;

internal sealed class RuntimeSiegeSession
{
    public RuntimeSiegeSession(int siegeId, int attackerColonyId, int defenderColonyId, int startedTick)
    {
        SiegeId = siegeId;
        AttackerColonyId = attackerColonyId;
        DefenderColonyId = defenderColonyId;
        StartedTick = startedTick;
        LastActiveTick = startedTick;
    }

    public int SiegeId { get; }
    public int AttackerColonyId { get; }
    public int DefenderColonyId { get; }
    public int StartedTick { get; }
    public int LastActiveTick { get; set; }
    public int ActiveAttackerCount { get; set; }
    public int BreachCount { get; set; }
}

internal sealed class RuntimeSiegeState
{
    public RuntimeSiegeState(
        int siegeId,
        int attackerColonyId,
        int defenderColonyId,
        int targetStructureId,
        DefensiveStructureKind targetKind,
        (int x, int y) center,
        int activeAttackerCount,
        int startedTick,
        int lastActiveTick,
        int breachCount,
        string status)
    {
        SiegeId = siegeId;
        AttackerColonyId = attackerColonyId;
        DefenderColonyId = defenderColonyId;
        TargetStructureId = targetStructureId;
        TargetKind = targetKind;
        Center = center;
        ActiveAttackerCount = activeAttackerCount;
        StartedTick = startedTick;
        LastActiveTick = lastActiveTick;
        BreachCount = breachCount;
        Status = status;
    }

    public int SiegeId { get; }
    public int AttackerColonyId { get; }
    public int DefenderColonyId { get; }
    public int TargetStructureId { get; }
    public DefensiveStructureKind TargetKind { get; }
    public (int x, int y) Center { get; }
    public int ActiveAttackerCount { get; }
    public int StartedTick { get; }
    public int LastActiveTick { get; }
    public int BreachCount { get; }
    public string Status { get; }
}

internal sealed class RuntimeBreachState
{
    public RuntimeBreachState(
        int structureId,
        int defenderColonyId,
        int attackerColonyId,
        (int x, int y) pos,
        int createdTick,
        DefensiveStructureKind structureKind)
    {
        StructureId = structureId;
        DefenderColonyId = defenderColonyId;
        AttackerColonyId = attackerColonyId;
        Pos = pos;
        CreatedTick = createdTick;
        StructureKind = structureKind;
    }

    public int StructureId { get; }
    public int DefenderColonyId { get; }
    public int AttackerColonyId { get; }
    public (int x, int y) Pos { get; }
    public int CreatedTick { get; }
    public DefensiveStructureKind StructureKind { get; }
}

internal readonly record struct RuntimeSiegePressure(
    int attackerColonyId,
    int defenderColonyId,
    int targetStructureId,
    DefensiveStructureKind targetKind,
    (int x, int y) targetPos);
