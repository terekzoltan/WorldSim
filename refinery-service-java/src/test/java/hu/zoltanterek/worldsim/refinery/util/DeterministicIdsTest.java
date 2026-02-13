package hu.zoltanterek.worldsim.refinery.util;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import org.junit.jupiter.api.Test;

class DeterministicIdsTest {

    @Test
    void opIdIsStableFromGoalSeedTickAndStableKey() {
        String first = DeterministicIds.opId(123L, 42L, "TECH_TREE_PATCH", "addTech", "IRRIGATION_1");
        String second = DeterministicIds.opId(123L, 42L, "TECH_TREE_PATCH", "addTech", "IRRIGATION_1");
        assertEquals(first, second);
    }

    @Test
    void opIdChangesWhenGoalStringChanges() {
        String tech = DeterministicIds.opId(123L, 42L, "TECH_TREE_PATCH", "addTech", "IRRIGATION_1");
        String world = DeterministicIds.opId(123L, 42L, "WORLD_EVENT", "addTech", "IRRIGATION_1");
        assertNotEquals(tech, world);
    }
}
