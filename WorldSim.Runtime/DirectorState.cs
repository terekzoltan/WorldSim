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

public readonly record struct DirectorApplyResult(bool Success, string Message)
{
    public static DirectorApplyResult Ok(string message) => new(true, message);
    public static DirectorApplyResult Fail(string message) => new(false, message);
}

public sealed class DirectorState
{
    private readonly Dictionary<string, ActiveBeatMutable> _beats = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ActiveDirectiveMutable> _directives = new();

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
                MajorBeatCooldownRemainingTicks = Math.Max(MajorBeatCooldownRemainingTicks, 20);
                break;
            case DirectorBeatSeverity.Epic:
                EpicBeatCooldownRemainingTicks = Math.Max(EpicBeatCooldownRemainingTicks, 40);
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
}
