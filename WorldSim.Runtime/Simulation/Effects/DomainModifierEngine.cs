using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.Simulation.Effects;

public enum RuntimeDomain
{
    Food,
    Morale,
    Economy,
    Military,
    Research
}

public readonly record struct ActiveDomainModifierInfo(
    string SourceId,
    RuntimeDomain Domain,
    double BaseModifier,
    double EffectiveModifier,
    int RemainingTicks,
    int TotalDurationTicks);

public sealed class DomainModifierEngine
{
    private const double MaxAbsEffectivePerDomain = 0.4;
    private readonly List<ActiveModifier> _modifiers = new();

    public void RegisterModifier(string sourceId, RuntimeDomain domain, double modifier, int durationTicks, double dampeningFactor)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("sourceId is required", nameof(sourceId));
        if (durationTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationTicks), "durationTicks must be > 0");

        var clampedDampening = Math.Clamp(dampeningFactor, 0d, 1d);
        var dampened = modifier * clampedDampening;
        if (Math.Abs(dampened) < 0.0001d)
            return;

        _modifiers.Add(new ActiveModifier(sourceId, domain, dampened, durationTicks));
    }

    public void Tick()
    {
        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            var modifier = _modifiers[i];
            modifier.RemainingTicks--;
            if (modifier.RemainingTicks <= 0)
                _modifiers.RemoveAt(i);
        }
    }

    public double GetEffectiveModifier(RuntimeDomain domain)
    {
        var sum = 0d;
        foreach (var modifier in _modifiers)
        {
            if (modifier.Domain != domain)
                continue;

            sum += modifier.BaseModifier * ((double)modifier.RemainingTicks / modifier.TotalDurationTicks);
        }

        return Math.Clamp(sum, -MaxAbsEffectivePerDomain, MaxAbsEffectivePerDomain);
    }

    public IReadOnlyList<ActiveDomainModifierInfo> GetActiveModifiers()
        => _modifiers
            .Select(modifier => new ActiveDomainModifierInfo(
                modifier.SourceId,
                modifier.Domain,
                modifier.BaseModifier,
                modifier.BaseModifier * ((double)modifier.RemainingTicks / modifier.TotalDurationTicks),
                modifier.RemainingTicks,
                modifier.TotalDurationTicks))
            .ToList();

    private sealed class ActiveModifier
    {
        public ActiveModifier(string sourceId, RuntimeDomain domain, double baseModifier, int totalDurationTicks)
        {
            SourceId = sourceId;
            Domain = domain;
            BaseModifier = baseModifier;
            TotalDurationTicks = totalDurationTicks;
            RemainingTicks = totalDurationTicks;
        }

        public string SourceId { get; }
        public RuntimeDomain Domain { get; }
        public double BaseModifier { get; }
        public int TotalDurationTicks { get; }
        public int RemainingTicks { get; set; }
    }
}
