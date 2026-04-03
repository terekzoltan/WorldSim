package hu.zoltanterek.worldsim.refinery.planner.refinery;

public enum RefineryArtifactFamily {
    COMMON("common"),
    DIRECTOR("director"),
    COMBAT("combat"),
    CAMPAIGN("campaign");

    private final String directoryName;

    RefineryArtifactFamily(String directoryName) {
        this.directoryName = directoryName;
    }

    public String directoryName() {
        return directoryName;
    }
}
