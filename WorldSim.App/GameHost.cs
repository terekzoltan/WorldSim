using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldSim.Graphics.Assets;
using WorldSim.Graphics.Camera;
using WorldSim.Graphics.Rendering;
using WorldSim.Graphics.Rendering.PostFx;
using WorldSim.Graphics.UI;
using WorldSim.Graphics.UI.Panels;
using WorldSim.RefineryAdapter;
using WorldSim.Runtime;
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;

namespace WorldSim;

public class GameHost : Game
{
    private enum VisualQualityProfile
    {
        Low,
        Medium,
        High
    }

    private static readonly float[] HudScales = { 1.0f, 1.15f, 1.3f };

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
    private readonly CameraRoutePlayer _cameraRoutePlayer = new();
    private readonly WorldRenderer _worldRenderer = new(theme: WorldRenderTheme.DaylightAtlas);
    private readonly HudRenderer _hudRenderer = new();
    private readonly SettingsPanelRenderer _settingsPanelRenderer = new();
    private readonly DiplomacyPanelRenderer _diplomacyPanelRenderer = new();
    private readonly CampaignPanelRenderer _campaignPanelRenderer = new();

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
    private bool _showSettingsOverlay;
    private bool _showDiplomacyPanel;
    private bool _showCampaignPanel;
    private VisualQualityProfile _qualityProfile = VisualQualityProfile.Medium;
    private int _hudScaleIndex;
    private float _hudOpacity = 1f;
    private bool _cleanShotMode;
    private bool _screenshotRequested;
    private string _lastCaptureStatus = "No capture";
    private bool _postFxEnabled = true;
    private PostFxQuality _postFxQuality = PostFxQuality.Medium;
    private string _toastMessage = string.Empty;
    private float _toastTimer;

    public GameHost()
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
        ApplyQualityProfile(_qualityProfile);
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

    private bool IsPlainPressed(KeyboardState keys, Keys key)
    {
        return keys.IsKeyDown(key)
            && !_previousKeys.IsKeyDown(key)
            && !IsCtrlDown(keys)
            && !IsShiftDown(keys)
            && !IsAltDown(keys);
    }

    private bool IsChordPressed(KeyboardState keys, Keys key, bool requireCtrl = false, bool requireShift = false, bool requireAlt = false)
    {
        if (!keys.IsKeyDown(key))
            return false;

        bool ctrlNow = IsCtrlDown(keys);
        bool shiftNow = IsShiftDown(keys);
        bool altNow = IsAltDown(keys);
        if (requireCtrl && !ctrlNow)
            return false;
        if (requireShift && !shiftNow)
            return false;
        if (requireAlt && !altNow)
            return false;

        bool wasDown = _previousKeys.IsKeyDown(key)
            && (!requireCtrl || IsCtrlDown(_previousKeys))
            && (!requireShift || IsShiftDown(_previousKeys))
            && (!requireAlt || IsAltDown(_previousKeys));

        return !wasDown;
    }

