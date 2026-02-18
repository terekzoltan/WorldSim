using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldSim.AI;

public sealed class GoapState : IEquatable<GoapState>
{
    private readonly Dictionary<string, int> _values = new(StringComparer.OrdinalIgnoreCase);

    public GoapState()
    {
    }

    public GoapState(GoapState other)
    {
        _values = new Dictionary<string, int>(other._values, StringComparer.OrdinalIgnoreCase);
    }

    public int this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : 0;
        set => _values[key] = value;
    }

    public IEnumerable<KeyValuePair<string, int>> Values => _values;

    public bool Contains(GoapState subset)
    {
        foreach (var kv in subset._values)
        {
            if (!_values.TryGetValue(kv.Key, out var value) || value < kv.Value)
                return false;
        }

        return true;
    }

    public GoapState Apply(GoapState delta)
    {
        var next = new GoapState(this);
        foreach (var kv in delta._values)
            next[kv.Key] = next[kv.Key] + kv.Value;
        return next;
    }

    public bool Equals(GoapState? other)
    {
        if (other == null || _values.Count != other._values.Count)
            return false;

        foreach (var kv in _values)
        {
            if (!other._values.TryGetValue(kv.Key, out var value) || value != kv.Value)
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is GoapState state && Equals(state);
    }

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var kv in _values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            hash = hash * 31 + HashCode.Combine(kv.Key.ToUpperInvariant(), kv.Value);
        return hash;
    }
}

public sealed class GoapAction
{
    public string Name { get; }
    public NpcCommand Command { get; }
    public GoapState Preconditions { get; } = new();
    public GoapState Effects { get; } = new();
    public int Cost { get; set; } = 1;

    public GoapAction(string name, NpcCommand command)
    {
        Name = name;
        Command = command;
    }
}
