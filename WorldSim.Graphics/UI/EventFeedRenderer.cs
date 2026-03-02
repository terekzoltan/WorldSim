using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class EventFeedRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, IReadOnlyList<string> events, int startX, int startY, int maxWidth, HudTheme theme)
    {
        var y = startY;
        foreach (var evt in events)
        {
            var category = EventFeedClassifier.Classify(evt);
            var color = GetColor(theme, category);
            y = TextWrap.DrawWrapped(spriteBatch, font, $"Event: {evt}", new Vector2(startX, y), color, maxWidth, 18);
        }

        return y;
    }

    private static Color GetColor(HudTheme theme, EventFeedCategory category)
    {
        return category switch
        {
            EventFeedCategory.Combat => theme.CombatEventText,
            EventFeedCategory.Siege => theme.SiegeEventText,
            EventFeedCategory.Campaign => theme.CampaignEventText,
            EventFeedCategory.Director => theme.DirectorEventText,
            _ => theme.EventText
        };
    }
}
