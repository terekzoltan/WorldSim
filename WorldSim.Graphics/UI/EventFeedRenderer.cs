using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class EventFeedRenderer
{
    public int Draw(SpriteBatch spriteBatch, SpriteFont font, IReadOnlyList<string> events, int startY, HudTheme theme)
    {
        var y = startY;
        foreach (var evt in events)
        {
            spriteBatch.DrawString(font, $"Event: {evt}", new Vector2(10, y), theme.EventText);
            y += 18;
        }

        return y;
    }
}
