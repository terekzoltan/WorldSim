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
            y = TextWrap.DrawWrapped(spriteBatch, font, $"Event: {evt}", new Vector2(startX, y), theme.EventText, maxWidth, 18);
        }

        return y;
    }
}
