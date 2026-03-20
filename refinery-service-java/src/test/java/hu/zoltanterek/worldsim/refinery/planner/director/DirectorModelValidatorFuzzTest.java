package hu.zoltanterek.worldsim.refinery.planner.director;

import static org.junit.jupiter.api.Assertions.fail;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

import org.junit.jupiter.api.Test;

import hu.zoltanterek.worldsim.refinery.model.PatchOp;

class DirectorModelValidatorFuzzTest {
    private final DirectorModelValidator validator = new DirectorModelValidator();

    @Test
    void validateAndRepair_Fuzz1000RandomCandidates_NeverCrashes() {
        for (int i = 0; i < 1000; i++) {
            Random random = new Random(1000L + i);
            List<PatchOp> candidate = randomCandidate(random);
            DirectorRuntimeFacts facts = randomFacts(random);

            try {
                validator.validateAndRepair(candidate, facts);
            } catch (IllegalArgumentException expectedValidationFailure) {
                // Expected for invalid randomized inputs.
            } catch (Throwable unexpected) {
                fail("Unexpected exception in validateAndRepair iteration=" + i + " error=" + unexpected);
            }

            try {
                validator.conservativeRetryPatch(candidate, facts);
            } catch (Throwable unexpected) {
                fail("Unexpected exception in conservativeRetryPatch iteration=" + i + " error=" + unexpected);
            }
        }
    }

    private static DirectorRuntimeFacts randomFacts(Random random) {
        int colonyCount = random.nextInt(4) + 1;
        long cooldown = random.nextBoolean() ? 0L : random.nextInt(8);

        List<DirectorRuntimeFacts.ActiveBeatFact> activeBeats = new ArrayList<>();
        int beatCount = random.nextInt(3);
        String[] severities = {"minor", "major", "epic"};
        for (int i = 0; i < beatCount; i++) {
            activeBeats.add(new DirectorRuntimeFacts.ActiveBeatFact(
                    "ACTIVE_" + i,
                    severities[random.nextInt(severities.length)],
                    random.nextInt(20)
            ));
        }

        return new DirectorRuntimeFacts(
                random.nextInt(2000),
                colonyCount,
                cooldown,
                random.nextDouble() * 6.0,
                List.copyOf(activeBeats),
                List.of()
        );
    }

    private static List<PatchOp> randomCandidate(Random random) {
        int opCount = random.nextInt(7);
        List<PatchOp> ops = new ArrayList<>(opCount);
        for (int i = 0; i < opCount; i++) {
            if (random.nextBoolean()) {
                ops.add(randomStoryBeat(random, i));
            } else {
                ops.add(randomDirective(random, i));
            }
        }
        return List.copyOf(ops);
    }

    private static PatchOp.AddStoryBeat randomStoryBeat(Random random, int idx) {
        int effectCount = random.nextInt(5);
        List<PatchOp.EffectEntry> effects = new ArrayList<>(effectCount);
        String[] effectTypes = {"domain_modifier", "bad_type", "", null};
        String[] domains = {"food", "morale", "economy", "military", "research", "unknown"};
        for (int i = 0; i < effectCount; i++) {
            String type = effectTypes[random.nextInt(effectTypes.length)];
            String domain = domains[random.nextInt(domains.length)];
            double modifier = -0.5 + random.nextDouble();
            long duration = random.nextInt(120) - 10L;
            effects.add(new PatchOp.EffectEntry(type, domain, modifier, duration));
        }

        String[] severities = {"minor", "major", "epic", "unknown", "", null};
        String severity = severities[random.nextInt(severities.length)];
        String opId = random.nextInt(8) == 0 ? "dup_story" : "story_" + idx + '_' + random.nextInt(8);
        String beatId = random.nextInt(10) == 0 ? "" : "BEAT_" + random.nextInt(100);
        String text = random.nextInt(12) == 0 ? "" : "Story text " + random.nextInt(1000);
        long duration = random.nextInt(140) - 20L;

        return new PatchOp.AddStoryBeat(opId, beatId, text, duration, severity, List.copyOf(effects));
    }

    private static PatchOp.SetColonyDirective randomDirective(Random random, int idx) {
        int biasCount = random.nextInt(5);
        List<PatchOp.GoalBiasEntry> biases = new ArrayList<>(biasCount);
        String[] biasTypes = {"goal_bias", "bad_bias", "", null};
        String[] categories = {"farming", "gathering", "military", "research", "unknown"};

        for (int i = 0; i < biasCount; i++) {
            String type = biasTypes[random.nextInt(biasTypes.length)];
            String category = categories[random.nextInt(categories.length)];
            double weight = -0.2 + random.nextDouble();
            Long duration = random.nextBoolean() ? null : (long) (random.nextInt(120) - 10);
            biases.add(new PatchOp.GoalBiasEntry(type, category, weight, duration));
        }

        String[] directives = {"PrioritizeFood", "StabilizeMorale", "BoostIndustry", "UnknownDirective", ""};
        String opId = random.nextInt(8) == 0 ? "dup_directive" : "dir_" + idx + '_' + random.nextInt(8);
        int colonyId = random.nextInt(8) - 2;
        long duration = random.nextInt(120) - 20L;

        return new PatchOp.SetColonyDirective(
                opId,
                colonyId,
                directives[random.nextInt(directives.length)],
                duration,
                List.copyOf(biases)
        );
    }
}
