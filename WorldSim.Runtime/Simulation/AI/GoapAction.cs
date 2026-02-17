using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorldSim.Simulation;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class State : IEquatable<State>
{
    private readonly Dictionary<string, int> _values = new(StringComparer.OrdinalIgnoreCase);

    public State() { }
    public State(State other) => _values = new Dictionary<string, int>(other._values, StringComparer.OrdinalIgnoreCase);

    public int this[string key]
    {
        get => _values.TryGetValue(key, out var v) ? v : 0;
        set => _values[key] = value;
    }

    public IEnumerable<KeyValuePair<string, int>> Values => _values;

    public bool Contains(State subset)
    {
        foreach (var kv in subset._values)
            if (!_values.TryGetValue(kv.Key, out var v) || v < kv.Value)
                return false;
        return true;
    }

    public State Apply(State change)
    {
        var result = new State(this);
        foreach (var kv in change._values)
            result[kv.Key] = result[kv.Key] + kv.Value;
        return result;
    }

    public bool Equals(State? other)
    {
        if (other == null || _values.Count != other._values.Count) return false;
        foreach (var kv in _values)
            if (!other._values.TryGetValue(kv.Key, out var v) || v != kv.Value)
                return false;
        return true;
    }

    public override bool Equals(object? obj) => obj is State s && Equals(s);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (var kv in _values.OrderBy(k => k.Key))
            hash = hash * 31 + HashCode.Combine(kv.Key, kv.Value);
        return hash;
    }
}

public sealed class Action
{
    public string Name { get; }
    public State Preconditions { get; } = new();
    public State Effects { get; } = new();
    public int Cost { get; set; } = 1;
    public Job Job { get; }

    public Action(string name, Job job)
    {
        Name = name;
        Job = job;
    }
}
