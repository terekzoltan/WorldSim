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
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorDesign;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorCandidateParser;
import hu.zoltanterek.worldsim.refinery.planner.llm.DirectorPromptFactory;
import hu.zoltanterek.worldsim.refinery.planner.llm.OpenRouterClient;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

@Component
public class LlmDirectorPlanner {
    private static final Logger logger = LoggerFactory.getLogger(LlmDirectorPlanner.class);

    @FunctionalInterface
    interface CompletionGateway {
        String complete(String model, double temperature, int maxTokens, String systemPrompt, String userPrompt) throws Exception;
    }

    public record ProposalResult(
            Optional<List<PatchOp>> patch,
            int completionCount,
            boolean sanitized,
            List<String> sanitizeTags
    ) {
        static ProposalResult empty() {
            return new ProposalResult(Optional.empty(), 0, false, List.of());
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
            return ProposalResult.empty();
        }

        String outputMode = resolveOutputMode(request);
        double remainingInfluenceBudget = resolveRemainingInfluenceBudget(request);
        String systemPrompt = promptFactory.systemPrompt();
        String userPrompt = promptFactory.userPrompt(request.snapshot(), outputMode, remainingInfluenceBudget, feedbackHints);

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
                return new ProposalResult(Optional.empty(), 1, false, List.of());
            }

