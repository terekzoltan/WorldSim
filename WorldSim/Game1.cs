using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Linq;
using WorldSim.Simulation;

namespace WorldSim
{
    public class Game1 : Game
    {
        GraphicsDeviceManager _g;
        SpriteBatch _sb;
        const float Tick = 0.25f;
        float _acc;
        World _world;
        SpriteFont _font;

        const int TileSize = 7;
        Texture2D _pixel;

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

            _sb.End();

            _sb.Begin();

            int j = 10;
            foreach (var colony in _world._colonies) 
            {  
                string stats = $"Colony {colony.Id}: Wood {colony.Stock[Resource.Wood]}, Houses {colony.HouseCount}, People {_world._people.Count(p => p.Home == colony)}";
                _sb.DrawString(_font, stats, new Vector2(10, j), colony.Color); 
                j += 20; 
            } 

            _sb.End();

            base.Draw(gt);
        }
    }
}
