namespace WorldSim.AI;

public interface IPlanner<TGoal, TActor, TWorld, TCommand>
{
    void SetGoal(TGoal goal);
    TCommand GetNextCommand(TActor actor, TWorld world);
}

public interface INpcBrain<TActor, TWorld, TCommand>
{
    TCommand Think(TActor actor, TWorld world);
}
