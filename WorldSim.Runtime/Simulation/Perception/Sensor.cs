namespace WorldSim.Simulation;

public abstract class Sensor
{
    public abstract void Sense(World world, Person person, Blackboard blackboard);
}
