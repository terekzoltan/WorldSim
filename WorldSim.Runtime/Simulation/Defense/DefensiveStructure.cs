namespace WorldSim.Simulation.Defense;

public enum DefensiveStructureKind
{
    WoodWall,
    StoneWall,
    ReinforcedWall,
    Gate,
    Watchtower,
    ArrowTower,
    CatapultTower
}

public abstract class DefensiveStructure
{
    protected DefensiveStructure(int id, Colony owner, (int x, int y) pos, float maxHp)
    {
        Id = id;
        Owner = owner;
        Pos = pos;
        MaxHp = maxHp;
        Hp = maxHp;
    }

    public int Id { get; }
    public Colony Owner { get; }
    public (int x, int y) Pos { get; }
    public float Hp { get; private set; }
    public float MaxHp { get; }
    public bool IsDestroyed => Hp <= 0f;
    public bool IsActive { get; set; } = true;
    public abstract DefensiveStructureKind Kind { get; }
    public virtual int UpkeepWoodPerTick => 0;
    public virtual int UpkeepStonePerTick => 0;
    public virtual int UpkeepGoldPerTick => 0;

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || IsDestroyed)
            return;

        Hp = Math.Max(0f, Hp - amount);
    }
}

public sealed class WoodWallSegment : DefensiveStructure
{
    public const float DefaultHp = 120f;

    public WoodWallSegment(int id, Colony owner, (int x, int y) pos)
        : base(id, owner, pos, DefaultHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.WoodWall;
}

public sealed class Watchtower : DefensiveStructure
{
    public const float DefaultHp = 180f;

    public Watchtower(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.Watchtower;

    public int RangeTiles { get; init; } = 5;
    public float ShotDamage { get; init; } = 14f;
    public float CooldownSeconds { get; init; } = 0.5f;
    public float CooldownRemainingSeconds { get; set; }
}

public sealed class StoneWallSegment : DefensiveStructure
{
    public const float DefaultHp = 180f;

    public StoneWallSegment(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.StoneWall;
}

public sealed class ReinforcedWallSegment : DefensiveStructure
{
    public const float DefaultHp = 260f;

    public ReinforcedWallSegment(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.ReinforcedWall;
}

public sealed class GateStructure : DefensiveStructure
{
    public const float DefaultHp = 220f;

    public GateStructure(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.Gate;
    public bool IsOpen { get; set; }
}

public sealed class ArrowTower : DefensiveStructure
{
    public const float DefaultHp = 220f;

    public ArrowTower(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.ArrowTower;
    public override int UpkeepWoodPerTick => 1;
    public int RangeTiles { get; init; } = 6;
    public float ShotDamage { get; init; } = 18f;
    public float CooldownSeconds { get; init; } = 0.6f;
    public float CooldownRemainingSeconds { get; set; }
}

public sealed class CatapultTower : DefensiveStructure
{
    public const float DefaultHp = 260f;

    public CatapultTower(int id, Colony owner, (int x, int y) pos, float maxHp = DefaultHp)
        : base(id, owner, pos, maxHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.CatapultTower;
    public override int UpkeepWoodPerTick => 1;
    public override int UpkeepStonePerTick => 1;
    public int RangeTiles { get; init; } = 7;
    public int SplashRadius { get; init; } = 1;
    public float ShotDamage { get; init; } = 22f;
    public float CooldownSeconds { get; init; } = 0.9f;
    public float CooldownRemainingSeconds { get; set; }
}
