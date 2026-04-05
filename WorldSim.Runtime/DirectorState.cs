using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Runtime;

public enum DirectorBeatSeverity
{
    Minor,
    Major,
    Epic
}

public readonly record struct ActiveBeatState(
    string BeatId,
    string Text,
    DirectorBeatSeverity Severity,
    int RemainingTicks,
    int TotalTicks);

public readonly record struct ActiveDirectiveState(
    int ColonyId,
    string Directive,
    int RemainingTicks,
    int TotalTicks);

public readonly record struct DirectorCausalConditionSpec(
    string Metric,
    string Operator,
    double Threshold);

public readonly record struct DirectorFollowUpBeatSpec(
    string BeatId,
    string Text,
    long DurationTicks,
    IReadOnlyList<DirectorDomainModifierSpec> Effects);

public readonly record struct DirectorCausalChainSpec(
    DirectorCausalConditionSpec Condition,
    DirectorFollowUpBeatSpec FollowUpBeat,
    int WindowTicks,
    int MaxTriggers = 1);

public readonly record struct PendingCausalChainState(
    string ParentBeatId,
    string Status,
    string ConditionMetric,
    string ConditionOperator,
    double ConditionThreshold,
    string ConditionSummary,
    string FollowUpBeatId,
    string FollowUpSummary,
    int RemainingWindowTicks,
    int TriggerCount,
    int MaxTriggers,
    long EligibleFromTick,
    string LastFailureMessage);

public readonly record struct PendingCausalChainTrigger(
    string ParentBeatId,
    DirectorFollowUpBeatSpec FollowUpBeat);

public readonly record struct DirectorApplyResult(bool Success, string Message)
{
    public static DirectorApplyResult Ok(string message) => new(true, message);
    public static DirectorApplyResult Fail(string message) => new(false, message);
}

public sealed class DirectorState
{
    public const int MajorBeatCooldownTicks = 20;
    public const int EpicBeatCooldownTicks = 40;

    private readonly Dictionary<string, ActiveBeatMutable> _beats = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ActiveDirectiveMutable> _directives = new();
    private readonly Dictionary<string, PendingCausalChainMutable> _pendingCausalChains = new(StringComparer.Ordinal);

    public int MajorBeatCooldownRemainingTicks { get; private set; }
    public int EpicBeatCooldownRemainingTicks { get; private set; }
    public double MaxInfluenceBudget { get; private set; } = 5d;
    public double RemainingInfluenceBudget { get; private set; } = 5d;
    public double LastCheckpointBudgetUsed { get; private set; }
    public long LastBudgetCheckpointTick { get; private set; } = -1;
    public bool HasBudgetData { get; private set; }

    public IReadOnlyList<ActiveBeatState> ActiveBeats => _beats.Values
        .Select(beat => beat.ToState())
        .OrderByDescending(beat => beat.RemainingTicks)
        .ToList();

    public IReadOnlyList<ActiveDirectiveState> ActiveDirectives => _directives.Values
        .Select(directive => directive.ToState())
        .OrderBy(directive => directive.ColonyId)
        .ToList();

    public IReadOnlyList<PendingCausalChainState> PendingCausalChains => _pendingCausalChains.Values
        .Select(chain => chain.ToState())
        .OrderBy(chain => chain.ParentBeatId, StringComparer.Ordinal)
        .ToList();

    public int BeatCooldownRemainingTicks => Math.Max(MajorBeatCooldownRemainingTicks, EpicBeatCooldownRemainingTicks);

    public void BeginCheckpointBudget(long tick, double maxBudget)
    {
        var normalizedMaxBudget = NormalizeBudget(maxBudget, fallback: 5d);
        MaxInfluenceBudget = normalizedMaxBudget;
        RemainingInfluenceBudget = normalizedMaxBudget;
        LastCheckpointBudgetUsed = 0d;
        LastBudgetCheckpointTick = tick;
        HasBudgetData = true;
    }

    public void ApplyCheckpointBudgetUsed(long tick, double budgetUsed)
    {
        if (!HasBudgetData)
            BeginCheckpointBudget(tick, MaxInfluenceBudget);

        var normalizedBudgetUsed = NormalizeBudget(budgetUsed, fallback: 0d);
        LastCheckpointBudgetUsed = Math.Clamp(normalizedBudgetUsed, 0d, MaxInfluenceBudget);
        RemainingInfluenceBudget = Math.Max(0d, MaxInfluenceBudget - LastCheckpointBudgetUsed);
        LastBudgetCheckpointTick = tick;
    }

