using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.Assets;

public sealed class TextureCatalog
{
    public Texture2D Pixel { get; }
    public Texture2D Tree { get; }
    public Texture2D Rock { get; }
    public Texture2D Iron { get; }
    public Texture2D Gold { get; }
    public Texture2D SylvarPerson { get; }
    public Texture2D ObsidariPerson { get; }
    public Texture2D SylvarHouse { get; }
    public Texture2D ObsidariHouse { get; }
    public Texture2D AetheriHouse { get; }
    public Texture2D ChiritaHouse { get; }

    public TextureCatalog(GraphicsDevice graphicsDevice, ContentManager content)
    {
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });

        Tree = content.Load<Texture2D>("tree");
        Rock = content.Load<Texture2D>("rock");
        Iron = content.Load<Texture2D>("iron");
        Gold = content.Load<Texture2D>("gold");

        SylvarPerson = content.Load<Texture2D>("Sylvar");
        ObsidariPerson = content.Load<Texture2D>("Obsidiari");

        SylvarHouse = content.Load<Texture2D>("SylvarHouse");
        ObsidariHouse = content.Load<Texture2D>("ObsidiariHouse");
        AetheriHouse = content.Load<Texture2D>("AetheriHouse");
        ChiritaHouse = content.Load<Texture2D>("ChiritaHouse");
    }

    public Texture2D? GetPersonIcon(int colonyId)
    {
        return colonyId switch
        {
            0 => SylvarPerson,
            1 => ObsidariPerson,
            _ => null
        };
    }

    public Texture2D? GetHouseIcon(int colonyId)
    {
        return colonyId switch
        {
            0 => SylvarHouse,
            1 => ObsidariHouse,
            2 => AetheriHouse,
            3 => ChiritaHouse,
            _ => null
        };
    }
}
