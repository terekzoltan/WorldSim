using System;
using System.Linq;

namespace WorldSim.Graphics.UI;

public enum EventFeedCategory
{
    World,
    Combat,
    Siege,
    Campaign,
    Director
}

public static class EventFeedClassifier
{
    public static EventFeedCategory Classify(string evt)
    {
        if (string.IsNullOrWhiteSpace(evt))
            return EventFeedCategory.World;

        if (evt.StartsWith("[Combat]", StringComparison.OrdinalIgnoreCase) || ContainsAny(evt, "combat", "battle", "predator hit", "flee", "fight"))
            return EventFeedCategory.Combat;

        if (evt.StartsWith("[Siege]", StringComparison.OrdinalIgnoreCase) || ContainsAny(evt, "siege", "breach", "tower", "wall"))
            return EventFeedCategory.Siege;

        if (evt.StartsWith("[Campaign]", StringComparison.OrdinalIgnoreCase) || ContainsAny(evt, "campaign", "march", "army", "expedition"))
            return EventFeedCategory.Campaign;

        if (evt.StartsWith("[Director]", StringComparison.OrdinalIgnoreCase) || ContainsAny(evt, "director", "story beat", "nudge"))
            return EventFeedCategory.Director;

        return EventFeedCategory.World;
    }

    private static bool ContainsAny(string evt, params string[] keywords)
        => keywords.Any(k => evt.Contains(k, StringComparison.OrdinalIgnoreCase));
}