    public void Tick()
    {
        if (MajorBeatCooldownRemainingTicks > 0)
            MajorBeatCooldownRemainingTicks--;
        if (EpicBeatCooldownRemainingTicks > 0)
            EpicBeatCooldownRemainingTicks--;

        foreach (var beat in _beats.Values.ToList())
        {
            beat.RemainingTicks--;
            if (beat.RemainingTicks <= 0)
                _beats.Remove(beat.BeatId);
        }

        foreach (var directive in _directives.Values.ToList())
        {
            directive.RemainingTicks--;
            if (directive.RemainingTicks <= 0)
                _directives.Remove(directive.ColonyId);
        }
    }

    public void RegisterCausalChain(string parentBeatId, DirectorCausalChainSpec spec, long registrationTick)
    {
        var conditionSummary = BuildConditionSummary(spec.Condition);
        var followUpSummary = BuildFollowUpSummary(spec.FollowUpBeat.Text);

        _pendingCausalChains[parentBeatId] = new PendingCausalChainMutable(
            parentBeatId: parentBeatId,
            status: "pending",
            conditionMetric: spec.Condition.Metric,
            conditionOperator: spec.Condition.Operator,
            conditionThreshold: spec.Condition.Threshold,
            conditionSummary: conditionSummary,
            followUpBeat: spec.FollowUpBeat,
            followUpSummary: followUpSummary,
            remainingWindowTicks: spec.WindowTicks,
            triggerCount: 0,
            maxTriggers: spec.MaxTriggers,
            eligibleFromTick: registrationTick + 1,
            lastFailureMessage: string.Empty
        );
    }

    public IReadOnlyList<PendingCausalChainTrigger> EvaluatePendingCausalChains(
        long evaluationTick,
        Func<DirectorCausalConditionSpec, bool> conditionEvaluator)
    {
        var triggers = new List<PendingCausalChainTrigger>();
        foreach (var chain in _pendingCausalChains.Values)
        {
            if (chain.IsTerminal)
                continue;

            if (evaluationTick < chain.EligibleFromTick)
                continue;

            var conditionMet = conditionEvaluator(new DirectorCausalConditionSpec(
                chain.ConditionMetric,
                chain.ConditionOperator,
                chain.ConditionThreshold));

            if (conditionMet)
            {
                chain.TriggerCount++;
                chain.Status = "triggered";
                chain.LastFailureMessage = string.Empty;
                triggers.Add(new PendingCausalChainTrigger(chain.ParentBeatId, chain.FollowUpBeat));
                continue;
            }

            chain.RemainingWindowTicks = Math.Max(0, chain.RemainingWindowTicks - 1);
            if (chain.RemainingWindowTicks == 0)
            {
                chain.Status = "expired";
            }
        }

        return triggers;
    }

    public void MarkCausalChainTriggerFailed(string parentBeatId, string reason)
    {
        if (!_pendingCausalChains.TryGetValue(parentBeatId, out var chain))
            return;

        chain.Status = "trigger_failed";
        chain.LastFailureMessage = reason;
    }

    public DirectorApplyResult ApplyStoryBeat(string beatId, string text, int durationTicks, DirectorBeatSeverity severity)
    {
        if (_beats.ContainsKey(beatId))
            return DirectorApplyResult.Ok($"Story beat '{beatId}' already active (idempotent)");

        if (severity == DirectorBeatSeverity.Major && MajorBeatCooldownRemainingTicks > 0)
            return DirectorApplyResult.Fail($"Major beat cooldown active ({MajorBeatCooldownRemainingTicks} ticks)");
        if (severity == DirectorBeatSeverity.Epic && EpicBeatCooldownRemainingTicks > 0)
            return DirectorApplyResult.Fail($"Epic beat cooldown active ({EpicBeatCooldownRemainingTicks} ticks)");

        _beats[beatId] = new ActiveBeatMutable(beatId, text, severity, durationTicks, durationTicks);

        switch (severity)
        {
            case DirectorBeatSeverity.Major:
                MajorBeatCooldownRemainingTicks = Math.Max(MajorBeatCooldownRemainingTicks, MajorBeatCooldownTicks);
                break;
            case DirectorBeatSeverity.Epic:
                EpicBeatCooldownRemainingTicks = Math.Max(EpicBeatCooldownRemainingTicks, EpicBeatCooldownTicks);
                break;
        }

        return DirectorApplyResult.Ok($"Applied story beat '{beatId}' ({severity}, {durationTicks} ticks)");
    }

