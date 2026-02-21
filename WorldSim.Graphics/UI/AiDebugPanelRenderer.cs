using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using WorldSim.Runtime.ReadModel;

namespace WorldSim.Graphics.UI;

public sealed class AiDebugPanelRenderer
{
    public void Draw(
        SpriteBatch spriteBatch,
        SpriteFont font,
        AiDebugSnapshot debug,
        int viewportWidth,
        HudTheme theme,
        bool compact,
        int scoreOffset,
        int historyOffset)
    {
        var x = Math.Max(20, viewportWidth - 560);
        var y = 10;

        if (!debug.HasData)
        {
            spriteBatch.DrawString(font, "AI Debug: no decision trace yet", new Vector2(x, y), theme.StatusText);
            return;
        }

        spriteBatch.DrawString(font, $"AI Debug [{debug.PolicyMode}] ({debug.TrackingMode} {debug.TrackedNpcIndex}/{debug.TrackedNpcCount})", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        spriteBatch.DrawString(font, $"Planner {debug.PlannerMode} | Goal {debug.SelectedGoal} | Next {debug.NextCommand}", new Vector2(x, y), theme.StatusText);
        y += 20;
        spriteBatch.DrawString(font, $"PlanLen {debug.PlanLength} | Cost {debug.PlanCost} | Replan {debug.ReplanReason} | Method {debug.MethodName}", new Vector2(x, y), theme.StatusText);
        y += 20;
        spriteBatch.DrawString(font, $"Tracked NPC colony {debug.TrackedColonyId} @ ({debug.TrackedX},{debug.TrackedY}) seq {debug.DecisionSequence}", new Vector2(x, y), theme.StatusText);
        y += 24;

        if (compact)
        {
            spriteBatch.DrawString(font, "Keys: PgUp/PgDn track | Home latest | F4 expand", new Vector2(x, y), theme.SecondaryText);
            return;
        }

        var scorePage = debug.GoalScores.Skip(Math.Max(0, scoreOffset)).Take(8).ToList();
        var historyPage = debug.RecentDecisions.Skip(Math.Max(0, historyOffset)).Take(8).ToList();

        spriteBatch.DrawString(font, "Goal scores:", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        foreach (var score in scorePage)
        {
            var cooldown = score.IsOnCooldown ? " CD" : string.Empty;
            spriteBatch.DrawString(font, $"- {score.GoalName}: {score.Score:0.00}{cooldown}", new Vector2(x + 8, y), theme.StatusText);
            y += 18;
        }
        y += 4;
        spriteBatch.DrawString(font, "Scores page: Left/Right", new Vector2(x + 8, y), theme.SecondaryText);

        y += 22;
        spriteBatch.DrawString(font, "Recent decisions:", new Vector2(x, y), theme.PrimaryText);
        y += 20;
        foreach (var entry in historyPage)
        {
            spriteBatch.DrawString(font, $"- {entry}", new Vector2(x + 8, y), theme.StatusText);
            y += 18;
        }

        y += 4;
        spriteBatch.DrawString(font, "History page: Up/Down | F4 compact", new Vector2(x + 8, y), theme.SecondaryText);
    }
}
