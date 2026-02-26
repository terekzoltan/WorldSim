namespace WorldSim.Simulation;

[System.Flags]
public enum PersonRole
{
    None = 0,
    Warrior = 1 << 0,
    SupplyCarrier = 1 << 1,
    Scout = 1 << 2,
    Commander = 1 << 3
}