            PatchBuildResult buildResult = toPatchOps(request, candidate.get());
            logger.info(
                    "llm director proposal parsed candidateOps={} sanitized={} sanitizeTags={}",
                    buildResult.patch().size(),
                    buildResult.sanitized(),
                    buildResult.sanitizeTags().size()
            );
            Optional<List<PatchOp>> patch = buildResult.patch().isEmpty()
                    ? Optional.empty()
                    : Optional.of(buildResult.patch());
            return new ProposalResult(patch, 1, buildResult.sanitized(), buildResult.sanitizeTags());
        } catch (Exception ex) {
            logger.warn("llm director proposal failed: {}", ex.toString());
            return new ProposalResult(Optional.empty(), 0, false, List.of());
        }
    }

    private PatchBuildResult toPatchOps(PatchRequest request, DirectorCandidateParser.DirectorCandidate candidate) {
        SanitizeStats stats = new SanitizeStats();
        List<PatchOp> ops = new ArrayList<>(2);

        DirectorCandidateParser.StoryBeatCandidate story = candidate.storyBeat();
        if (story != null && story.enabled()) {
            String beatId = trimToNull(story.beatId());
            String text = trimToNull(story.text());
            if (beatId != null && text != null) {
                long duration = clamp(story.durationTicks(), DirectorDesign.MIN_STORY_DURATION, DirectorDesign.MAX_STORY_DURATION);
                if (duration != story.durationTicks()) {
                    stats.mark("story_duration_clamped");
                }
                String severity = normalizeSeverity(story.severity());
                if (story.severity() != null && severity == null) {
                    stats.mark("story_severity_normalized");
                }
                List<PatchOp.EffectEntry> effects = sanitizeEffects(story.effects(), duration, stats);
                String storyOpId = DeterministicIds.opId(
                        request.seed(),
                        request.tick(),
                        request.goal().name(),
                        "addStoryBeat",
                        beatId + ':' + duration + ':' + (severity == null ? "none" : severity)
                );
                ops.add(new PatchOp.AddStoryBeat(storyOpId, beatId, text, duration, severity, effects));
            } else {
                stats.mark("story_missing_required");
            }
        }

        DirectorCandidateParser.NudgeCandidate nudge = candidate.nudge();
        if (nudge != null && nudge.enabled()) {
            int colonyCount = readColonyCount(request.snapshot());
            int colonyId = clampInt(nudge.colonyId(), 0, Math.max(0, colonyCount - 1));
            if (colonyId != nudge.colonyId()) {
                stats.mark("directive_colony_clamped");
            }
            String directive = trimToNull(nudge.directive());
            if (directive != null) {
                long duration = clamp(nudge.durationTicks(), DirectorDesign.MIN_DIRECTIVE_DURATION, DirectorDesign.MAX_DIRECTIVE_DURATION);
                if (duration != nudge.durationTicks()) {
                    stats.mark("directive_duration_clamped");
                }
                List<PatchOp.GoalBiasEntry> biases = sanitizeBiases(nudge.biases(), stats);
                String directiveOpId = DeterministicIds.opId(
                        request.seed(),
                        request.tick(),
                        request.goal().name(),
                        "setColonyDirective",
                        colonyId + ":" + directive + ':' + duration
                );
                ops.add(new PatchOp.SetColonyDirective(directiveOpId, colonyId, directive, duration, biases));
            } else {
                stats.mark("directive_missing_required");
            }
        }

        return new PatchBuildResult(List.copyOf(ops), stats.sanitized(), stats.tags());
    }

    private static List<PatchOp.EffectEntry> sanitizeEffects(
            List<DirectorCandidateParser.StoryEffectCandidate> effects,
            long storyDurationTicks,
            SanitizeStats stats
    ) {
        if (effects == null || effects.isEmpty()) {
            return List.of();
        }

        List<PatchOp.EffectEntry> sanitized = new ArrayList<>();
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
            sanitized.add(new PatchOp.EffectEntry("domain_modifier", normalizedDomain, modifier, storyDurationTicks));
            if (sanitized.size() >= DirectorDesign.MAX_EFFECTS_PER_BEAT) {
                if (effects.size() > sanitized.size()) {
                    stats.mark("story_effect_truncated");
                }
                break;
            }
        }
        return List.copyOf(sanitized);
    }

    private static List<PatchOp.GoalBiasEntry> sanitizeBiases(List<DirectorCandidateParser.GoalBiasCandidate> biases, SanitizeStats stats) {
        if (biases == null || biases.isEmpty()) {
            return List.of();
        }

        List<PatchOp.GoalBiasEntry> sanitized = new ArrayList<>();
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
            sanitized.add(new PatchOp.GoalBiasEntry("goal_bias", normalizedCategory, weight, duration));
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

    private static int readColonyCount(JsonNode snapshot) {
        JsonNode directorCount = snapshot.path("director").path("colonyCount");
        if (!directorCount.isMissingNode() && !directorCount.isNull()) {
            return Math.max(1, directorCount.asInt(1));
        }

        JsonNode worldCount = snapshot.path("world").path("colonyCount");
        if (!worldCount.isMissingNode() && !worldCount.isNull()) {
            return Math.max(1, worldCount.asInt(1));
        }

        JsonNode director = snapshot.path("director");
        if (!director.path("colonyPopulation").isMissingNode() && !director.path("colonyPopulation").isNull()) {
            logger.warn("llm director snapshot missing colonyCount; falling back to colonyPopulation for compatibility");
            return Math.max(1, director.path("colonyPopulation").asInt(1));
        }

        return 1;
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

    private double resolveRemainingInfluenceBudget(PatchRequest request) {
        JsonNode constraints = request.constraints();
        if (constraints != null && !constraints.isNull()) {
            JsonNode maxBudget = constraints.path("maxBudget");
            if (!maxBudget.isMissingNode() && !maxBudget.isNull()) {
                return Math.max(0d, maxBudget.asDouble(defaultInfluenceBudget));
            }
            JsonNode nestedMaxBudget = constraints.path("director").path("maxBudget");
            if (!nestedMaxBudget.isMissingNode() && !nestedMaxBudget.isNull()) {
                return Math.max(0d, nestedMaxBudget.asDouble(defaultInfluenceBudget));
            }
        }

        JsonNode directorBudget = request.snapshot().path("director").path("remainingInfluenceBudget");
        if (!directorBudget.isMissingNode() && !directorBudget.isNull()) {
            return Math.max(0d, directorBudget.asDouble(defaultInfluenceBudget));
        }

        return defaultInfluenceBudget;
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
