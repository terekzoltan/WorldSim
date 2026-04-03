package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.ArrayList;
import java.util.List;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;
import hu.zoltanterek.worldsim.refinery.model.PatchRequest;
import hu.zoltanterek.worldsim.refinery.util.DeterministicIds;

public final class DirectorBridgeContractMapper {
    public List<PatchOp> toPatchOps(PatchRequest request, DirectorOutputAssertions assertions) {
        List<PatchOp> ops = new ArrayList<>(2);

        DirectorOutputAssertions.StoryBeatAssertion story = assertions.storyBeat();
        if (story != null) {
            String storyOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    request.goal().name(),
                    "addStoryBeat",
                    story.beatId() + ':' + story.durationTicks() + ':' + (story.severity() == null ? "none" : story.severity())
            );
            List<PatchOp.EffectEntry> effects = story.effects().stream()
                    .map(effect -> new PatchOp.EffectEntry("domain_modifier", effect.domain(), effect.modifier(), effect.durationTicks()))
                    .toList();
            ops.add(new PatchOp.AddStoryBeat(
                    storyOpId,
                    story.beatId(),
                    story.text(),
                    story.durationTicks(),
                    story.severity(),
                    effects
            ));
        }

        DirectorOutputAssertions.DirectiveAssertion directive = assertions.directive();
        if (directive != null) {
            String directiveOpId = DeterministicIds.opId(
                    request.seed(),
                    request.tick(),
                    request.goal().name(),
                    "setColonyDirective",
                    directive.colonyId() + ":" + directive.directive() + ':' + directive.durationTicks()
            );
            List<PatchOp.GoalBiasEntry> biases = directive.biases().stream()
                    .map(bias -> new PatchOp.GoalBiasEntry("goal_bias", bias.goalCategory(), bias.weight(), bias.durationTicks()))
                    .toList();
            ops.add(new PatchOp.SetColonyDirective(
                    directiveOpId,
                    directive.colonyId(),
                    directive.directive(),
                    directive.durationTicks(),
                    biases
            ));
        }

        return List.copyOf(ops);
    }
}
