using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;
using System.IO;
using WorldSim.Simulation;

namespace WorldSim
{
    public class Game1 : Game
    {
        GraphicsDeviceManager _g;
        SpriteBatch _sb;
        new const float Tick = 0.25f;
        float _acc;
        float timeScale = 10.0f; // Just for testing

        World _world;
        SpriteFont _font;

        const int TileSize = 7;
        Texture2D _pixel;
        Texture2D _treeTex;
        Texture2D _rockTex;

        bool _showTechMenu = false;
        KeyboardState _prevKeys;
        int _selectedColony = 0;

        // Zoom + camera state
        float _zoom = 1.0f;
        const float MinZoom = 0.5f;
        const float MaxZoom = 5.0f;
        int _prevWheel;

        Vector2 _camera = Vector2.Zero;          // world-space top-left (in pixels)
        bool _isPanning = false;
        Point _lastMousePos;

        public Game1()
        {
            _g = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _g.PreferredBackBufferWidth = 128 * TileSize;
            _g.PreferredBackBufferHeight = 128 * TileSize;
            _g.ApplyChanges();
        }

        protected override void Initialize()
        {
            _sb = new SpriteBatch(GraphicsDevice);
            _world = new World(width: 128, height: 128, initialPop: 25);
            string techPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tech", "technologies.json");
            TechTree.Load(techPath);

            // Initialize mouse wheel baseline
            _prevWheel = Mouse.GetState().ScrollWheelValue;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _sb     = new SpriteBatch(GraphicsDevice);
            _pixel  = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White});
            _font = Content.Load<SpriteFont>("DebugFont");

