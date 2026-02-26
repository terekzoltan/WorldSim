package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

public final class DirectorModelValidator {
    public DirectorValidationOutcome validateAndRepair(List<PatchOp> candidatePatch, DirectorRuntimeFacts facts) {
        List<String> warnings = new ArrayList<>();
        List<PatchOp> repaired = new ArrayList<>(candidatePatch.size());
        Map<Integer, String> directivesPerColony = new HashMap<>();
        boolean storyBeatSeen = false;

        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat) {
                if (storyBeatSeen) {
                    warnings.add("Dropped extra story beat in same checkpoint.");
                    continue;
                }
                if (facts.beatCooldownTicks() > 0) {
                    throw new IllegalArgumentException("Story beat cooldown active; cannot emit major beat this checkpoint.");
                }

                long repairedDuration = clamp(storyBeat.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
                if (repairedDuration != storyBeat.durationTicks()) {
                    warnings.add("Clamped story beat duration to safe range.");
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
                if (!DirectorDesign.ALLOWED_DIRECTIVES.contains(directive.directive())) {
                    throw new IllegalArgumentException("Unknown directive: " + directive.directive());
                }
                if (directive.colonyId() < 0 || directive.colonyId() >= facts.colonyCount()) {
                    throw new IllegalArgumentException("Directive references unknown colonyId: " + directive.colonyId());
                }

                String existing = directivesPerColony.get(directive.colonyId());
                if (existing != null && !existing.equals(directive.directive())) {
                    throw new IllegalArgumentException("Conflicting directives for colonyId " + directive.colonyId());
                }
                if (existing != null) {
                    warnings.add("Dropped duplicate directive for colony " + directive.colonyId() + ".");
                    continue;
                }

                long repairedDuration = clamp(directive.durationTicks(), DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                if (repairedDuration != directive.durationTicks()) {
                    warnings.add("Clamped directive duration to safe range.");
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

            throw new IllegalArgumentException("Director checkpoint supports only addStoryBeat/setColonyDirective ops.");
        }

        return new DirectorValidationOutcome(repaired, warnings);
    }

    public List<PatchOp> conservativeRetryPatch(List<PatchOp> candidatePatch, DirectorRuntimeFacts facts) {
        List<PatchOp> filtered = new ArrayList<>();
        Map<Integer, String> directivesPerColony = new HashMap<>();

        for (PatchOp op : candidatePatch) {
            if (op instanceof PatchOp.AddStoryBeat && facts.beatCooldownTicks() > 0) {
                continue;
            }
            if (op instanceof PatchOp.SetColonyDirective directive) {
                if (!DirectorDesign.ALLOWED_DIRECTIVES.contains(directive.directive())) {
                    continue;
                }
                if (directive.colonyId() < 0 || directive.colonyId() >= facts.colonyCount()) {
                    continue;
                }
                if (directivesPerColony.containsKey(directive.colonyId())) {
                    continue;
                }
                directivesPerColony.put(directive.colonyId(), directive.directive());
            }
            filtered.add(op);
        }

        return filtered;
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }
}
