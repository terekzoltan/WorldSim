using Microsoft.Xna.Framework.Graphics;
using WorldSim.Graphics.Assets;
using WorldSim.Graphics.Camera;
using WorldSim.Graphics.Rendering.PostFx;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.Rendering;

public sealed class WorldRenderer
{
    private readonly IReadOnlyList<IRenderPass> _passes;
    private readonly IReadOnlyList<IPostProcessPass> _postProcessPasses;
    private readonly RenderStats _renderStats = new();
    private RenderTarget2D? _sceneRenderTarget;
    private RenderTarget2D? _postProcessRenderTarget;

    public WorldRenderSettings Settings { get; }
    public WorldRenderTheme Theme { get; private set; }
    public RenderStats LastRenderStats => _renderStats;
    public PostFxSettings PostFx { get; private set; } = new(true, PostFxQuality.Medium);

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
            new FogHazeRenderPass()
        };

        _postProcessPasses = new IPostProcessPass[]
        {
            new ColorGradePass(),
            new VignettePass(),
            new BloomLitePass()
        };
    }

    public void SetTheme(WorldRenderTheme theme)
    {
        Theme = theme;
    }

    public void SetPostFxEnabled(bool enabled)
    {
        PostFx = PostFx with { Enabled = enabled };
    }

    public void SetPostFxQuality(PostFxQuality quality)
    {
        PostFx = PostFx with { Quality = quality };
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        Draw(spriteBatch, snapshot, camera, textures, 0f, 0f, 1f);
    }

    public void Draw(
        SpriteBatch spriteBatch,
        WorldRenderSnapshot snapshot,
        Camera2D camera,
        TextureCatalog textures,
        float timeSeconds,
        float tickAlpha,
        float fxIntensity)
    {
        EnsureRenderTargets(spriteBatch.GraphicsDevice);

        _renderStats.BeginFrame();

        spriteBatch.GraphicsDevice.SetRenderTarget(_sceneRenderTarget);
        spriteBatch.GraphicsDevice.Clear(Theme.Background);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());

        var context = new RenderFrameContext(
            spriteBatch,
            snapshot,
            textures,
            Settings,
            Theme,
            _renderStats,
            timeSeconds,
            tickAlpha,
            fxIntensity);
        foreach (var pass in _passes)
        {
            var started = _renderStats.BeginPass();
            pass.Draw(in context);
            _renderStats.EndPass(pass.Name, started);
        }

        spriteBatch.End();

        if (!PostFx.Enabled)
        {
            Present(spriteBatch, _sceneRenderTarget!);
            _renderStats.EndFrame();
            return;
        }

        var ppContext = new PostProcessFrameContext(
            spriteBatch.GraphicsDevice,
            spriteBatch,
            textures,
            Theme,
            PostFx.Quality,
            timeSeconds,
            fxIntensity);

        Texture2D current = _sceneRenderTarget!;
        bool ping = true;
        foreach (var postPass in _postProcessPasses)
        {
            var started = _renderStats.BeginPass();
            var destination = ping ? _postProcessRenderTarget! : _sceneRenderTarget!;
            postPass.Apply(in ppContext, current, destination);
            current = destination;
            ping = !ping;
            _renderStats.EndPass(postPass.Name, started);
        }

        Present(spriteBatch, current);
        _renderStats.EndFrame();
    }

    private void Present(SpriteBatch spriteBatch, Texture2D source)
    {
        var graphicsDevice = spriteBatch.GraphicsDevice;
        graphicsDevice.SetRenderTarget(null);
        graphicsDevice.Clear(Theme.Background);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        spriteBatch.Draw(source, graphicsDevice.Viewport.Bounds, Microsoft.Xna.Framework.Color.White);
        spriteBatch.End();
    }

    private void EnsureRenderTargets(GraphicsDevice graphicsDevice)
    {
        var width = Math.Max(1, graphicsDevice.Viewport.Width);
        var height = Math.Max(1, graphicsDevice.Viewport.Height);

        if (_sceneRenderTarget is { Width: var w, Height: var h } && w == width && h == height &&
            _postProcessRenderTarget is { Width: var pw, Height: var ph } && pw == width && ph == height)
        {
            return;
        }

        _sceneRenderTarget?.Dispose();
        _postProcessRenderTarget?.Dispose();

        _sceneRenderTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
        _postProcessRenderTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
    }
}
