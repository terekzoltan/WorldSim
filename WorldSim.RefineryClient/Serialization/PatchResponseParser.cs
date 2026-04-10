using System.Text.Json;
using System.Text.Json.Nodes;
using WorldSimRefineryClient.Apply;
using WorldSim.Contracts.V1;
using WorldSim.Contracts.V2;

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

        var root = JsonNode.Parse(json)?.AsObject() ?? throw new PatchApplyException("Invalid JSON object.");

        ValidateKnownOps(root);

        if (options.StrictMode)
        {
            ValidateRootShape(root);
        }

        var schemaVersion = root["schemaVersion"]?.GetValue<string>()
                            ?? throw new PatchApplyException("PatchResponse.schemaVersion is required.");
        var requestId = root["requestId"]?.GetValue<string>()
                        ?? throw new PatchApplyException("PatchResponse.requestId is required.");
        var seed = root["seed"]?.GetValue<long>()
                   ?? throw new PatchApplyException("PatchResponse.seed is required.");
        if (root["patch"] is not JsonArray patchArray)
        {
            throw new PatchApplyException("PatchResponse.patch must be an array.");
        }

        var patch = ParsePatchArray(patchArray);
        var explain = ParseStringArray(root["explain"], "PatchResponse.explain");
        var warnings = ParseStringArray(root["warnings"], "PatchResponse.warnings");

        return new PatchResponse(schemaVersion, requestId, seed, patch, explain, warnings);
    }

    private static IReadOnlyList<PatchOp> ParsePatchArray(JsonArray patchArray)
    {
        var patch = new List<PatchOp>(patchArray.Count);
        foreach (var opNode in patchArray)
        {
            if (opNode is not JsonObject opObject)
            {
                throw new PatchApplyException("Patch operation must be object.");
            }

            var op = opObject["op"]?.GetValue<string>()
                     ?? throw new PatchApplyException("Patch operation missing op.");
            patch.Add(ParseSingleOp(opObject, op));
        }

        return patch;
    }

    private static PatchOp ParseSingleOp(JsonObject opObject, string op)
    {
        return op switch
        {
            "addTech" => DeserializeOp<AddTechOp>(opObject, op),
            "tweakTech" => DeserializeOp<TweakTechOp>(opObject, op),
            "addWorldEvent" => DeserializeOp<AddWorldEventOp>(opObject, op),
            "addStoryBeat" => DeserializeOp<AddStoryBeatOp>(opObject, op),
            "setColonyDirective" => DeserializeOp<SetColonyDirectiveOp>(opObject, op),
            "declareWar" => ValidateDeclareWar(DeserializeOp<DeclareWarOp>(opObject, op)),
            "proposeTreaty" => ValidateProposeTreaty(DeserializeOp<ProposeTreatyOp>(opObject, op)),
            _ => throw new PatchApplyException($"Unknown op '{op}'.")
        };
    }

    private static PatchOp ValidateDeclareWar(DeclareWarOp op)
    {
        if (op.AttackerFactionId < 0)
            throw new PatchApplyException("declareWar.attackerFactionId must be >= 0.");

        if (op.DefenderFactionId < 0)
            throw new PatchApplyException("declareWar.defenderFactionId must be >= 0.");

        if (op.AttackerFactionId == op.DefenderFactionId)
            throw new PatchApplyException("declareWar requires attackerFactionId != defenderFactionId.");

        return op;
    }

    private static PatchOp ValidateProposeTreaty(ProposeTreatyOp op)
    {
        if (op.ProposerFactionId < 0)
            throw new PatchApplyException("proposeTreaty.proposerFactionId must be >= 0.");

        if (op.ReceiverFactionId < 0)
            throw new PatchApplyException("proposeTreaty.receiverFactionId must be >= 0.");

        if (op.ProposerFactionId == op.ReceiverFactionId)
            throw new PatchApplyException("proposeTreaty requires proposerFactionId != receiverFactionId.");

        if (op.TreatyKind is not ("ceasefire" or "peace_talks"))
        {
            throw new PatchApplyException(
                $"Unsupported proposeTreaty.treatyKind '{op.TreatyKind}'. Expected one of: ceasefire, peace_talks.");
        }

        return op;
    }

    private static T DeserializeOp<T>(JsonObject opObject, string op)
        where T : PatchOp
    {
        try
        {
            var parsed = opObject.Deserialize<T>(SerializerOptions);
            return parsed ?? throw new PatchApplyException($"Invalid '{op}' operation payload.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new PatchApplyException($"Invalid '{op}' operation payload: {ex.Message}");
        }
    }

    private static IReadOnlyList<string> ParseStringArray(JsonNode? node, string fieldName)
    {
        if (node is not JsonArray array)
        {
            throw new PatchApplyException($"{fieldName} must be an array.");
        }

        var values = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is null)
            {
                throw new PatchApplyException($"{fieldName} must contain strings only.");
            }

            values.Add(item.GetValue<string>());
        }

        return values;
    }

    private static void ValidateKnownOps(JsonObject obj)
    {
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
            if (op is not (
                    "addTech"
                    or "tweakTech"
                    or "addWorldEvent"
                    or "addStoryBeat"
                    or "setColonyDirective"
                    or "declareWar"
                    or "proposeTreaty"))
            {
                throw new PatchApplyException($"Unknown op '{op}'.");
            }
        }
    }

    private static void ValidateRootShape(JsonObject obj)
    {
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
