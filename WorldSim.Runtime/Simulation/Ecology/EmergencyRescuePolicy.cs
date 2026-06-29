namespace WorldSim.Simulation.Ecology;

public enum EmergencyRescuePolicy
{
    Disabled,
    Enabled
}

public enum EmergencyRescueReason
{
    None,
    HerbivoreFloor,
    PredatorExtinctWithPrey
}

public static class EmergencyRescuePolicyFormatter
{
    public static string ToWireValue(this EmergencyRescuePolicy policy)
        => policy switch
        {
            EmergencyRescuePolicy.Enabled => "enabled",
            _ => "disabled"
        };

    public static string ToWireValue(this EmergencyRescueReason reason)
        => reason switch
        {
            EmergencyRescueReason.HerbivoreFloor => "herbivore_floor",
            EmergencyRescueReason.PredatorExtinctWithPrey => "predator_extinct_with_prey",
            _ => "none"
        };

    public static EmergencyRescuePolicy ParsePolicyOrDisabled(string? value)
        => string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase)
            ? EmergencyRescuePolicy.Enabled
            : EmergencyRescuePolicy.Disabled;
}
