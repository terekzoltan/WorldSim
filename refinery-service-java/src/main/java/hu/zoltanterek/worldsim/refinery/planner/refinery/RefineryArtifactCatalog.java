package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.List;

public final class RefineryArtifactCatalog {
    private static final String ROOT = "/refinery";

    private RefineryArtifactCatalog() {
    }

    public static String familyRootResourcePath(RefineryArtifactFamily family) {
        return ROOT + "/" + family.directoryName() + "/";
    }

    public static String familyMarkerResourcePath(RefineryArtifactFamily family) {
        return familyRootResourcePath(family) + ".gitkeep";
    }

    public static String familyDesignProblemResourcePath(RefineryArtifactFamily family) {
        return familyRootResourcePath(family) + "design.problem";
    }

    public static String familyModelProblemResourcePath(RefineryArtifactFamily family) {
        return familyRootResourcePath(family) + "model.problem";
    }

    public static String familyRuntimeProblemResourcePath(RefineryArtifactFamily family) {
        return familyRootResourcePath(family) + "runtime.problem";
    }

    public static String familyOutputProblemResourcePath(RefineryArtifactFamily family) {
        return familyRootResourcePath(family) + "output.problem";
    }

    public static String directorSpikeProblemResourcePath() {
        return ROOT + "/director/tr1a-spike.problem";
    }

    public static String directorDesignProblemResourcePath() {
        return familyDesignProblemResourcePath(RefineryArtifactFamily.DIRECTOR);
    }

    public static String directorModelProblemResourcePath() {
        return familyModelProblemResourcePath(RefineryArtifactFamily.DIRECTOR);
    }

    public static String directorRuntimeProblemResourcePath() {
        return familyRuntimeProblemResourcePath(RefineryArtifactFamily.DIRECTOR);
    }

    public static String directorOutputProblemResourcePath() {
        return familyOutputProblemResourcePath(RefineryArtifactFamily.DIRECTOR);
    }

    public static List<String> canonicalProblemResourcePaths(RefineryArtifactFamily family) {
        if (family == RefineryArtifactFamily.COMMON) {
            return List.of(familyDesignProblemResourcePath(family));
        }

        return List.of(
                familyDesignProblemResourcePath(family),
                familyModelProblemResourcePath(family),
                familyRuntimeProblemResourcePath(family),
                familyOutputProblemResourcePath(family)
        );
    }

    public static List<String> directorCanonicalProblemResourcePaths() {
        return canonicalProblemResourcePaths(RefineryArtifactFamily.DIRECTOR);
    }
}
