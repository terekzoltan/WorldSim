package hu.zoltanterek.worldsim.refinery.contracts;

import java.util.List;
import java.util.Set;

public final class RefineryVocabulary {
    private RefineryVocabulary() {
    }

    public static final String OUTPUT_MODE_BOTH = "both";
    public static final String OUTPUT_MODE_STORY_ONLY = "story_only";
    public static final String OUTPUT_MODE_NUDGE_ONLY = "nudge_only";
    public static final String OUTPUT_MODE_OFF = "off";

    public static final String SEVERITY_MINOR = "minor";
    public static final String SEVERITY_MAJOR = "major";
    public static final String SEVERITY_EPIC = "epic";

    public static final String EFFECT_TYPE_DOMAIN_MODIFIER = "domain_modifier";
    public static final String BIAS_TYPE_GOAL_BIAS = "goal_bias";

    public static final String CAMPAIGN_KIND_DECLARE_WAR = "declare_war";
    public static final String CAMPAIGN_KIND_PROPOSE_TREATY = "propose_treaty";
    public static final String TREATY_KIND_CEASEFIRE = "ceasefire";
    public static final String TREATY_KIND_PEACE_TALKS = "peace_talks";

    public static final List<String> SHARED_OUTPUT_MODES = List.of(
            OUTPUT_MODE_BOTH,
            OUTPUT_MODE_STORY_ONLY,
            OUTPUT_MODE_NUDGE_ONLY,
            OUTPUT_MODE_OFF
    );

    public static final Set<String> SEVERITIES = Set.of(SEVERITY_MINOR, SEVERITY_MAJOR, SEVERITY_EPIC);

    public static final Set<String> DOMAINS = Set.of(
            "food",
            "morale",
            "economy",
            "military",
            "research"
    );

    public static final Set<String> GOAL_CATEGORIES = Set.of(
            "farming",
            "gathering",
            "crafting",
            "building",
            "social",
            "military",
            "research",
            "rest"
    );

    public static final Set<String> CAMPAIGN_KINDS = Set.of(CAMPAIGN_KIND_DECLARE_WAR, CAMPAIGN_KIND_PROPOSE_TREATY);
    public static final Set<String> TREATY_KINDS = Set.of(TREATY_KIND_CEASEFIRE, TREATY_KIND_PEACE_TALKS);
}
