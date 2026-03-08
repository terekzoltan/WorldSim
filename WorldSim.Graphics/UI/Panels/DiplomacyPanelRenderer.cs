using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI.Panels;

public sealed class DiplomacyPanelRenderer
{
    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFont font,
        int viewportWidth,
        int viewportHeight,
        HudTheme theme,
        WorldRenderSnapshot snapshot)
    {
        int width = Math.Min(560, viewportWidth - 28);
        int height = 272;
        int x = 14;
        int y = viewportHeight - height - 14;
        var rect = new Rectangle(x, y, width, height);

        spriteBatch.Draw(pixel, rect, theme.PanelBackground);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), theme.PanelBorder);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), theme.PanelBorder);

        int pad = 10;
        int ty = y + pad;
        spriteBatch.DrawString(font, "Faction Relations", new Vector2(x + pad, ty), theme.AccentText);
        ty += 22;

        var factionIds = snapshot.Colonies
            .Select(colony => colony.FactionId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (factionIds.Count == 0)
        {
            spriteBatch.DrawString(font, "No faction data available.", new Vector2(x + pad, ty), theme.SecondaryText);
            return;
        }

        int matrixSize = factionIds.Count;
        int labelWidth = 54;
        int gridWidth = Math.Max(150, width - (pad * 2) - labelWidth);
        int cellSize = Math.Max(28, Math.Min(52, gridWidth / Math.Max(1, matrixSize)));
        int gridX = x + pad + labelWidth;
        int gridY = ty + 20;

        for (int i = 0; i < matrixSize; i++)
        {
            int faction = factionIds[i];
            var headerPos = new Vector2(gridX + (i * cellSize) + 8, ty);
            var rowPos = new Vector2(x + pad, gridY + (i * cellSize) + 7);
            string factionLabel = GetFactionAbbreviation(faction);
            spriteBatch.DrawString(font, factionLabel, headerPos, theme.PrimaryText);
            spriteBatch.DrawString(font, factionLabel, rowPos, theme.PrimaryText);
        }

        foreach (var rowFaction in factionIds)
        {
            foreach (var colFaction in factionIds)
            {
                int row = factionIds.IndexOf(rowFaction);
                int col = factionIds.IndexOf(colFaction);
                int cellX = gridX + (col * cellSize);
                int cellY = gridY + (row * cellSize);
                string stance = ResolveStance(snapshot, rowFaction, colFaction);
                Color fill = GetStanceColor(theme, stance);

                spriteBatch.Draw(pixel, new Rectangle(cellX, cellY, cellSize - 2, cellSize - 2), fill);
                spriteBatch.Draw(pixel, new Rectangle(cellX, cellY, cellSize - 2, 1), theme.PanelBorder);
                spriteBatch.Draw(pixel, new Rectangle(cellX, cellY + cellSize - 3, cellSize - 2, 1), theme.PanelBorder);
                spriteBatch.Draw(pixel, new Rectangle(cellX, cellY, 1, cellSize - 2), theme.PanelBorder);
                spriteBatch.Draw(pixel, new Rectangle(cellX + cellSize - 3, cellY, 1, cellSize - 2), theme.PanelBorder);

                string label = Abbrev(stance);
                spriteBatch.DrawString(font, label, new Vector2(cellX + 6, cellY + 7), theme.PrimaryText);
            }
        }

        int legendY = gridY + (matrixSize * cellSize) + 8;
        spriteBatch.DrawString(font, "Legend: N=Neutral  H=Hostile  W=War", new Vector2(x + pad, legendY), theme.SecondaryText);
        spriteBatch.DrawString(font, "Factions: Syl=Sylvars  Obs=Obsidari  Aet=Aetheri  Chi=Chirita", new Vector2(x + pad, legendY + 20), theme.SecondaryText);
        spriteBatch.DrawString(font, "Territory: Ctrl+F7 overlay (yellow border = contested)", new Vector2(x + pad, legendY + 40), theme.SecondaryText);
    }

    private static string ResolveStance(WorldRenderSnapshot snapshot, int leftFaction, int rightFaction)
    {
        if (leftFaction == rightFaction)
            return "Neutral";

        var direct = snapshot.FactionStances.FirstOrDefault(s => s.LeftFactionId == leftFaction && s.RightFactionId == rightFaction);
        if (direct != null)
            return direct.Stance;

        var reverse = snapshot.FactionStances.FirstOrDefault(s => s.LeftFactionId == rightFaction && s.RightFactionId == leftFaction);
        return reverse?.Stance ?? "Unknown";
    }

    private static Color GetStanceColor(HudTheme theme, string stance)
    {
        return stance.ToLowerInvariant() switch
        {
            "neutral" => theme.SuccessText * 0.45f,
            "hostile" => theme.WarningText * 0.5f,
            "war" => new Color(196, 90, 84) * 0.55f,
            _ => theme.PanelBackground * 0.75f
        };
    }

    private static string Abbrev(string stance)
    {
        return stance.ToLowerInvariant() switch
        {
            "neutral" => "N",
            "hostile" => "H",
            "war" => "W",
            _ => "?"
        };
    }

    private static string GetFactionAbbreviation(int factionId)
    {
        return factionId switch
        {
            0 => "Syl",
            1 => "Obs",
            2 => "Aet",
            3 => "Chi",
            _ => $"F{factionId}"
        };
    }
}
