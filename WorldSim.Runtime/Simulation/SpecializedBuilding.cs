namespace WorldSim.Simulation;

public enum SpecializedBuildingKind
{
    FarmPlot,
    Workshop,
    Storehouse
}

public sealed class SpecializedBuilding
{
    public Colony Owner { get; }
    public (int x, int y) Pos { get; }
    public SpecializedBuildingKind Kind { get; }

    public SpecializedBuilding(Colony owner, (int x, int y) pos, SpecializedBuildingKind kind)
    {
        Owner = owner;
        Pos = pos;
        Kind = kind;
    }
}
