package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.List;

public record DirectorRuntimeAssertions(List<String> lines) {
    public DirectorRuntimeAssertions {
        lines = List.copyOf(lines == null ? List.of() : lines);
    }

    public String problemFragment() {
        return String.join(System.lineSeparator(), lines);
    }
}
