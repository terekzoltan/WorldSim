using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WorldSim.Graphics.UI;

public sealed class TechMenuPanelRenderer
{
    private static readonly string[] MilitaryKeywords =
    {
        "weapon",
        "armor",
        "military",
        "war drums",
        "scout",
        "tactic",
        "fortification",
        "siege"
    };

    public void Draw(SpriteBatch spriteBatch, SpriteFont font, TechMenuView menu, HudTheme theme)
    {
        var y = 100;
        spriteBatch.DrawString(
            font,
            $"-- Tech Tree for Colony {menu.ColonyId} (Left/Right to change, F1 to close) --",
            new Vector2(0, y),
            theme.PrimaryText);
        y += 20;

        bool militaryHeaderDrawn = false;
        foreach (var entry in BuildVisibleEntries(menu.LockedTechNames))
        {
            if (entry.IsMilitary && !militaryHeaderDrawn)
            {
                spriteBatch.DrawString(font, "-- Military & Fortification --", new Vector2(0, y), theme.AccentText);
                y += 20;
                militaryHeaderDrawn = true;
            }

            spriteBatch.DrawString(font, $"{entry.Slot + 1}. {entry.Name}", new Vector2(0, y), theme.PrimaryText);
            y += 20;
        }
    }

    private static IReadOnlyList<(int Slot, string Name, bool IsMilitary)> BuildVisibleEntries(IReadOnlyList<string> names)
    {
        var entries = new List<(int Slot, string Name, bool IsMilitary)>();
        for (int i = 0; i < names.Count && i < 9; i++)
        {
            var name = names[i];
            entries.Add((i, name, IsMilitaryTechName(name)));
        }

        return entries;
    }

    private static bool IsMilitaryTechName(string techName)
    {
        var normalized = techName.Trim().ToLowerInvariant();
        return MilitaryKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
    }
}
