using System.ComponentModel.DataAnnotations;
using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO.Payloads;

public sealed record GameServerSecretValuePayload
{
    [Required, MaxLength(128)] public string Key { get; init; } = string.Empty;
    [Required, MaxLength(4096)] public string Value { get; init; } = string.Empty;
    public EGameServerSecretOrigin Origin { get; init; } = EGameServerSecretOrigin.UserProvided;
}
