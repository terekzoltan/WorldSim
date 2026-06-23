package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.List;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

/**
 * Conservative deterministic safety net used only after validation/retry exhaustion.
 */
public final class DirectorDeterministicFallbackPlanner {
    public List<PatchOp> build(PatchRequest request, DirectorRuntimeFacts facts, boolean campaignEnabled) {
        List<PatchOp> fallback = new ArrayList<>(2);

        if (facts.beatCooldownTicks() <= 0) {
            String beatId = "BEAT_FALLBACK_" + DeterministicIds.shortStableId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "fallback_beat"
            );
            String storyOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "addStoryBeat",
                    beatId
            );

            fallback.add(new PatchOp.AddStoryBeat(
                    storyOpId,
                    beatId,
                    "Season pressure rises; hold lines and preserve reserves.",
                    DirectorDesign.MIN_STORY_DURATION + 11
            ));
        }

        if (facts.colonyCount() > 0) {
            String directive = "PrioritizeFood";
            String directiveOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "setColonyDirective",
                    "fallback:colony0:" + directive
            );
            fallback.add(new PatchOp.SetColonyDirective(
                    directiveOpId,
                    0,
                    directive,
                    DirectorDesign.MIN_DIRECTIVE_DURATION + 17
            ));
        }

        DirectorCampaignOpFactory.buildDeterministicCampaignOp(request, campaignEnabled).ifPresent(fallback::add);

        return fallback;
    }
}
