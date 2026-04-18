namespace WorldSim.Runtime.Profiles;

public readonly record struct LowCostProfileResolution(
    string Requested,
    LowCostProfileLane Effective,
    string Source);
