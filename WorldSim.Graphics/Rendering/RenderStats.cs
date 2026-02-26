using System;
using System.Diagnostics;
using System.Linq;

namespace WorldSim.Graphics.Rendering;

public sealed class RenderStats
{
    private readonly List<RenderPassSample> _passSamples = new();
    private readonly Queue<double> _frameHistory = new();
    private long _frameStart;
    private const int MaxFrameHistory = 240;

    public IReadOnlyList<RenderPassSample> PassSamples => _passSamples;
    public double LastFrameMilliseconds { get; private set; }
    public double AverageFrameMilliseconds { get; private set; }
    public double P99FrameMilliseconds { get; private set; }

    public void BeginFrame()
    {
        _passSamples.Clear();
        _frameStart = Stopwatch.GetTimestamp();
        LastFrameMilliseconds = 0;
    }

    public long BeginPass() => Stopwatch.GetTimestamp();

    public void EndPass(string passName, long startedAt)
    {
        var elapsedMs = (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;
        _passSamples.Add(new RenderPassSample(passName, elapsedMs));
    }

    public void EndFrame()
    {
        LastFrameMilliseconds = (Stopwatch.GetTimestamp() - _frameStart) * 1000d / Stopwatch.Frequency;
        _frameHistory.Enqueue(LastFrameMilliseconds);
        while (_frameHistory.Count > MaxFrameHistory)
            _frameHistory.Dequeue();

        if (_frameHistory.Count == 0)
        {
            AverageFrameMilliseconds = 0;
            P99FrameMilliseconds = 0;
            return;
        }

        AverageFrameMilliseconds = _frameHistory.Average();
        var sorted = _frameHistory.OrderBy(v => v).ToArray();
        int idx = (int)Math.Clamp(MathF.Ceiling(sorted.Length * 0.99f) - 1, 0, sorted.Length - 1);
        P99FrameMilliseconds = sorted[idx];
    }
}

public readonly record struct RenderPassSample(string PassName, double Milliseconds);