    private static bool IsShiftDown(KeyboardState keys)
        => keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);

    private static bool IsCtrlDown(KeyboardState keys)
        => keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);

    private static bool IsAltDown(KeyboardState keys)
        => keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt);

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();
        bool shift = IsShiftDown(keys);
        bool ctrl = IsCtrlDown(keys);
        bool alt = IsAltDown(keys);
        bool anyModifier = shift || ctrl || alt;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_cameraRoutePlayer.IsActive)
        {
            _cameraRoutePlayer.Update(dt, _camera, MinZoom, MaxZoom);
            ClampCamera();
        }

        _camera.Step(dt);

        float targetHudOpacity = _cleanShotMode ? 0f : (_cameraRoutePlayer.IsActive ? 0.35f : 1f);
        _hudOpacity = MathHelper.Lerp(_hudOpacity, targetHudOpacity, 1f - MathF.Exp(-8f * dt));
        _toastTimer = Math.Max(0f, _toastTimer - dt);

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

        if (IsChordPressed(keys, Keys.F1, requireCtrl: true))
        {
            _showDiplomacyPanel = !_showDiplomacyPanel;
            _showCampaignPanel = false;
            _showTechMenu = false;
            SetToast($"Diplomacy panel: {(_showDiplomacyPanel ? "ON" : "OFF")}");
        }

        if (IsPlainPressed(keys, Keys.F1))
        {
            _showTechMenu = !_showTechMenu;
            SetToast($"Tech menu: {(_showTechMenu ? "ON" : "OFF")}");
        }

        if (IsChordPressed(keys, Keys.F2, requireCtrl: true))
        {
            _showCampaignPanel = !_showCampaignPanel;
            _showDiplomacyPanel = false;
            _showTechMenu = false;
            SetToast($"Campaign panel: {(_showCampaignPanel ? "ON" : "OFF")}");
        }

        if (IsPlainPressed(keys, Keys.F2))
        {
            FocusCameraOnTrackedNpc();
        }

        if (IsChordPressed(keys, Keys.F9, requireCtrl: true))
            ToggleCinematicRoute();

        if (IsPlainPressed(keys, Keys.F9))
            CycleTheme(-1);

        if (IsChordPressed(keys, Keys.F10, requireCtrl: true))
        {
            _screenshotRequested = true;
            SetToast("Screenshot requested");
        }

        if (IsPlainPressed(keys, Keys.F10))
            CycleTheme(1);

        if (IsChordPressed(keys, Keys.F5, requireCtrl: true))
            CycleQualityProfile();

        if (IsChordPressed(keys, Keys.F6, requireCtrl: true))
            CycleHudScale();

        if (IsChordPressed(keys, Keys.F12, requireCtrl: true))
        {
            _showSettingsOverlay = !_showSettingsOverlay;
            SetToast($"Settings overlay: {(_showSettingsOverlay ? "ON" : "OFF")}");
        }

        if (IsChordPressed(keys, Keys.F7, requireCtrl: true))
        {
            _worldRenderer.TerritoryOverlayEnabled = !_worldRenderer.TerritoryOverlayEnabled;
            SetToast($"Territory overlay: {(_worldRenderer.TerritoryOverlayEnabled ? "ON" : "OFF")}");
        }

        if (IsChordPressed(keys, Keys.F8, requireCtrl: true))
        {
            _worldRenderer.CombatOverlayEnabled = !_worldRenderer.CombatOverlayEnabled;
            SetToast($"Combat overlay: {(_worldRenderer.CombatOverlayEnabled ? "ON" : "OFF")}");
        }

        if (IsPlainPressed(keys, Keys.F12))
        {
            _cleanShotMode = !_cleanShotMode;
            SetToast($"Clean shot: {(_cleanShotMode ? "ON" : "OFF")}");
        }

        if (IsPlainPressed(keys, Keys.F6))
            _refineryRuntime.Trigger(_runtime, _runtime.Tick);

#if DEBUG
        HandlePlannerToggleDebug(keys, anyModifier);
