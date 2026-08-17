namespace X39.UnitedTacticalForces.Api.Services.GameServerDefinition;

public sealed record GameSecretDefinition(
    string Key,
    string DisplayName,
    bool RequiredForStart,
    bool Generated,
    bool Revealable,
    bool ExternalProvider = false);
