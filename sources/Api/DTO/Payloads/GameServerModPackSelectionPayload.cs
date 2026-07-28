namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

/// <summary>
/// Changes or clears the desired game-specific content definition for a durable server.
/// </summary>
public sealed record GameServerModPackSelectionPayload
{
    public long? ModPackDefinitionId { get; init; }
}
