using System.Collections.Generic;

namespace WorldSim.AI;

public sealed class UtilityGoapBrain : INpcDecisionBrain
{
    private readonly GoalSelector _goalSelector = new();
    private readonly IPlanner _planner;
    private readonly IReadOnlyList<Goal> _goals;
    private readonly string _policyName;

    public UtilityGoapBrain(IPlanner planner, IReadOnlyList<Goal> goals, string policyName = "Default")
    {
        _planner = planner;
        _goals = goals;
        _policyName = policyName;
    }

    public AiDecisionResult Think(in NpcAiContext context)
    {
        var selection = _goalSelector.SelectGoal(_goals, _planner, context);
        if (selection.SelectedGoal == null)
        {
            var idleTrace = new AiDecisionTrace(
                SelectedGoal: "None",
                PlannerName: _planner.Name,
                PolicyName: _policyName,
                PlanLength: 0,
                PlanPreview: System.Array.Empty<NpcCommand>(),
                PlanCost: 0,
                ReplanReason: "NoGoal",
                MethodName: "None",
                GoalScores: selection.Scores);
            return new AiDecisionResult(NpcCommand.Idle, idleTrace);
        }

        var plannerDecision = _planner.GetNextCommand(context);
        var trace = new AiDecisionTrace(
            SelectedGoal: selection.SelectedGoal?.Name ?? "None",
            PlannerName: _planner.Name,
            PolicyName: _policyName,
            PlanLength: plannerDecision.PlanLength,
            PlanPreview: plannerDecision.PlanPreview,
            PlanCost: plannerDecision.PlanCost,
            ReplanReason: plannerDecision.ReplanReason,
            MethodName: plannerDecision.MethodName,
            GoalScores: selection.Scores);
        return new AiDecisionResult(plannerDecision.Command, trace);
    }
}
