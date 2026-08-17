namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record GameServerSecretCapabilityDto(
    string Key,
    string DisplayName,
    bool RequiredForStart,
    bool Generated,
    bool Revealable,
    bool ExternalProvider);
