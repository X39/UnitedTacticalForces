using X39.UnitedTacticalForces.Hosting.Contracts;

namespace X39.UnitedTacticalForces.Api.DTO;

public sealed record GameServerSecretStatusDto(
    string Key,
    string DisplayName,
    bool RequiredForStart,
    bool Generated,
    bool Revealable,
    bool ExternalProvider,
    bool IsConfigured,
    int? Version,
    EGameServerSecretOrigin? Origin,
    Instant? TimeStampCreated);
