namespace WorldSim.Runtime.Profiles;

public static class LowCostProfileResolver
{
    public static LowCostProfileResolution ResolveForApp(string? rawRequested)
    {
        var (hasRequested, requested, parseOk, parsed) = Parse(rawRequested);
        if (!hasRequested)
            return new LowCostProfileResolution("default", LowCostProfileLane.DevLite, "default");

        if (!parseOk)
            return new LowCostProfileResolution(requested, LowCostProfileLane.DevLite, "fallback_invalid");

        if (parsed == LowCostProfileLane.Headless)
            return new LowCostProfileResolution(requested, LowCostProfileLane.DevLite, "fallback_headless_not_supported");

        return new LowCostProfileResolution(requested, parsed, "env");
    }

    public static LowCostProfileResolution ResolveForScenarioRunner(string? rawRequested)
    {
        var (hasRequested, requested, parseOk, parsed) = Parse(rawRequested);
        if (!hasRequested)
            return new LowCostProfileResolution("default", LowCostProfileLane.Headless, "default");

        if (!parseOk)
            return new LowCostProfileResolution(requested, LowCostProfileLane.Headless, "fallback_invalid");

        return new LowCostProfileResolution(requested, parsed, "env");
    }

    private static (bool HasRequested, string Requested, bool ParseOk, LowCostProfileLane Parsed) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (false, "", false, default);

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "showcase" => (true, normalized, true, LowCostProfileLane.Showcase),
            "devlite" => (true, normalized, true, LowCostProfileLane.DevLite),
            "dev_lite" => (true, normalized, true, LowCostProfileLane.DevLite),
            "headless" => (true, normalized, true, LowCostProfileLane.Headless),
            "low" => (true, normalized, true, LowCostProfileLane.DevLite),
            "medium" => (true, normalized, true, LowCostProfileLane.Showcase),
            "high" => (true, normalized, true, LowCostProfileLane.Showcase),
            _ => (true, normalized, false, default)
        };
    }
}
