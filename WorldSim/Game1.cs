using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;
using System.IO;
using WorldSim.Simulation;
using WorldSim.Integration;

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

        // mezők
        const int TileSize = 7;
        const float IconScale = 3.0f; // draw icons larger than a tile (resource/person)
        const int HouseIconTiles = 3; // house icon size in tiles (3x3)
        Texture2D _pixel;
        Texture2D _treeTex;
        Texture2D _rockTex;
        Texture2D _ironTex;
        Texture2D _goldTex;

        Texture2D _sylvarsTex;    // person icon (Cyan)
        Texture2D _obsidariTex;   // person icon (Bronze)
        // Texture2D _aetheriTex; // person icon (Purple)
        // Texture2D _chitariTex; // person icon (Amber)

        // house icons
        Texture2D _sylvarHouseTex;
        Texture2D _obsidiariHouseTex;
        Texture2D _aetheriHouseTex;   // (előre bekészítve)
        Texture2D _chiritaHouseTex;   // (előre bekészítve)

        bool _showTechMenu = false;
        KeyboardState _prevKeys;
        int _selectedColony = 0;
        long _simTick;
        RefineryPatchRuntime _refineryRuntime;

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
            _g.PreferredBackBufferHeight = 110 * TileSize;
            _g.IsFullScreen = false;
            _g.ApplyChanges();

            // Ensure the window starts windowed, is resizable and positioned
            // so the OS window chrome (close button) remains visible.
            Window.AllowUserResizing = true;
            try
            {
                Window.Position = new Point(50, 50);
            }
            catch
            {
                // Some platforms may not support setting position; ignore failures.
            }

            var refineryOptions = RefineryRuntimeOptions.FromEnvironment(AppDomain.CurrentDomain.BaseDirectory);
            _refineryRuntime = new RefineryPatchRuntime(refineryOptions);
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
            _ironTex = Content.Load<Texture2D>("iron");
            _goldTex = Content.Load<Texture2D>("gold");

            // persons
            _sylvarsTex  = Content.Load<Texture2D>("Sylvar");
            _obsidariTex = Content.Load<Texture2D>("Obsidiari");
            // _aetheriTex  = Content.Load<Texture2D>("Aetheri");
            // _chitariTex  = Content.Load<Texture2D>("Chirita");

            // houses (ikonnevek: ChiritaHouse, AetheriHouse, ObsidiariHouse, SylvarHouse)
            _sylvarHouseTex    = Content.Load<Texture2D>("SylvarHouse");
            _obsidiariHouseTex = Content.Load<Texture2D>("ObsidiariHouse");
            _aetheriHouseTex   = Content.Load<Texture2D>("AetheriHouse");   // későbbre
            _chiritaHouseTex   = Content.Load<Texture2D>("ChiritaHouse");   // későbbre
        }

        protected override void Update(GameTime gt)
        {
            KeyboardState keys = Keyboard.GetState();
            MouseState mouse = Mouse.GetState();

            if (keys.IsKeyDown(Keys.F1) && !_prevKeys.IsKeyDown(Keys.F1))
                _showTechMenu = !_showTechMenu;

            if (keys.IsKeyDown(Keys.F6) && !_prevKeys.IsKeyDown(Keys.F6))
            {
                _refineryRuntime.Trigger(_world, _simTick);
            }

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
            _refineryRuntime.Pump();

            _acc += (float)gt.ElapsedGameTime.TotalSeconds * timeScale;
            while (_acc >= Tick)
            {
                _world.Update(Tick);
                _simTick++;
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

        // helper az ikon kiválasztásához kolónia alapján (személy)
        Texture2D? GetColonyIcon(Colony c)
        {
            // Select by Id instead of color
            switch (c.Id)
            {
                case 0: return _sylvarsTex;   // Sylvars
                case 1: return _obsidariTex;  // Obsidari
                // case 2: return _aetheriTex; // Aetheri (if enabled)
                // case 3: return _chitariTex; // Chitáriak (if enabled)
                default: return null;
            }
        }

        // helper az ikon kiválasztásához kolónia alapján (ház)
        Texture2D? GetHouseIcon(Colony c)
        {
            // Select by Id instead of color
            switch (c.Id)
            {
                case 0: return _sylvarHouseTex;      // SylvarHouse
                case 1: return _obsidiariHouseTex;   // ObsidiariHouse
                case 2: return _aetheriHouseTex;     // AetheriHouse (loaded)
                case 3: return _chiritaHouseTex;     // ChiritaHouse (loaded)
                default: return null;
            }
        }

        protected override void Draw(GameTime gt)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // World pass (scaled + translated by camera)
            var worldTransform =
                Matrix.CreateTranslation(-_camera.X, -_camera.Y, 0f) *
                Matrix.CreateScale(_zoom, _zoom, 1f);

            _sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: worldTransform);

            // 1) Base tiles
            for (int y = 0; y < _world.Height; y++)
            {
                for (int x = 0; x < _world.Width; x++)
                {
                    Tile tile = _world.GetTile(x, y);
                    Color color = tile.Ground switch
                    {
                        Ground.Water => Color.Blue,
                        Ground.Grass => Color.ForestGreen,
                        _ => Color.SandyBrown // Dirt és egyéb
                    };
                    int bx = x * TileSize;
                    int by = y * TileSize;

                    _sb.Draw(_pixel, new Rectangle(bx, by, TileSize, TileSize), color);
                }
            }

            // 2) Icons for resource nodes
            for (int y = 0; y < _world.Height; y++)
            {
                for (int x = 0; x < _world.Width; x++)
                {
                    Tile tile = _world.GetTile(x, y);
                    var node = tile.Node;
                    if (node != null && node.Amount > 0)
                    {
                        int bx = x * TileSize;
                        int by = y * TileSize;

                        int iconSize = (int)MathF.Round(TileSize * IconScale);
                        int iconX = bx + (TileSize - iconSize) / 2;
                        int iconY = by + (TileSize - iconSize) / 2;

                        if (node.Type == Resource.Wood && _treeTex != null)
                            _sb.Draw(_treeTex, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                        else if (node.Type == Resource.Stone && _rockTex != null)
                            _sb.Draw(_rockTex, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                        else if (node.Type == Resource.Iron && _ironTex != null)
                            _sb.Draw(_ironTex, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                        else if (node.Type == Resource.Gold && _goldTex != null)
                            _sb.Draw(_goldTex, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                    }
                }
            }

            // 3) Houses
            foreach (var house in _world.Houses)
            {
                int hx = house.Pos.x * TileSize;
                int hy = house.Pos.y * TileSize;

                // 3x3 pixel “base” marker a tile közepén (mindig)
                int baseW = 3, baseH = 3;
                int baseX = hx + (TileSize - baseW) / 2;
                int baseY = hy + (TileSize - baseH) / 2;
                _sb.Draw(_pixel, new Rectangle(baseX, baseY, baseW, baseH), Color.White);

                // Ház ikon: ~3x3 tile méretű, a tile középpontjára igazítva
                var hIcon = GetHouseIcon(house.Owner);
                if (hIcon != null)
                {
                    int iconSize = TileSize * HouseIconTiles * 2; // pontosan 3x3 tile
                    int iconX = hx + (TileSize - iconSize) / 2;
                    int iconY = hy + (TileSize - iconSize) / 2;
                    _sb.Draw(hIcon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                }
                // nincs else: ha nincs ikon, elég a 3x3 pixeles marker
            }

            // 4) Személyek (frakcióikon, ha van)
            foreach (Person person in _world._people)
            {
                int dx = person.Pos.x * TileSize;
                int dy = person.Pos.y * TileSize;

                var icon = GetColonyIcon(person.Home);
                if (icon != null)
                {
                    int iconSize = (int)MathF.Round(TileSize * IconScale);
                    int iconX = dx + (TileSize - iconSize) / 2;
                    int iconY = dy + (TileSize - iconSize) / 2;
                    _sb.Draw(icon, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                }
                else
                {
                    _sb.Draw(_pixel, new Rectangle(dx, dy, TileSize, TileSize), Color.White);
                }
            }

            // 5) Animals
            foreach (var animal in _world._animals)
            {
                _sb.Draw(
                    _pixel,
                    new Rectangle(animal.Pos.x * TileSize, animal.Pos.y * TileSize, TileSize, TileSize),
                    animal.Color
                );
            }

            _sb.End();

            // UI pass (unchanged)
            _sb.Begin();

            int j = 10;
            foreach (var colony in _world._colonies)
            {
                string stats =
                    $"Colony {colony.Id}: Wood {colony.Stock[Resource.Wood]}, Stone {colony.Stock[Resource.Stone]}, Iron {colony.Stock[Resource.Iron]}, Gold {colony.Stock[Resource.Gold]}, Houses {colony.HouseCount}, People {_world._people.Count(p => p.Home == colony)}";
                _sb.DrawString(_font, stats, new Vector2(10, j), Color.White);
                j += 20;
            }

            _sb.DrawString(_font, _refineryRuntime.LastStatus, new Vector2(10, j + 10), Color.LightCyan);

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
