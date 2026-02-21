using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Camera;

public sealed class Camera2D
{
    public Vector2 Position { get; private set; } = Vector2.Zero;
    public float Zoom { get; private set; } = 1f;
    public Vector2 TargetPosition { get; private set; } = Vector2.Zero;
    public float TargetZoom { get; private set; } = 1f;

    public void SetPosition(Vector2 position)
    {
        Position = position;
        TargetPosition = position;
    }

    public void SetZoom(float zoom, float minZoom, float maxZoom)
    {
        Zoom = Math.Clamp(zoom, minZoom, maxZoom);
        TargetZoom = Zoom;
    }

    public void SetTarget(Vector2 position, float zoom, float minZoom, float maxZoom)
    {
        TargetPosition = position;
        TargetZoom = Math.Clamp(zoom, minZoom, maxZoom);
    }

    public void SnapToTarget()
    {
        Position = TargetPosition;
        Zoom = TargetZoom;
    }

    public void Step(float dt, float positionDamping = 14f, float zoomDamping = 16f)
    {
        float positionLerp = 1f - MathF.Exp(-positionDamping * dt);
        float zoomLerp = 1f - MathF.Exp(-zoomDamping * dt);
        Position = Vector2.Lerp(Position, TargetPosition, positionLerp);
        Zoom = MathHelper.Lerp(Zoom, TargetZoom, zoomLerp);
    }

    public void ZoomAt(Vector2 screenPoint, float zoomFactor, float minZoom, float maxZoom)
    {
        var worldBefore = ScreenToWorld(screenPoint);
        SetZoom(Zoom * zoomFactor, minZoom, maxZoom);
        var worldAfter = ScreenToWorld(screenPoint);
        Position += worldBefore - worldAfter;
        TargetPosition = Position;
        TargetZoom = Zoom;
    }

    public void Translate(Vector2 delta)
    {
        Position += delta;
        TargetPosition = Position;
    }

    public Vector2 ScreenToWorld(Vector2 screenPoint)
    {
        return screenPoint / Zoom + Position;
    }

    public void ClampToWorld(float worldWidth, float worldHeight, float viewportWidth, float viewportHeight)
    {
        var viewWidth = viewportWidth / Zoom;
        var viewHeight = viewportHeight / Zoom;

        var maxX = Math.Max(0f, worldWidth - viewWidth);
        var maxY = Math.Max(0f, worldHeight - viewHeight);

        Position = new Vector2(
            Math.Clamp(Position.X, 0f, maxX),
            Math.Clamp(Position.Y, 0f, maxY));
        TargetPosition = new Vector2(
            Math.Clamp(TargetPosition.X, 0f, maxX),
            Math.Clamp(TargetPosition.Y, 0f, maxY));
    }

    public Matrix BuildMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);
    }
}
