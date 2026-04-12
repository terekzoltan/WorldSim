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
    private final boolean campaignEnabled;

    public DirectorModelValidator() {
        this(false);
    }

    public DirectorModelValidator(boolean campaignEnabled) {
        this.campaignEnabled = campaignEnabled;
    }

    public DirectorValidationOutcome validateAndRepair(List<PatchOp> candidatePatch, DirectorRuntimeFacts facts) {
        List<String> warnings = new ArrayList<>();
        List<String> feedback = new ArrayList<>();
        List<PatchOp> repaired = new ArrayList<>(candidatePatch.size());
        Map<Integer, String> directivesPerColony = new HashMap<>();
        Set<String> seenOpIds = new HashSet<>();
        boolean storyBeatSeen = false;
        boolean campaignSeen = false;
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
                String newBeatSeverity = inferSeverity(effects);
                if (explicitSeverity != null && !explicitSeverity.equals(newBeatSeverity)) {
                    warnings.add(code(
                            DirectorDesign.INV_01,
                            "Normalized story beat severity from '" + explicitSeverity + "' to '" + newBeatSeverity
                                    + "' based on effect count " + effects.size() + "."
                    ));
                    feedback.add(code(
                            DirectorDesign.INV_01,
                            "Story beat severity must match effect count (minor=0, major=1-2, epic=3)."
                    ));
                    changed = true;
                }
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

                PatchOp.CausalChainEntry causalChain = validateAndSanitizeCausalChain(
                        storyBeat,
                        repairedDuration,
                        newBeatSeverity,
                        effects,
                        facts
                );

                repaired.add(new PatchOp.AddStoryBeat(
                        storyBeat.opId(),
                        storyBeat.beatId(),
                        storyBeat.text(),
                        repairedDuration,
                        newBeatSeverity,
                        effects,
                        causalChain
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

            if (op instanceof PatchOp.DeclareWar declareWar) {
                if (!campaignEnabled) {
                    throw invalid(DirectorDesign.INV_01, "Campaign ops disabled by planner.director.campaignEnabled=false.");
                }
                if (isBlank(declareWar.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Campaign opId is required.");
                }
                if (!seenOpIds.add(declareWar.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Duplicate opId detected: " + declareWar.opId());
                }
                if (campaignSeen) {
                    throw invalid(DirectorDesign.INV_12, "Only one campaign op is allowed per checkpoint.");
                }
                validateFactionRange(declareWar.attackerFactionId(), "declareWar.attackerFactionId");
                validateFactionRange(declareWar.defenderFactionId(), "declareWar.defenderFactionId");
                if (declareWar.attackerFactionId() == declareWar.defenderFactionId()) {
                    throw invalid(DirectorDesign.INV_11, "declareWar requires attackerFactionId != defenderFactionId.");
                }
                repaired.add(declareWar);
                campaignSeen = true;
                continue;
            }

            if (op instanceof PatchOp.ProposeTreaty treaty) {
                if (!campaignEnabled) {
                    throw invalid(DirectorDesign.INV_01, "Campaign ops disabled by planner.director.campaignEnabled=false.");
                }
                if (isBlank(treaty.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Campaign opId is required.");
                }
                if (!seenOpIds.add(treaty.opId())) {
                    throw invalid(DirectorDesign.INV_11, "Duplicate opId detected: " + treaty.opId());
                }
                if (campaignSeen) {
                    throw invalid(DirectorDesign.INV_12, "Only one campaign op is allowed per checkpoint.");
                }
                validateFactionRange(treaty.proposerFactionId(), "proposeTreaty.proposerFactionId");
                validateFactionRange(treaty.receiverFactionId(), "proposeTreaty.receiverFactionId");
                if (treaty.proposerFactionId() == treaty.receiverFactionId()) {
                    throw invalid(DirectorDesign.INV_11, "proposeTreaty requires proposerFactionId != receiverFactionId.");
                }
                String normalizedTreatyKind = normalizeTreatyKind(treaty.treatyKind());
                repaired.add(new PatchOp.ProposeTreaty(
                        treaty.opId(),
                        treaty.proposerFactionId(),
                        treaty.receiverFactionId(),
                        normalizedTreatyKind,
                        treaty.note()
                ));
                campaignSeen = true;
                continue;
            }

            throw invalid(DirectorDesign.INV_01, "Director checkpoint supports only addStoryBeat/setColonyDirective/declareWar/proposeTreaty ops.");
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
        boolean campaignSeen = false;

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
                    severity = inferSeverity(effects);
                    PatchOp.CausalChainEntry causalChain = validateAndSanitizeCausalChain(
                            storyBeat,
                            duration,
                            severity,
                            effects,
                            facts
                    );
                    filtered.add(new PatchOp.AddStoryBeat(
                            storyBeat.opId(),
                            storyBeat.beatId(),
                            storyBeat.text(),
                            duration,
                            severity,
                            effects,
                            causalChain
                    ));
                } catch (IllegalArgumentException ex) {
                    continue;
                }
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
                continue;
            }

            if (op instanceof PatchOp.DeclareWar declareWar) {
                if (!campaignEnabled) {
                    continue;
                }
                if (isBlank(declareWar.opId())) {
                    continue;
                }
                if (!seenOpIds.add(declareWar.opId())) {
                    continue;
                }
                if (campaignSeen) {
                    continue;
                }
                if (!isValidFactionRange(declareWar.attackerFactionId())
                        || !isValidFactionRange(declareWar.defenderFactionId())
                        || declareWar.attackerFactionId() == declareWar.defenderFactionId()) {
                    continue;
                }
                filtered.add(declareWar);
                campaignSeen = true;
                continue;
            }

            if (op instanceof PatchOp.ProposeTreaty treaty) {
                if (!campaignEnabled) {
                    continue;
                }
                if (isBlank(treaty.opId())) {
                    continue;
                }
                if (!seenOpIds.add(treaty.opId())) {
                    continue;
                }
                if (campaignSeen) {
                    continue;
                }
                if (!isValidFactionRange(treaty.proposerFactionId())
                        || !isValidFactionRange(treaty.receiverFactionId())
                        || treaty.proposerFactionId() == treaty.receiverFactionId()) {
                    continue;
                }
                try {
                    String normalizedTreatyKind = normalizeTreatyKind(treaty.treatyKind());
                    filtered.add(new PatchOp.ProposeTreaty(
                            treaty.opId(),
                            treaty.proposerFactionId(),
                            treaty.receiverFactionId(),
                            normalizedTreatyKind,
                            treaty.note()
                    ));
                    campaignSeen = true;
                } catch (IllegalArgumentException ex) {
                    continue;
                }
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
        if (op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty) {
            return 2;
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
        if (op instanceof PatchOp.DeclareWar declareWar) {
            return declareWar.attackerFactionId() + ":" + declareWar.defenderFactionId();
        }
        if (op instanceof PatchOp.ProposeTreaty treaty) {
            return treaty.proposerFactionId() + ":" + treaty.receiverFactionId() + ":" + treaty.treatyKind();
        }
        return op.getClass().getSimpleName();
    }

    private static void validateFactionRange(int factionId, String fieldName) {
        if (!isValidFactionRange(factionId)) {
            throw invalid(
                    DirectorDesign.INV_11,
                    fieldName + " out of range: " + factionId + " (expected "
                            + DirectorDesign.MIN_FACTION_ID + ".." + DirectorDesign.MAX_FACTION_ID + ")"
            );
        }
    }

    private static boolean isValidFactionRange(int factionId) {
        return factionId >= DirectorDesign.MIN_FACTION_ID && factionId <= DirectorDesign.MAX_FACTION_ID;
    }

    private static String normalizeTreatyKind(String treatyKindRaw) {
        if (isBlank(treatyKindRaw)) {
            throw invalid(DirectorDesign.INV_11, "proposeTreaty.treatyKind is required.");
        }

        String normalized = treatyKindRaw.trim().toLowerCase(Locale.ROOT);
        if (!DirectorDesign.VALID_TREATY_KINDS.contains(normalized)) {
            throw invalid(
                    DirectorDesign.INV_11,
                    "Unsupported proposeTreaty.treatyKind '" + treatyKindRaw + "'. Expected one of: ceasefire, peace_talks."
            );
        }

        return normalized;
    }

    private static boolean hasActiveSeverity(DirectorRuntimeFacts facts, String severity) {
        return facts.activeBeats().stream().anyMatch(beat ->
                severity.equals(beat.severity()) && beat.remainingTicks() > 0
        );
    }

    private static String inferSeverity(List<PatchOp.EffectEntry> effects) {
        int effectCount = effects == null ? 0 : effects.size();
        return switch (effectCount) {
            case 0 -> "minor";
            case 1, 2 -> "major";
            default -> "epic";
        };
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

    private static PatchOp.CausalChainEntry validateAndSanitizeCausalChain(
            PatchOp.AddStoryBeat storyBeat,
            long repairedDuration,
            String explicitSeverity,
            List<PatchOp.EffectEntry> effects,
            DirectorRuntimeFacts facts
    ) {
        PatchOp.CausalChainEntry causalChain = storyBeat.causalChain();
        if (causalChain == null) {
            return null;
        }

        if (isBlank(causalChain.type()) || !"causal_chain".equalsIgnoreCase(causalChain.type())) {
            throw invalid(DirectorDesign.INV_18, "Unsupported causal chain type: " + causalChain.type());
        }

        PatchOp.CausalCondition condition = causalChain.condition();
        if (condition == null) {
            throw invalid(DirectorDesign.INV_18, "Causal chain condition is required.");
        }

        String metric = condition.metric() == null ? "" : condition.metric().trim().toLowerCase(Locale.ROOT);
        if (!DirectorDesign.CAUSAL_ALLOWED_METRICS.contains(metric)) {
            throw invalid(DirectorDesign.INV_18, "Unknown condition metric '" + condition.metric() + "'.");
        }

        String operator = condition.operator() == null ? "" : condition.operator().trim().toLowerCase(Locale.ROOT);
        if (!DirectorDesign.CAUSAL_ALLOWED_OPERATORS.contains(operator)) {
            throw invalid(DirectorDesign.INV_18, "Unknown condition operator '" + condition.operator() + "'.");
        }

        double threshold = condition.threshold();
        if (Double.isNaN(threshold) || Double.isInfinite(threshold)) {
            throw invalid(DirectorDesign.INV_18, "Condition threshold must be finite.");
        }

        if ("population".equals(metric) && "eq".equals(operator) && Math.rint(threshold) != threshold) {
            throw invalid(DirectorDesign.INV_18, "Population eq threshold must be an integer value.");
        }

        if (causalChain.windowTicks() < DirectorDesign.MIN_CAUSAL_WINDOW_TICKS
                || causalChain.windowTicks() > DirectorDesign.MAX_CAUSAL_WINDOW_TICKS) {
            throw invalid(
                    DirectorDesign.INV_19,
                    "Chain window " + causalChain.windowTicks() + " out of bounds ["
                            + DirectorDesign.MIN_CAUSAL_WINDOW_TICKS + ", " + DirectorDesign.MAX_CAUSAL_WINDOW_TICKS + "]"
            );
        }

        if (causalChain.maxTriggers() != DirectorDesign.CAUSAL_MAX_TRIGGERS) {
            throw invalid(
                    DirectorDesign.INV_19,
                    "Causal chain maxTriggers must be " + DirectorDesign.CAUSAL_MAX_TRIGGERS
                            + " in S7-A, got " + causalChain.maxTriggers()
            );
        }

        PatchOp.CausalFollowUpBeat followUpBeat = causalChain.followUpBeat();
        if (followUpBeat == null) {
            throw invalid(DirectorDesign.INV_16, "Causal chain followUpBeat is required.");
        }
        if (isBlank(followUpBeat.beatId()) || isBlank(followUpBeat.text())) {
            throw invalid(DirectorDesign.INV_16, "Causal follow-up beatId and text are required.");
        }
        if (storyBeat.beatId().equals(followUpBeat.beatId())) {
            throw invalid(DirectorDesign.INV_16, "Causal chain references parent beat, creating loop.");
        }
        if (followUpBeat.text().length() > DirectorDesign.MAX_STORY_TEXT_LENGTH) {
            throw invalid(
                    DirectorDesign.INV_16,
                    "Causal follow-up text too long: " + followUpBeat.text().length()
                            + " (max " + DirectorDesign.MAX_STORY_TEXT_LENGTH + ")"
            );
        }

        long followUpDuration = clamp(
                followUpBeat.durationTicks(),
                DirectorDesign.MIN_STORY_DURATION,
                DirectorDesign.MAX_STORY_DURATION
        );
        List<PatchOp.EffectEntry> followUpEffects = sanitizeEffects(followUpBeat.effects(), followUpDuration);
        String followUpSeverity = inferSeverity(followUpEffects);
        validateNoContradictoryModifiers(followUpEffects);
        validateDomainStackCap(followUpEffects);

        PatchOp.CausalChainEntry repairedChain = new PatchOp.CausalChainEntry(
                "causal_chain",
                new PatchOp.CausalCondition(metric, operator, threshold),
                new PatchOp.CausalFollowUpBeat(
                        followUpBeat.beatId(),
                        followUpBeat.text(),
                        followUpDuration,
                        followUpSeverity,
                        followUpEffects
                ),
                causalChain.windowTicks(),
                DirectorDesign.CAUSAL_MAX_TRIGGERS
        );

        double storyWithChainBudget = DirectorInfluenceBudget.calculateBudgetUsed(List.of(
                new PatchOp.AddStoryBeat(
                        storyBeat.opId(),
                        storyBeat.beatId(),
                        storyBeat.text(),
                        repairedDuration,
                        explicitSeverity,
                        effects,
                        repairedChain
                )
        ));
        if (storyWithChainBudget > facts.remainingInfluenceBudget()) {
            throw invalid(
                    DirectorDesign.INV_17,
                    "Chain total cost " + storyWithChainBudget + " exceeds limit " + facts.remainingInfluenceBudget()
            );
        }

        return repairedChain;
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }
}
