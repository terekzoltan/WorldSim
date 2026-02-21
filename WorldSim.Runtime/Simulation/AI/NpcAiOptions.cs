namespace WorldSim.Simulation;

public enum NpcPlannerMode
{
    Goap,
    Simple,
    Htn
}

public enum NpcPolicyMode
{
    GlobalPlanner,
    FactionMix,
    HtnPilot
}

public sealed class RuntimeAiOptions
{
    public NpcPlannerMode PlannerMode { get; init; } = NpcPlannerMode.Goap;
    public NpcPolicyMode PolicyMode { get; init; } = NpcPolicyMode.GlobalPlanner;

    public static RuntimeAiOptions FromEnvironment()
    {
        var plannerValue = System.Environment.GetEnvironmentVariable("WORLDSIM_AI_PLANNER");
        var policyValue = System.Environment.GetEnvironmentVariable("WORLDSIM_AI_POLICY");

        var plannerMode = NpcPlannerMode.Goap;
        if (string.Equals(plannerValue, "simple", System.StringComparison.OrdinalIgnoreCase))
            plannerMode = NpcPlannerMode.Simple;
        else if (string.Equals(plannerValue, "htn", System.StringComparison.OrdinalIgnoreCase))
            plannerMode = NpcPlannerMode.Htn;

        var policyMode = NpcPolicyMode.GlobalPlanner;
        if (string.Equals(policyValue, "faction-mix", System.StringComparison.OrdinalIgnoreCase))
            policyMode = NpcPolicyMode.FactionMix;
        else if (string.Equals(policyValue, "htn-pilot", System.StringComparison.OrdinalIgnoreCase))
            policyMode = NpcPolicyMode.HtnPilot;

        if (policyMode == NpcPolicyMode.HtnPilot)
            plannerMode = NpcPlannerMode.Htn;

        return new RuntimeAiOptions
        {
            PlannerMode = plannerMode,
            PolicyMode = policyMode
        };
    }
}
