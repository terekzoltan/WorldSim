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
            List<PatchOp.EffectEntry> effects = (story.effects() == null ? List.<DirectorOutputAssertions.EffectAssertion>of() : story.effects()).stream()
                    .map(effect -> new PatchOp.EffectEntry("domain_modifier", effect.domain(), effect.modifier(), effect.durationTicks()))
                    .toList();

            PatchOp.CausalChainEntry causalChain = null;
            if (story.causalChain() != null) {
                List<PatchOp.EffectEntry> followUpEffects = (story.causalChain().followUpBeat().effects() == null
                        ? List.<DirectorOutputAssertions.EffectAssertion>of()
                        : story.causalChain().followUpBeat().effects()).stream()
                        .map(effect -> new PatchOp.EffectEntry("domain_modifier", effect.domain(), effect.modifier(), effect.durationTicks()))
                        .toList();
                causalChain = new PatchOp.CausalChainEntry(
                        "causal_chain",
                        new PatchOp.CausalCondition(
                                story.causalChain().condition().metric(),
                                story.causalChain().condition().operator(),
                                story.causalChain().condition().threshold()
                        ),
                        new PatchOp.CausalFollowUpBeat(
                                story.causalChain().followUpBeat().beatId(),
                                story.causalChain().followUpBeat().text(),
                                story.causalChain().followUpBeat().durationTicks(),
                                story.causalChain().followUpBeat().severity(),
                                followUpEffects
                        ),
                        story.causalChain().windowTicks(),
                        story.causalChain().maxTriggers()
                );
            }

            ops.add(new PatchOp.AddStoryBeat(
                    storyOpId,
                    story.beatId(),
                    story.text(),
                    story.durationTicks(),
                    story.severity(),
                    effects,
                    causalChain
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
            List<PatchOp.GoalBiasEntry> biases = (directive.biases() == null ? List.<DirectorOutputAssertions.BiasAssertion>of() : directive.biases()).stream()
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
