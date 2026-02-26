using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Camera;

public readonly record struct CameraKeyframe(Vector2 Position, float Zoom, float DurationSeconds);
