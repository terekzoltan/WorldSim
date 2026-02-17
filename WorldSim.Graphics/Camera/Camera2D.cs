using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Camera;

public sealed class Camera2D
{
    public Vector2 Position { get; private set; } = Vector2.Zero;
    public float Zoom { get; private set; } = 1f;

    public void SetPosition(Vector2 position)
    {
        Position = position;
    }

    public void SetZoom(float zoom, float minZoom, float maxZoom)
    {
        Zoom = Math.Clamp(zoom, minZoom, maxZoom);
    }

    public void ZoomAt(Vector2 screenPoint, float zoomFactor, float minZoom, float maxZoom)
    {
        var worldBefore = ScreenToWorld(screenPoint);
        SetZoom(Zoom * zoomFactor, minZoom, maxZoom);
        var worldAfter = ScreenToWorld(screenPoint);
        Position += worldBefore - worldAfter;
    }

    public void Translate(Vector2 delta)
    {
        Position += delta;
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
    }

    public Matrix BuildMatrix()
    {
        return Matrix.CreateTranslation(-Position.X, -Position.Y, 0f) * Matrix.CreateScale(Zoom, Zoom, 1f);
    }
}
