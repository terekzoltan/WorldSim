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

    public static String directorSpikeProblemResourcePath() {
        return ROOT + "/director/tr1a-spike.problem";
    }

    public static String directorDesignProblemResourcePath() {
        return ROOT + "/director/design.problem";
    }

    public static String directorModelProblemResourcePath() {
        return ROOT + "/director/model.problem";
    }

    public static String directorRuntimeProblemResourcePath() {
        return ROOT + "/director/runtime.problem";
    }

    public static String directorOutputProblemResourcePath() {
        return ROOT + "/director/output.problem";
    }

    public static List<String> directorCanonicalProblemResourcePaths() {
        return List.of(
                directorDesignProblemResourcePath(),
                directorModelProblemResourcePath(),
                directorRuntimeProblemResourcePath(),
                directorOutputProblemResourcePath()
        );
    }
}
