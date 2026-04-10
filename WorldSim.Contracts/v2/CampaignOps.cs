using System.Text.Json.Serialization;
using WorldSim.Contracts.V1;

namespace WorldSim.Contracts.V2;

public sealed record DeclareWarOp : PatchOp
{
    [JsonPropertyName("attackerFactionId")]
    public required int AttackerFactionId { get; init; }

    [JsonPropertyName("defenderFactionId")]
    public required int DefenderFactionId { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

public sealed record ProposeTreatyOp : PatchOp
{
    [JsonPropertyName("proposerFactionId")]
    public required int ProposerFactionId { get; init; }

    [JsonPropertyName("receiverFactionId")]
    public required int ReceiverFactionId { get; init; }

    [JsonPropertyName("treatyKind")]
    public required string TreatyKind { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}
