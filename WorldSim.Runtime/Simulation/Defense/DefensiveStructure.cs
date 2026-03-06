namespace WorldSim.Simulation.Defense;

public enum DefensiveStructureKind
{
    WoodWall,
    Watchtower
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
    public abstract DefensiveStructureKind Kind { get; }

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

    public Watchtower(int id, Colony owner, (int x, int y) pos)
        : base(id, owner, pos, DefaultHp)
    {
    }

    public override DefensiveStructureKind Kind => DefensiveStructureKind.Watchtower;

    public int RangeTiles { get; init; } = 5;
    public float ShotDamage { get; init; } = 14f;
    public float CooldownSeconds { get; init; } = 0.5f;
    public float CooldownRemainingSeconds { get; set; }
}
