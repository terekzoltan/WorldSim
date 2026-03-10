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

    public static final long MIN_STORY_DURATION = 1;
    public static final long MAX_STORY_DURATION = 96;
    public static final long MIN_DIRECTIVE_DURATION = 1;
    public static final long MAX_DIRECTIVE_DURATION = 48;
    public static final int MAX_STORY_TEXT_LENGTH = 160;
    public static final int MAX_OPS_PER_CHECKPOINT = 4;

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
    public static final String INV_10 = "INV-10"; // domain stacking cap (deferred)
    public static final String INV_11 = "INV-11"; // colony reference bounds
    public static final String INV_12 = "INV-12"; // no conflicting directives
    public static final String INV_13 = "INV-13"; // deterministic operation ordering
    public static final String INV_14 = "INV-14"; // conservative retry never adds new ops
    public static final String INV_20 = "INV-20"; // deferred to S5-B: requires effects/biases fields
}