    public DirectorApplyResult ApplyDirective(int colonyId, string directive, int durationTicks)
    {
        _directives[colonyId] = new ActiveDirectiveMutable(colonyId, directive, durationTicks, durationTicks);
        return DirectorApplyResult.Ok($"Applied directive '{directive}' to colony {colonyId} ({durationTicks} ticks)");
    }

    private static double NormalizeBudget(double value, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;

        return Math.Max(0d, value);
    }

    private static string BuildConditionSummary(DirectorCausalConditionSpec condition)
    {
        return condition.Metric + " " + condition.Operator + " " + condition.Threshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildFollowUpSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var trimmed = text.Trim();
        return trimmed.Length <= 96 ? trimmed : trimmed[..96] + "...";
    }

    private sealed class ActiveBeatMutable
    {
        public ActiveBeatMutable(string beatId, string text, DirectorBeatSeverity severity, int remainingTicks, int totalTicks)
        {
            BeatId = beatId;
            Text = text;
            Severity = severity;
            RemainingTicks = remainingTicks;
            TotalTicks = totalTicks;
        }

        public string BeatId { get; }
        public string Text { get; }
        public DirectorBeatSeverity Severity { get; }
        public int RemainingTicks { get; set; }
        public int TotalTicks { get; }

        public ActiveBeatState ToState() => new(BeatId, Text, Severity, RemainingTicks, TotalTicks);
    }

    private sealed class ActiveDirectiveMutable
    {
        public ActiveDirectiveMutable(int colonyId, string directive, int remainingTicks, int totalTicks)
        {
            ColonyId = colonyId;
            Directive = directive;
            RemainingTicks = remainingTicks;
            TotalTicks = totalTicks;
        }

        public int ColonyId { get; }
        public string Directive { get; }
        public int RemainingTicks { get; set; }
        public int TotalTicks { get; }

        public ActiveDirectiveState ToState() => new(ColonyId, Directive, RemainingTicks, TotalTicks);
    }

    private sealed class PendingCausalChainMutable
    {
        public PendingCausalChainMutable(
            string parentBeatId,
            string status,
            string conditionMetric,
            string conditionOperator,
            double conditionThreshold,
            string conditionSummary,
            DirectorFollowUpBeatSpec followUpBeat,
            string followUpSummary,
            int remainingWindowTicks,
            int triggerCount,
            int maxTriggers,
            long eligibleFromTick,
            string lastFailureMessage)
        {
            ParentBeatId = parentBeatId;
            Status = status;
            ConditionMetric = conditionMetric;
            ConditionOperator = conditionOperator;
            ConditionThreshold = conditionThreshold;
            ConditionSummary = conditionSummary;
            FollowUpBeat = followUpBeat;
            FollowUpSummary = followUpSummary;
            RemainingWindowTicks = remainingWindowTicks;
            TriggerCount = triggerCount;
            MaxTriggers = maxTriggers;
            EligibleFromTick = eligibleFromTick;
            LastFailureMessage = lastFailureMessage;
        }

        public string ParentBeatId { get; }
        public string Status { get; set; }
        public string ConditionMetric { get; }
        public string ConditionOperator { get; }
        public double ConditionThreshold { get; }
        public string ConditionSummary { get; }
        public DirectorFollowUpBeatSpec FollowUpBeat { get; }
        public string FollowUpSummary { get; }
        public int RemainingWindowTicks { get; set; }
        public int TriggerCount { get; set; }
        public int MaxTriggers { get; }
        public long EligibleFromTick { get; }
        public string LastFailureMessage { get; set; }
        public bool IsTerminal => TriggerCount >= MaxTriggers || RemainingWindowTicks <= 0 || string.Equals(Status, "trigger_failed", StringComparison.Ordinal);

        public PendingCausalChainState ToState() => new(
            ParentBeatId,
            Status,
            ConditionMetric,
            ConditionOperator,
            ConditionThreshold,
            ConditionSummary,
            FollowUpBeat.BeatId,
            FollowUpSummary,
            RemainingWindowTicks,
            TriggerCount,
            MaxTriggers,
            EligibleFromTick,
            LastFailureMessage);
    }
}
