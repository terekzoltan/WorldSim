using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

internal static class TextWrap
{
    public static int DrawWrapped(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string text,
        Vector2 origin,
        Color color,
        int maxWidth,
        int lineHeight)
    {
        var lines = Wrap(font, text, maxWidth);
        var y = (int)origin.Y;
        foreach (var line in lines)
        {
            spriteBatch.DrawString(font, line, new Vector2(origin.X, y), color);
            y += lineHeight;
        }

        return y;
    }

    private static IReadOnlyList<string> Wrap(SpriteFont font, string text, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new[] { string.Empty };

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (font.MeasureString(candidate).X <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = string.Empty;
            }

            if (font.MeasureString(word).X <= maxWidth)
            {
                current = word;
            }
            else
            {
                lines.Add(TrimToWidth(font, word, maxWidth));
            }
        }

        if (!string.IsNullOrEmpty(current))
            lines.Add(current);

        return lines;
    }

    private static string TrimToWidth(SpriteFont font, string value, int maxWidth)
    {
        const string ellipsis = "...";
        if (font.MeasureString(value).X <= maxWidth)
            return value;

        var limit = Math.Max(1, value.Length - 1);
        while (limit > 1)
        {
            var candidate = value[..limit] + ellipsis;
            if (font.MeasureString(candidate).X <= maxWidth)
                return candidate;

            limit--;
        }

        return ellipsis;
    }
}
