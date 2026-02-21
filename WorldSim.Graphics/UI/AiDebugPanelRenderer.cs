using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class AiDebugPanelRenderer
{
    public void Draw(SpriteBatch spriteBatch, SpriteFont font, AiDebugSnapshot debug, int viewportWidth, HudTheme theme)
    {
        var x = Math.Max(20, viewportWidth - 560);
        var y = 10;

        if (!debug.HasData)
        {
            spriteBatch.DrawString(font, "AI Debug: no decision trace yet", new Vector2(x, y), theme.StatusText);
            return;
        }

        spriteBatch.DrawString(font, $"AI Debug [{debug.PolicyMode}]", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        spriteBatch.DrawString(font, $"Planner {debug.PlannerMode} | Goal {debug.SelectedGoal} | Next {debug.NextCommand} | Plan {debug.PlanLength}", new Vector2(x, y), theme.StatusText);
        y += 20;
        spriteBatch.DrawString(font, $"Tracked NPC colony {debug.TrackedColonyId} @ ({debug.TrackedX},{debug.TrackedY})", new Vector2(x, y), theme.StatusText);
        y += 24;

        spriteBatch.DrawString(font, "Goal scores:", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        foreach (var score in debug.GoalScores)
        {
            var cooldown = score.IsOnCooldown ? " CD" : string.Empty;
            spriteBatch.DrawString(font, $"- {score.GoalName}: {score.Score:0.00}{cooldown}", new Vector2(x + 8, y), theme.StatusText);
            y += 18;
        }

        y += 6;
        spriteBatch.DrawString(font, "Recent decisions:", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        foreach (var entry in debug.RecentDecisions)
        {
            spriteBatch.DrawString(font, $"- {entry}", new Vector2(x + 8, y), theme.StatusText);
            y += 18;
        }
    }
}
