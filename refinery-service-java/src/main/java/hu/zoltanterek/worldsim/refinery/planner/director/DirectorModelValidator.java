package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Locale;
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
                long repairedDuration = clamp(storyBeat.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
                if (repairedDuration != storyBeat.durationTicks()) {
                    warnings.add(code(DirectorDesign.INV_06, "Clamped story beat duration to safe range."));
                    feedback.add(code(DirectorDesign.INV_06, "Story beat duration was clamped from " + storyBeat.durationTicks() + " to " + repairedDuration + '.'));
                    changed = true;
                }
                if (hasMismatchedEffectDuration(storyBeat.effects(), repairedDuration)) {
                    warnings.add(code(DirectorDesign.INV_06, "Aligned story effect durations to parent story beat duration."));
                    feedback.add(code(DirectorDesign.INV_06, "Story effect durationTicks must match story beat durationTicks " + repairedDuration + '.'));
                    changed = true;
                }

                List<PatchOp.EffectEntry> effects = sanitizeEffects(storyBeat.effects(), repairedDuration);
                String explicitSeverity = normalizeOptionalSeverity(storyBeat.severity());
                String newBeatSeverity = inferSeverity(storyBeat, effects, explicitSeverity);
                if ("major".equals(newBeatSeverity) && hasActiveSeverity(facts, "major")) {
                    throw invalid(DirectorDesign.INV_08, "Major beat already active; cannot emit another major beat.");
                }
                if ("epic".equals(newBeatSeverity) && hasActiveSeverity(facts, "epic")) {
                    throw invalid(DirectorDesign.INV_09, "Epic beat already active; cannot emit another epic beat.");
                }
                validateNoContradictoryModifiers(effects);
                validateDomainStackCap(effects);
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

                repaired.add(new PatchOp.AddStoryBeat(
                        storyBeat.opId(),
                        storyBeat.beatId(),
                        storyBeat.text(),
                        repairedDuration,
                        explicitSeverity,
                        effects
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

                List<PatchOp.GoalBiasEntry> biases = sanitizeBiases(directive.biases());

                repaired.add(new PatchOp.SetColonyDirective(
                        directive.opId(),
                        directive.colonyId(),
                        directive.directive(),
                        repairedDuration,
                        biases
                ));
                directivesPerColony.put(directive.colonyId(), directive.directive());
                continue;
            }

            throw invalid(DirectorDesign.INV_01, "Director checkpoint supports only addStoryBeat/setColonyDirective ops.");
        }

        validateInfluenceBudget(repaired, facts);

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
                List<PatchOp.EffectEntry> effects;
                String severity;
                try {
                    effects = sanitizeEffects(storyBeat.effects(), duration);
                    validateNoContradictoryModifiers(effects);
                    validateDomainStackCap(effects);
                    severity = normalizeOptionalSeverity(storyBeat.severity());
                } catch (IllegalArgumentException ex) {
                    continue;
                }
                filtered.add(new PatchOp.AddStoryBeat(
                        storyBeat.opId(),
                        storyBeat.beatId(),
                        storyBeat.text(),
                        duration,
                        severity,
                        effects
                ));
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
                List<PatchOp.GoalBiasEntry> biases;
                try {
                    biases = sanitizeBiases(directive.biases());
                } catch (IllegalArgumentException ex) {
                    continue;
                }
                filtered.add(new PatchOp.SetColonyDirective(
                        directive.opId(),
                        directive.colonyId(),
                        directive.directive(),
                        duration,
                        biases
                ));
            }
        }

        filtered.sort(Comparator.comparingInt(DirectorModelValidator::sortKey)
                .thenComparing(DirectorModelValidator::stableSecondaryKey));

        if (DirectorInfluenceBudget.calculateBudgetUsed(filtered) > facts.remainingInfluenceBudget()) {
            return List.of();
        }

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

    private static String inferSeverity(
            PatchOp.AddStoryBeat storyBeat,
            List<PatchOp.EffectEntry> effects,
            String explicitSeverity
    ) {
        if (explicitSeverity != null) {
            return explicitSeverity;
        }
        if (!effects.isEmpty()) {
            return switch (effects.size()) {
                case 0 -> "minor";
                case 1, 2 -> "major";
                default -> "epic";
            };
        }

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

    private static String normalizeOptionalSeverity(String rawSeverity) {
        if (isBlank(rawSeverity)) {
            return null;
        }
        String severity = rawSeverity.trim().toLowerCase(Locale.ROOT);
        if (!DirectorDesign.VALID_SEVERITIES.contains(severity)) {
            throw invalid(DirectorDesign.INV_01, "Unknown story beat severity: " + rawSeverity);
        }
        return severity;
    }

    private static List<PatchOp.EffectEntry> sanitizeEffects(List<PatchOp.EffectEntry> effects, long storyDurationTicks) {
        if (effects == null || effects.isEmpty()) {
            return List.of();
        }
        if (effects.size() > DirectorDesign.MAX_EFFECTS_PER_BEAT) {
            throw invalid(
                    DirectorDesign.INV_05,
                    "Story beat has too many effects: " + effects.size() + " (max " + DirectorDesign.MAX_EFFECTS_PER_BEAT + ")"
            );
        }

        List<PatchOp.EffectEntry> sanitized = new ArrayList<>(effects.size());
        for (PatchOp.EffectEntry effect : effects) {
            if (effect == null) {
                throw invalid(DirectorDesign.INV_02, "Effect entry cannot be null.");
            }
            if (isBlank(effect.type()) || !"domain_modifier".equalsIgnoreCase(effect.type())) {
                throw invalid(DirectorDesign.INV_02, "Unsupported effect type: " + effect.type());
            }
            String domain = effect.domain() == null ? "" : effect.domain().trim().toLowerCase(Locale.ROOT);
            if (!DirectorDesign.VALID_DOMAINS.contains(domain)) {
                throw invalid(DirectorDesign.INV_02, "Unknown effect domain: " + effect.domain());
            }
            if (effect.modifier() < DirectorDesign.MODIFIER_MIN || effect.modifier() > DirectorDesign.MODIFIER_MAX) {
                throw invalid(
                        DirectorDesign.INV_03,
                        "Effect modifier out of range for domain '" + domain + "': " + effect.modifier()
                );
            }
            sanitized.add(new PatchOp.EffectEntry("domain_modifier", domain, effect.modifier(), storyDurationTicks));
        }

        return List.copyOf(sanitized);
    }

    private static boolean hasMismatchedEffectDuration(List<PatchOp.EffectEntry> effects, long storyDurationTicks) {
        if (effects == null || effects.isEmpty()) {
            return false;
        }

        for (PatchOp.EffectEntry effect : effects) {
            if (effect == null) {
                continue;
            }
            if (effect.durationTicks() != storyDurationTicks) {
                return true;
            }
        }

        return false;
    }

    private static List<PatchOp.GoalBiasEntry> sanitizeBiases(List<PatchOp.GoalBiasEntry> biases) {
        if (biases == null || biases.isEmpty()) {
            return List.of();
        }
        if (biases.size() > DirectorDesign.MAX_BIASES_PER_DIRECTIVE) {
            throw invalid(
                    DirectorDesign.INV_12,
                    "Directive has too many biases: " + biases.size() + " (max " + DirectorDesign.MAX_BIASES_PER_DIRECTIVE + ")"
            );
        }

        List<PatchOp.GoalBiasEntry> sanitized = new ArrayList<>(biases.size());
        for (PatchOp.GoalBiasEntry bias : biases) {
            if (bias == null) {
                throw invalid(DirectorDesign.INV_12, "Bias entry cannot be null.");
            }
            if (isBlank(bias.type()) || !"goal_bias".equalsIgnoreCase(bias.type())) {
                throw invalid(DirectorDesign.INV_12, "Unsupported bias type: " + bias.type());
            }
            String goalCategory = bias.goalCategory() == null ? "" : bias.goalCategory().trim().toLowerCase(Locale.ROOT);
            if (!DirectorDesign.VALID_GOAL_CATEGORIES.contains(goalCategory)) {
                throw invalid(DirectorDesign.INV_12, "Unknown goal category in bias: " + bias.goalCategory());
            }
            if (bias.weight() < DirectorDesign.WEIGHT_MIN || bias.weight() > DirectorDesign.WEIGHT_MAX) {
                throw invalid(
                        DirectorDesign.INV_12,
                        "Bias weight out of range for goal category '" + goalCategory + "': " + bias.weight()
                );
            }
            Long duration = bias.durationTicks();
            if (duration != null) {
                long clamped = clamp(duration, DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                if (clamped != duration) {
                    throw invalid(
                            DirectorDesign.INV_12,
                            "Bias duration out of range for goal category '" + goalCategory + "': " + duration
                    );
                }
            }
            sanitized.add(new PatchOp.GoalBiasEntry("goal_bias", goalCategory, bias.weight(), duration));
        }

        return List.copyOf(sanitized);
    }

    private static void validateNoContradictoryModifiers(List<PatchOp.EffectEntry> effects) {
        Map<String, Double> firstByDomain = new HashMap<>();
        for (PatchOp.EffectEntry effect : effects) {
            if (effect.modifier() == 0.0d) {
                continue;
            }
            Double previous = firstByDomain.get(effect.domain());
            if (previous != null && Math.signum(previous) != Math.signum(effect.modifier())) {
                String previousText = String.format(Locale.ROOT, "%+.3f", previous);
                String nextText = String.format(Locale.ROOT, "%+.3f", effect.modifier());
                throw invalid(
                        DirectorDesign.INV_20,
                        "Contradictory modifiers on '" + effect.domain() + "': " + previousText + " and " + nextText + " in same checkpoint"
                );
            }
            firstByDomain.putIfAbsent(effect.domain(), effect.modifier());
        }
    }

    private static void validateDomainStackCap(List<PatchOp.EffectEntry> effects) {
        Map<String, Double> sumByDomain = new HashMap<>();
        for (PatchOp.EffectEntry effect : effects) {
            double sum = sumByDomain.getOrDefault(effect.domain(), 0.0d) + effect.modifier();
            sumByDomain.put(effect.domain(), sum);
            if (Math.abs(sum) > DirectorDesign.MAX_DOMAIN_STACK) {
                throw invalid(
                        DirectorDesign.INV_10,
                        "Domain stack exceeds cap on '" + effect.domain() + "': " + sum
                                + " (max abs " + DirectorDesign.MAX_DOMAIN_STACK + ")"
                );
            }
        }
    }

    private static void validateInfluenceBudget(List<PatchOp> repaired, DirectorRuntimeFacts facts) {
        double budgetUsed = DirectorInfluenceBudget.calculateBudgetUsed(repaired);
        if (budgetUsed > facts.remainingInfluenceBudget()) {
            throw invalid(
                    DirectorDesign.INV_15,
                    "Budget cost " + budgetUsed + " exceeds limit " + facts.remainingInfluenceBudget()
            );
        }
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }
}
