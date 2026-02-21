using Microsoft.Xna.Framework;

namespace WorldSim.Graphics.UI;

public sealed record HudTheme(
    Color PrimaryText,
    Color SecondaryText,
    Color AccentText,
    Color EventText,
    Color StatusText,
    Color PanelBackground,
    Color PanelBorder,
    Color WarningText,
    Color SuccessText
)
{
    public static HudTheme Default { get; } = new(
        PrimaryText: Color.White,
        SecondaryText: Color.LightGray,
        AccentText: Color.LightGoldenrodYellow,
        EventText: Color.LightGray,
        StatusText: Color.LightCyan,
        PanelBackground: new Color(17, 25, 34, 180),
        PanelBorder: new Color(126, 165, 200, 220),
        WarningText: new Color(233, 159, 94),
        SuccessText: new Color(132, 220, 142)
    );

    public static HudTheme FromWorldTheme(WorldSim.Graphics.Rendering.WorldRenderTheme theme)
    {
        return new HudTheme(
            PrimaryText: Color.White,
            SecondaryText: Color.LightGray,
            AccentText: theme.Highlight,
            EventText: Color.LightGray,
            StatusText: Color.LightCyan,
            PanelBackground: theme.PanelBackground,
            PanelBorder: theme.PanelBorder,
            WarningText: theme.Warning,
            SuccessText: theme.Success);
    }
}
