package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

public final class DirectorModelValidator {
    public DirectorValidationOutcome validateAndRepair(List<PatchOp> candidatePatch, DirectorRuntimeFacts facts) {
        List<String> warnings = new ArrayList<>();
        List<String> feedback = new ArrayList<>();
        List<PatchOp> repaired = new ArrayList<>(candidatePatch.size());
        Map<Integer, String> directivesPerColony = new HashMap<>();
        Set<String> seenOpIds = new HashSet<>();
        boolean storyBeatSeen = false;
        boolean changed = false;

        if (candidatePatch.size() > DirectorDesign.MAX_OPS_PER_CHECKPOINT) {
            throw invalid(
                    DirectorDesign.INV_12,
                    "Too many ops in checkpoint: " + candidatePatch.size() +
                            " (max " + DirectorDesign.MAX_OPS_PER_CHECKPOINT + ")"
            );
        }

        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat) {
                if (isBlank(storyBeat.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Story beat opId is required.");
                }
                if (!seenOpIds.add(storyBeat.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Duplicate opId detected: " + storyBeat.opId());
                }
                if (storyBeatSeen) {
                    warnings.add(code(DirectorDesign.INV_02, "Dropped extra story beat in same checkpoint."));
                    changed = true;
                    continue;
                }
                if (facts.beatCooldownTicks() > 0) {
                    throw invalid(DirectorDesign.INV_03, "Story beat cooldown active; cannot emit beat this checkpoint.");
                }
                String newBeatSeverity = inferSeverity(storyBeat);
                if ("major".equals(newBeatSeverity) && hasActiveSeverity(facts, "major")) {
                    throw invalid(DirectorDesign.INV_08, "Major beat already active; cannot emit another major beat.");
                }
                if ("epic".equals(newBeatSeverity) && hasActiveSeverity(facts, "epic")) {
                    throw invalid(DirectorDesign.INV_09, "Epic beat already active; cannot emit another epic beat.");
                }
                // INV-10: deferred domain stacking cap check until story effect payload is available.
                if (isBlank(storyBeat.beatId())) {
                    throw invalid(DirectorDesign.INV_04, "Story beat beatId is required.");
                }
                if (isBlank(storyBeat.text())) {
                    throw invalid(DirectorDesign.INV_05, "Story beat text is required.");
                }
                if (storyBeat.text().length() > DirectorDesign.MAX_STORY_TEXT_LENGTH) {
                    throw invalid(
                            DirectorDesign.INV_05,
                            "Story beat text too long: " + storyBeat.text().length() +
                                    " (max " + DirectorDesign.MAX_STORY_TEXT_LENGTH + ")"
                    );
                }

                long repairedDuration = clamp(storyBeat.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
                if (repairedDuration != storyBeat.durationTicks()) {
                    warnings.add(code(DirectorDesign.INV_06, "Clamped story beat duration to safe range."));
                    feedback.add(code(DirectorDesign.INV_06, "Story beat duration was clamped from " + storyBeat.durationTicks() + " to " + repairedDuration + '.'));
                    changed = true;
                }

                repaired.add(new PatchOp.AddStoryBeat(
                        storyBeat.opId(),
                        storyBeat.beatId(),
                        storyBeat.text(),
                        repairedDuration
                ));
                storyBeatSeen = true;
                continue;
            }

            if (op instanceof PatchOp.SetColonyDirective directive) {
                if (isBlank(directive.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Directive opId is required.");
                }
                if (!seenOpIds.add(directive.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Duplicate opId detected: " + directive.opId());
                }
                if (isBlank(directive.directive())) {
                    throw invalid(DirectorDesign.INV_07, "Directive name is required.");
                }
                if (!DirectorDesign.ALLOWED_DIRECTIVES.contains(directive.directive())) {
                    throw invalid(DirectorDesign.INV_07, "Unknown directive: " + directive.directive());
                }
                if (directive.colonyId() < 0 || directive.colonyId() >= facts.colonyCount()) {
                    throw invalid(DirectorDesign.INV_11, "Directive references unknown colonyId: " + directive.colonyId());
                }

                String existing = directivesPerColony.get(directive.colonyId());
                if (existing != null && !existing.equals(directive.directive())) {
                    throw invalid(DirectorDesign.INV_12, "Conflicting directives for colonyId " + directive.colonyId());
                }
                if (existing != null) {
                    warnings.add(code(DirectorDesign.INV_12, "Dropped duplicate directive for colony " + directive.colonyId() + '.'));
                    changed = true;
                    continue;
                }

                long repairedDuration = clamp(directive.durationTicks(), DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                if (repairedDuration != directive.durationTicks()) {
                    warnings.add(code(DirectorDesign.INV_10, "Clamped directive duration to safe range."));
                    feedback.add(code(DirectorDesign.INV_10, "Directive duration was clamped from " + directive.durationTicks() + " to " + repairedDuration + '.'));
                    changed = true;
                }

                repaired.add(new PatchOp.SetColonyDirective(
                        directive.opId(),
                        directive.colonyId(),
                        directive.directive(),
                        repairedDuration
                ));
                directivesPerColony.put(directive.colonyId(), directive.directive());
                continue;
            }

            throw invalid(DirectorDesign.INV_01, "Director checkpoint supports only addStoryBeat/setColonyDirective ops.");
        }

        repaired.sort(Comparator.comparingInt(DirectorModelValidator::sortKey)
                .thenComparing(DirectorModelValidator::stableSecondaryKey));
        if (!candidatePatch.equals(repaired)) {
            changed = true;
            warnings.add(code(DirectorDesign.INV_13, "Normalized director op ordering for deterministic output."));
        }

        return new DirectorValidationOutcome(repaired, warnings, feedback, changed);
    }

    public List<PatchOp> conservativeRetryPatch(List<PatchOp> candidatePatch, DirectorRuntimeFacts facts) {
        List<PatchOp> filtered = new ArrayList<>();
        Map<Integer, String> directivesPerColony = new HashMap<>();
        Set<String> seenOpIds = new HashSet<>();

        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat) {
                if (facts.beatCooldownTicks() > 0 || isBlank(storyBeat.opId()) || isBlank(storyBeat.beatId()) || isBlank(storyBeat.text())) {
                    continue;
                }
                if (!seenOpIds.add(storyBeat.opId())) {
                    continue;
                }
                long duration = clamp(storyBeat.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
                filtered.add(new PatchOp.AddStoryBeat(storyBeat.opId(), storyBeat.beatId(), storyBeat.text(), duration));
                continue;
            }
            if (op instanceof PatchOp.SetColonyDirective directive) {
                if (isBlank(directive.opId()) || isBlank(directive.directive())) {
                    continue;
                }
                if (!DirectorDesign.ALLOWED_DIRECTIVES.contains(directive.directive())) {
                    continue;
                }
                if (directive.colonyId() < 0 || directive.colonyId() >= facts.colonyCount()) {
                    continue;
                }
                if (!seenOpIds.add(directive.opId())) {
                    continue;
                }
                if (directivesPerColony.containsKey(directive.colonyId())) {
                    continue;
                }
                directivesPerColony.put(directive.colonyId(), directive.directive());
                long duration = clamp(directive.durationTicks(), DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                filtered.add(new PatchOp.SetColonyDirective(
                        directive.opId(),
                        directive.colonyId(),
                        directive.directive(),
                        duration
                ));
            }
        }

        filtered.sort(Comparator.comparingInt(DirectorModelValidator::sortKey)
                .thenComparing(DirectorModelValidator::stableSecondaryKey));

        return filtered;
    }

    private static IllegalArgumentException invalid(String invariantCode, String message) {
        return new IllegalArgumentException(code(invariantCode, message));
    }

    private static String code(String invariantCode, String message) {
        return invariantCode + " " + message;
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }

    private static int sortKey(PatchOp op) {
        if (op instanceof PatchOp.AddStoryBeat) {
            return 0;
        }
        if (op instanceof PatchOp.SetColonyDirective) {
            return 1;
        }
        return 99;
    }

    private static String stableSecondaryKey(PatchOp op) {
        if (op instanceof PatchOp.AddStoryBeat story) {
            return story.beatId();
        }
        if (op instanceof PatchOp.SetColonyDirective directive) {
            return directive.colonyId() + ":" + directive.directive();
        }
        return op.getClass().getSimpleName();
    }

    private static boolean hasActiveSeverity(DirectorRuntimeFacts facts, String severity) {
        return facts.activeBeats().stream().anyMatch(beat ->
                severity.equals(beat.severity()) && beat.remainingTicks() > 0
        );
    }

    private static String inferSeverity(PatchOp.AddStoryBeat storyBeat) {
        String source = (storyBeat.beatId() + " " + storyBeat.text()).toLowerCase();
        if (source.contains("epic")) {
            return "epic";
        }
        if (source.contains("minor")) {
            return "minor";
        }
        if (source.contains("major")) {
            return "major";
        }
        return "major";
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }
}
