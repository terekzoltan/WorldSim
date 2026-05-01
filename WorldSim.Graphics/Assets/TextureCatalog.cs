using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

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
    public Texture2D AetheriPerson { get; }
    public Texture2D ChiritaPerson { get; }
    public Texture2D SylvarHouse { get; }
    public Texture2D ObsidariHouse { get; }
    public Texture2D AetheriHouse { get; }
    public Texture2D ChiritaHouse { get; }
    public Texture2D? Predator { get; }
    public Texture2D? Herbivore { get; }
    public Texture2D? Food { get; }
    public Texture2D? FarmPlot { get; }
    public Texture2D? Workshop { get; }
    public Texture2D? Storehouse { get; }
    public Texture2D? WoodWall { get; }
    public Texture2D? StoneWall { get; }
    public Texture2D? ReinforcedWall { get; }
    public Texture2D? Gate { get; }
    public Texture2D? Watchtower { get; }
    public Texture2D? ArrowTower { get; }
    public Texture2D? CatapultTower { get; }
    public Texture2D MissingTexture { get; }

    public TextureCatalog(GraphicsDevice graphicsDevice, ContentManager content)
    {
        Pixel = new Texture2D(graphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });

        MissingTexture = new Texture2D(graphicsDevice, 2, 2);
        MissingTexture.SetData(new[]
        {
            Color.Magenta, Color.Black,
            Color.Black, Color.Magenta
        });

        Tree = LoadOrFallback(content, "tree");
        Rock = LoadOrFallback(content, "rock");
        Iron = LoadOrFallback(content, "iron");
        Gold = LoadOrFallback(content, "gold");

        SylvarPerson = LoadOrFallback(content, "Sylvar");
        ObsidariPerson = LoadOrFallback(content, "Obsidiari");
        AetheriPerson = LoadOrFallback(content, "Aetheri");
        ChiritaPerson = LoadOrFallback(content, "Chirita");

        SylvarHouse = LoadOrFallback(content, "SylvarHouse");
        ObsidariHouse = LoadOrFallback(content, "ObsidiariHouse");
        AetheriHouse = LoadOrFallback(content, "AetheriHouse");
        ChiritaHouse = LoadOrFallback(content, "ChiritaHouse");

        Predator = LoadOptional(content, "predator");
        Herbivore = LoadOptional(content, "herbivore");
        Food = LoadOptional(content, "food1") ?? LoadOptional(content, "food");
        FarmPlot = LoadOptional(content, "farmplot");
        Workshop = LoadOptional(content, "workshop");
        Storehouse = LoadOptional(content, "storehouse");

        WoodWall = LoadOptional(content, "woodwall");
        StoneWall = LoadOptional(content, "stonewall");
        ReinforcedWall = LoadOptional(content, "reinforcedwall");
        Gate = LoadOptional(content, "gate");
        Watchtower = LoadOptional(content, "watchtower");
        ArrowTower = LoadOptional(content, "arrowtower");
        CatapultTower = LoadOptional(content, "catapulttower");
    }

    private Texture2D LoadOrFallback(ContentManager content, string assetName)
    {
        try
        {
            return content.Load<Texture2D>(assetName);
        }
        catch
        {
            return MissingTexture;
        }
    }

    private static Texture2D? LoadOptional(ContentManager content, string assetName)
    {
        try
        {
            return content.Load<Texture2D>(assetName);
        }
        catch
        {
            return null;
        }
    }

    public Texture2D? GetPersonIcon(int colonyId)
    {
        return colonyId switch
        {
            0 => SylvarPerson,
            1 => ObsidariPerson,
            2 => AetheriPerson,
            3 => ChiritaPerson,
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

    public Texture2D? GetAnimalIcon(AnimalKindView kind)
    {
        return kind switch
        {
            AnimalKindView.Predator => Predator,
            AnimalKindView.Herbivore => Herbivore,
            _ => null
        };
    }

    public Texture2D? GetSpecializedBuildingIcon(SpecializedBuildingKindView kind)
    {
        return kind switch
        {
            SpecializedBuildingKindView.FarmPlot => FarmPlot,
            SpecializedBuildingKindView.Workshop => Workshop,
            SpecializedBuildingKindView.Storehouse => Storehouse,
            _ => null
        };
    }

    public Texture2D? GetDefensiveStructureIcon(DefensiveStructureKindView kind)
    {
        return kind switch
        {
            DefensiveStructureKindView.WoodWall => WoodWall,
            DefensiveStructureKindView.StoneWall => StoneWall,
            DefensiveStructureKindView.ReinforcedWall => ReinforcedWall,
            DefensiveStructureKindView.Gate => Gate,
            DefensiveStructureKindView.Watchtower => Watchtower,
            DefensiveStructureKindView.ArrowTower => ArrowTower,
            DefensiveStructureKindView.CatapultTower => CatapultTower,
            _ => null
        };
    }
}
