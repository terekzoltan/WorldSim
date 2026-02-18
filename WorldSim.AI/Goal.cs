namespace WorldSim.AI;

public sealed class Goal
{
    public string Name { get; }
    public List<Consideration> Considerations { get; } = new();
    public float CooldownSeconds { get; set; }

    private float _lastSelectedAtSeconds = float.NegativeInfinity;

    public Goal(string name)
    {
        Name = name;
    }

    public bool IsOnCooldown(float nowSeconds)
    {
        return nowSeconds - _lastSelectedAtSeconds < CooldownSeconds;
    }

    public void MarkSelected(float nowSeconds)
    {
        _lastSelectedAtSeconds = nowSeconds;
    }
}
