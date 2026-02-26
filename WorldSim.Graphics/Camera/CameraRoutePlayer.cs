using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Camera;

public sealed class CameraRoutePlayer
{
    private CameraRoute? _route;
    private int _segmentIndex;
    private float _segmentTime;

    public bool IsActive => _route is not null;

    public void Start(CameraRoute route, Camera2D camera, float minZoom, float maxZoom)
    {
        _route = route;
        _segmentIndex = 0;
        _segmentTime = 0f;

        var first = route.Keyframes[0];
        camera.SetTarget(first.Position, first.Zoom, minZoom, maxZoom);
        camera.SnapToTarget();
    }

    public void Stop()
    {
        _route = null;
        _segmentIndex = 0;
        _segmentTime = 0f;
    }

    public void Update(float dt, Camera2D camera, float minZoom, float maxZoom)
    {
        if (_route is null)
            return;

        var keyframes = _route.Keyframes;
        if (_segmentIndex >= keyframes.Count - 1)
        {
            if (_route.Loop)
            {
                _segmentIndex = 0;
                _segmentTime = 0f;
            }
            else
            {
                Stop();
                return;
            }
        }

        var from = keyframes[_segmentIndex];
        var to = keyframes[_segmentIndex + 1];
        float duration = Math.Max(0.01f, to.DurationSeconds);

        _segmentTime += dt;
        float t = Math.Clamp(_segmentTime / duration, 0f, 1f);

        var pos = Vector2.Lerp(from.Position, to.Position, SmoothStep(t));
        float zoom = MathHelper.Lerp(from.Zoom, to.Zoom, SmoothStep(t));
        camera.SetTarget(pos, zoom, minZoom, maxZoom);

        if (_segmentTime >= duration)
        {
            _segmentIndex++;
            _segmentTime = 0f;
        }
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
