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

    public WorldRenderSettings Settings { get; }
    public WorldRenderTheme Theme { get; private set; }
    public RenderStats LastRenderStats => _renderStats;
    public bool TerritoryOverlayEnabled { get; set; }
    public bool CombatOverlayEnabled { get; set; }

    public WorldRenderer(WorldRenderSettings? settings = null, WorldRenderTheme? theme = null)
    {
        Settings = settings ?? new WorldRenderSettings();
        Theme = theme ?? WorldRenderTheme.Default;
        _passes = new IRenderPass[]
        {
            new TerrainRenderPass(),
            new ResourceRenderPass(),
            new StructureRenderPass(),
            new ActorRenderPass()
        };
    }

    public void SetTheme(WorldRenderTheme theme)
    {
        Theme = theme;
    }

    public void SetPostFxEnabled(bool enabled)
    {
    }

    public void SetPostFxQuality(PostFxQuality quality)
    {
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        _renderStats.BeginFrame();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());

        var context = new RenderFrameContext(spriteBatch, snapshot, textures, Settings, Theme, _renderStats);
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
