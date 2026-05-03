package hu.zoltanterek.worldsim.refinery.planner.director;

import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

public final class DirectorCorePatchAssertionsMapper {
    public Result map(List<PatchOp> patch) {
        DirectorOutputAssertions.StoryBeatAssertion story = null;
        DirectorOutputAssertions.DirectiveAssertion directive = null;
        Set<String> unsupported = new LinkedHashSet<>();

        for (PatchOp op : patch == null ? List.<PatchOp>of() : patch) {
            if (op instanceof PatchOp.AddStoryBeat storyBeat) {
                if (story != null) {
                    return Result.unavailable("multiple_story_ops");
                }
                if (!hasCoreStoryFields(storyBeat)) {
                    return Result.unavailable("story_core_unavailable");
                }
                if (storyBeat.causalChain() != null) {
                    unsupported.add("causalChain");
                }
                story = new DirectorOutputAssertions.StoryBeatAssertion(
                        storyBeat.beatId(),
                        storyBeat.text(),
                        storyBeat.durationTicks(),
                        storyBeat.severity(),
                        List.of(),
                        null
                );
            } else if (op instanceof PatchOp.SetColonyDirective directiveOp) {
                if (directive != null) {
                    return Result.unavailable("multiple_directive_ops");
                }
                if (!hasCoreDirectiveFields(directiveOp)) {
                    return Result.unavailable("directive_core_unavailable");
                }
                directive = new DirectorOutputAssertions.DirectiveAssertion(
                        directiveOp.colonyId(),
                        directiveOp.directive(),
                        directiveOp.durationTicks(),
                        List.of()
                );
            } else if (op instanceof PatchOp.DeclareWar || op instanceof PatchOp.ProposeTreaty) {
                unsupported.add("campaign");
            } else {
                return Result.unavailable("non_director_core_op");
            }
        }

        return Result.available(new DirectorOutputAssertions(story, directive, null), List.copyOf(unsupported));
    }

    private static boolean hasCoreStoryFields(PatchOp.AddStoryBeat storyBeat) {
        return !isBlank(storyBeat.beatId())
                && !isBlank(storyBeat.text())
                && !isBlank(storyBeat.severity())
                && storyBeat.durationTicks() >= 0
                && storyBeat.durationTicks() <= Integer.MAX_VALUE;
    }

    private static boolean hasCoreDirectiveFields(PatchOp.SetColonyDirective directive) {
        return !isBlank(directive.directive())
                && directive.colonyId() >= 0
                && directive.durationTicks() >= 0
                && directive.durationTicks() <= Integer.MAX_VALUE;
    }

    private static boolean isBlank(String value) {
        return value == null || value.isBlank();
    }

    public record Result(
            boolean available,
            DirectorOutputAssertions assertions,
            List<String> unsupportedFeatures,
            String unavailableReason
    ) {
        public Result {
            unsupportedFeatures = List.copyOf(unsupportedFeatures == null ? List.of() : unsupportedFeatures);
        }

        static Result available(DirectorOutputAssertions assertions, List<String> unsupportedFeatures) {
            return new Result(true, assertions, unsupportedFeatures, null);
        }

        static Result unavailable(String reason) {
            return new Result(false, null, List.of(), reason);
        }
    }
}
