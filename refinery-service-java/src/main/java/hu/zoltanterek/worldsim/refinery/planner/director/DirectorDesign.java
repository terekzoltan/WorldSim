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
}
