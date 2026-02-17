using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.UI;

public sealed record HudTheme(
    Color PrimaryText,
    Color SecondaryText,
    Color AccentText,
    Color EventText,
    Color StatusText
)
{
    public static HudTheme Default { get; } = new(
        PrimaryText: Color.White,
        SecondaryText: Color.LightGray,
        AccentText: Color.LightGoldenrodYellow,
        EventText: Color.LightGray,
        StatusText: Color.LightCyan
    );
}
