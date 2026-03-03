using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Effects;

public readonly record struct GoalBiasSpec(string GoalCategory, double Weight);

public readonly record struct ActiveGoalBiasInfo(
    int ColonyId,
    string SourceId,
    string GoalCategory,
    double BaseWeight,
    double EffectiveWeight,
    int RemainingTicks,
    int TotalDurationTicks,
    bool IsBlendActive);

public sealed class GoalBiasEngine
{
    private const int BlendTicks = 5;
    private const double PriorityThreshold = 0.25;

    private readonly Dictionary<int, ColonyBiasState> _states = new();

    public void RegisterBiases(string sourceId, int colonyId, IReadOnlyList<GoalBiasSpec> biases, int durationTicks, double dampeningFactor)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("sourceId is required", nameof(sourceId));
        if (durationTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationTicks), "durationTicks must be > 0");

        var normalized = NormalizeBiases(biases, dampeningFactor);

        if (!_states.TryGetValue(colonyId, out var state))
        {
            _states[colonyId] = ColonyBiasState.Create(sourceId, normalized, durationTicks);
            return;
        }

        if (state.SourceId == sourceId)
        {
            state.ReplaceWithoutBlend(sourceId, normalized, durationTicks);
            return;
        }

        state.ReplaceWithBlend(sourceId, normalized, durationTicks);
    }

    public void ReplaceDirective(string sourceId, int colonyId, IReadOnlyList<GoalBiasSpec> biases, int durationTicks, double dampeningFactor)
        => RegisterBiases(sourceId, colonyId, biases, durationTicks, dampeningFactor);

    public void Tick()
    {
        foreach (var colonyId in _states.Keys.ToList())
        {
            var state = _states[colonyId];
            state.Tick();
            if (!state.IsActive)
                _states.Remove(colonyId);
        }
    }

    public double GetEffectiveBias(int colonyId, string goalCategory)
    {
        if (!_states.TryGetValue(colonyId, out var state))
            return 0d;
        if (string.IsNullOrWhiteSpace(goalCategory))
            return 0d;

        return state.GetEffectiveBias(goalCategory);
    }

    public bool IsJobPriorityActive(int colonyId, string goalCategory)
        => GetEffectiveBias(colonyId, goalCategory) >= PriorityThreshold;

    public IReadOnlyList<ActiveGoalBiasInfo> GetActiveBiases(int colonyId)
    {
        if (!_states.TryGetValue(colonyId, out var state))
            return Array.Empty<ActiveGoalBiasInfo>();

        return state.Entries
            .Select(entry => new ActiveGoalBiasInfo(
                ColonyId: colonyId,
                SourceId: state.SourceId,
                GoalCategory: entry.Key,
                BaseWeight: entry.Value,
                EffectiveWeight: state.GetEffectiveBias(entry.Key),
                RemainingTicks: state.RemainingTicks,
                TotalDurationTicks: state.TotalTicks,
                IsBlendActive: state.BlendTicksRemaining > 0))
            .ToList();
    }

    private static Dictionary<string, double> NormalizeBiases(IReadOnlyList<GoalBiasSpec> biases, double dampeningFactor)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var damp = Math.Clamp(dampeningFactor, 0d, 1d);

        if (biases == null)
            return result;

        foreach (var bias in biases)
        {
            if (string.IsNullOrWhiteSpace(bias.GoalCategory))
                continue;

            var clampedWeight = Math.Clamp(bias.Weight, 0d, 0.50d) * damp;
            if (clampedWeight <= 0d)
                continue;

            result[bias.GoalCategory] = clampedWeight;
        }

        return result;
    }

    private sealed class ColonyBiasState
    {
        private readonly Dictionary<string, double> _blendFromSnapshot = new(StringComparer.OrdinalIgnoreCase);

        private ColonyBiasState(string sourceId, Dictionary<string, double> entries, int durationTicks)
        {
            SourceId = sourceId;
            Entries = entries;
            TotalTicks = durationTicks;
            RemainingTicks = durationTicks;
        }

        public string SourceId { get; private set; }
        public Dictionary<string, double> Entries { get; private set; }
        public int TotalTicks { get; private set; }
        public int RemainingTicks { get; private set; }
        public int BlendTicksRemaining { get; private set; }
        public bool IsActive => RemainingTicks > 0;

        public static ColonyBiasState Create(string sourceId, Dictionary<string, double> entries, int durationTicks)
            => new(sourceId, entries, durationTicks);

        public void ReplaceWithoutBlend(string sourceId, Dictionary<string, double> entries, int durationTicks)
        {
            SourceId = sourceId;
            Entries = entries;
            TotalTicks = durationTicks;
            RemainingTicks = durationTicks;
            BlendTicksRemaining = 0;
            _blendFromSnapshot.Clear();
        }

        public void ReplaceWithBlend(string sourceId, Dictionary<string, double> entries, int durationTicks)
        {
            _blendFromSnapshot.Clear();
            foreach (var key in Entries.Keys.Union(entries.Keys, StringComparer.OrdinalIgnoreCase))
                _blendFromSnapshot[key] = GetBaseEffectiveBias(key);

            SourceId = sourceId;
            Entries = entries;
            TotalTicks = durationTicks;
            RemainingTicks = durationTicks;
            BlendTicksRemaining = BlendTicks;
        }

        public void Tick()
        {
            RemainingTicks--;
            if (BlendTicksRemaining > 0)
                BlendTicksRemaining--;
            if (BlendTicksRemaining <= 0)
                _blendFromSnapshot.Clear();
        }

        public double GetEffectiveBias(string goalCategory)
        {
            var baseValue = GetBaseEffectiveBias(goalCategory);
            if (BlendTicksRemaining <= 0)
                return baseValue;

            var prevValue = _blendFromSnapshot.GetValueOrDefault(goalCategory, 0d);
            var t = 1d - (BlendTicksRemaining / (double)BlendTicks);
            return (prevValue * (1d - t)) + (baseValue * t);
        }

        private double GetBaseEffectiveBias(string goalCategory)
        {
            var baseWeight = Entries.GetValueOrDefault(goalCategory, 0d);
            if (baseWeight <= 0d || TotalTicks <= 0 || RemainingTicks <= 0)
                return 0d;

            return baseWeight * (RemainingTicks / (double)TotalTicks);
        }
    }
}
