using System;
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
            var color = GetColor(theme, category, evt);
            y = TextWrap.DrawWrapped(spriteBatch, font, $"Event: {evt}", new Vector2(startX, y), color, maxWidth, 18);
        }

        return y;
    }

    private static Color GetColor(HudTheme theme, EventFeedCategory category, string evt)
    {
        if (category == EventFeedCategory.Director)
        {
            if (evt.Contains("[Director:EPIC]", StringComparison.OrdinalIgnoreCase))
                return new Color(239, 158, 110);
            if (evt.Contains("[Director:MAJOR]", StringComparison.OrdinalIgnoreCase))
                return new Color(208, 175, 242);
            if (evt.Contains("[Director:MINOR]", StringComparison.OrdinalIgnoreCase))
                return new Color(155, 201, 242);
        }

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
