using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorldSimRefineryClient.Apply;

public static class CanonicalStateSerializer
{
    public static string Serialize(SimulationPatchState state)
    {
        var payload = new
        {
            techIds = state.TechIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            eventIds = state.EventIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            storyBeatIds = state.StoryBeatIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            colonyDirectives = state.ColonyDirectives
                .OrderBy(x => x.Key)
                .Select(x => new { colonyId = x.Key, directive = x.Value })
                .ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string Sha256(SimulationPatchState state)
    {
        var bytes = Encoding.UTF8.GetBytes(Serialize(state));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
