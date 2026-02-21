using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldSim.Graphics.Assets;
using WorldSim.Graphics.Camera;
using WorldSim.Graphics.Rendering;
using WorldSim.Graphics.UI;
using WorldSim.RefineryAdapter;
using WorldSim.Runtime;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;

namespace WorldSim;

public class Game1 : Game
{
    private static readonly (string Name, WorldRenderTheme Theme)[] ThemePresets =
    {
        ("Daylight", WorldRenderTheme.DaylightAtlas),
        ("Parchment", WorldRenderTheme.ParchmentFrontier),
        ("Industrial", WorldRenderTheme.IndustrialDawn),
        ("Classic", WorldRenderTheme.Default)
    };

    private const float SimulationTickDuration = 0.25f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 5.0f;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SimulationRuntime _runtime = null!;
    private SpriteFont _font = null!;
    private TextureCatalog _textures = null!;
    private readonly Camera2D _camera = new();
    private readonly WorldRenderer _worldRenderer = new(theme: WorldRenderTheme.DaylightAtlas);
    private readonly HudRenderer _hudRenderer = new();

    private readonly RefineryTriggerAdapter _refineryRuntime;

    private float _accumulator;
    private float _timeScale = 10.0f;
    private bool _showTechMenu;
    private int _selectedColony;
    private int _previousWheel;
    private KeyboardState _previousKeys;
    private bool _isPanning;
    private Point _lastMousePos;
    private bool _cameraInitialized;
    private bool _isFullscreen = true;
    private bool _fullscreenKeyDown;
    private bool _showAiDebugPanel;
    private bool _aiDebugCompact;
    private int _aiGoalScoreOffset;
    private int _aiHistoryOffset;
    private bool _showRenderStats;
    private bool _showTelemetryHud = true;
    private int _themeIndex;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = display.Width;
        _graphics.PreferredBackBufferHeight = display.Height;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.ApplyChanges();
        Window.IsBorderless = true;
        Window.ClientSizeChanged += (_, _) => FitCameraToViewport();

        _refineryRuntime = new RefineryTriggerAdapter(AppDomain.CurrentDomain.BaseDirectory);
        _hudRenderer.SetTheme(HudTheme.FromWorldTheme(_worldRenderer.Theme));
    }

    protected override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _runtime = CreateRuntime();

        _previousWheel = Mouse.GetState().ScrollWheelValue;
        FitCameraToViewport();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _textures = new TextureCatalog(GraphicsDevice, Content);
        try
        {
            _font = Content.Load<SpriteFont>("UiFont");
        }
        catch
        {
            _font = Content.Load<SpriteFont>("DebugFont");
        }
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        _camera.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (keys.IsKeyDown(Keys.F11))
        {
            if (!_fullscreenKeyDown)
                ToggleFullscreen();
            _fullscreenKeyDown = true;
        }
        else
        {
            _fullscreenKeyDown = false;
        }

        if (keys.IsKeyDown(Keys.F1) && !_previousKeys.IsKeyDown(Keys.F1))
            _showTechMenu = !_showTechMenu;

        if (keys.IsKeyDown(Keys.F9) && !_previousKeys.IsKeyDown(Keys.F9))
            CycleTheme(-1);

        if (keys.IsKeyDown(Keys.F10) && !_previousKeys.IsKeyDown(Keys.F10))
            CycleTheme(1);

        if (keys.IsKeyDown(Keys.F6) && !_previousKeys.IsKeyDown(Keys.F6))
            _refineryRuntime.Trigger(_runtime, _runtime.Tick);

#if DEBUG
        HandlePlannerToggleDebug(keys);
