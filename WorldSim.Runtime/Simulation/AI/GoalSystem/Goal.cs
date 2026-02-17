using System;
using System.Collections.Generic;

namespace WorldSim.Simulation;

public class Goal
{
    public string Name { get; }
    public List<Consideration> Considerations { get; } = new();
    public TimeSpan Cooldown { get; set; } = TimeSpan.Zero;

    DateTime _lastSelected = DateTime.MinValue;

    public Goal(string name) => Name = name;

    public bool IsOnCooldown => DateTime.UtcNow - _lastSelected < Cooldown;
    public void MarkSelected() => _lastSelected = DateTime.UtcNow;
}