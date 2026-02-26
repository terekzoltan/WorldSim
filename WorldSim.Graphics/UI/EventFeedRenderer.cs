using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class EventFeedRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, IReadOnlyList<string> events, int startX, int startY, int maxWidth, HudTheme theme)
    {
        var y = startY;
        foreach (var evt in events.Reverse())
        {
            var category = EventFeedClassifier.Classify(evt);
            var color = category switch
            {
                EventFeedCategory.Combat => theme.WarningText,
                EventFeedCategory.Siege => theme.WarningText,
                EventFeedCategory.Campaign => theme.AccentText,
                EventFeedCategory.Director => theme.SuccessText,
                _ => theme.EventText
            };

            y = TextWrap.DrawWrapped(spriteBatch, font, $"[{category}] {evt}", new Vector2(startX, y), color, maxWidth, 18);
        }

        return y;
    }
}
