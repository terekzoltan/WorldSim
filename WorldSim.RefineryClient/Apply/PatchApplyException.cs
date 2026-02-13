namespace WorldSimRefineryClient.Apply;

public sealed class PatchApplyException : Exception
{
    public PatchApplyException(string message) : base(message)
    {
    }
}
