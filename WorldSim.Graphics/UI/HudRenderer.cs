using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class HudRenderer
{
    private readonly ColonyPanelRenderer _colonyPanel = new();
    private readonly EcologyPanelRenderer _ecologyPanel = new();
    private readonly EventFeedRenderer _eventFeed = new();
    private readonly TechMenuPanelRenderer _techMenuPanel = new();

    public HudTheme Theme { get; }

    public HudRenderer(HudTheme? theme = null)
    {
        Theme = theme ?? HudTheme.Default;
    }

    public void Draw(
        SpriteBatch spriteBatch,
        SpriteFont font,
        WorldRenderSnapshot snapshot,
        string refineryStatus,
        TechMenuView? techMenu)
    {
        var y = 10;
        y = _colonyPanel.Draw(spriteBatch, font, snapshot.Colonies, y, Theme);
        y = _ecologyPanel.Draw(spriteBatch, font, snapshot, y, Theme);
        y = _eventFeed.Draw(spriteBatch, font, snapshot.RecentEvents, y, Theme);
        spriteBatch.DrawString(font, refineryStatus, new Vector2(10, y + 10), Theme.StatusText);

        if (techMenu == null)
            return;

        _techMenuPanel.Draw(spriteBatch, font, techMenu.Value, Theme);
    }
}

public readonly record struct TechMenuView(int ColonyId, IReadOnlyList<string> LockedTechNames);
