using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Contracts;

namespace WorldSimRefineryClient.Serialization;

public sealed class PatchResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public PatchResponse Parse(string json, PatchApplyOptions? options = null)
    {
        options ??= new PatchApplyOptions();

        ValidateKnownOps(json);

        if (options.StrictMode)
        {
            ValidateRootShape(json);
        }

        var parsed = JsonSerializer.Deserialize<PatchResponse>(json, SerializerOptions);
        return parsed ?? throw new PatchApplyException("Invalid patch response JSON.");
    }

    private static void ValidateKnownOps(string json)
    {
        var obj = JsonNode.Parse(json)?.AsObject() ?? throw new PatchApplyException("Invalid JSON object.");
        if (obj["patch"] is not JsonArray patchArray)
        {
            throw new PatchApplyException("PatchResponse.patch must be an array.");
        }

        foreach (var opNode in patchArray)
        {
            if (opNode is not JsonObject opObj)
            {
                throw new PatchApplyException("Patch operation must be object.");
            }

            var op = opObj["op"]?.GetValue<string>()
                     ?? throw new PatchApplyException("Patch operation missing op.");
            if (op is not ("addTech" or "tweakTech" or "addWorldEvent"))
            {
                throw new PatchApplyException($"Unknown op '{op}'.");
            }
        }
    }

    private static void ValidateRootShape(string json)
    {
        var obj = JsonNode.Parse(json)?.AsObject() ?? throw new PatchApplyException("Invalid JSON object.");
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "requestId", "seed", "patch", "explain", "warnings"
        };

        foreach (var property in obj)
        {
            if (!allowed.Contains(property.Key))
            {
                throw new PatchApplyException($"Unknown root property '{property.Key}'.");
            }
        }
    }
}
