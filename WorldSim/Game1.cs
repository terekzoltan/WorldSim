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
        float timeScale = 5.0f; // Just for testing

        World _world;
        SpriteFont _font;

        const int TileSize = 7;
        Texture2D _pixel;

        bool _showTechMenu = false;
        KeyboardState _prevKeys;
        int _selectedColony = 0;

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
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _sb     = new SpriteBatch(GraphicsDevice);
            _pixel  = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White});
            _font = Content.Load<SpriteFont>("DebugFont");
        }

        protected override void Update(GameTime gt)
        {
            KeyboardState keys = Keyboard.GetState();

            if (keys.IsKeyDown(Keys.F1) && !_prevKeys.IsKeyDown(Keys.F1))
                _showTechMenu = !_showTechMenu;

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

        protected override void Draw(GameTime gt)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _sb.Begin();

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
                    _sb.Draw(_pixel, new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize), color);
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

            _sb.End();

            _sb.Begin();

            int j = 10;
            foreach (var colony in _world._colonies)
            {
                string stats = $"Colony {colony.Id}: Wood {colony.Stock[Resource.Wood]}, Houses {colony.HouseCount}, People {_world._people.Count(p => p.Home == colony)}";
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
