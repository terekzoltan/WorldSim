using System.Diagnostics;

namespace WorldSim.Graphics.Rendering;

public sealed class RenderStats
{
    private readonly List<RenderPassSample> _passSamples = new();
    private long _frameStart;

    public IReadOnlyList<RenderPassSample> PassSamples => _passSamples;
    public double LastFrameMilliseconds { get; private set; }

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
    }
}

public readonly record struct RenderPassSample(string PassName, double Milliseconds);
