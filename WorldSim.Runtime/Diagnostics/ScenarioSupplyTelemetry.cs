namespace WorldSim.Runtime.Diagnostics;

public sealed record ScenarioSupplyTimelineSnapshot(
    int InventoryFoodConsumed,
    int CarriersWithFood,
    int TotalCarriedFood,
    float AvgInventoryUsedSlots,
    float AvgInventoryCapacitySlots)
{
    public static ScenarioSupplyTimelineSnapshot Empty { get; } = new(
        InventoryFoodConsumed: 0,
        CarriersWithFood: 0,
        TotalCarriedFood: 0,
        AvgInventoryUsedSlots: 0f,
        AvgInventoryCapacitySlots: 0f);
}

public sealed record ScenarioSupplyTelemetrySnapshot(
    int InventoryFoodConsumed,
    int CarriersWithFood,
    int TotalCarriedFood,
    float AvgInventoryUsedSlots,
    float AvgInventoryCapacitySlots,
    int ColoniesWithBackpacks,
    int ColoniesWithRationing)
{
    public static ScenarioSupplyTelemetrySnapshot Empty { get; } = new(
        InventoryFoodConsumed: 0,
        CarriersWithFood: 0,
        TotalCarriedFood: 0,
        AvgInventoryUsedSlots: 0f,
        AvgInventoryCapacitySlots: 0f,
        ColoniesWithBackpacks: 0,
        ColoniesWithRationing: 0);

    public ScenarioSupplyTimelineSnapshot ToTimelineSnapshot()
        => new(
            InventoryFoodConsumed,
            CarriersWithFood,
            TotalCarriedFood,
            AvgInventoryUsedSlots,
            AvgInventoryCapacitySlots);
}