#endif

        if (IsPlainPressed(keys, Keys.F8))
            _showAiDebugPanel = !_showAiDebugPanel;

        HandleAiDebugInput(keys, anyModifier);

        if (IsChordPressed(keys, Keys.F3, requireCtrl: true))
        {
            _postFxEnabled = !_postFxEnabled;
            _worldRenderer.SetPostFxEnabled(_postFxEnabled);
            SetToast($"PostFx: {(_postFxEnabled ? "ON" : "OFF")}");
        }
        else if (IsPlainPressed(keys, Keys.F3))
        {
            _showRenderStats = !_showRenderStats;
        }

        if (IsChordPressed(keys, Keys.F4, requireCtrl: true))
        {
            _postFxQuality = _postFxQuality switch
            {
                PostFxQuality.Low => PostFxQuality.Medium,
                PostFxQuality.Medium => PostFxQuality.High,
                _ => PostFxQuality.Low
            };
            _worldRenderer.SetPostFxQuality(_postFxQuality);
            SetToast($"PostFx quality: {_postFxQuality}");
        }

        if (IsPlainPressed(keys, Keys.T))
            _showTelemetryHud = !_showTelemetryHud;

        if (!_cameraRoutePlayer.IsActive)
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

    private void HandleAiDebugInput(KeyboardState keys, bool modifiersDown)
    {
        if (!_showAiDebugPanel)
            return;

        if (keys.IsKeyDown(Keys.PageUp) && !_previousKeys.IsKeyDown(Keys.PageUp))
        {
            _runtime.CycleTrackedNpc(-1);
            SetTrackedNpcToast("Tracked NPC: previous");
        }

        if (keys.IsKeyDown(Keys.PageDown) && !_previousKeys.IsKeyDown(Keys.PageDown))
        {
            _runtime.CycleTrackedNpc(1);
            SetTrackedNpcToast("Tracked NPC: next");
        }

        if (keys.IsKeyDown(Keys.Home) && !_previousKeys.IsKeyDown(Keys.Home))
        {
            _runtime.ResetTrackedNpc();
            SetTrackedNpcToast("Tracked NPC: latest");
        }

        if (!modifiersDown && keys.IsKeyDown(Keys.F4) && !_previousKeys.IsKeyDown(Keys.F4))
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
    private void HandlePlannerToggleDebug(KeyboardState keys, bool modifiersDown)
    {
        if (modifiersDown)
            return;

        if (!keys.IsKeyDown(Keys.F7) || _previousKeys.IsKeyDown(Keys.F7))
            return;

        var nextMode = _runtime.PlannerMode switch
        {
            NpcPlannerMode.Simple => NpcPlannerMode.Goap,
            NpcPlannerMode.Goap => NpcPlannerMode.Htn,
            _ => NpcPlannerMode.Simple
        };
        _runtime = CreateRuntime(new RuntimeAiOptions { PlannerMode = nextMode, PolicyMode = _runtime.PolicyMode });
        SetToast($"Planner mode: {nextMode}");
        _showTechMenu = false;
        _cameraRoutePlayer.Stop();
        _cameraInitialized = false;
        FitCameraToViewport();
    }
#endif

    private void FocusCameraOnTrackedNpc()
    {
        var ai = _runtime.GetAiDebugSnapshot();
        if (!ai.HasData || ai.TrackedX < 0 || ai.TrackedY < 0)
        {
            SetToast("Tracked focus unavailable");
            return;
        }

        var tileSize = _worldRenderer.Settings.TileSize;
        float worldX = ai.TrackedX * tileSize;
        float worldY = ai.TrackedY * tileSize;

        float viewWidth = GraphicsDevice.Viewport.Width / _camera.Zoom;
        float viewHeight = GraphicsDevice.Viewport.Height / _camera.Zoom;
        var target = new Vector2(worldX - viewWidth * 0.5f, worldY - viewHeight * 0.5f);

        _camera.SetTarget(target, _camera.Zoom, MinZoom, MaxZoom);
        ClampCamera();
        SetToast($"Focus tracked NPC at ({ai.TrackedX},{ai.TrackedY})");
    }

    private void SetToast(string message)
    {
        _toastMessage = message;
        _toastTimer = 1.8f;
    }

    private void SetTrackedNpcToast(string prefix)
    {
        var ai = _runtime.GetAiDebugSnapshot();
        if (!ai.HasData)
        {
            SetToast($"{prefix} (no AI data)");
            return;
        }

        SetToast($"{prefix} #{ai.TrackedNpcIndex + 1}/{Math.Max(1, ai.TrackedNpcCount)} C{ai.TrackedColonyId} @({ai.TrackedX},{ai.TrackedY})");
    }

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
            _graphics.PreferredBackBufferWidth = 1600;
            _graphics.PreferredBackBufferHeight = 900;
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

    private void CycleQualityProfile()
    {
        _qualityProfile = _qualityProfile switch
        {
            VisualQualityProfile.Low => VisualQualityProfile.Medium,
            VisualQualityProfile.Medium => VisualQualityProfile.High,
            _ => VisualQualityProfile.Low
        };
        ApplyQualityProfile(_qualityProfile);
    }

    private void ApplyQualityProfile(VisualQualityProfile profile)
    {
        switch (profile)
        {
            case VisualQualityProfile.Low:
                _postFxEnabled = false;
                _postFxQuality = PostFxQuality.Low;
                break;
            case VisualQualityProfile.High:
                _postFxEnabled = true;
                _postFxQuality = PostFxQuality.High;
                break;
            default:
                _postFxEnabled = true;
                _postFxQuality = PostFxQuality.Medium;
                break;
        }

        _worldRenderer.SetPostFxEnabled(_postFxEnabled);
        _worldRenderer.SetPostFxQuality(_postFxQuality);

        SetToast($"Quality profile: {profile}");
    }

    private void CycleHudScale()
    {
        _hudScaleIndex = (_hudScaleIndex + 1) % HudScales.Length;
        SetToast($"HUD scale: {HudScales[_hudScaleIndex]:0.00}");
    }

    private void ToggleCinematicRoute()
    {
        if (_cameraRoutePlayer.IsActive)
        {
            _cameraRoutePlayer.Stop();
            SetToast("Cinematic route: stopped");
            return;
        }

        var route = BuildDefaultRoute();
        _cameraRoutePlayer.Start(route, _camera, MinZoom, MaxZoom);
        ClampCamera();
        SetToast("Cinematic route: playing");
    }

    private CameraRoute BuildDefaultRoute()
    {
        float mapWidth = _runtime.Width * _worldRenderer.Settings.TileSize;
        float mapHeight = _runtime.Height * _worldRenderer.Settings.TileSize;
        float viewportWidth = GraphicsDevice.Viewport.Width;
        float viewportHeight = GraphicsDevice.Viewport.Height;

        float coverZoom = Math.Clamp(MathF.Max(viewportWidth / mapWidth, viewportHeight / mapHeight), MinZoom, MaxZoom);
        float zoomWide = Math.Clamp(coverZoom * 1.15f, MinZoom, MaxZoom);
        float zoomClose = Math.Clamp(coverZoom * 1.45f, MinZoom, MaxZoom);

        Vector2 Focus(float normalizedX, float normalizedY, float zoom)
        {
            float centerX = mapWidth * normalizedX;
            float centerY = mapHeight * normalizedY;
            float viewW = viewportWidth / zoom;
            float viewH = viewportHeight / zoom;
            float x = Math.Clamp(centerX - viewW * 0.5f, 0f, Math.Max(0f, mapWidth - viewW));
            float y = Math.Clamp(centerY - viewH * 0.5f, 0f, Math.Max(0f, mapHeight - viewH));
            return new Vector2(x, y);
        }

        var keyframes = new List<CameraKeyframe>
        {
            new(_camera.TargetPosition, _camera.TargetZoom, 1f),
            new(Focus(0.2f, 0.2f, zoomWide), zoomWide, 5f),
            new(Focus(0.8f, 0.28f, zoomClose), zoomClose, 6f),
            new(Focus(0.72f, 0.78f, zoomWide), zoomWide, 6f),
            new(Focus(0.35f, 0.68f, zoomClose), zoomClose, 6f),
            new(Focus(0.5f, 0.5f, coverZoom), coverZoom, 5f)
        };

        return new CameraRoute(keyframes, loop: false);
    }

    private void CaptureScreenshot()
    {
        _screenshotRequested = false;

        try
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (width <= 0 || height <= 0)
            {
                _lastCaptureStatus = "Capture failed: invalid backbuffer size";
                return;
            }

            var data = new Color[width * height];
            GraphicsDevice.GetBackBufferData(data);

            using var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            texture.SetData(data);

            var screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(screenshotsDir);
            var filename = $"worldsim-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            var path = Path.Combine(screenshotsDir, filename);

            using var stream = File.Create(path);
            texture.SaveAsPng(stream, width, height);
            _lastCaptureStatus = filename;
            SetToast($"Captured: {filename}");
        }
        catch (Exception ex)
        {
            _lastCaptureStatus = $"Capture failed: {ex.Message}";
            SetToast(_lastCaptureStatus);
        }
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

        float hudScale = HudScales[_hudScaleIndex];
        int hudViewportWidth = Math.Max(1, (int)(GraphicsDevice.Viewport.Width / hudScale));
        int hudViewportHeight = Math.Max(1, (int)(GraphicsDevice.Viewport.Height / hudScale));
        bool panelExclusive = _showDiplomacyPanel || _showCampaignPanel;

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(hudScale, hudScale, 1f));
        var plannerStatus = $"AI Planner: {_runtime.PlannerMode} | Policy: {_runtime.PolicyMode} | HUD: {(_showTelemetryHud ? "ON" : "OFF")} (T) | PostFx: {(_postFxEnabled ? _postFxQuality.ToString() : "OFF")} | Q:{_qualityProfile}";
