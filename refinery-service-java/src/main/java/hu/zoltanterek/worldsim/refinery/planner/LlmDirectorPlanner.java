package hu.zoltanterek.worldsim.refinery.planner;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Set;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorBridgeContractMapper;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorOutputAssertions;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorRuntimeFacts;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorSnapshotMapper;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorCandidateParser;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorPromptFactory;
import hu.zoltanterek.worldsim.refinery.planner.llm.OpenRouterClient;

@Component
public class LlmDirectorPlanner {
    private static final Logger logger = LoggerFactory.getLogger(LlmDirectorPlanner.class);

    @FunctionalInterface
    interface CompletionGateway {
        String complete(String model, double temperature, int maxTokens, String systemPrompt, String userPrompt) throws Exception;
    }

    public enum ProposalStatus {
        DISABLED,
        MISSING_CONFIG,
        CANDIDATE,
        PARSE_FAILED,
        REQUEST_FAILED
    }

    public record ProposalResult(
            Optional<List<PatchOp>> patch,
            int completionCount,
            boolean sanitized,
            List<String> sanitizeTags,
            ProposalStatus status
    ) {
        static ProposalResult empty() {
            return new ProposalResult(Optional.empty(), 0, false, List.of(), ProposalStatus.DISABLED);
        }

        static ProposalResult missingConfig() {
            return new ProposalResult(Optional.empty(), 0, false, List.of(), ProposalStatus.MISSING_CONFIG);
        }
    }

    private record PatchBuildResult(
            List<PatchOp> patch,
            boolean sanitized,
            List<String> sanitizeTags
    ) {
    }

    private final boolean enabled;
    private final String apiKey;
    private final String model;
    private final double temperature;
    private final int maxTokens;
    private final String defaultOutputMode;
    private final double defaultInfluenceBudget;
    private final DirectorPromptFactory promptFactory;
    private final DirectorCandidateParser candidateParser;
    private final CompletionGateway completionGateway;
    private final DirectorSnapshotMapper snapshotMapper = new DirectorSnapshotMapper();
    private final DirectorBridgeContractMapper bridgeContractMapper = new DirectorBridgeContractMapper();

    @Autowired
    public LlmDirectorPlanner(
            ObjectMapper objectMapper,
            @Value("${planner.llm.enabled:false}") boolean enabled,
            @Value("${planner.llm.apiKey:}") String apiKey,
            @Value("${planner.llm.model:openai/gpt-4o-mini}") String model,
            @Value("${planner.llm.temperature:0.4}") double temperature,
            @Value("${planner.llm.maxTokens:500}") int maxTokens,
            @Value("${planner.director.outputMode:both}") String defaultOutputMode,
            @Value("${planner.director.budget:5.0}") double defaultInfluenceBudget,
            @Value("${planner.llm.baseUrl:https://openrouter.ai/api/v1}") String baseUrl,
            @Value("${planner.llm.httpReferer:https://worldsim.local}") String httpReferer,
            @Value("${planner.llm.appTitle:WorldSim}") String appTitle,
            @Value("${planner.llm.timeoutMs:3000}") int timeoutMs
    ) {
        this(
                enabled,
                apiKey,
                model,
                temperature,
                maxTokens,
                defaultOutputMode,
                defaultInfluenceBudget,
                new DirectorPromptFactory(),
                new DirectorCandidateParser(objectMapper),
                new OpenRouterClient(objectMapper, baseUrl, apiKey, httpReferer, appTitle, timeoutMs)::chatCompletion
        );
    }

    LlmDirectorPlanner(
            boolean enabled,
            String apiKey,
            String model,
            double temperature,
            int maxTokens,
            String defaultOutputMode,
            double defaultInfluenceBudget,
            DirectorPromptFactory promptFactory,
            DirectorCandidateParser candidateParser,
            CompletionGateway completionGateway
    ) {
        this.enabled = enabled;
        this.apiKey = apiKey == null ? "" : apiKey;
        this.model = model == null ? "" : model;
        this.temperature = temperature;
        this.maxTokens = maxTokens;
        this.defaultOutputMode = normalizeOutputMode(defaultOutputMode);
        this.defaultInfluenceBudget = defaultInfluenceBudget > 0d
                ? defaultInfluenceBudget
                : DirectorDesign.DEFAULT_INFLUENCE_BUDGET;
        this.promptFactory = promptFactory;
        this.candidateParser = candidateParser;
        this.completionGateway = completionGateway;
    }

