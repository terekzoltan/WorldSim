package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.Optional;

import hu.zoltanterek.worldsim.refinery.model.Goal;
import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

public final class DirectorCampaignOpFactory {
    private DirectorCampaignOpFactory() {
    }

    public static Optional<PatchOp> buildDeterministicCampaignOp(PatchRequest request, boolean campaignEnabled) {
        if (!campaignEnabled) {
            return Optional.empty();
        }

        long selector = DeterministicIds.combineSeed(
                request.seed(),
                request.tick(),
                Goal.SEASON_DIRECTOR_CHECKPOINT.name() + ":campaign"
        );

        int leftFactionId = Math.floorMod((int) selector, DirectorDesign.MAX_FACTION_ID + 1);
        int offset = 1 + Math.floorMod((int) (selector >>> 3), DirectorDesign.MAX_FACTION_ID);
        int rightFactionId = (leftFactionId + offset) % (DirectorDesign.MAX_FACTION_ID + 1);
        if (leftFactionId == rightFactionId) {
            rightFactionId = (rightFactionId + 1) % (DirectorDesign.MAX_FACTION_ID + 1);
        }

        if ((selector & 1L) == 0L) {
            String opId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                    "declareWar",
                    leftFactionId + ":" + rightFactionId
            );
            return Optional.of(new PatchOp.DeclareWar(
                    opId,
                    leftFactionId,
                    rightFactionId,
                    "deterministic_campaign_nudge"
            ));
        }

        String treatyKind = ((selector >>> 1) & 1L) == 0L ? "ceasefire" : "peace_talks";
        String opId = DeterministicIds.opId(
                request.seed(),
                request.tick(),
                Goal.SEASON_DIRECTOR_CHECKPOINT.name(),
                "proposeTreaty",
                leftFactionId + ":" + rightFactionId + ":" + treatyKind
        );
        return Optional.of(new PatchOp.ProposeTreaty(
                opId,
                leftFactionId,
                rightFactionId,
                treatyKind,
                "deterministic_campaign_nudge"
        ));
    }

    public static boolean isCampaignOp(PatchOp op) {
        return op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty;
    }
}