#if DEBUG
        plannerStatus += " (F2 tracked focus | Ctrl+F1/F2 panels | Ctrl+F3/F4 postfx | Ctrl+F5 quality | Ctrl+F6 HUD scale | Ctrl+F7/F8 overlays | Ctrl+F9 route | Ctrl+F10 screenshot | Ctrl+F12 settings)";
#endif
        if (_showTelemetryHud && !_cleanShotMode && !panelExclusive)
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
                hudViewportWidth,
                hudViewportHeight,
                _showRenderStats ? _worldRenderer.LastRenderStats : null,
                _hudOpacity);
        }

        if (_toastTimer > 0f)
            _spriteBatch.DrawString(_font, _toastMessage, new Vector2(12, Math.Max(12, hudViewportHeight - 40)), _hudRenderer.Theme.AccentText * MathF.Min(1f, _toastTimer));
        _spriteBatch.End();

        if (_showSettingsOverlay)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _settingsPanelRenderer.Draw(
                _spriteBatch,
                _textures.Pixel,
                _font,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                _hudRenderer.Theme,
                _qualityProfile.ToString(),
                _postFxEnabled ? _postFxQuality.ToString() : "OFF",
                hudScale.ToString("0.00"),
                _cameraRoutePlayer.IsActive ? "Playing" : "Idle",
                _lastCaptureStatus);

            var overlayStatus = $"Diplomacy:{(_showDiplomacyPanel ? "ON" : "off")}  Campaign:{(_showCampaignPanel ? "ON" : "off")}  Territory:{(_worldRenderer.TerritoryOverlayEnabled ? "ON" : "off")}  Combat:{(_worldRenderer.CombatOverlayEnabled ? "ON" : "off")}";
            _spriteBatch.DrawString(_font, overlayStatus, new Vector2(16, GraphicsDevice.Viewport.Height - 34), _hudRenderer.Theme.SecondaryText);
            _spriteBatch.End();
        }

        if (_showDiplomacyPanel && !_cleanShotMode)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _diplomacyPanelRenderer.Draw(
                _spriteBatch,
                _textures.Pixel,
                _font,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                _hudRenderer.Theme);
            _spriteBatch.End();
        }

        if (_showCampaignPanel && !_cleanShotMode)
        {
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _campaignPanelRenderer.Draw(
                _spriteBatch,
                _textures.Pixel,
                _font,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height,
                _hudRenderer.Theme);
            _spriteBatch.End();
        }

        if (_screenshotRequested)
            CaptureScreenshot();

        base.Draw(gameTime);
    }
}