    public Optional<List<PatchOp>> propose(PatchRequest request, List<String> feedbackHints) {
        return proposeDetailed(request, feedbackHints).patch();
    }

    public ProposalResult proposeDetailed(PatchRequest request, List<String> feedbackHints) {
        if (request.goal() != Goal.SEASON_DIRECTOR_CHECKPOINT || !enabled) {
            return ProposalResult.empty();
        }
        if (apiKey.isBlank() || model.isBlank()) {
            logger.warn("llm director planner disabled due to missing api key or model");
            return ProposalResult.missingConfig();
        }

        String outputMode = resolveOutputMode(request);
        DirectorRuntimeFacts runtimeFacts = snapshotMapper.map(request, defaultInfluenceBudget);
        String systemPrompt = promptFactory.systemPrompt();
        String userPrompt = promptFactory.userPrompt(runtimeFacts, outputMode, feedbackHints);

        try {
            logger.info(
                    "llm director proposal start requestId={} outputMode={} feedbackHints={}",
                    request.requestId(),
                    outputMode,
                    feedbackHints.size()
            );
            String response = completionGateway.complete(model, temperature, maxTokens, systemPrompt, userPrompt);
            Optional<DirectorCandidateParser.DirectorCandidate> candidate = candidateParser.parse(response);
            if (candidate.isEmpty()) {
                logger.warn("llm director proposal parse failed responsePreview={}", preview(response));
                return new ProposalResult(Optional.empty(), 1, false, List.of(), ProposalStatus.PARSE_FAILED);
            }

            PatchBuildResult buildResult = toPatchOps(request, runtimeFacts, candidate.get());
            logger.info(
                    "llm director proposal parsed candidateOps={} sanitized={} sanitizeTags={}",
                    buildResult.patch().size(),
                    buildResult.sanitized(),
                    buildResult.sanitizeTags().size()
            );
            Optional<List<PatchOp>> patch = buildResult.patch().isEmpty()
                    ? Optional.empty()
                    : Optional.of(buildResult.patch());
            return new ProposalResult(patch, 1, buildResult.sanitized(), buildResult.sanitizeTags(), ProposalStatus.CANDIDATE);
        } catch (Exception ex) {
            logger.warn("llm director proposal failed: {}", ex.toString());
            return new ProposalResult(Optional.empty(), 0, false, List.of(), ProposalStatus.REQUEST_FAILED);
        }
    }

    private PatchBuildResult toPatchOps(
            PatchRequest request,
            DirectorRuntimeFacts runtimeFacts,
            DirectorCandidateParser.DirectorCandidate candidate
    ) {
        SanitizeStats stats = new SanitizeStats();
        DirectorOutputAssertions assertions = mapToOutputAssertions(candidate, runtimeFacts, stats);

        List<PatchOp> ops = bridgeContractMapper.toPatchOps(request, assertions);
        return new PatchBuildResult(List.copyOf(ops), stats.sanitized(), stats.tags());
    }

    private static DirectorOutputAssertions mapToOutputAssertions(
            DirectorCandidateParser.DirectorCandidate candidate,
            DirectorRuntimeFacts runtimeFacts,
            SanitizeStats stats
    ) {
        DirectorOutputAssertions.StoryBeatAssertion storyBeatAssertion = mapStoryBeatAssertion(candidate.designatedOutput().storyBeatSlot(), stats);
        DirectorOutputAssertions.DirectiveAssertion directiveAssertion = mapDirectiveAssertion(candidate.designatedOutput().directiveSlot(), runtimeFacts, stats);
        return new DirectorOutputAssertions(storyBeatAssertion, directiveAssertion);
    }

