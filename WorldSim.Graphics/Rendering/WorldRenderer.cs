using System;
using Microsoft.Xna.Framework;
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
    public string RequestedVisualLane { get; private set; } = "DevLite";
    public PostFxSettings CurrentPostFxSettings { get; private set; } = new(false, PostFxQuality.Low);
    public bool TerritoryOverlayEnabled { get; set; }
    public bool CombatOverlayEnabled { get; set; }

    public WorldRenderer(WorldRenderSettings? settings = null, WorldRenderTheme? theme = null)
    {
        Settings = settings ?? new WorldRenderSettings();
        Theme = theme ?? WorldRenderTheme.Default;
        _passes = new IRenderPass[]
        {
            new TerrainRenderPass(),
            new FogHazeRenderPass(),
            new ResourceRenderPass(),
            new StructureRenderPass(),
            new ActorRenderPass()
        };
    }

    public void SetTheme(WorldRenderTheme theme)
    {
        Theme = theme;
    }

    public void SetRequestedVisualLane(string lane)
    {
        RequestedVisualLane = string.IsNullOrWhiteSpace(lane) ? "DevLite" : lane;
    }

    public void SetPostFxEnabled(bool enabled)
    {
        CurrentPostFxSettings = CurrentPostFxSettings with { Enabled = enabled };
    }

    public void SetPostFxQuality(PostFxQuality quality)
    {
        CurrentPostFxSettings = CurrentPostFxSettings with { Quality = quality };
    }

    public void Draw(SpriteBatch spriteBatch, WorldRenderSnapshot snapshot, Camera2D camera, TextureCatalog textures)
    {
        _renderStats.BeginFrame();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.BuildMatrix());

        var viewport = spriteBatch.GraphicsDevice.Viewport;
        var visibleTileBounds = ComputeVisibleTileBounds(snapshot, camera, viewport, Settings);
        var visualPolicy = ResolveVisualPolicy(RequestedVisualLane);

        _territoryOverlayPass.Enabled = TerritoryOverlayEnabled;
        _combatOverlayPass.Enabled = CombatOverlayEnabled;

        var context = new RenderFrameContext(spriteBatch, snapshot, textures, Settings, Theme, _renderStats, visibleTileBounds, visualPolicy);
        foreach (var pass in _passes)
        {
            var started = _renderStats.BeginPass();
            pass.Draw(in context);
            _renderStats.EndPass(pass.Name, started);
        }

        var territoryStarted = _renderStats.BeginPass();
        _territoryOverlayPass.Draw(in context);
        _renderStats.EndPass(_territoryOverlayPass.Name, territoryStarted);

        var combatStarted = _renderStats.BeginPass();
        _combatOverlayPass.Draw(in context);
        _renderStats.EndPass(_combatOverlayPass.Name, combatStarted);

        spriteBatch.End();
        _renderStats.EndFrame();
    }

    private static TileBounds ComputeVisibleTileBounds(
        WorldRenderSnapshot snapshot,
        Camera2D camera,
        Viewport viewport,
        WorldRenderSettings settings)
    {
        int tileSize = settings.TileSize;
        var worldTopLeft = camera.ScreenToWorld(Vector2.Zero);
        var worldBottomRight = camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height));

        var minWorldX = MathF.Min(worldTopLeft.X, worldBottomRight.X);
        var maxWorldX = MathF.Max(worldTopLeft.X, worldBottomRight.X);
        var minWorldY = MathF.Min(worldTopLeft.Y, worldBottomRight.Y);
        var maxWorldY = MathF.Max(worldTopLeft.Y, worldBottomRight.Y);

        var padding = RenderLayout.CalculateVisibleTilePadding(settings);
        var minTileX = Math.Max(0, (int)MathF.Floor(minWorldX / tileSize) - padding.Horizontal);
        var minTileY = Math.Max(0, (int)MathF.Floor(minWorldY / tileSize) - padding.Top);
        var maxTileX = Math.Min(snapshot.Width - 1, (int)MathF.Floor(maxWorldX / tileSize) + padding.Horizontal);
        var maxTileY = Math.Min(snapshot.Height - 1, (int)MathF.Floor(maxWorldY / tileSize) + padding.Bottom);

        return new TileBounds(minTileX, minTileY, maxTileX, maxTileY);
    }

    private static LowCostVisualPolicy ResolveVisualPolicy(string requestedLane)
    {
        var normalizedLane = requestedLane?.Trim().ToLowerInvariant() ?? "devlite";
        return normalizedLane switch
        {
            "showcase" => new LowCostVisualPolicy(HazeEnabled: true, HazeIntensityMultiplier: 1f, TerrainAmbientMultiplier: 1f),
            "headless" => new LowCostVisualPolicy(HazeEnabled: false, HazeIntensityMultiplier: 0f, TerrainAmbientMultiplier: 0f),
            _ => new LowCostVisualPolicy(HazeEnabled: true, HazeIntensityMultiplier: 0.55f, TerrainAmbientMultiplier: 0.6f)
        };
    }
}
