namespace WorldSim.Simulation;

public sealed class RuntimeBalanceOptions
{
    public float HungerPerSecond { get; set; } = 1.65f;
    public float StarvationDamageSevere { get; set; } = 2.6f;
    public float StarvationDamageLight { get; set; } = 1.2f;
    public float CriticalEatPreemptThreshold { get; set; } = 78f;
    public float EmergencyInstantEatThreshold { get; set; } = 96f;
    public float EatThresholdNormal { get; set; } = 62f;
    public float EatThresholdEmergency { get; set; } = 54f;
    public float SeekFoodThresholdNormal { get; set; } = 50f;
    public float SeekFoodThresholdEmergency { get; set; } = 42f;
    public float AgingTickDivisor { get; set; } = 90f;
}
