namespace WorldSim.Simulation;

public interface IPlanner
{
    void SetGoal(Goal goal);
    Job GetNextJob(Person p, World w);
}
