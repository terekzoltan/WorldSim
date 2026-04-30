package hu.zoltanterek.worldsim.refinery.planner.director;

import java.text.Normalizer;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;

public final class DirectorRuntimeAssertionsMapper {
    private static final String CONTEXT_ID = "runtimeCheckpoint";

    public DirectorRuntimeAssertions map(DirectorRuntimeFacts facts) {
        List<String> lines = new ArrayList<>();

        lines.add("RuntimeCheckpointContext(" + CONTEXT_ID + ").");
        lines.add("tick(" + CONTEXT_ID + "): " + facts.tick() + ".");
        lines.add("colonyCount(" + CONTEXT_ID + "): " + facts.colonyCount() + ".");
        lines.add("beatCooldownRemainingTicks(" + CONTEXT_ID + "): " + facts.beatCooldownTicks() + ".");
        lines.add("remainingInfluenceBudget(" + CONTEXT_ID + "): " + formatReal(facts.remainingInfluenceBudget()) + ".");

        List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats = sortedActiveBeats(facts.activeBeats());
        for (int i = 0; i < activeBeats.size(); i++) {
            appendActiveBeat(lines, activeBeats.get(i), i);
        }

        List<DirectorRuntimeFacts.ActiveDirectiveFact> activeDirectives = sortedActiveDirectives(facts.activeDirectives());
        for (int i = 0; i < activeDirectives.size(); i++) {
            appendActiveDirective(lines, activeDirectives.get(i), i);
        }

        return new DirectorRuntimeAssertions(lines);
    }

    private static List<DirectorRuntimeFacts.ActiveBeatFact> sortedActiveBeats(
            List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats
    ) {
        return (activeBeats == null ? List.<DirectorRuntimeFacts.ActiveBeatFact>of() : activeBeats).stream()
                .sorted(Comparator
                        .comparing(DirectorRuntimeFacts.ActiveBeatFact::beatId, Comparator.nullsFirst(String::compareTo))
                        .thenComparing(DirectorRuntimeFacts.ActiveBeatFact::severity, Comparator.nullsFirst(String::compareTo))
                        .thenComparingLong(DirectorRuntimeFacts.ActiveBeatFact::remainingTicks))
                .toList();
    }

    private static List<DirectorRuntimeFacts.ActiveDirectiveFact> sortedActiveDirectives(
            List<DirectorRuntimeFacts.ActiveDirectiveFact> activeDirectives
    ) {
        return (activeDirectives == null ? List.<DirectorRuntimeFacts.ActiveDirectiveFact>of() : activeDirectives).stream()
                .sorted(Comparator
                        .comparingInt(DirectorRuntimeFacts.ActiveDirectiveFact::colonyId)
                        .thenComparing(DirectorRuntimeFacts.ActiveDirectiveFact::directive, Comparator.nullsFirst(String::compareTo)))
                .toList();
    }

    private static void appendActiveBeat(
            List<String> lines,
            DirectorRuntimeFacts.ActiveBeatFact beat,
            int index
    ) {
        String beatId = "activeBeat_" + formatIndex(index);
        String severityId = "severity_" + safeIdentifierPart(beat.severity());
        lines.add("ActiveBeatFact(" + beatId + ").");
        lines.add("activeBeats(" + CONTEXT_ID + ", " + beatId + ").");
        lines.add("beatId(" + beatId + "): \"" + escapeString(beat.beatId()) + "\".");
        lines.add("remainingTicks(" + beatId + "): " + Math.max(0L, beat.remainingTicks()) + ".");
        lines.add("Severity(" + severityId + ").");
        lines.add("ActiveBeatFact::severity(" + beatId + ", " + severityId + ").");
    }

    private static void appendActiveDirective(
            List<String> lines,
            DirectorRuntimeFacts.ActiveDirectiveFact directive,
            int index
    ) {
        String directiveFactId = "activeDirective_" + formatIndex(index);
        String directiveKindId = "directive_" + safeIdentifierPart(directive.directive());
        lines.add("ActiveDirectiveFact(" + directiveFactId + ").");
        lines.add("activeDirectives(" + CONTEXT_ID + ", " + directiveFactId + ").");
        lines.add("colonyId(" + directiveFactId + "): " + Math.max(0, directive.colonyId()) + ".");
        lines.add("directiveKey(" + directiveFactId + "): \"" + escapeString(directive.directive()) + "\".");
        lines.add("DirectiveKind(" + directiveKindId + ").");
        lines.add("ActiveDirectiveFact::directive(" + directiveFactId + ", " + directiveKindId + ").");
    }

    private static String formatIndex(int index) {
        return String.format(Locale.ROOT, "%03d", index);
    }

    private static String safeIdentifierPart(String value) {
        String normalized = value == null ? "unknown" : Normalizer.normalize(value, Normalizer.Form.NFKD)
                .replaceAll("\\p{M}", "")
                .toLowerCase(Locale.ROOT)
                .replaceAll("[^a-z0-9]+", "_")
                .replaceAll("^_+|_+$", "");
        if (normalized.isBlank()) {
            return "unknown";
        }
        if (Character.isDigit(normalized.charAt(0))) {
            return "v_" + normalized;
        }
        return normalized;
    }

    private static String escapeString(String value) {
        StringBuilder builder = new StringBuilder();
        String text = value == null ? "" : value;
        for (int i = 0; i < text.length(); i++) {
            char ch = text.charAt(i);
            switch (ch) {
                case '\\' -> builder.append("\\\\");
                case '"' -> builder.append("\\\"");
                case '\n' -> builder.append("\\n");
                case '\r' -> builder.append("\\r");
                case '\t' -> builder.append("\\t");
                default -> builder.append(ch);
            }
        }
        return builder.toString();
    }

    private static String formatReal(double value) {
        if (Double.isNaN(value) || Double.isInfinite(value)) {
            return "0.0";
        }
        return Double.toString(Math.max(0d, value));
    }
}
