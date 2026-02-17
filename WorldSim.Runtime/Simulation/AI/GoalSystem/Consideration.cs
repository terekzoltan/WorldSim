namespace WorldSim.Simulation;

public abstract class Consideration
{
    // Returns 0..1 where 1 = strongly preferred
    public abstract float Evaluate(Person p, World w);
}