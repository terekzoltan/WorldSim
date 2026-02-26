namespace WorldSim.Graphics.Camera;

public sealed class CameraRoute
{
    public IReadOnlyList<CameraKeyframe> Keyframes { get; }
    public bool Loop { get; }

    public CameraRoute(IReadOnlyList<CameraKeyframe> keyframes, bool loop = false)
    {
        if (keyframes.Count < 2)
            throw new ArgumentException("Camera route needs at least 2 keyframes.", nameof(keyframes));

        Keyframes = keyframes;
        Loop = loop;
    }
}
