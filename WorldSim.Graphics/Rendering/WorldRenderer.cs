using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Graphics.Camera;
using WorldSim.Graphics.Rendering.PostFx;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class WorldRenderer
{
    private readonly IReadOnlyList<IRenderPass> _passes;
    private readonly RenderStats _renderStats = new();
    private readonly TerritoryOverlayPass _territoryOverlayPass = new();
    private readonly CombatOverlayPass _combatOverlayPass = new();

    public WorldRenderSettings Settings { get; }
    public WorldRenderTheme Theme { get; private set; }
    public RenderStats LastRenderStats => _renderStats;
    public bool PostFxEnabled { get; private set; } = true;
    public PostFxQuality PostFxQuality { get; private set; } = PostFxQuality.Medium;

    public WorldRenderer(WorldRenderSettings? settings = null, WorldRenderTheme? theme = null)
    {
        Settings = settings ?? new WorldRenderSettings();
        Theme = theme ?? WorldRenderTheme.Default;
        _passes = new IRenderPass[]
        {
            new TerrainRenderPass(),
            new ResourceRenderPass(),
            new StructureRenderPass(),
            new ActorRenderPass(),
            new FogHazeRenderPass(),
            new PostFxOverlayPass(),
            _territoryOverlayPass,
            _combatOverlayPass
        };
    }

    public void SetTheme(WorldRenderTheme theme)
    {
        Theme = theme;
    }

    public bool TerritoryOverlayEnabled
    {
        get => _territoryOverlayPass.Enabled;
        set => _territoryOverlayPass.Enabled = value;
    }

    public bool CombatOverlayEnabled
    {
        get => _combatOverlayPass.Enabled;
        set => _combatOverlayPass.Enabled = value;
    }

    public void SetPostFxEnabled(bool enabled)
    {
        PostFxEnabled = enabled;
    }

    public void SetPostFxQuality(PostFxQuality quality)
    {
        PostFxQuality = quality;
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        float fxIntensity = PostFxEnabled
            ? PostFxQuality switch
            {
                PostFxQuality.Low => 0.35f,
                PostFxQuality.Medium => 1f,
                _ => 1.85f
            }
            : 0.15f;

        _renderStats.BeginFrame();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());

        var context = new RenderFrameContext(
            spriteBatch,
            snapshot,
            textures,
            Settings,
            Theme,
            _renderStats,
            Environment.TickCount64 / 1000f,
            fxIntensity,
            camera.Position.X,
            camera.Position.Y,
            camera.Zoom,
            spriteBatch.GraphicsDevice.Viewport.Width,
            spriteBatch.GraphicsDevice.Viewport.Height);
        foreach (var pass in _passes)
        {
            var started = _renderStats.BeginPass();
            pass.Draw(in context);
            _renderStats.EndPass(pass.Name, started);
        }

        spriteBatch.End();
        _renderStats.EndFrame();
    }
}