    private static DirectorOutputAssertions.StoryBeatAssertion mapStoryBeatAssertion(
            DirectorCandidateParser.StoryBeatSlotCandidate story,
            SanitizeStats stats
    ) {
        if (story == null) {
            return null;
        }

        String beatId = trimToNull(story.beatId());
        String text = trimToNull(story.text());
        if (beatId == null || text == null) {
            stats.mark("story_missing_required");
            return null;
        }

        long duration = clamp(story.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
        if (duration != story.durationTicks()) {
            stats.mark("story_duration_clamped");
        }

        String severity = normalizeSeverity(story.severity());
        if (story.severity() != null && severity == null) {
            stats.mark("story_severity_normalized");
        }

        List<DirectorOutputAssertions.EffectAssertion> effects = sanitizeEffects(story.effects(), duration, stats);
        DirectorOutputAssertions.CausalChainAssertion causalChain = mapCausalChainAssertion(story.causalChain(), stats);
        return new DirectorOutputAssertions.StoryBeatAssertion(beatId, text, duration, severity, effects, causalChain);
    }

    private static DirectorOutputAssertions.CausalChainAssertion mapCausalChainAssertion(
            DirectorCandidateParser.CausalChainCandidate causalChainCandidate,
            SanitizeStats stats
    ) {
        if (causalChainCandidate == null) {
            return null;
        }

        String type = trimToNull(causalChainCandidate.type());
        if (type == null || !"causal_chain".equalsIgnoreCase(type)) {
            stats.mark("causal_chain_dropped");
            return null;
        }

        DirectorCandidateParser.ConditionCandidate conditionCandidate = causalChainCandidate.condition();
        if (conditionCandidate == null) {
            stats.mark("causal_chain_missing_condition");
            return null;
        }

        String metric = normalizeMetric(conditionCandidate.metric());
        if (metric == null) {
            stats.mark("causal_chain_metric_dropped");
            return null;
        }

        String operator = normalizeOperator(conditionCandidate.operator());
        if (operator == null) {
            stats.mark("causal_chain_operator_dropped");
            return null;
        }

        double threshold = conditionCandidate.threshold();
        if (Double.isNaN(threshold) || Double.isInfinite(threshold)) {
            stats.mark("causal_chain_threshold_dropped");
            return null;
        }
        if ("population".equals(metric) && "eq".equals(operator) && Math.rint(threshold) != threshold) {
            stats.mark("causal_chain_population_eq_non_integer");
            return null;
        }

        DirectorCandidateParser.FollowUpBeatCandidate followUpCandidate = causalChainCandidate.followUpBeat();
        if (followUpCandidate == null) {
            stats.mark("causal_chain_follow_up_missing");
            return null;
        }

        String followUpBeatId = trimToNull(followUpCandidate.beatId());
        String followUpText = trimToNull(followUpCandidate.text());
        if (followUpBeatId == null || followUpText == null) {
            stats.mark("causal_chain_follow_up_missing_required");
            return null;
        }

        long followUpDuration = clamp(
                followUpCandidate.durationTicks(),
                DirectorDesign.MIN_STORY_DURATION,
                DirectorDesign.MAX_STORY_DURATION
        );
        if (followUpDuration != followUpCandidate.durationTicks()) {
            stats.mark("causal_chain_follow_up_duration_clamped");
        }

        String followUpSeverity = normalizeSeverity(followUpCandidate.severity());
        if (followUpCandidate.severity() != null && followUpSeverity == null) {
            stats.mark("causal_chain_follow_up_severity_normalized");
        }

        List<DirectorOutputAssertions.EffectAssertion> followUpEffects = sanitizeEffects(
                followUpCandidate.effects(),
                followUpDuration,
                stats
        );

        long windowTicks = clamp(
                causalChainCandidate.windowTicks(),
                DirectorDesign.MIN_CAUSAL_WINDOW_TICKS,
                DirectorDesign.MAX_CAUSAL_WINDOW_TICKS
        );
        if (windowTicks != causalChainCandidate.windowTicks()) {
            stats.mark("causal_chain_window_clamped");
        }

        int maxTriggers = causalChainCandidate.maxTriggers();
        if (maxTriggers != DirectorDesign.CAUSAL_MAX_TRIGGERS) {
            stats.mark("causal_chain_max_triggers_forced");
        }

        return new DirectorOutputAssertions.CausalChainAssertion(
                new DirectorOutputAssertions.ConditionAssertion(metric, operator, threshold),
                new DirectorOutputAssertions.FollowUpBeatAssertion(
                        followUpBeatId,
                        followUpText,
                        followUpDuration,
                        followUpSeverity,
                        followUpEffects
                ),
                windowTicks,
                DirectorDesign.CAUSAL_MAX_TRIGGERS
        );
    }

    private static DirectorOutputAssertions.DirectiveAssertion mapDirectiveAssertion(
            DirectorCandidateParser.DirectiveSlotCandidate directiveCandidate,
            DirectorRuntimeFacts runtimeFacts,
            SanitizeStats stats
    ) {
        if (directiveCandidate == null) {
            return null;
        }

        int colonyId = clampInt(directiveCandidate.colonyId(), 0, Math.max(0, runtimeFacts.colonyCount() - 1));
        if (colonyId != directiveCandidate.colonyId()) {
            stats.mark("directive_colony_clamped");
        }

        String directive = trimToNull(directiveCandidate.directive());
        if (directive == null) {
            stats.mark("directive_missing_required");
            return null;
        }

        long duration = clamp(directiveCandidate.durationTicks(), DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
        if (duration != directiveCandidate.durationTicks()) {
            stats.mark("directive_duration_clamped");
        }

        List<DirectorOutputAssertions.BiasAssertion> biases = sanitizeBiases(directiveCandidate.biases(), stats);
        return new DirectorOutputAssertions.DirectiveAssertion(colonyId, directive, duration, biases);
    }

    private static List<DirectorOutputAssertions.EffectAssertion> sanitizeEffects(
            List<DirectorCandidateParser.StoryEffectCandidate> effects,
            long storyDurationTicks,
            SanitizeStats stats
    ) {
        if (effects == null || effects.isEmpty()) {
            return List.of();
        }

        List<DirectorOutputAssertions.EffectAssertion> sanitized = new ArrayList<>();
        for (DirectorCandidateParser.StoryEffectCandidate effect : effects) {
            if (effect == null) {
                stats.mark("story_effect_dropped");
                continue;
            }
            String type = trimToNull(effect.type());
            String domain = trimToNull(effect.domain());
            if (!"domain_modifier".equalsIgnoreCase(type) || domain == null) {
                stats.mark("story_effect_dropped");
                continue;
            }
            String normalizedDomain = domain.toLowerCase(Locale.ROOT);
            if (!DirectorDesign.VALID_DOMAINS.contains(normalizedDomain)) {
                stats.mark("story_effect_domain_dropped");
                continue;
            }
            double modifier = clampDouble(effect.modifier(), DirectorDesign.MODIFIER_MIN, DirectorDesign.MODIFIER_MAX);
            if (modifier != effect.modifier()) {
                stats.mark("story_effect_modifier_clamped");
            }
            if (effect.durationTicks() != storyDurationTicks) {
                stats.mark("story_effect_duration_aligned");
            }
            sanitized.add(new DirectorOutputAssertions.EffectAssertion(normalizedDomain, modifier, storyDurationTicks));
            if (sanitized.size() >= DirectorDesign.MAX_EFFECTS_PER_BEAT) {
                if (effects.size() > sanitized.size()) {
                    stats.mark("story_effect_truncated");
                }
                break;
            }
        }
        return List.copyOf(sanitized);
    }

    private static List<DirectorOutputAssertions.BiasAssertion> sanitizeBiases(List<DirectorCandidateParser.GoalBiasCandidate> biases, SanitizeStats stats) {
        if (biases == null || biases.isEmpty()) {
            return List.of();
        }

        List<DirectorOutputAssertions.BiasAssertion> sanitized = new ArrayList<>();
        for (DirectorCandidateParser.GoalBiasCandidate bias : biases) {
            if (bias == null) {
                stats.mark("directive_bias_dropped");
                continue;
            }
            String type = trimToNull(bias.type());
            String category = trimToNull(bias.goalCategory());
            if (!"goal_bias".equalsIgnoreCase(type) || category == null) {
                stats.mark("directive_bias_dropped");
                continue;
            }
            String normalizedCategory = category.toLowerCase(Locale.ROOT);
            if (!DirectorDesign.VALID_GOAL_CATEGORIES.contains(normalizedCategory)) {
                stats.mark("directive_bias_category_dropped");
                continue;
            }
            double weight = clampDouble(bias.weight(), DirectorDesign.WEIGHT_MIN, DirectorDesign.WEIGHT_MAX);
            if (weight != bias.weight()) {
                stats.mark("directive_bias_weight_clamped");
            }
            Long duration = bias.durationTicks();
            if (duration != null) {
                long clampedDuration = clamp(duration, DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                if (clampedDuration != duration) {
                    stats.mark("directive_bias_duration_clamped");
                }
                duration = clampedDuration;
            }
            sanitized.add(new DirectorOutputAssertions.BiasAssertion(normalizedCategory, weight, duration));
            if (sanitized.size() >= DirectorDesign.MAX_BIASES_PER_DIRECTIVE) {
                if (biases.size() > sanitized.size()) {
                    stats.mark("directive_bias_truncated");
                }
                break;
            }
        }
        return List.copyOf(sanitized);
    }

    private static final class SanitizeStats {
        private final Set<String> tags = new LinkedHashSet<>();

        void mark(String tag) {
            if (tag == null || tag.isBlank()) {
                return;
            }
            tags.add(tag);
        }

        boolean sanitized() {
            return !tags.isEmpty();
        }

        List<String> tags() {
            return List.copyOf(tags);
        }
    }

    private String resolveOutputMode(PatchRequest request) {
        JsonNode constraints = request.constraints();
        if (constraints != null && !constraints.isNull()) {
            String fromRoot = trimToNull(constraints.path("outputMode").asText(null));
            if (fromRoot != null) {
                return normalizeOutputMode(fromRoot);
            }
            String fromDirector = trimToNull(constraints.path("director").path("outputMode").asText(null));
            if (fromDirector != null) {
                return normalizeOutputMode(fromDirector);
            }
        }
        return defaultOutputMode;
    }

    private static String normalizeOutputMode(String rawMode) {
        if (rawMode == null) {
            return "both";
        }
        return switch (rawMode.trim().toLowerCase(Locale.ROOT)) {
            case "both", "story_only", "nudge_only", "off" -> rawMode.trim().toLowerCase(Locale.ROOT);
            default -> "both";
        };
    }

    private static String normalizeSeverity(String rawSeverity) {
        String normalized = trimToNull(rawSeverity);
        if (normalized == null) {
            return null;
        }
        normalized = normalized.toLowerCase(Locale.ROOT);
        return DirectorDesign.VALID_SEVERITIES.contains(normalized) ? normalized : null;
    }

    private static String normalizeMetric(String rawMetric) {
        String metric = trimToNull(rawMetric);
        if (metric == null) {
            return null;
        }
        metric = metric.toLowerCase(Locale.ROOT);
        return DirectorDesign.CAUSAL_ALLOWED_METRICS.contains(metric) ? metric : null;
    }

    private static String normalizeOperator(String rawOperator) {
        String operator = trimToNull(rawOperator);
        if (operator == null) {
            return null;
        }
        operator = operator.toLowerCase(Locale.ROOT);
        return DirectorDesign.CAUSAL_ALLOWED_OPERATORS.contains(operator) ? operator : null;
    }

    private static String trimToNull(String value) {
        if (value == null) {
            return null;
        }
        String trimmed = value.trim();
        return trimmed.isEmpty() ? null : trimmed;
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }

    private static int clampInt(int value, int min, int max) {
        return Math.max(min, Math.min(max, value));
    }

    private static double clampDouble(double value, double min, double max) {
        return Math.max(min, Math.min(max, value));
    }

    private static String preview(String response) {
        if (response == null) {
            return "<null>";
        }
        String compact = response.replace('\n', ' ').replace('\r', ' ').trim();
        if (compact.length() <= 160) {
            return compact;
        }
        return compact.substring(0, 160) + "...";
    }
}
