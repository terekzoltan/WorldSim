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
    private float _timeSeconds;
    private float _fxIntensity = 1f;

    public WorldRenderSettings Settings { get; }
    public WorldRenderTheme Theme { get; private set; }
    public RenderStats LastRenderStats => _renderStats;
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

    public PostFxSettings PostFxSettings { get; private set; } = new(true, PostFxQuality.Medium);

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
            _territoryOverlayPass,
            _combatOverlayPass,
            new FogHazeRenderPass(),
            new PostFxOverlayPass()
        };
    }

    public void SetTheme(WorldRenderTheme theme)
    {
        Theme = theme;
    }

    public void SetPostFxEnabled(bool enabled)
    {
        PostFxSettings = PostFxSettings with { Enabled = enabled };
        _fxIntensity = enabled ? 1f : 0f;
    }

    public void SetPostFxQuality(PostFxQuality quality)
    {
        PostFxSettings = PostFxSettings with { Quality = quality };
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        _renderStats.BeginFrame();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());
        _timeSeconds += 1f / 60f;

        var context = new RenderFrameContext(spriteBatch, snapshot, textures, Settings, Theme, _renderStats, _timeSeconds, _fxIntensity);
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
