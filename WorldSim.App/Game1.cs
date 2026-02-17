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
using WorldSim.Runtime.ReadModel;
using WorldSim.Simulation;

namespace WorldSim;

public class Game1 : Game
{
    private const float SimulationTickDuration = 0.25f;
    private const float MinZoom = 0.5f;
    private const float MaxZoom = 5.0f;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private World _world = null!;
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
    private long _simTick;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = display.Width;
        _graphics.PreferredBackBufferHeight = display.Height;
        _graphics.IsFullScreen = true;
        _graphics.ApplyChanges();

        _refineryRuntime = new RefineryTriggerAdapter(AppDomain.CurrentDomain.BaseDirectory);
    }

    protected override void Initialize()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _world = new World(width: 128, height: 128, initialPop: 25);

        var techPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tech", "technologies.json");
        TechTree.Load(techPath);

        _previousWheel = Mouse.GetState().ScrollWheelValue;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _textures = new TextureCatalog(GraphicsDevice, Content);
        _font = Content.Load<SpriteFont>("DebugFont");
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (keys.IsKeyDown(Keys.F1) && !_previousKeys.IsKeyDown(Keys.F1))
            _showTechMenu = !_showTechMenu;

        if (keys.IsKeyDown(Keys.F6) && !_previousKeys.IsKeyDown(Keys.F6))
            _refineryRuntime.Trigger(_world, _simTick);

        HandleCameraInput(keys, mouse);
        HandleTechMenuInput(keys);

        _previousKeys = keys;
        _refineryRuntime.Pump();

        _accumulator += (float)gameTime.ElapsedGameTime.TotalSeconds * _timeScale;
        while (_accumulator >= SimulationTickDuration)
        {
            _world.Update(SimulationTickDuration);
            _simTick++;
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
        if (!_showTechMenu || _world._colonies.Count == 0)
            return;

        if (keys.IsKeyDown(Keys.Left) && !_previousKeys.IsKeyDown(Keys.Left))
            _selectedColony = (_selectedColony - 1 + _world._colonies.Count) % _world._colonies.Count;
        if (keys.IsKeyDown(Keys.Right) && !_previousKeys.IsKeyDown(Keys.Right))
            _selectedColony = (_selectedColony + 1) % _world._colonies.Count;

        var colony = _world._colonies[_selectedColony];
        var locked = TechTree.Techs.Where(t => !colony.UnlockedTechs.Contains(t.Id)).ToList();
        for (var i = 0; i < locked.Count && i < 9; i++)
        {
            var hotkey = Keys.D1 + i;
            if (keys.IsKeyDown(hotkey) && !_previousKeys.IsKeyDown(hotkey))
                TechTree.Unlock(locked[i].Id, _world, colony);
        }
    }

    private void ClampCamera()
    {
        _camera.ClampToWorld(
            _world.Width * _worldRenderer.Settings.TileSize,
            _world.Height * _worldRenderer.Settings.TileSize,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_worldRenderer.Theme.Background);

        var snapshot = WorldSnapshotBuilder.Build(_world);
        _worldRenderer.Draw(_spriteBatch, snapshot, _camera, _textures);

        TechMenuView? techMenu = null;
        if (_showTechMenu && _world._colonies.Count > 0)
        {
            var colony = _world._colonies[_selectedColony];
            var lockedNames = TechTree.Techs
                .Where(t => !colony.UnlockedTechs.Contains(t.Id))
                .Select(t => t.Name)
                .ToList();
            techMenu = new TechMenuView(colony.Id, lockedNames);
        }

        _spriteBatch.Begin();
        _hudRenderer.Draw(_spriteBatch, _font, snapshot, _refineryRuntime.LastStatus, techMenu);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
