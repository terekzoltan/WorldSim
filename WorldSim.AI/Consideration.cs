namespace WorldSim.AI;

public abstract class Consideration
{
    public abstract float Evaluate(in NpcAiContext context);
}
