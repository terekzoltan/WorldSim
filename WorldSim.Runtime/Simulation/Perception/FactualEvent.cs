using System;

namespace WorldSim.Simulation;

public class FactualEvent
{
    public string Type { get; }
    public object? Data { get; }
    public DateTime Time { get; }
    public (int x, int y)? Pos { get; }
    public int? SourceId { get; }

    public FactualEvent(string type, object? data = null)
    {
        Type  = type;
        Data  = data;
        Time  = DateTime.UtcNow;
    }

    public FactualEvent(string type, object? data, (int x, int y)? pos, int? sourceId = null)
    {
        Type     = type;
        Data     = data;
        Pos      = pos;
        SourceId = sourceId;
        Time     = DateTime.UtcNow;
    }
}
