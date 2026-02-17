namespace WorldSim.Simulation;

public interface INpcBrain
{
    Job Think(Person actor, World world);
}
