namespace WorldSimRefineryClient.Apply;

public sealed record PatchApplyResult(int AppliedCount, int DedupedCount, int NoOpCount);
