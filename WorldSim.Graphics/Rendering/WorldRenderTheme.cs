using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.Rendering;

public sealed record WorldRenderTheme(
    Color Background,
    Color Water,
    Color Grass,
    Color Dirt,
    Color FoodNode,
    Color Predator,
    Color Herbivore
)
{
    public static WorldRenderTheme Default { get; } = new(
        Background: Color.CornflowerBlue,
        Water: Color.Blue,
        Grass: Color.ForestGreen,
        Dirt: Color.SandyBrown,
        FoodNode: Color.YellowGreen,
        Predator: Color.Red,
        Herbivore: Color.LightGreen
    );

    public static WorldRenderTheme DaylightAtlas { get; } = new(
        Background: new Color(146, 196, 222),
        Water: new Color(64, 143, 212),
        Grass: new Color(86, 143, 84),
        Dirt: new Color(177, 146, 111),
        FoodNode: new Color(180, 196, 88),
        Predator: new Color(190, 72, 58),
        Herbivore: new Color(131, 201, 139)
    );

    public static WorldRenderTheme ParchmentFrontier { get; } = new(
        Background: new Color(217, 201, 173),
        Water: new Color(110, 139, 164),
        Grass: new Color(122, 141, 89),
        Dirt: new Color(165, 130, 94),
        FoodNode: new Color(170, 153, 78),
        Predator: new Color(138, 67, 51),
        Herbivore: new Color(151, 169, 117)
    );

    public static WorldRenderTheme IndustrialDawn { get; } = new(
        Background: new Color(112, 128, 146),
        Water: new Color(77, 108, 140),
        Grass: new Color(100, 126, 94),
        Dirt: new Color(131, 112, 95),
        FoodNode: new Color(154, 167, 81),
        Predator: new Color(161, 74, 62),
        Herbivore: new Color(123, 170, 126)
    );
}
