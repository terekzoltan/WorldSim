package hu.zoltanterek.worldsim.refinery.util;

import hu.zoltanterek.worldsim.refinery.model.Goal;

public final class DeterministicIds {
    private DeterministicIds() {
    }

    public static long combineSeed(long seed, long tick, Goal goal) {
        long h = 1469598103934665603L;
        h ^= seed;
        h *= 1099511628211L;
        h ^= tick;
        h *= 1099511628211L;
        h ^= goal.ordinal();
        h *= 1099511628211L;
        return h;
    }

    public static String shortStableId(long seed, long tick, String key) {
        long mix = combineSeed(seed, tick, Goal.WORLD_EVENT) ^ key.hashCode();
        return Long.toUnsignedString(mix, 36).toUpperCase();
    }

    public static String opId(long seed, long tick, Goal goal, String opType, String stableKey) {
        long mix = combineSeed(seed, tick, goal);
        mix ^= opType.hashCode();
        mix *= 1099511628211L;
        mix ^= stableKey.hashCode();
        mix *= 1099511628211L;
        return "op_" + Long.toUnsignedString(mix, 36).toUpperCase();
    }
}
