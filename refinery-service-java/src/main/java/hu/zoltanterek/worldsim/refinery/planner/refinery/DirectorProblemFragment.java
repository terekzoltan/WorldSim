package hu.zoltanterek.worldsim.refinery.planner.refinery;

import java.util.List;

public record DirectorProblemFragment(List<String> lines) {
    public DirectorProblemFragment {
        lines = List.copyOf(lines == null ? List.of() : lines);
    }

    public String problemFragment() {
        return String.join(System.lineSeparator(), lines);
    }
}
