using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using WorldSim.Simulation;

namespace WorldSim
{
    public class Game1 : Game
    {
        GraphicsDeviceManager _g;
        SpriteBatch _sb;
        const float Tick = 0.25f;          // simulation 4× per second
        float _acc;
        World _world;
        SpriteFont _font;

        // tile square size (px)
        const int TileSize = 4;
        Texture2D _pixel;

        public Game1()
        {
            _g = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // set the window size based on TileSize
            _g.PreferredBackBufferWidth = 128 * TileSize;
            _g.PreferredBackBufferHeight = 128 * TileSize;
            _g.ApplyChanges();
        }

        protected override void Initialize()
        {
            _sb = new SpriteBatch(GraphicsDevice);
            _sb = new(GraphicsDevice);
            _world = new World(width: 128, height: 128, initialPop: 25);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            // create SpriteBatch and a 1×1 pixel
            _sb     = new SpriteBatch(GraphicsDevice);
            _pixel  = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White});
            _font = Content.Load<SpriteFont>("DebugFont");
        }

        protected override void Update(GameTime gt)
        {
            _acc += (float)gt.ElapsedGameTime.TotalSeconds;
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

            // draw tiles
            for (int y = 0; y < _world.Height; y++)
            {
                for (int x = 0; x < _world.Width; x++)
                {
                    var tile = _world.GetTile(x, y);
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

            foreach (var person in _world._people)
            {
                _sb.Draw(
                    _pixel,
                    new Rectangle(person.Pos.x * TileSize, person.Pos.y * TileSize, TileSize, TileSize),
                    Color.Red
                );
            }

            _sb.End();

            _sb.Begin();
            var colony = _world._colonies[0];
            string stats = $"Wood: {colony.Stock[Resource.Wood]}\nHouses: {colony.HouseCount}";
            _sb.DrawString(_font, stats, new Vector2(10, 10), Color.White);
            _sb.End();

            base.Draw(gt);
        }
    }
}
