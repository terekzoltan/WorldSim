namespace WorldSim.Simulation;

public sealed class SimplePlanner : IPlanner
{
    private Goal? _current;

    public void SetGoal(Goal goal) => _current = goal;

    public Job GetNextJob(Person p, World w)
    {
        if (_current == null) return Job.Idle;

        // Minimal mapping for now
        switch (_current.Name)
        {
            case "GatherWood": return Job.GatherWood;
            case "GatherStone": return Job.GatherStone;
            case "BuildHouse":
                if (p.Home.Stock[Resource.Wood] >= p.Home.HouseWoodCost)
                    return Job.BuildHouse;
                return Job.GatherWood;
            default: return Job.Idle;
        }
    }
}