#endif

        if (keys.IsKeyDown(Keys.F8) && !_previousKeys.IsKeyDown(Keys.F8))
            _showAiDebugPanel = !_showAiDebugPanel;

        HandleAiDebugInput(keys);

        if (keys.IsKeyDown(Keys.F3) && !_previousKeys.IsKeyDown(Keys.F3))
            _showRenderStats = !_showRenderStats;

        if (keys.IsKeyDown(Keys.T) && !_previousKeys.IsKeyDown(Keys.T))
            _showTelemetryHud = !_showTelemetryHud;

        HandleCameraInput(keys, mouse);
        HandleTechMenuInput(keys);

        _previousKeys = keys;
        _refineryRuntime.Pump();

        _accumulator += (float)gameTime.ElapsedGameTime.TotalSeconds * _timeScale;
        while (_accumulator >= SimulationTickDuration)
        {
            _runtime.AdvanceTick(SimulationTickDuration);
            _accumulator -= SimulationTickDuration;
        }

        base.Update(gameTime);
    }

    private void HandleCameraInput(KeyboardState keys, MouseState mouse)
    {
        if (mouse.MiddleButton == ButtonState.Pressed && !_isPanning)
        {
            _isPanning = true;
            _lastMousePos = mouse.Position;
        }
        else if (mouse.MiddleButton == ButtonState.Released && _isPanning)
        {
            _isPanning = false;
        }

        if (_isPanning)
        {
            var current = mouse.Position;
            var delta = current - _lastMousePos;
            _lastMousePos = current;
            _camera.Translate(-new Vector2(delta.X, delta.Y) / _camera.Zoom);
            ClampCamera();
        }

        var wheel = mouse.ScrollWheelValue;
        var wheelDelta = wheel - _previousWheel;
        if (wheelDelta != 0)
        {
            var steps = wheelDelta / 120f;
            var factor = MathF.Pow(1.1f, steps);
            _camera.ZoomAt(new Vector2(mouse.X, mouse.Y), factor, MinZoom, MaxZoom);
            ClampCamera();
        }
        _previousWheel = wheel;

        if ((keys.IsKeyDown(Keys.OemPlus) && !_previousKeys.IsKeyDown(Keys.OemPlus)) ||
            (keys.IsKeyDown(Keys.Add) && !_previousKeys.IsKeyDown(Keys.Add)))
        {
            _camera.ZoomAt(new Vector2(mouse.X, mouse.Y), 1.1f, MinZoom, MaxZoom);
            ClampCamera();
        }

        if ((keys.IsKeyDown(Keys.OemMinus) && !_previousKeys.IsKeyDown(Keys.OemMinus)) ||
            (keys.IsKeyDown(Keys.Subtract) && !_previousKeys.IsKeyDown(Keys.Subtract)))
        {
            _camera.ZoomAt(new Vector2(mouse.X, mouse.Y), 1f / 1.1f, MinZoom, MaxZoom);
            ClampCamera();
        }
    }

    private void HandleTechMenuInput(KeyboardState keys)
    {
        if (!_showTechMenu || _runtime.ColonyCount == 0)
            return;

        if (keys.IsKeyDown(Keys.Left) && !_previousKeys.IsKeyDown(Keys.Left))
            _selectedColony = _runtime.NormalizeColonyIndex(_selectedColony - 1);
        if (keys.IsKeyDown(Keys.Right) && !_previousKeys.IsKeyDown(Keys.Right))
            _selectedColony = _runtime.NormalizeColonyIndex(_selectedColony + 1);

        var lockedNames = _runtime.GetLockedTechNames(_selectedColony);
        for (var i = 0; i < lockedNames.Count && i < 9; i++)
        {
            var hotkey = Keys.D1 + i;
            if (keys.IsKeyDown(hotkey) && !_previousKeys.IsKeyDown(hotkey))
                _runtime.UnlockLockedTechBySlot(_selectedColony, i);
        }
    }

    private void HandleAiDebugInput(KeyboardState keys)
    {
        if (!_showAiDebugPanel)
            return;

        if (keys.IsKeyDown(Keys.PageUp) && !_previousKeys.IsKeyDown(Keys.PageUp))
            _runtime.CycleTrackedNpc(-1);

        if (keys.IsKeyDown(Keys.PageDown) && !_previousKeys.IsKeyDown(Keys.PageDown))
            _runtime.CycleTrackedNpc(1);

        if (keys.IsKeyDown(Keys.Home) && !_previousKeys.IsKeyDown(Keys.Home))
            _runtime.ResetTrackedNpc();

        if (keys.IsKeyDown(Keys.F4) && !_previousKeys.IsKeyDown(Keys.F4))
            _aiDebugCompact = !_aiDebugCompact;

        if (keys.IsKeyDown(Keys.Up) && !_previousKeys.IsKeyDown(Keys.Up))
            _aiHistoryOffset++;

        if (keys.IsKeyDown(Keys.Down) && !_previousKeys.IsKeyDown(Keys.Down))
            _aiHistoryOffset = Math.Max(0, _aiHistoryOffset - 1);

        if (keys.IsKeyDown(Keys.Left) && !_previousKeys.IsKeyDown(Keys.Left))
            _aiGoalScoreOffset++;

        if (keys.IsKeyDown(Keys.Right) && !_previousKeys.IsKeyDown(Keys.Right))
            _aiGoalScoreOffset = Math.Max(0, _aiGoalScoreOffset - 1);
    }

#if DEBUG
    private void HandlePlannerToggleDebug(KeyboardState keys)
    {
        if (!keys.IsKeyDown(Keys.F7) || _previousKeys.IsKeyDown(Keys.F7))
            return;

        var nextMode = _runtime.PlannerMode switch
        {
            NpcPlannerMode.Goap => NpcPlannerMode.Simple,
            NpcPlannerMode.Simple => NpcPlannerMode.Htn,
            _ => NpcPlannerMode.Goap
        };

        _runtime = CreateRuntime(new RuntimeAiOptions { PlannerMode = nextMode, PolicyMode = _runtime.PolicyMode });
        _showTechMenu = false;
    }
