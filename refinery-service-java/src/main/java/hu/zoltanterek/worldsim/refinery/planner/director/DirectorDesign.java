package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.Set;

public final class DirectorDesign {
    private DirectorDesign() {
    }

    public static final Set<String> ALLOWED_DIRECTIVES = Set.of(
            "PrioritizeFood",
            "StabilizeMorale",
            "BoostIndustry"
    );

    public static final Set<String> VALID_DOMAINS = Set.of(
            "food",
            "morale",
            "economy",
            "military",
            "research"
    );

    public static final Set<String> VALID_GOAL_CATEGORIES = Set.of(
            "farming",
            "gathering",
            "crafting",
            "building",
            "social",
            "military",
            "research",
            "rest"
    );

    public static final Set<String> VALID_SEVERITIES = Set.of("minor", "major", "epic");
    public static final Set<String> VALID_CAMPAIGN_KINDS = Set.of("declare_war", "propose_treaty");
    public static final Set<String> VALID_TREATY_KINDS = Set.of("ceasefire", "peace_talks");

    public static final Set<String> CAUSAL_ALLOWED_METRICS = Set.of(
            "food_reserves_pct",
            "morale_avg",
            "population",
            "economy_output"
    );
    public static final Set<String> CAUSAL_ALLOWED_OPERATORS = Set.of("lt", "gt", "eq");

    public static final long MIN_STORY_DURATION = 1;
    public static final long MAX_STORY_DURATION = 96;
    public static final long MIN_DIRECTIVE_DURATION = 1;
    public static final long MAX_DIRECTIVE_DURATION = 48;
    public static final int MAX_STORY_TEXT_LENGTH = 160;
    public static final int MAX_OPS_PER_CHECKPOINT = 4;
    public static final int MAX_EFFECTS_PER_BEAT = 3;
    public static final int MAX_BIASES_PER_DIRECTIVE = 3;
    public static final int MAX_CAMPAIGN_OPS_PER_CHECKPOINT = 1;
    public static final int MIN_CAUSAL_WINDOW_TICKS = 10;
    public static final int MAX_CAUSAL_WINDOW_TICKS = 100;
    public static final int CAUSAL_MAX_TRIGGERS = 1;
    public static final double CAUSAL_CHAIN_BASE_COST = 2.0;
    public static final double CAUSAL_EQ_TOLERANCE = 0.0001;
    public static final double MODIFIER_MIN = -0.30;
    public static final double MODIFIER_MAX = 0.30;
    public static final double WEIGHT_MIN = 0.0;
    public static final double WEIGHT_MAX = 0.50;
    public static final double MAX_DOMAIN_STACK = 0.40;
    public static final double DEFAULT_INFLUENCE_BUDGET = 5.0;
    public static final double DOMAIN_MODIFIER_COST_FACTOR = 0.5;
    public static final double GOAL_BIAS_COST_FACTOR = 0.3;
    public static final int MIN_FACTION_ID = 0;
    public static final int MAX_FACTION_ID = 3;

    /**
     * Invariant labels are implementation-level validator codes used in runtime feedback.
     * They are not guaranteed to map 1:1 to the Director-Integration-Master-Plan 10.5 table.
     */
    public static final String INV_01 = "INV-01"; // supported op types only
    public static final String INV_02 = "INV-02"; // at most one story beat
    public static final String INV_03 = "INV-03"; // cooldown gate for story beat
    public static final String INV_04 = "INV-04"; // beat identity fields required
    public static final String INV_05 = "INV-05"; // story text bounds
    public static final String INV_06 = "INV-06"; // story duration bounds
    public static final String INV_07 = "INV-07"; // directive vocabulary
    public static final String INV_08 = "INV-08"; // max one active major beat
    public static final String INV_09 = "INV-09"; // max one active epic beat
    public static final String INV_10 = "INV-10"; // domain stacking cap
    public static final String INV_11 = "INV-11"; // colony reference bounds
    public static final String INV_12 = "INV-12"; // no conflicting directives
    public static final String INV_13 = "INV-13"; // deterministic operation ordering
    public static final String INV_14 = "INV-14"; // conservative retry never adds new ops
    public static final String INV_15 = "INV-15"; // total checkpoint cost within influence budget
    public static final String INV_16 = "INV-16"; // causal chain loop guard
    public static final String INV_17 = "INV-17"; // causal chain combined budget guard
    public static final String INV_18 = "INV-18"; // causal condition metric/operator validity
    public static final String INV_19 = "INV-19"; // causal chain window/maxTriggers bounds
    public static final String INV_20 = "INV-20"; // no contradictory same-domain modifiers
}