            // PNG icons from Content pipeline
            _treeTex = Content.Load<Texture2D>("tree");
            _rockTex = Content.Load<Texture2D>("rock");
        }

        protected override void Update(GameTime gt)
        {
            KeyboardState keys = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();

            if (keys.IsKeyDown(Keys.F1) && !_prevKeys.IsKeyDown(Keys.F1))
                _showTechMenu = !_showTechMenu;

            // Middle-mouse drag to pan
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
                Point cur = mouse.Position;
                Point deltaPt = cur - _lastMousePos;
                _lastMousePos = cur;

                // Divide by zoom so panning speed is consistent at all zoom levels
                _camera -= new Vector2(deltaPt.X, deltaPt.Y) / _zoom;
                ClampCamera();
            }

            // Zoom with mouse wheel (120 per notch), anchored to mouse position
            int wheel = mouse.ScrollWheelValue;
            int wheelDelta = wheel - _prevWheel;
            if (wheelDelta != 0)
            {
                float steps = wheelDelta / 120f;
                float factor = MathF.Pow(1.1f, steps); // ~10% per notch
                ZoomAt(new Vector2(mouse.X, mouse.Y), factor);
            }
            _prevWheel = wheel;

            // Zoom with +/- keys (also anchored to mouse for convenience)
            if ((keys.IsKeyDown(Keys.OemPlus) && !_prevKeys.IsKeyDown(Keys.OemPlus)) ||
                (keys.IsKeyDown(Keys.Add) && !_prevKeys.IsKeyDown(Keys.Add)))
            {
                ZoomAt(new Vector2(mouse.X, mouse.Y), 1.1f);
            }
            if ((keys.IsKeyDown(Keys.OemMinus) && !_prevKeys.IsKeyDown(Keys.OemMinus)) ||
                (keys.IsKeyDown(Keys.Subtract) && !_prevKeys.IsKeyDown(Keys.Subtract)))
            {
                ZoomAt(new Vector2(mouse.X, mouse.Y), 1f / 1.1f);
            }

            if (_showTechMenu)
            {
                if (keys.IsKeyDown(Keys.Left) && !_prevKeys.IsKeyDown(Keys.Left))
                    _selectedColony = (_selectedColony - 1 + _world._colonies.Count) % _world._colonies.Count;
                if (keys.IsKeyDown(Keys.Right) && !_prevKeys.IsKeyDown(Keys.Right))
                    _selectedColony = (_selectedColony + 1) % _world._colonies.Count;

                var colony = _world._colonies[_selectedColony];
                var locked = TechTree.Techs.Where(t => !colony.UnlockedTechs.Contains(t.Id)).ToList();
                for (int i = 0; i < locked.Count && i < 9; i++)
                {
                    Keys key = Keys.D1 + i;
                    if (keys.IsKeyDown(key) && !_prevKeys.IsKeyDown(key))
                        TechTree.Unlock(locked[i].Id, _world, colony);
                }
            }

            _prevKeys = keys;

            _acc += (float)gt.ElapsedGameTime.TotalSeconds * timeScale;
            while (_acc >= Tick)
            {
                _world.Update(Tick);
                _acc -= Tick;
            }
            base.Update(gt);
        }

        // Keep mouse position fixed when zooming
        void ZoomAt(Vector2 screenPoint, float zoomFactor)
        {
            // World position under the cursor BEFORE the zoom
            Vector2 worldBefore = ScreenToWorld(screenPoint);

            _zoom = Math.Clamp(_zoom * zoomFactor, MinZoom, MaxZoom);

            // Adjust camera so the same world point stays under the cursor AFTER zoom
            Vector2 worldAfter = ScreenToWorld(screenPoint);
            _camera += worldBefore - worldAfter;

            ClampCamera();
        }

        Vector2 ScreenToWorld(Vector2 screenPoint)
        {
            // screen = (world - camera) * zoom  => world = screen/zoom + camera
            return screenPoint / _zoom + _camera;
        }

        void ClampCamera()
        {
            float mapW = _world.Width * TileSize;
            float mapH = _world.Height * TileSize;

            float viewW = GraphicsDevice.Viewport.Width / _zoom;
            float viewH = GraphicsDevice.Viewport.Height / _zoom;

            float maxX = Math.Max(0f, mapW - viewW);
            float maxY = Math.Max(0f, mapH - viewH);

            _camera.X = Math.Clamp(_camera.X, 0f, maxX);
            _camera.Y = Math.Clamp(_camera.Y, 0f, maxY);
        }

        protected override void Draw(GameTime gt)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // World pass (scaled + translated by camera)
            var worldTransform =
                Matrix.CreateTranslation(-_camera.X, -_camera.Y, 0f) *
                Matrix.CreateScale(_zoom, _zoom, 1f);

            _sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: worldTransform);

            for (int y = 0; y < _world.Height; y++)
            {
                for (int x = 0; x < _world.Width; x++)
                {
                    Tile tile = _world.GetTile(x, y);
                    Color color = tile.Type switch
                    {
                        Resource.Wood => Color.ForestGreen,
                        Resource.Stone => Color.Gray,
                        Resource.Food => Color.Yellow,
                        Resource.Water => Color.Blue,
                        _ => Color.SandyBrown
                    };
                    int bx = x * TileSize;
                    int by = y * TileSize;

                    // Base tile
                    _sb.Draw(_pixel, new Rectangle(bx, by, TileSize, TileSize), color);

                    // Icon overlay
                    if (tile.Amount > 0)
                    {
                        if (tile.Type == Resource.Wood && _treeTex != null)
                            _sb.Draw(_treeTex, new Rectangle(bx, by, TileSize, TileSize), Color.White);
                        else if (tile.Type == Resource.Stone && _rockTex != null)
                            _sb.Draw(_rockTex, new Rectangle(bx, by, TileSize, TileSize), Color.White);
                    }
                }
            }

            foreach (Person person in _world._people)
            {
                _sb.Draw(
                    _pixel,
                    new Rectangle(person.Pos.x * TileSize, person.Pos.y * TileSize, TileSize, TileSize),
                    person.Color
                );
            }

            foreach (var house in _world.Houses)
            {
                Color houseColor = house.Owner.Color * 0.8f;
                int px = house.Pos.x * TileSize + (TileSize - 3) / 2;
                int py = house.Pos.y * TileSize + (TileSize - 3) / 2;
                _sb.Draw(_pixel, new Rectangle(px, py, TileSize*3, TileSize*3), houseColor);
            }

            foreach (var animal in _world._animals)
            {
                _sb.Draw(
                    _pixel,
                    new Rectangle(animal.Pos.x * TileSize, animal.Pos.y * TileSize, TileSize, TileSize),
                    animal.Color
                );
            }

            _sb.End();

            _sb.Begin();

            int j = 10;
            foreach (var colony in _world._colonies)
            {
                string stats =
                    $"Colony {colony.Id}: Wood {colony.Stock[Resource.Wood]}, Stone {colony.Stock[Resource.Stone]}, Houses {colony.HouseCount}, People {_world._people.Count(p => p.Home == colony)}";
                _sb.DrawString(_font, stats, new Vector2(10, j), colony.Color);
                j += 20;
            }

            if (_showTechMenu)
            {
                var colony = _world._colonies[_selectedColony];
                var locked = TechTree.Techs.Where(t => !colony.UnlockedTechs.Contains(t.Id)).ToList();
                j = 100;
                _sb.DrawString(_font, $"-- Tech Tree for Colony {colony.Id} (Left/Right to change, F1 to close) --", new Vector2(0, j), Color.White);
                j += 20;
                for (int i = 0; i < locked.Count && i < 9; i++)
                {
                    string line = $"{i + 1}. {locked[i].Name}";
                    _sb.DrawString(_font, line, new Vector2(0, j), Color.White);
                    j += 20;
                }
            }

            _sb.End();

            base.Draw(gt);
        }
    }
}
