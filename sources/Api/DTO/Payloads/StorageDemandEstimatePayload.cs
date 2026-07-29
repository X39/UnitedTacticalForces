using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record StorageDemandEstimatePayload
{
    [Required, MaxLength(128)]
    public string GameIdentifier { get; init; } = string.Empty;

    public long? ModPackDefinitionId { get; init; }

    [Required, MaxLength(128)]
    public string RuntimeProviderIdentifier { get; init; } = string.Empty;
}
