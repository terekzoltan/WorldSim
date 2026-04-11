package hu.zoltanterek.worldsim.refinery.planner;

import java.util.List;
import java.util.Locale;
import java.util.Random;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.node.ObjectNode;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.model.PatchResponse;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorCampaignOpFactory;
import hu.zoltanterek.worldsim.refinery.planner.director.DirectorInfluenceBudget;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

@Component
public class MockPlanner implements PatchPlanner {
    private static final String SCHEMA_VERSION = "v1";
    private final ObjectMapper objectMapper;
    private final String directorOutputMode;
    private final boolean directorCampaignEnabled;

    public MockPlanner(
            ObjectMapper objectMapper,
            @Value("${planner.director.outputMode:both}") String directorOutputMode,
            @Value("${planner.director.campaignEnabled:false}") boolean directorCampaignEnabled
    ) {
        this.objectMapper = objectMapper;
        this.directorOutputMode = normalizeOutputMode(directorOutputMode);
        this.directorCampaignEnabled = directorCampaignEnabled;
    }

    @Override
    public PatchResponse plan(PatchRequest request) {
        Random random = new Random(DeterministicIds.combineSeed(request.seed(), request.tick(), request.goal().name()));

        List<PatchOp> patch = switch (request.goal()) {
            case Goal.TECH_TREE_PATCH -> List.of(planTechTreePatch(request));
            case Goal.WORLD_EVENT -> List.of(planWorldEvent(request, random));
            case Goal.NPC_POLICY -> List.of(planNpcPolicy(request, random));
            case Goal.SEASON_DIRECTOR_CHECKPOINT -> applyDirectorOutputMode(
                    planDirectorCheckpoint(request),
                    directorOutputMode
            );
        };

        List<String> explain = request.goal() == Goal.SEASON_DIRECTOR_CHECKPOINT
                ? List.of(
                "directorStage:mock",
                "directorOutputMode:" + directorOutputMode,
                "budgetUsed:" + formatBudgetUsed(DirectorInfluenceBudget.calculateBudgetUsed(patch)),
                "MockPlanner produced deterministic director checkpoint output.",
                "Given the same goal, seed, and tick, this service returns the same patch."
        )
                : List.of(
                "MockPlanner produced a deterministic response for pipeline testing.",
                "Given the same goal, seed, and tick, this service returns the same patch."
        );

        // TODO: Replace with LLM -> Refinery validate/repair planner chain.
        return new PatchResponse(
                SCHEMA_VERSION,
                request.requestId(),
                request.seed(),
                patch,
                explain,
                List.of()
        );
    }

    private PatchOp planTechTreePatch(PatchRequest request) {
        ObjectNode cost = objectMapper.createObjectNode();
        cost.put("research", 80);

        ObjectNode effects = objectMapper.createObjectNode();
        effects.put("foodYieldDelta", 1);
        effects.put("waterEfficiencyDelta", 1);

        String opId = DeterministicIds.opId(
                request.seed(),
                request.tick(),
                request.goal().name(),
                "addTech",
                "agriculture"
        );

        return new PatchOp.AddTech(
                opId,
                "agriculture",
                List.of("woodcutting"),
                cost,
                effects
        );
    }

    private PatchOp planWorldEvent(PatchRequest request, Random random) {
        String eventId = "WEATHER_" + DeterministicIds.shortStableId(
                request.seed(),
                request.tick(),
                request.goal().name(),
                request.goal().name()
        );
        String type = random.nextBoolean() ? "DROUGHT" : "RAIN_BONUS";

        ObjectNode params = objectMapper.createObjectNode();
        params.put("severity", 1 + random.nextInt(3));
        params.put("region", "GLOBAL");

        long duration = 10L + random.nextInt(11);
        String opId = DeterministicIds.opId(request.seed(), request.tick(), request.goal().name(), "addWorldEvent", eventId);
        return new PatchOp.AddWorldEvent(opId, eventId, type, params, duration);
    }

    private PatchOp planNpcPolicy(PatchRequest request, Random random) {
        String[] fields = {"economy.taxRate", "military.readiness", "science.focus"};
        String field = fields[random.nextInt(fields.length)];
        double delta = random.nextBoolean() ? 0.05 : -0.05;
        String opId = DeterministicIds.opId(request.seed(), request.tick(), request.goal().name(), "tweakTech", field + ":" + delta);
        return new PatchOp.TweakTech(opId, "POLICY_GLOBAL", field, delta);
    }

    private List<PatchOp> planDirectorCheckpoint(PatchRequest request) {
        String directive = "PrioritizeFood";

        PatchOp.AddStoryBeat storyBeat = new PatchOp.AddStoryBeat(
                "op_director_story_1",
                "BEAT_SAMPLE_1",
                "Rations tighten this season; avoid expansion and secure food routes.",
                24
        );

        PatchOp.SetColonyDirective directiveOp = new PatchOp.SetColonyDirective(
                "op_director_nudge_1",
                0,
                directive,
                18
        );

        List<PatchOp> patch = new java.util.ArrayList<>(3);
        patch.add(storyBeat);
        patch.add(directiveOp);
        DirectorCampaignOpFactory
                .buildDeterministicCampaignOp(request, directorCampaignEnabled)
                .ifPresent(patch::add);
        return List.copyOf(patch);
    }

    private static String normalizeOutputMode(String rawMode) {
        String mode = rawMode == null ? "both" : rawMode.trim().toLowerCase();
        return switch (mode) {
            case "both", "story_only", "nudge_only", "off" -> mode;
            default -> "both";
        };
    }

    private static List<PatchOp> applyDirectorOutputMode(List<PatchOp> patch, String outputMode) {
        return switch (outputMode) {
            case "story_only" -> patch.stream().filter(op -> op instanceof PatchOp.AddStoryBeat).toList();
            case "nudge_only" -> patch.stream().filter(MockPlanner::isNudgeSideDirectorOp).toList();
            case "off" -> List.of();
            default -> patch;
        };
    }

    private static boolean isNudgeSideDirectorOp(PatchOp op) {
        return op instanceof PatchOp.SetColonyDirective || DirectorCampaignOpFactory.isCampaignOp(op);
    }

    private static String formatBudgetUsed(double budgetUsed) {
        return String.format(Locale.ROOT, "%.3f", budgetUsed);
    }
}
