using System.ComponentModel.DataAnnotations;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record GameServerRuntimeProfileUpdatePayload
{
    [Required, MaxLength(128)]
    public string RuntimeProfileIdentifier { get; init; } = string.Empty;
}