#endif

    private static SimulationRuntime CreateRuntime()
    {
        var techPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tech", "technologies.json");
        return new SimulationRuntime(width: 128, height: 128, initialPopulation: 25, technologyFilePath: techPath);
    }

    private static SimulationRuntime CreateRuntime(RuntimeAiOptions aiOptions)
    {
        var techPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tech", "technologies.json");
        return new SimulationRuntime(width: 128, height: 128, initialPopulation: 25, technologyFilePath: techPath, aiOptions: aiOptions);
    }

    private void ClampCamera()
    {
        _camera.ClampToWorld(
            _runtime.Width * _worldRenderer.Settings.TileSize,
            _runtime.Height * _worldRenderer.Settings.TileSize,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
    }

    private void FitCameraToViewport()
    {
        if (_runtime == null)
            return;

        var mapWidth = _runtime.Width * _worldRenderer.Settings.TileSize;
        var mapHeight = _runtime.Height * _worldRenderer.Settings.TileSize;
        if (mapWidth <= 0 || mapHeight <= 0)
            return;

        var viewportWidth = GraphicsDevice.Viewport.Width;
        var viewportHeight = GraphicsDevice.Viewport.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        var zoomX = viewportWidth / (float)mapWidth;
        var zoomY = viewportHeight / (float)mapHeight;
        var coverZoom = MathF.Max(zoomX, zoomY);

        _camera.SetZoom(coverZoom, MinZoom, MaxZoom);
        var visibleWidth = viewportWidth / coverZoom;
        var visibleHeight = viewportHeight / coverZoom;
        var centered = new Vector2(
            (mapWidth - visibleWidth) * 0.5f,
            (mapHeight - visibleHeight) * 0.5f);

        _camera.SetTarget(centered, coverZoom, MinZoom, MaxZoom);
        if (!_cameraInitialized)
            _camera.SnapToTarget();
        ClampCamera();
        _cameraInitialized = true;
    }

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        if (_isFullscreen)
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = display.Width;
            _graphics.PreferredBackBufferHeight = display.Height;
            _graphics.IsFullScreen = true;
            Window.IsBorderless = true;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = 128 * _worldRenderer.Settings.TileSize;
            _graphics.PreferredBackBufferHeight = 110 * _worldRenderer.Settings.TileSize;
            _graphics.IsFullScreen = false;
            Window.IsBorderless = false;
        }

        _graphics.ApplyChanges();
        FitCameraToViewport();
    }

    private void CycleTheme(int delta)
    {
        _themeIndex = (_themeIndex + delta + ThemePresets.Length) % ThemePresets.Length;
        _worldRenderer.SetTheme(ThemePresets[_themeIndex].Theme);
        _hudRenderer.SetTheme(HudTheme.FromWorldTheme(_worldRenderer.Theme));
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!_cameraInitialized)
            FitCameraToViewport();

        GraphicsDevice.Clear(_worldRenderer.Theme.Background);

        var snapshot = _runtime.GetSnapshot();
        _worldRenderer.Draw(_spriteBatch, snapshot, _camera, _textures);

        TechMenuView? techMenu = null;
        if (_showTechMenu && _runtime.ColonyCount > 0)
        {
            _selectedColony = _runtime.NormalizeColonyIndex(_selectedColony);
            var lockedNames = _runtime.GetLockedTechNames(_selectedColony);
            techMenu = new TechMenuView(_runtime.GetColonyId(_selectedColony), lockedNames);
        }

        _spriteBatch.Begin();
        var plannerStatus = $"AI Planner: {_runtime.PlannerMode} | Policy: {_runtime.PolicyMode} | HUD: {(_showTelemetryHud ? "ON" : "OFF")} (T)";
#if DEBUG
        plannerStatus += " (F7 planner, F8 AI panel, PgUp/PgDn tracked NPC, Home latest, F4 compact)";
#endif
        if (_showTelemetryHud)
        {
            var aiDebug = _runtime.GetAiDebugSnapshot();
            _hudRenderer.Draw(
                _spriteBatch,
                _textures.Pixel,
                _font,
                snapshot,
                $"{_refineryRuntime.LastStatus} | {_runtime.LastTechActionStatus} | Theme: {ThemePresets[_themeIndex].Name}",
                plannerStatus,
                techMenu,
                aiDebug,
                _showAiDebugPanel,
                _aiDebugCompact,
                _aiGoalScoreOffset,
                _aiHistoryOffset,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                _showRenderStats ? _worldRenderer.LastRenderStats : null);
        }
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
